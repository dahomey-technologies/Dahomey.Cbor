using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IObjectConverter
    {
        void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName, ref MemberReadState state);
        void ReadValue(ref CborReader reader, object obj, int memberIndex, ref MemberReadState state);
        bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, ref MemberReadState state, [MaybeNullWhen(false)] out object value);
        bool ReadValue(ref CborReader reader, int memberIndex, ref MemberReadState state, [MaybeNullWhen(false)] out object value);
        IReadOnlyList<IMemberConverter> MemberConvertersForWrite { get; }
        ByteBufferDictionary<MemberReadEntry> MemberConvertersForRead { get; }
        Dictionary<int, MemberReadEntry> MemberConvertersForReadByIndex { get; }

        /// <summary>
        /// The declared member index occupying each position of a
        /// <see cref="CborObjectFormat.Array"/> document, in the order the members are written. Empty
        /// for every other object format, which name their members in the document itself.
        /// </summary>
        /// <remarks>
        /// An array carries no keys, so the only thing a position can be resolved against is the
        /// member list, and this is that list reduced to what the read needs from it. Exposed on the
        /// interface for the same reason as <see cref="MemberConvertersForReadByIndex"/>: on a
        /// polymorphic read the positions belong to the resolved type, which the declared type's
        /// converter reaches only through here.
        /// </remarks>
        IReadOnlyList<int> MemberIndexesByPosition { get; }
        IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead { get; }
        IObjectMapping ObjectMapping { get; }
    }

    public interface IObjectConverter<out T> : IObjectConverter
    {
        T CreateInstance();
    }

    /// <summary>
    /// A converter that can be told a polymorphic read is about to run on it, so that it brings its
    /// discriminator convention up to date first.
    /// </summary>
    /// <remarks>
    /// A read through a base type dispatches to the derived type's converter and never makes that
    /// converter the declared one, so nothing else would ask it. Internal, because
    /// <see cref="IObjectConverter"/> is public and a member added to it would break implementers.
    /// </remarks>
    internal interface IDiscriminatorConventionHolder
    {
        void EnsureDiscriminatorConventionCurrent();
    }

    public class ObjectConverter<T> :
        CborConverterBase<T>,
        IObjectConverter<T>,
        IDiscriminatorConventionHolder,
        ICborMapReader<ObjectConverter<T>.ReaderContext>,
        ICborMapWriter<ObjectConverter<T>.WriterContext>,
        ICborArrayReader<ObjectConverter<T>.ReaderContext>,
        ICborArrayWriter<ObjectConverter<T>.WriterContext>
    {
        public struct ReaderContext
        {
            public T obj;
            public IObjectConverter<T> converter;
            public Dictionary<RawString, object>? creatorValues;
            public Dictionary<RawString, object>? regularValues;
            public Dictionary<int, object>? creatorValuesByIndex;
            public Dictionary<int, object>? regularValuesByIndex;
            public MemberReadState readState;

            /// <summary>
            /// How many members of a <see cref="CborObjectFormat.Array"/> document this read has
            /// already consumed, and so which position the next item holds. Unused by the map
            /// formats, which read their key from the document.
            /// </summary>
            /// <remarks>
            /// A position, not a member index: the two coincide only for a type whose declared
            /// indexes start at zero and run consecutively, and treating this as an index is what
            /// lost the members of every type that did not. <see cref="MemberIndexesByPosition"/>
            /// turns it into one.
            /// </remarks>
            public int memberPosition;
        }

        public struct WriterContext
        {
            public CborOptions options;
            public T obj;
            public int memberIndex;
            public IObjectConverter objectConverter;
            /// <summary>
            /// The member list this write runs on, read from
            /// <see cref="IObjectConverter.MemberConvertersForWrite"/> once, when the write starts.
            /// </summary>
            /// <remarks>
            /// That property answers from <see cref="CborOptions.Deterministic"/>, which any code
            /// running during the write -- a property getter, a custom converter, another thread
            /// sharing <see cref="CborOptions.Default"/> -- is free to change. Consulting it per item
            /// would let one write start on one ordering and finish on the other, writing some members
            /// twice and dropping others while the map header still claims the original count: a
            /// structurally corrupt document that nothing downstream can detect. Snapshotting also
            /// keeps the property off the per-member path, where it costs a non-inlineable call on
            /// every object write, deterministic or not.
            /// </remarks>
            public IReadOnlyList<IMemberConverter> memberConvertersForWrite;

            /// <summary>
            /// The object format this write runs on, taken from the same converter as
            /// <see cref="memberConvertersForWrite"/>.
            /// </summary>
            /// <remarks>
            /// In the polymorphic case that converter is the derived type's, so reading the format
            /// from the declared type's mapping instead would let a base and a derived type that
            /// disagree on it decide the exclusion of
            /// <see cref="CborObjectFormat.Array"/> from one mapping and the write from the other:
            /// members would be written positionally in sorted order, moving values between array
            /// slots. Both come from one converter, read once.
            /// </remarks>
            public CborObjectFormat objectFormat;
            public LengthMode lengthMode;
        }

        private readonly ByteBufferDictionary<MemberReadEntry> _memberConvertersForRead = new ByteBufferDictionary<MemberReadEntry>();
        private readonly Dictionary<int, MemberReadEntry> _memberConvertersForReadByIndex = new();

        /// <summary>
        /// Numbers the members added to the read lookups above, so each carries a
        /// <see cref="MemberReadEntry.Ordinal"/> distinct within this converter. Counts entries rather
        /// than distinct member converters, so that presenting the same member converter twice cannot
        /// hand two members one number. Starts at 1, leaving 0 to mean no member at all.
        /// </summary>
        private int _nextReadOrdinal = 1;
        public List<IMemberConverter> _requiredMemberConvertersForRead = new List<IMemberConverter>();
        private readonly List<IMemberConverter> _memberConvertersForWrite;

        /// <inheritdoc cref="IObjectConverter.MemberIndexesByPosition"/>
        /// <remarks>
        /// Built alongside <see cref="_memberConvertersForWrite"/> and from the same mappings in the
        /// same pass, because the positions this describes are the ones that list produces: the write
        /// emits those members in order and nothing else, so any list assembled separately could only
        /// drift from what is actually on the wire.
        /// </remarks>
        private readonly int[] _memberIndexesByPosition;

        /// <summary>
        /// The write order of a deterministic document, stamped with the discriminator state it was
        /// sorted under, or null before the first deterministic write.
        /// </summary>
        /// <remarks>
        /// The discriminator is one of the keys being ordered, and its name is the convention's, which
        /// <c>ClearConventions</c> and a later registration can replace. So a type that writes a
        /// discriminator is stamped with the registry's version and sorted again when it moves; one
        /// that does not is stamped with a constant and sorted once, as the member set is fixed at
        /// construction. The list and its stamp are one object behind one reference, replaced whole,
        /// for the reason <see cref="_resolvedDiscriminatorConvention"/> is: two threads building the
        /// same order is harmless, and a stamp that outlives the list it describes is not possible.
        /// </remarks>
        private DeterministicOrder? _deterministicOrder;

        private sealed class DeterministicOrder
        {
            public readonly int Version;
            public readonly List<IMemberConverter> Members;

            public DeterministicOrder(int version, List<IMemberConverter> members)
            {
                Version = version;
                Members = members;
            }
        }
        private readonly CborOptions _options;
        private readonly SerializationRegistry _registry;
        private readonly IObjectMapping _objectMapping;
        private readonly Func<T>? _constructor;
        private readonly bool _isInterfaceOrAbstract;
        private readonly bool _isStruct;

        /// <summary>
        /// The convention resolving this type's discriminator, stamped with the registry version it was
        /// resolved at. An answer of null means no type in the hierarchy carries one.
        /// </summary>
        /// <remarks>
        /// Not final at construction. A converter is built once per type and cached for the lifetime of
        /// the options, so resolving this only in the constructor froze the answer at whatever was
        /// registered the first time the type was touched -- null for a type read as an abstract base
        /// or an interface, whose subtypes alone carry a discriminator, and a stale convention after
        /// <see cref="DiscriminatorConventionRegistry.ClearConventions"/>.
        /// <para>
        /// The answer is re-resolved whenever the registry's <see cref="DiscriminatorConventionRegistry.Version"/>
        /// has moved, so the steady state is a field read and an int compare rather than a lookup.
        /// </para>
        /// <para>
        /// Answer and version are one immutable object behind one reference, replaced whole. Two fields
        /// written separately could be interleaved by racing resolvers into an old convention beside a
        /// new version, which would be taken for current and never re-asked now that a non-null answer
        /// can go stale too. With one reference a racing write can only leave a stamp older than the
        /// registry's, which the next read answers by resolving again. The reference is published with
        /// <see cref="Volatile"/> so the object's fields are visible to a reader on weak memory models.
        /// </para>
        /// <para>
        /// Nothing on the read side is keyed by the discriminator's name: the discriminator mapping
        /// cannot be deserialized, so it has no entry in the member lookup tables, and the document's
        /// discriminator is recognised through this convention's current name. The one exception is a
        /// creator parameter that happens to share that name, whose entry is built once with the
        /// constructor and keeps the name the convention had then.
        /// </para>
        /// </remarks>
        private ResolvedDiscriminatorConvention _resolvedDiscriminatorConvention = ResolvedDiscriminatorConvention.Unresolved;

        private sealed class ResolvedDiscriminatorConvention
        {
            // Never equal to a registry version, which starts at 0 and only grows.
            public static readonly ResolvedDiscriminatorConvention Unresolved = new ResolvedDiscriminatorConvention(-1, null);

            public readonly int Version;
            public readonly IDiscriminatorConvention? Convention;

            public ResolvedDiscriminatorConvention(int version, IDiscriminatorConvention? convention)
            {
                Version = version;
                Convention = convention;
            }
        }

        /// <summary>
        /// Whether this type's own mapping carries a discriminator, as opposed to merely resolving a
        /// convention.
        /// </summary>
        /// <remarks>
        /// <see cref="DiscriminatorConventionRegistry"/> answers with the convention of a hierarchy
        /// for every base type and interface in it, so a convention being resolvable says nothing
        /// about this type writing a discriminator. Only a type that does can have the key in its
        /// documents, and only for such a type is an unmapped key at that name - or at index 0, where
        /// a discriminator always sits - the discriminator rather than something the document made up.
        /// </remarks>
        private readonly bool _hasDiscriminatorMember;

        /// <summary>
        /// The members to write, in the order to write them: declaration order normally, deterministic
        /// key order when <see cref="CborOptions.Deterministic"/> is set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The flag is read here, where the list is consumed, rather than in the constructor where it is
        /// built. A converter is built once per type and then cached in
        /// <see cref="CborConverterRegistry"/> for the lifetime of the options, so an order chosen at
        /// construction would be frozen at whatever the flag happened to be the first time that type was
        /// serialized -- and silently wrong, not loudly wrong, for every write after the flag changed.
        /// <see cref="CborOptions.Default"/> being a process-wide singleton makes that ordinary rather
        /// than exotic. Reading the flag per write costs a branch and keeps the guarantee honest.
        /// </para>
        /// <para>
        /// <see cref="CborObjectFormat.Array"/> is excluded because it writes members positionally and
        /// emits no keys at all. There is nothing to order, and reordering would move values between
        /// array positions -- changing what the document means rather than only how it is spelled.
        /// </para>
        /// </remarks>
        public IReadOnlyList<IMemberConverter> MemberConvertersForWrite
        {
            get
            {
                if (!_options.Deterministic || _objectMapping.ObjectFormat == CborObjectFormat.Array)
                {
                    return _memberConvertersForWrite;
                }

                // The member set itself is fixed at construction. What can move is the discriminator's
                // name, so the order is stamped with the registry's version when this type writes one.
                // Two threads racing here build identical lists, and the loser's is simply dropped.
                // The version is sampled before sorting, so a change landing meanwhile is seen next time.
                int version = _hasDiscriminatorMember ? _registry.DiscriminatorConventionRegistry.Version : 0;
                DeterministicOrder? order = Volatile.Read(ref _deterministicOrder);

                if (order == null || order.Version != version)
                {
                    List<IMemberConverter> sorted = new List<IMemberConverter>(_memberConvertersForWrite);
                    sorted.Sort(CompareMembersForDeterministicOrder);
                    order = new DeterministicOrder(version, sorted);
                    Volatile.Write(ref _deterministicOrder, order);
                }

                return order.Members;
            }
        }
        public ByteBufferDictionary<MemberReadEntry> MemberConvertersForRead => _memberConvertersForRead;
        public Dictionary<int, MemberReadEntry> MemberConvertersForReadByIndex => _memberConvertersForReadByIndex;
        public IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead => _requiredMemberConvertersForRead;
        public IReadOnlyList<int> MemberIndexesByPosition => _memberIndexesByPosition;
        public IObjectMapping ObjectMapping => _objectMapping;

        public ObjectConverter(CborOptions options)
            : this(options, null)
        {
        }

        /// <summary>
        /// Creates an object converter that instantiates <typeparamref name="T"/> with an explicit
        /// factory instead of discovering its default constructor by reflection.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="factory">
        /// Creates a new <typeparamref name="T"/>. When supplied, the reflection lookup of the
        /// default constructor — and the <c>Expression.Compile</c> that turns it into a delegate —
        /// is skipped entirely. Required for Native AOT, where the constructor metadata may be
        /// trimmed away: <c>Type.GetConstructor</c> then returns null and deserialization fails with
        /// "Cannot find a default constructor". Pass <c>() => new T()</c> from generated code.
        /// </param>
        public ObjectConverter(CborOptions options, Func<T>? factory)
        {
            _options = options;
            _registry = options.Registry;
            _objectMapping = _registry.ObjectMappingRegistry.Lookup<T>();

            _memberConvertersForWrite = new List<IMemberConverter>();

            // Positions exist only in the Array format; every other format keys its members in the
            // document, so there is nothing for a position to resolve against and nothing to collect.
            List<int>? memberIndexesByPosition =
                _objectMapping.ObjectFormat == CborObjectFormat.Array ? new List<int>() : null;

            foreach (IMemberMapping memberMapping in _objectMapping.GetMemberMappingsForConverter(this))
            {
                IMemberConverter memberConverter = memberMapping.GenerateMemberConverter();

                _hasDiscriminatorMember |= memberMapping is IDiscriminatorMapping;

                bool isCreatorMember = false;

                switch (_objectMapping.ObjectFormat)
                {
                    case CborObjectFormat.StringKeyMap:
                        isCreatorMember = _objectMapping.IsCreatorMember(memberConverter.MemberName);
                        break;

                    case CborObjectFormat.IntKeyMap:
                    case CborObjectFormat.Array:
                        isCreatorMember = _objectMapping.IsCreatorMember(memberConverter.MemberIndex!.Value);
                        break;
                }

                if (memberMapping.CanBeDeserialized || isCreatorMember)
                {
                    // Both lookups refuse a key they already hold, and the mapping was validated for
                    // exactly that before this loop began. A collision reaching here is therefore one
                    // that reached the mapping after its validation ran - a SetMemberName or
                    // SetMemberIndex onto a key already taken, once something has read
                    // MemberMappings - which is the same mistake arriving late, and is reported as
                    // the same kind of failure rather
                    // than as the raw ArgumentException of the container that caught it. The clause
                    // naming the lateness is what tells the two apart: the validator sees the whole
                    // mapping and refuses any collision in it, while this sees only the members that
                    // can be read, so a collision between members that are written but never read
                    // reaches the validator alone.
                    //
                    // Only the Add is guarded. IMemberConverter is a public interface an implementer
                    // outside this library can supply, and an ArgumentException out of one of its
                    // getters is not this failure - naming it a duplicate key would be a diagnosis
                    // the catch has no grounds for.
                    switch (_objectMapping.ObjectFormat)
                    {
                        case CborObjectFormat.StringKeyMap:
                            {
                                ReadOnlySpan<byte> memberName = memberConverter.MemberName;
                                MemberReadEntry entry = new MemberReadEntry(memberConverter, _nextReadOrdinal);

                                try
                                {
                                    _memberConvertersForRead.Add(memberName, entry);
                                }
                                catch (ArgumentException)
                                {
                                    throw new CborException(
                                        MemberMappingErrors.DuplicateMemberName(typeof(T), memberMapping.MemberName)
                                        + MemberMappingErrors.AddedAfterValidation);
                                }

                                _nextReadOrdinal++;
                            }
                            break;
                        case CborObjectFormat.IntKeyMap:
                        case CborObjectFormat.Array:
                            if (memberConverter.MemberIndex.HasValue)
                            {
                                int memberIndex = memberConverter.MemberIndex.Value;
                                MemberReadEntry entry = new MemberReadEntry(memberConverter, _nextReadOrdinal);

                                try
                                {
                                    _memberConvertersForReadByIndex.Add(memberIndex, entry);
                                }
                                catch (ArgumentException)
                                {
                                    throw new CborException(
                                        MemberMappingErrors.DuplicateMemberIndex(typeof(T))
                                        + MemberMappingErrors.AddedAfterValidation);
                                }

                                _nextReadOrdinal++;
                            }
                            break;
                    }

                    if (memberConverter.RequirementPolicy == RequirementPolicy.AllowNull
                        || memberConverter.RequirementPolicy == RequirementPolicy.Always)
                    {
                        _requiredMemberConvertersForRead.Add(memberConverter);
                    }
                }

                if (memberMapping.CanBeSerialized)
                {
                    _memberConvertersForWrite.Add(memberConverter);

                    // The discriminator holds a position of its own when it is written, but it is not
                    // a member and is resolved by its semantic tag rather than by counting -- see the
                    // Array arm of ReadItem, which consumes that item before any position is counted.
                    // Leaving it out is also what makes the count independent of whether the policy
                    // wrote one for this particular document.
                    //
                    // A member that can be written but not read still takes a position: the writer
                    // emits it, so every member after it sits one place further along, and dropping it
                    // here would shift them all. It resolves to an index no read lookup holds, which
                    // skips the item and leaves the positions intact.
                    if (memberIndexesByPosition != null
                        && memberMapping is not IDiscriminatorMapping
                        && memberConverter.MemberIndex.HasValue)
                    {
                        memberIndexesByPosition.Add(memberConverter.MemberIndex.Value);
                    }
                }
            }

            _memberIndexesByPosition = memberIndexesByPosition?.ToArray() ?? Array.Empty<int>();

            _isInterfaceOrAbstract = typeof(T).IsInterface || typeof(T).IsAbstract;
            _isStruct = typeof(T).IsStruct();

            if (factory != null)
            {
                _constructor = factory;
            }
            else if (!_isInterfaceOrAbstract && !_isStruct && _objectMapping.CreatorMapping == null)
            {
                ConstructorInfo? defaultConstructorInfo = typeof(T).GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);

                if (defaultConstructorInfo == null)
                {
                    throw new CborException($"Cannot find a default constructor on type {typeof(T)}");
                }

                _constructor = defaultConstructorInfo.CreateDelegate<T>();
            }

            ResolveDiscriminatorConvention();
        }

        /// <summary>
        /// The convention for this type, asking the registry again whenever its version has moved
        /// since the held answer was resolved - null or not, since clearing the conventions can
        /// displace a convention as well as add one.
        /// </summary>
        /// <remarks>
        /// The version is sampled before the lookup, never after: a registration landing between the
        /// two would otherwise be stamped as already accounted for and never looked at again.
        /// </remarks>
        private IDiscriminatorConvention? GetDiscriminatorConvention()
        {
            ResolvedDiscriminatorConvention resolved = Volatile.Read(ref _resolvedDiscriminatorConvention);

            if (resolved.Version == _registry.DiscriminatorConventionRegistry.Version)
            {
                return resolved.Convention;
            }

            return ResolveDiscriminatorConvention();
        }

        void IDiscriminatorConventionHolder.EnsureDiscriminatorConventionCurrent()
        {
            GetDiscriminatorConvention();
        }

        private IDiscriminatorConvention? ResolveDiscriminatorConvention()
        {
            int version = _registry.DiscriminatorConventionRegistry.Version;
            IDiscriminatorConvention? convention =
                _registry.DiscriminatorConventionRegistry.GetConvention(typeof(T));

            // A convention that arrives after the mapping was validated brings a member name that
            // validation never saw; see the method for what it refuses.
            MemberMappingErrors.ThrowIfDiscriminatorNameCollides(_objectMapping, convention);

            Volatile.Write(ref _resolvedDiscriminatorConvention, new ResolvedDiscriminatorConvention(version, convention));

            return convention;
        }

        public T CreateInstance()
        {
            if (_isInterfaceOrAbstract || _constructor == null)
            {
                throw new CborException($"A CreatorMapping should be defined for interfaces or abstract classes ({typeof(T)})");
            }

            return _constructor();
        }

        public override T Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default!;
            }

            ReaderContext context = new ReaderContext
            {
                readState = new MemberReadState(
                    trackRequiredMembers: _requiredMemberConvertersForRead.Count != 0,
                    rejectDuplicates: _options.DuplicateKeyMode == DuplicateKeyMode.Reject)
            };

            // Members name themselves in ReadItem, where the name is still in hand. What is left for
            // this frame is every failure that never reached a member -- a document whose shape
            // contradicts the mapping outright, a creator that rejects what was collected, a required
            // member that never arrived -- which has no segment to contribute but is still a position
            // worth reporting as such: the object itself, $ at the root. The whole body is covered so
            // that a required member missing from the root reports the same way as one missing from a
            // nested object, rather than losing its path for being validated after the loop.
            try
            {
                switch (_objectMapping.ObjectFormat)
                {
                    case CborObjectFormat.StringKeyMap:
                        {
                            context.creatorValues = _objectMapping.CreatorMapping != null ? new() : null;
                            context.regularValues = _objectMapping.CreatorMapping != null ? new () : null;
                            reader.ReadMap(this, ref context);
                        }
                        break;
                    case CborObjectFormat.IntKeyMap:
                        {
                            context.creatorValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                            context.regularValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                            reader.ReadMap(this, ref context);
                        }
                        break;
                    case CborObjectFormat.Array:
                        {
                            context.creatorValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                            context.regularValuesByIndex = _objectMapping.CreatorMapping != null ? new() : null;
                            reader.ReadArray(this, ref context);
                        }
                        break;
                }

                if (context.converter == null)
                {
                    // A container with no items never reached ReadItem, which is where the instance is
                    // made and the deserializing callbacks run, so the object is begun here instead.
                    // With no item there is no discriminator either, so the object is this
                    // converter's type -- as it is for a map holding nothing but a name no member
                    // answers to -- and an interface or an abstract class is refused the same way.
                    context.converter = this;
                    BeginObject(ref context);
                }
                IObjectMapping objectMapping = context.converter.ObjectMapping;

                if (objectMapping.CreatorMapping != null)
                {
                    switch (_objectMapping.ObjectFormat)
                    {
                        case CborObjectFormat.StringKeyMap:
                            {
                                context.obj = (T)objectMapping.CreatorMapping.CreateInstance(context.creatorValues!);
                                if (objectMapping.OnDeserializingMethod != null)
                                {
                                    ((Action<T>)objectMapping.OnDeserializingMethod)(context.obj);
                                }

                                foreach (KeyValuePair<RawString, object> value in context.regularValues!)
                                {
                                    if (!context.converter.MemberConvertersForRead.TryGetValue(value.Key.Buffer.Span, out MemberReadEntry entry))
                                    {
                                        // should not happen
                                        throw new CborException("Unexpected error");
                                    }

                                    entry.Converter.Set(context.obj, value.Value);
                                }
                            }
                            break;
                        case CborObjectFormat.IntKeyMap:
                        case CborObjectFormat.Array:
                            {
                                context.obj = (T)objectMapping.CreatorMapping.CreateInstance(context.creatorValuesByIndex!);
                                if (objectMapping.OnDeserializingMethod != null)
                                {
                                    ((Action<T>)objectMapping.OnDeserializingMethod)(context.obj);
                                }

                                foreach (KeyValuePair<int, object> value in context.regularValuesByIndex!)
                                {
                                    if (!context.converter.MemberConvertersForReadByIndex.TryGetValue(value.Key, out MemberReadEntry entry))
                                    {
                                        // should not happen
                                        throw new CborException("Unexpected error");
                                    }

                                    entry.Converter.Set(context.obj, value.Value);
                                }
                            }
                            break;
                    }
                }

                if (context.readState.TracksRequiredMembers)
                {
                    foreach (IMemberConverter memberConverter in context.converter.RequiredMemberConvertersForRead)
                    {
                        if (!context.readState.WasRead(memberConverter))
                        {
                            throw new CborException($"Required property '{Encoding.UTF8.GetString(memberConverter.MemberName)}' not found in JSON.");
                        }
                    }
                }

                if (objectMapping.OnDeserializedMethod != null)
                {
                    ((Action<T>)objectMapping.OnDeserializedMethod)(context.obj);
                }

                return context.obj;
            }
            catch (CborException exception) when (exception.MarkPathKnownAndDecline())
            {
                // Not entered: the filter marks the path and declines.
                throw;
            }
        }

        public void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName, ref MemberReadState state)
        {
            T value = (T)obj;

            if (!_memberConvertersForRead.TryGetValue(memberName, out MemberReadEntry entry))
            {
                HandleUnmappedName(ref reader, ref state, memberName);
            }
            else
            {
                MarkMemberRead(ref reader, ref state, entry, memberName);
                entry.Converter.Read(ref reader, value);
            }
        }
        public void ReadValue(ref CborReader reader, object obj, int memberIndex, ref MemberReadState state)
        {
            T value = (T)obj;

            if (!_memberConvertersForReadByIndex.TryGetValue(memberIndex, out MemberReadEntry entry))
            {
                HandleUnmappedIndex(ref reader, ref state, memberIndex);
            }
            else
            {
                MarkMemberRead(ref reader, ref state, entry, memberIndex);
                entry.Converter.Read(ref reader, value);
            }
        }

        public bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, ref MemberReadState state, [MaybeNullWhen(false)] out object value)
        {
            if (!_memberConvertersForRead.TryGetValue(memberName, out MemberReadEntry entry))
            {
                HandleUnmappedName(ref reader, ref state, memberName);
                value = default!;
                return false;
            }
            else
            {
                MarkMemberRead(ref reader, ref state, entry, memberName);
                value = entry.Converter.Read(ref reader);
                return true;
            }
        }

        public bool ReadValue(ref CborReader reader, int memberIndex, ref MemberReadState state, [MaybeNullWhen(false)] out object value)
        {
            if (!_memberConvertersForReadByIndex.TryGetValue(memberIndex, out MemberReadEntry entry))
            {
                HandleUnmappedIndex(ref reader, ref state, memberIndex);
                value = default!;
                return false;
            }
            else
            {
                MarkMemberRead(ref reader, ref state, entry, memberIndex);
                value = entry.Converter.Read(ref reader);
                return true;
            }
        }

        public override void Write(ref CborWriter writer, T value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (_objectMapping.OnSerializingMethod != null)
            {
                ((Action<T>)_objectMapping.OnSerializingMethod)(value);
            }

            WriterContext context = new WriterContext
            {
                options = _options,
                obj = value,
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _objectMapping.LengthMode != LengthMode.Default
                        ? _objectMapping.LengthMode : _options.MapLengthMode
            };

            Type declaredType = typeof(T);
            Type actualType = value.GetType();

            if (_objectMapping.CreatorMapping == null && actualType != declaredType)
            {
                var converter = _registry.ConverterRegistry.Lookup(actualType);

                if (converter is IObjectConverter objectConverter)
                {
                    context.objectConverter = objectConverter;
                }
                else if (converter is not null)
                {
                    converter.Write(ref writer, value);
                    return;
                }
                else
                {
                    throw new CborException($"No converter found for type {actualType.Name}");
                }
            }
            else
            {
                context.objectConverter = this;
            }

            // One read of each, here, now that the converter this write runs on is settled -- see
            // WriterContext.memberConvertersForWrite and WriterContext.objectFormat.
            context.memberConvertersForWrite = context.objectConverter.MemberConvertersForWrite;
            context.objectFormat = context.objectConverter.ObjectMapping.ObjectFormat;

            switch (context.objectFormat)
            {
                case CborObjectFormat.StringKeyMap:
                case CborObjectFormat.IntKeyMap:
                    writer.WriteMap(this, ref context);
                    break;
                case CborObjectFormat.Array:
                    writer.WriteArray(this, ref context);
                    break;
            }

            if (_objectMapping.OnSerializedMethod != null)
            {
                ((Action<T>)_objectMapping.OnSerializedMethod)(value);
            }
        }

        void ICborMapReader<ReaderContext>.ReadBeginMap(int size, ref ReaderContext context)
        {
        }

        void ICborMapReader<ReaderContext>.ReadMapItem(ref CborReader reader, ref ReaderContext context)
        {
            ReadItem(ref reader, ref context);
        }

        void ICborArrayReader<ReaderContext>.ReadBeginArray(int size, ref ReaderContext context)
        {
        }

        void ICborArrayReader<ReaderContext>.ReadArrayItem(ref CborReader reader, ref ReaderContext context)
        {
            ReadItem(ref reader, ref context);
        }

        int ICborMapWriter<WriterContext>.GetMapSize(ref WriterContext context)
        {
            return GetSize(ref context);
        }

        bool ICborMapWriter<WriterContext>.WriteMapItem(ref CborWriter writer, ref WriterContext context)
        {
            return WriteItem(ref writer, ref context);
        }

        int ICborArrayWriter<WriterContext>.GetArraySize(ref WriterContext context)
        {
            return GetSize(ref context);
        }

        bool ICborArrayWriter<WriterContext>.WriteArrayItem(ref CborWriter writer, ref WriterContext context)
        {
            return WriteItem(ref writer, ref context);
        }

        /// <summary>
        /// Makes the instance the members are read into and runs the deserializing callbacks, for a
        /// type that is not built from a creator mapping afterwards.
        /// </summary>
        /// <remarks>
        /// Reached from the first item of a container and, when it has none, from the end of the read:
        /// the two have to do the same thing, or an object holding nothing is a different object from
        /// one holding something.
        /// </remarks>
        private void BeginObject(ref ReaderContext context)
        {
            if (context.creatorValues == null && context.creatorValuesByIndex == null)
            {
                if (!_isStruct && context.obj == null)
                {
                    context.obj = context.converter.CreateInstance();
                }

                if (context.converter.ObjectMapping.OnDeserializingMethod != null)
                {
                    ((Action<T>)context.converter.ObjectMapping.OnDeserializingMethod)(context.obj);
                }
            }
        }

        private void ReadItem(ref CborReader reader, ref ReaderContext context)
        {
            // Only the Array arm below consumes what it read to resolve the type; the map arms hand
            // the reader back to where they found it. So this says "the current item was the
            // discriminator and is spent", which is what decides whether this call still has a member
            // to read - see the exit at the end of the block.
            bool discriminatorConsumed = false;

            if (context.obj == null || context.converter == null)
            {
                if (context.converter == null)
                {
                    IDiscriminatorConvention? discriminatorConvention = GetDiscriminatorConvention();

                    if (discriminatorConvention != null)
                    {
                        switch (_objectMapping.ObjectFormat)
                        {
                            case CborObjectFormat.StringKeyMap:
                                {
                                    CborReaderBookmark bookmark = reader.GetBookmark();

                                    if (FindItem(ref reader, discriminatorConvention.MemberName))
                                    {
                                        // discriminator value
                                        Type actualType = discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                                        (context.converter as IDiscriminatorConventionHolder)?.EnsureDiscriminatorConventionCurrent();
                                        ICreatorMapping? creatorMapping = context.converter.ObjectMapping.CreatorMapping;
                                        context.creatorValues = creatorMapping != null ? new() : null;
                                        context.regularValues = creatorMapping != null ? new() : null;
                                    }
                                    else
                                    {
                                        context.converter = this;
                                    }

                                    reader.ReturnToBookmark(bookmark);
                                }
                                break;
                            case CborObjectFormat.IntKeyMap:
                                {
                                    CborReaderBookmark bookmark = reader.GetBookmark();

                                    if (FindItem(ref reader, 0)) // discriminator index is always 0
                                    {
                                        // discriminator value
                                        Type actualType = discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                                        (context.converter as IDiscriminatorConventionHolder)?.EnsureDiscriminatorConventionCurrent();
                                        ICreatorMapping? creatorMapping = context.converter.ObjectMapping.CreatorMapping;
                                        context.creatorValuesByIndex = creatorMapping != null ? new() : null;
                                        context.regularValuesByIndex = creatorMapping != null ? new() : null;
                                    }
                                    else
                                    {
                                        context.converter = this;
                                    }

                                    reader.ReturnToBookmark(bookmark);
                                }
                                break;
                            case CborObjectFormat.Array:
                            default:
                                {
                                    // discriminator is always the first item
                                    // we need a Semantic Tag to check if the discriminator is present
                                    CborReaderBookmark bookmark = reader.GetBookmark();

                                    if (reader.TryReadSemanticTag(out ulong semanticTag) && semanticTag == _options.DiscriminatorSemanticTag)
                                    {
                                        // discriminator value
                                        Type actualType = discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                                        (context.converter as IDiscriminatorConventionHolder)?.EnsureDiscriminatorConventionCurrent();
                                        ICreatorMapping? creatorMapping = context.converter.ObjectMapping.CreatorMapping;
                                        context.creatorValuesByIndex = creatorMapping != null ? new() : null;
                                        context.regularValuesByIndex = creatorMapping != null ? new() : null;
                                        discriminatorConsumed = true;
                                    }
                                    else
                                    {
                                        // Any tag read here was not the discriminator tag, so it belongs to the
                                        // first item and must survive for that item's own converter — an RFC 8746
                                        // typed array is decoded from its tag.
                                        reader.ReturnToBookmark(bookmark);
                                        context.converter = this;
                                    }

                                    // No position is counted here, in either branch. The discriminator
                                    // is not one of the members positions are counted over, so when it
                                    // was present this call consumed its item and the next call is
                                    // still at position 0; when it was absent, the item this call is
                                    // looking at IS position 0. Counting one here instead made the
                                    // read start at the second member of a type whose declared indexes
                                    // did not happen to begin at 1, and made that offset depend on
                                    // whether some other type in the hierarchy had been registered.
                                }
                                break;
                        }
                    }
                    else
                    {
                        // No convention resolved for the declared type, which for an interface or an
                        // abstract class is what a subtype nobody registered looks like from here: the
                        // document may carry a discriminator and nothing above has looked for one. Asked
                        // before CreateInstance so the caller is told which registration is missing
                        // rather than that a CreatorMapping is.
                        if (_isInterfaceOrAbstract)
                        {
                            ThrowIfADiscriminatorIsPresent(ref reader);
                        }

                        context.converter = this;
                    }

                    // The read state was built from this converter's required members, which on a
                    // polymorphic read is the declared type's - so a member required only by the
                    // resolved type had nothing tracking it, and the check at the end of Read, which
                    // iterates the resolved converter's list, was skipped entirely. Enabled here
                    // because this is the point the converter is settled and no member has been read
                    // yet. Only when the discriminator moved the read off this converter: where it did
                    // not, the constructor asked this same question of this same list already.
                    if (context.converter != this
                        && context.converter.RequiredMemberConvertersForRead.Count != 0)
                    {
                        context.readState.TrackRequiredMembers();
                    }
                }

                // Settled once, on the first item, and not revisited: every member of this object is
                // resolved by the converter chosen here, so the ordinals its entries carry mean the
                // same thing for the whole read -- which is what lets one bitmask stand for "already
                // read" across it.
                BeginObject(ref context);

                if (discriminatorConsumed)
                {
                    // The discriminator was this item, and reading it left the reader on the next one
                    // with no bookmark to return to, so there is no member here to read. Asking
                    // whether the converter had changed instead answered this correctly only while
                    // the resolved type differed from the declared one: a document read as the very
                    // type that wrote it resolves to the converter the read started on, and the read
                    // went on to take the following item for the discriminator's own slot.
                    return;
                }
            }
            else if (context.converter == null)
            {
                context.converter = this;
            }

            switch (_objectMapping.ObjectFormat)
            {
                case CborObjectFormat.StringKeyMap:
                    {
                        ReadOnlySpan<byte> memberName = reader.ReadRawString();

                        try
                        {
                            if (context.creatorValues == null)
                            {
                                if (_isStruct)
                                {
                                    ReadValueForStruct(ref reader, ref context.obj, memberName, ref context.readState);
                                }
                                else
                                {
                                    context.converter.ReadValue(ref reader, context.obj!, memberName, ref context.readState);
                                }
                            }
                            else if (context.converter.ReadValue(ref reader, memberName, ref context.readState, out object? value))
                            {
                                if (context.converter.ObjectMapping.IsCreatorMember(memberName))
                                {
                                    AddMemberValue(ref reader, context.readState.Mode, context.creatorValues, new RawString(memberName), value);
                                }
                                else
                                {
                                    AddMemberValue(ref reader, context.readState.Mode, context.regularValues!, new RawString(memberName), value);
                                }
                            }
                        }
                        // The name as it appears in the document. Decoding it in the filter rather than
                        // keeping it around costs nothing until something has already failed.
                        catch (CborException exception) when (exception.PrependPathMemberAndDecline(Encoding.UTF8.GetString(memberName)))
                        {
                            // Not entered: the filter records the name and declines.
                            throw;
                        }
                    }
                    break;
                case CborObjectFormat.IntKeyMap:
                    {
                        int memberIndex = reader.ReadInt32();

                        try
                        {
                            if (context.creatorValuesByIndex == null)
                            {
                                if (_isStruct)
                                {
                                    ReadValueForStruct(ref reader, ref context.obj, memberIndex, ref context.readState);
                                }
                                else
                                {
                                    context.converter.ReadValue(ref reader, context.obj!, memberIndex, ref context.readState);
                                }
                            }
                            else if (context.converter.ReadValue(ref reader, memberIndex, ref context.readState, out object? value))
                            {
                                if (context.converter.ObjectMapping.IsCreatorMember(memberIndex))
                                {
                                    AddMemberValue(ref reader, context.readState.Mode, context.creatorValuesByIndex, memberIndex, value);
                                }
                                else
                                {
                                    AddMemberValue(ref reader, context.readState.Mode, context.regularValuesByIndex!, memberIndex, value);
                                }
                            }
                        }
                        catch (CborException exception) when (PushMemberSegmentAndDecline(exception, context.converter, memberIndex))
                        {
                            // Not entered: the filter records the member and declines.
                            throw;
                        }
                    }
                    break;
                case CborObjectFormat.Array:
                    {
                        // An array names nothing, so what the document supplies is a position, and the
                        // member holding that position is the one the type declares there. The declared
                        // index is then what everything downstream is keyed on -- the read lookups, the
                        // creator's member list, the failure path -- exactly as in the map formats,
                        // because that index is what those were built from. Reading the position itself
                        // as the index is what made a type whose indexes did not start at 0 and run
                        // consecutively lose its members, since only then are the two the same number.
                        IReadOnlyList<int> memberIndexesByPosition = context.converter.MemberIndexesByPosition;
                        int position = context.memberPosition++;

                        if (position >= memberIndexesByPosition.Count)
                        {
                            // More items than the type has members to put in them. There is no declared
                            // index to name, so the position is reported instead - the only thing about
                            // this item the type has anything to say about.
                            HandleUnknownIndex(ref reader, typeof(T), position);
                            reader.SkipDataItem();
                            break;
                        }

                        int memberIndex = memberIndexesByPosition[position];

                        try
                        {
                            if (context.creatorValuesByIndex == null)
                            {
                                if (_isStruct)
                                {
                                    ReadValueForStruct(ref reader, ref context.obj, memberIndex, ref context.readState);
                                }
                                else
                                {
                                    context.converter.ReadValue(ref reader, context.obj!, memberIndex, ref context.readState);
                                }
                            }
                            else if (context.converter.ReadValue(ref reader, memberIndex, ref context.readState, out object? value))
                            {
                                if (context.converter.ObjectMapping.IsCreatorMember(memberIndex))
                                {
                                    AddMemberValue(ref reader, context.readState.Mode, context.creatorValuesByIndex, memberIndex, value);
                                }
                                else
                                {
                                    AddMemberValue(ref reader, context.readState.Mode, context.regularValuesByIndex!, memberIndex, value);
                                }
                            }
                        }
                        catch (CborException exception) when (PushMemberSegmentAndDecline(exception, context.converter, memberIndex))
                        {
                            // Not entered: the filter records the member and declines.
                            throw;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Names the member at <paramref name="memberIndex"/> in the failure path, by the name it has
        /// on the type rather than by its position, which is what a caller reading the message is
        /// looking for. Falls back to the position when the index maps to no member -- an index the
        /// document invented, which has no name to give. Returns <c>false</c> so that it can be an
        /// exception filter; see <see cref="CborException.PrependPathMemberAndDecline(string)"/>.
        /// </summary>
        private static bool PushMemberSegmentAndDecline(CborException exception, IObjectConverter converter, int memberIndex)
        {
            // The name comes from the mapping rather than from the member converter: an integer-keyed
            // member has no name in the document, so the converter carries an empty one, and the name
            // on the type is exactly what the document does not say and the caller needs told.
            foreach (IMemberMapping memberMapping in converter.ObjectMapping.MemberMappings)
            {
                if (memberMapping.MemberIndex == memberIndex && memberMapping.MemberInfo != null)
                {
                    exception.PrependPathMember(memberMapping.MemberInfo.Name);
                    return false;
                }
            }

            exception.PrependPathIndex(memberIndex);
            return false;
        }

        private void ReadValueForStruct(ref CborReader reader, ref T instance, ReadOnlySpan<byte> memberName, ref MemberReadState state)
        {
            if (_memberConvertersForRead.TryGetValue(memberName, out MemberReadEntry entry))
            {
                MarkMemberRead(ref reader, ref state, entry, memberName);

                ((IMemberConverter<T>)entry.Converter).Read(ref reader, ref instance);
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        private void ReadValueForStruct(ref CborReader reader, ref T instance, int memberIndex, ref MemberReadState state)
        {
            if (_memberConvertersForReadByIndex.TryGetValue(memberIndex, out MemberReadEntry entry))
            {
                MarkMemberRead(ref reader, ref state, entry, memberIndex);

                ((IMemberConverter<T>)entry.Converter).Read(ref reader, ref instance);
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        /// <summary>
        /// Records that the document has supplied this member, and refuses it if the document has
        /// supplied it once already and <see cref="DuplicateKeyMode.Reject"/> is in force. Every site
        /// that resolves a member converter goes through here, so that the assign path, the struct
        /// path and both creator paths cannot drift apart on what a repeated member means.
        /// </summary>
        /// <remarks>
        /// A key matching no member is not tracked and so is never refused as a repeat: it has no
        /// member to be a repeat of, and what becomes of it is
        /// <see cref="CborOptions.UnhandledNameMode"/>'s question rather than this one's.
        /// </remarks>
        private static void MarkMemberRead(
            ref CborReader reader, ref MemberReadState state, in MemberReadEntry entry, ReadOnlySpan<byte> memberName)
        {
            if (state.MarkRead(entry))
            {
                // Decoded here rather than kept around: the name is in hand as bytes, and is only
                // wanted as text once the read has already failed.
                throw reader.BuildException(MapKeyErrors.Duplicate(Encoding.UTF8.GetString(memberName)));
            }
        }

        /// <inheritdoc cref="MarkMemberRead(ref CborReader, ref MemberReadState, in MemberReadEntry, ReadOnlySpan{byte})"/>
        private static void MarkMemberRead(
            ref CborReader reader, ref MemberReadState state, in MemberReadEntry entry, int memberIndex)
        {
            if (state.MarkRead(entry))
            {
                throw reader.BuildException(MapKeyErrors.Duplicate(memberIndex));
            }
        }

        private static int CompareMembersForDeterministicOrder(IMemberConverter x, IMemberConverter y)
        {
            // IntKeyMap/Array members carry an index and no name. Ordinary StringKeyMap members carry
            // a name and no index. But DiscriminatorMemberConverter.MemberIndex is hardcoded to 0 in
            // every format, so a StringKeyMap type with a discriminator has one entry with BOTH a
            // MemberIndex and a MemberName. That still routes correctly, because the two-sided
            // HasValue test below only takes the int branch when *both* sides carry an index -- an
            // ordinary StringKeyMap member's MemberIndex is null, so comparing it against the
            // discriminator entry always falls through to CompareTextKeys, which is the comparison
            // that matches how the whole map is actually keyed.
            if (x.MemberIndex.HasValue && y.MemberIndex.HasValue)
            {
                return CborKeyComparer.CompareIntKeys(x.MemberIndex.Value, y.MemberIndex.Value);
            }

            return CborKeyComparer.CompareTextKeys(x.MemberName, y.MemberName);
        }

        /// <summary>
        /// Collects one member value for a type with a creator mapping, where values are held until the
        /// constructor can be called rather than being set on an instance.
        /// </summary>
        /// <remarks>
        /// A repeated member is refused before it gets here: every value reaching this has come
        /// through <c>ReadValue</c>, which resolves the member and calls <c>MarkMemberRead</c> first,
        /// so under <see cref="DuplicateKeyMode.Reject"/> the <c>Add</c> below cannot be handed a key
        /// it already holds. That is deliberate - one place decides what a repeated member means, for
        /// the creator path and the assign path alike, rather than each learning it from the container
        /// it happens to write into. What is left here is the container's own refusals, and a repeat
        /// this converter could not identify, which <c>Add</c> still catches and names correctly.
        /// </remarks>
        private static void AddMemberValue<TKey>(
            ref CborReader reader, DuplicateKeyMode mode, Dictionary<TKey, object> values, TKey key, object value)
            where TKey : notnull
        {
            if (mode == DuplicateKeyMode.LastWins)
            {
                values[key] = value;
                return;
            }

            try
            {
                values.Add(key, value);
            }
            catch (ArgumentException exception)
            {
                throw reader.BuildException(values.ContainsKey(key)
                    ? MapKeyErrors.Duplicate(key)
                    : MapKeyErrors.Rejected(key, exception.Message));
            }
        }

        /// <summary>
        /// Refuses a document carrying a discriminator that no convention resolved for
        /// <typeparamref name="T"/>, naming the registration that would resolve it.
        /// </summary>
        /// <remarks>
        /// Only reachable where this converter cannot instantiate its own type and no convention was
        /// found for it — a base declared abstract or as an interface, whose subtype was never passed to
        /// <c>DiscriminatorConventionRegistry.RegisterType</c>. Writing needs no registration, because it
        /// goes through the runtime type and registers as a side effect of building that type's
        /// converter; reading only ever names the declared type, so the asymmetry is easy to walk into
        /// and the document looks complete.
        /// <para>
        /// Only the string-keyed arm depends on a convention, and there every registered one is asked,
        /// since which of them would have written the key is exactly what is not known here. The other
        /// two formats put the discriminator at a place no convention gets to choose — index 0, or the
        /// first item behind the semantic tag — so they are probed once and hold even where the
        /// conventions have been cleared. Where nothing is found the document really is undiscriminated,
        /// and the missing <c>CreatorMapping</c> that <see cref="CreateInstance"/> reports is the right
        /// answer.
        /// </para>
        /// <para>
        /// The place is all any of this has to go on, so a type that is not polymorphic at all but does
        /// declare a member where a discriminator would sit — <c>_t</c>, or index 0 — is reported as an
        /// unregistered subtype rather than as the missing creator it is. Nothing in the document tells
        /// the two apart; the resolution path above reads the same place on the same evidence.
        /// </para>
        /// </remarks>
        private void ThrowIfADiscriminatorIsPresent(ref CborReader reader)
        {
            if (_objectMapping.ObjectFormat == CborObjectFormat.StringKeyMap)
            {
                foreach (IDiscriminatorConvention convention in _registry.DiscriminatorConventionRegistry.Conventions)
                {
                    CborReaderBookmark namedBookmark = reader.GetBookmark();
                    CborValue? named = FindItem(ref reader, convention.MemberName) ? ReadDiscriminatorValue(ref reader) : null;
                    reader.ReturnToBookmark(namedBookmark);

                    ThrowIfPresent(named);
                }

                return;
            }

            CborReaderBookmark bookmark = reader.GetBookmark();

            bool present = _objectMapping.ObjectFormat == CborObjectFormat.IntKeyMap
                ? FindItem(ref reader, 0) // discriminator index is always 0
                : reader.TryReadSemanticTag(out ulong semanticTag) && semanticTag == _options.DiscriminatorSemanticTag;

            CborValue? discriminator = present ? ReadDiscriminatorValue(ref reader) : null;
            reader.ReturnToBookmark(bookmark);

            ThrowIfPresent(discriminator);
        }

        private static void ThrowIfPresent(CborValue? discriminator)
        {
            if (discriminator != null)
            {
                throw new CborException(
                    MemberMappingErrors.UnregisteredDiscriminatedSubtype(typeof(T), discriminator));
            }
        }

        /// <summary>
        /// The discriminator value the reader is sitting on, as a <see cref="CborValue"/>.
        /// </summary>
        /// <remarks>
        /// Read as a value rather than through the convention, which would resolve it to a
        /// <see cref="Type"/> and fail for the very value being reported. That also renders a string and
        /// an integer discriminator without this method knowing which it is.
        /// </remarks>
        private CborValue ReadDiscriminatorValue(ref CborReader reader)
        {
            return _registry.ConverterRegistry.Lookup<CborValue>().Read(ref reader);
        }

        private static bool FindItem(ref CborReader reader, ReadOnlySpan<byte> name)
        {
            do
            {
                ReadOnlySpan<byte> memberName = reader.ReadRawString();
                if (memberName.SequenceEqual(name))
                {
                    return true;
                }

                reader.SkipDataItem();
            }
            while (reader.MoveNextMapItem());

            return false;
        }

        private static bool FindItem(ref CborReader reader, int index)
        {
            do
            {
                int memberIndex = reader.ReadInt32();
                if (memberIndex == index)
                {
                    return true;
                }

                reader.SkipDataItem();
            }
            while (reader.MoveNextMapItem());

            return false;
        }

        private int GetSize(ref WriterContext context)
        {
            if (context.lengthMode == LengthMode.IndefiniteLength)
            {
                return -1;
            }

            int writableMembersCount = 0;

            foreach (IMemberConverter memberConverter in context.memberConvertersForWrite)
            {
                if (HoldsAPosition(memberConverter, ref context))
                {
                    // Written whatever ShouldSerialize says: see WriteItem.
                    writableMembersCount++;
                }
                else if (_isStruct)
                {
                    IMemberConverter<T> typedMemberConverter = (IMemberConverter<T>)memberConverter;

                    if (typedMemberConverter.ShouldSerialize(ref context.obj, typeof(T)))
                    {
                        writableMembersCount++;
                    }
                }
                else if (memberConverter.ShouldSerialize(context.obj!, typeof(T), context.options))
                {
                    writableMembersCount++;
                }
            }

            return writableMembersCount;
        }

        /// <summary>
        /// Whether this member is addressed by nothing but its position in the document, and so has
        /// to be written whatever <c>ShouldSerialize</c> says about it.
        /// </summary>
        /// <remarks>
        /// <see cref="CborObjectFormat.Array"/> carries no keys, so an omitted member slides every
        /// member after it a position earlier, onto the wrong member on the read; the format has no
        /// way to express absence, so the member is written with its current value -- the default, in
        /// the very case the predicate exists to catch -- and the predicate is consulted only where a
        /// key travels with the value to say which member it belongs to. That is the same reasoning
        /// that keeps <see cref="CborObjectFormat.Array"/> out of the deterministic member sort in
        /// <see cref="MemberConvertersForWrite"/>.
        /// <para>
        /// The discriminator is the exception: it is not a member of the type and holds no position,
        /// being written under a semantic tag and recognised by it on the read. Whether it appears at
        /// all is the discriminator policy's business, so it keeps answering for itself.
        /// </para>
        /// </remarks>
        private static bool HoldsAPosition(IMemberConverter memberConverter, ref WriterContext context)
        {
            return context.objectFormat == CborObjectFormat.Array
                && memberConverter is not IDiscriminatorMemberConverter;
        }

        private bool WriteItem(ref CborWriter writer, ref WriterContext context)
        {
            while (context.memberIndex < context.memberConvertersForWrite.Count)
            {
                IMemberConverter memberConverter = context.memberConvertersForWrite[context.memberIndex++];
                if (_isStruct)
                {
                    IMemberConverter<T> typedMemberConverter = (IMemberConverter<T>)memberConverter;

                    if (HoldsAPosition(memberConverter, ref context) || typedMemberConverter.ShouldSerialize(ref context.obj, typeof(T)))
                    {
                        switch (context.objectFormat)
                        {
                            case CborObjectFormat.StringKeyMap:
                                writer.WriteString(memberConverter.MemberName);
                                break;
                            case CborObjectFormat.IntKeyMap:
                                if (memberConverter.MemberIndex.HasValue)
                                {
                                    writer.WriteInt32(memberConverter.MemberIndex.Value);
                                }
                                break;
                            case CborObjectFormat.Array:
                                //nothing to write here
                                break;
                        }

                        typedMemberConverter.Write(ref writer, ref context.obj);
                        break;
                    }
                }
                else if (HoldsAPosition(memberConverter, ref context) || memberConverter.ShouldSerialize(context.obj!, typeof(T), context.options))
                {
                    switch (context.objectFormat)
                    {
                        case CborObjectFormat.StringKeyMap:
                            writer.WriteString(memberConverter.MemberName);
                            break;
                        case CborObjectFormat.IntKeyMap:
                            if (memberConverter.MemberIndex.HasValue)
                            {
                                writer.WriteInt32(memberConverter.MemberIndex.Value);
                            }
                            break;
                        case CborObjectFormat.Array:
                            //nothing to write here
                            break;
                    }

                    memberConverter.Write(ref writer, context.obj!);
                    break;
                }
            }

            return context.memberIndex < context.memberConvertersForWrite.Count;
        }

        /// <summary>
        /// A key that matched no member. That is the discriminator - which is a key of the map without
        /// being a member of the type - or a name the type does not know, and the two are answered
        /// differently: the discriminator is expected here and has already been read by the
        /// convention, so it is skipped rather than reported as unhandled, while a repeat of it is a
        /// duplicate map key like any other.
        /// </summary>
        /// <remarks>
        /// Reached only once a lookup has already missed, so recognising the discriminator costs
        /// nothing on the path taken by every member that maps to something.
        /// </remarks>
        private void HandleUnmappedName(ref CborReader reader, ref MemberReadState state, ReadOnlySpan<byte> memberName)
        {
            if (_hasDiscriminatorMember
                && GetDiscriminatorConvention() is IDiscriminatorConvention convention
                && memberName.SequenceEqual(convention.MemberName))
            {
                if (state.MarkDiscriminatorRead())
                {
                    throw reader.BuildException(MapKeyErrors.Duplicate(Encoding.UTF8.GetString(memberName)));
                }
            }
            else
            {
                HandleUnknownName(ref reader, typeof(T), memberName);
            }

            reader.SkipDataItem();
        }

        /// <inheritdoc cref="HandleUnmappedName"/>
        private void HandleUnmappedIndex(ref CborReader reader, ref MemberReadState state, int memberIndex)
        {
            // The discriminator is always written at index 0 -- see DiscriminatorMapping.MemberIndex --
            // so in an integer-keyed map that index is its own, for a type that writes one.
            if (_hasDiscriminatorMember && memberIndex == 0)
            {
                if (state.MarkDiscriminatorRead())
                {
                    throw reader.BuildException(MapKeyErrors.Duplicate(memberIndex));
                }
            }
            else
            {
                HandleUnknownIndex(ref reader, typeof(T), memberIndex);
            }

            reader.SkipDataItem();
        }

        private void HandleUnknownName(ref CborReader reader, Type type, ReadOnlySpan<byte> rawName)
        {
            if (_options.UnhandledNameMode == UnhandledNameMode.ThrowException)
            {
                // The name is whatever the document chose to send, so it is bounded and escaped on the
                // same terms as any other document text rather than copied into the message whole.
                // UTF-8 rather than ASCII: a CBOR text string is UTF-8, and decoding it as ASCII turned
                // every non-ASCII member name into a row of question marks.
                throw reader.BuildException(
                    $"Unhandled name [{TextTruncation.Ellipsize(Encoding.UTF8.GetString(rawName))}] in class [{type.Name}] while deserializing.");
            }
        }

        private void HandleUnknownIndex(ref CborReader reader, Type type, int memberIndex)
        {
            if (_options.UnhandledNameMode == UnhandledNameMode.ThrowException)
            {
                throw reader.BuildException($"Unhandled index [{memberIndex}] in class [{type.Name}] while deserializing.");
            }
        }
    }
}
