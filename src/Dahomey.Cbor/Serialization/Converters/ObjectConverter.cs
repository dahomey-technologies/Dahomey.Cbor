using Dahomey.Cbor.Attributes;
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

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IObjectConverter
    {
        void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers);
        void ReadValue(ref CborReader reader, object obj, int memberIndex, HashSet<IMemberConverter> readMembers);
        bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value);
        bool ReadValue(ref CborReader reader, int memberIndex, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value);
        IReadOnlyList<IMemberConverter> MemberConvertersForWrite { get; }
        ByteBufferDictionary<IMemberConverter> MemberConvertersForRead { get; }
        Dictionary<int, IMemberConverter> MemberConvertersForReadByIndex { get; }
        IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead { get; }
        IObjectMapping ObjectMapping { get; }
    }

    public interface IObjectConverter<out T> : IObjectConverter
    {
        T CreateInstance();
    }

    public class ObjectConverter<T> :
        CborConverterBase<T>,
        IObjectConverter<T>,
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
            public HashSet<IMemberConverter>? readMembers;
            public int memberIndex;
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

        private readonly ByteBufferDictionary<IMemberConverter> _memberConvertersForRead = new ByteBufferDictionary<IMemberConverter>();
        private readonly Dictionary<int, IMemberConverter> _memberConvertersForReadByIndex = new();
        public List<IMemberConverter> _requiredMemberConvertersForRead = new List<IMemberConverter>();
        private readonly List<IMemberConverter> _memberConvertersForWrite;
        private List<IMemberConverter>? _deterministicMemberConvertersForWrite;
        private readonly CborOptions _options;
        private readonly SerializationRegistry _registry;
        private readonly IObjectMapping _objectMapping;
        private readonly Func<T>? _constructor;
        private readonly bool _isInterfaceOrAbstract;
        private readonly bool _isStruct;
        private readonly IDiscriminatorConvention? _discriminatorConvention = null;

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

                // The member set itself is fixed at construction, so its sorted permutation is too, and
                // is computed once. Two threads racing here build identical lists; the reference
                // assignment is atomic, so the loser's copy is simply dropped.
                List<IMemberConverter>? sorted = _deterministicMemberConvertersForWrite;

                if (sorted == null)
                {
                    sorted = new List<IMemberConverter>(_memberConvertersForWrite);
                    sorted.Sort(CompareMembersForDeterministicOrder);
                    _deterministicMemberConvertersForWrite = sorted;
                }

                return sorted;
            }
        }
        public ByteBufferDictionary<IMemberConverter> MemberConvertersForRead => _memberConvertersForRead;
        public Dictionary<int, IMemberConverter> MemberConvertersForReadByIndex => _memberConvertersForReadByIndex;
        public IReadOnlyList<IMemberConverter> RequiredMemberConvertersForRead => _requiredMemberConvertersForRead;
        public IObjectMapping ObjectMapping => _objectMapping;

        public ObjectConverter(CborOptions options)
        {
            _options = options;
            _registry = options.Registry;
            _objectMapping = _registry.ObjectMappingRegistry.Lookup<T>();

            _memberConvertersForWrite = new List<IMemberConverter>();

            foreach (IMemberMapping memberMapping in _objectMapping.GetMemberMappingsForConverter(this))
            {
                IMemberConverter memberConverter = memberMapping.GenerateMemberConverter();

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
                    switch (_objectMapping.ObjectFormat)
                    {
                        case CborObjectFormat.StringKeyMap:
                            _memberConvertersForRead.Add(memberConverter.MemberName, memberConverter);
                            break;
                        case CborObjectFormat.IntKeyMap:
                        case CborObjectFormat.Array:
                            if (memberConverter.MemberIndex.HasValue)
                            {
                                _memberConvertersForReadByIndex.Add(memberConverter.MemberIndex.Value, memberConverter);
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
                }
            }

            _isInterfaceOrAbstract = typeof(T).IsInterface || typeof(T).IsAbstract;
            _isStruct = typeof(T).IsStruct();

            if (!_isInterfaceOrAbstract && !_isStruct && _objectMapping.CreatorMapping == null)
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

            _discriminatorConvention = _registry.DiscriminatorConventionRegistry.GetConvention(typeof(T));
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
                readMembers = _requiredMemberConvertersForRead.Count != 0 ? new HashSet<IMemberConverter>() : null
            };

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
                context.converter = this;
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
                                if (!context.converter.MemberConvertersForRead.TryGetValue(value.Key.Buffer.Span, out IMemberConverter? memberConverter))
                                {
                                    // should not happen
                                    throw new CborException("Unexpected error");
                                }

                                memberConverter.Set(context.obj, value.Value);
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
                                if (!context.converter.MemberConvertersForReadByIndex.TryGetValue(value.Key, out IMemberConverter? memberConverter))
                                {
                                    // should not happen
                                    throw new CborException("Unexpected error");
                                }

                                memberConverter.Set(context.obj, value.Value);
                            }
                        }
                        break;
                }
            }

            if (context.readMembers != null)
            {
                foreach (IMemberConverter memberConverter in context.converter.RequiredMemberConvertersForRead)
                {
                    if (!context.readMembers.Contains(memberConverter))
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

        public void ReadValue(ref CborReader reader, object obj, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers)
        {
            T value = (T)obj;

            if (!_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter? memberConverter))
            {
                HandleUnknownName(ref reader, typeof(T), memberName);
                reader.SkipDataItem();
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                memberConverter.Read(ref reader, value);
            }
        }
        public void ReadValue(ref CborReader reader, object obj, int memberIndex, HashSet<IMemberConverter> readMembers)
        {
            T value = (T)obj;

            if (!_memberConvertersForReadByIndex.TryGetValue(memberIndex, out IMemberConverter? memberConverter))
            {
                HandleUnknownIndex(ref reader, typeof(T), memberIndex);
                reader.SkipDataItem();
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                memberConverter.Read(ref reader, value);
            }
        }

        public bool ReadValue(ref CborReader reader, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value)
        {
            if (!_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter? memberConverter))
            {
                HandleUnknownName(ref reader, typeof(T), memberName);
                reader.SkipDataItem();
                value = default!;
                return false;
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                value = memberConverter.Read(ref reader);
                return true;
            }
        }

        public bool ReadValue(ref CborReader reader, int memberIndex, HashSet<IMemberConverter> readMembers, [MaybeNullWhen(false)] out object value)
        {
            if (!_memberConvertersForReadByIndex.TryGetValue(memberIndex, out IMemberConverter? memberConverter))
            {
                HandleUnknownIndex(ref reader, typeof(T), memberIndex);
                reader.SkipDataItem();
                value = default!;
                return false;
            }
            else
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }
                value = memberConverter.Read(ref reader);
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

        private void ReadItem(ref CborReader reader, ref ReaderContext context)
        {
            if (context.obj == null || context.converter == null)
            {
                if (context.converter == null)
                {
                    if (_discriminatorConvention != null)
                    {
                        switch (_objectMapping.ObjectFormat)
                        {
                            case CborObjectFormat.StringKeyMap:
                                {
                                    CborReaderBookmark bookmark = reader.GetBookmark();

                                    if (FindItem(ref reader, _discriminatorConvention.MemberName))
                                    {
                                        // discriminator value
                                        Type actualType = _discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
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
                                        Type actualType = _discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
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
                                        Type actualType = _discriminatorConvention.ReadDiscriminator(ref reader);

                                        if (!_objectMapping.ObjectType.IsAssignableFrom(actualType))
                                        {
                                            throw new CborException($"expected type {_objectMapping.ObjectType} is not assignable from actual type {actualType}");
                                        }

                                        context.converter = (IObjectConverter<T>)_registry.ConverterRegistry.Lookup(actualType);
                                        ICreatorMapping? creatorMapping = context.converter.ObjectMapping.CreatorMapping;
                                        context.creatorValuesByIndex = creatorMapping != null ? new() : null;
                                        context.regularValuesByIndex = creatorMapping != null ? new() : null;
                                    }
                                    else
                                    {
                                        // Any tag read here was not the discriminator tag, so it belongs to the
                                        // first item and must survive for that item's own converter — an RFC 8746
                                        // typed array is decoded from its tag.
                                        reader.ReturnToBookmark(bookmark);
                                        context.converter = this;
                                    }

                                    // increment to skip discriminator index even when the semantic tag is not present
                                    context.memberIndex++;
                                }
                                break;
                        }
                    }
                    else
                    {
                        context.converter = this;
                    }
                }

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

                if (_objectMapping.ObjectFormat == CborObjectFormat.Array && context.converter != this)
                {
                    // discrimnator read with no ReturnToBoomark, must exit here
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
                        if (context.creatorValues == null)
                        {
                            if (_isStruct)
                            {
                                ReadValueForStruct(ref reader, ref context.obj, memberName, context.readMembers!);
                            }
                            else
                            {
                                context.converter.ReadValue(ref reader, context.obj!, memberName, context.readMembers!);
                            }
                        }
                        else if (context.converter.ReadValue(ref reader, memberName, context.readMembers!, out object? value))
                        {
                            if (context.converter.ObjectMapping.IsCreatorMember(memberName))
                            {
                                AddMemberValue(ref reader, context.creatorValues, new RawString(memberName), value);
                            }
                            else
                            {
                                AddMemberValue(ref reader, context.regularValues!, new RawString(memberName), value);
                            }
                        }
                    }
                    break;
                case CborObjectFormat.IntKeyMap:
                    {
                        int memberIndex = reader.ReadInt32();

                        if (context.creatorValuesByIndex == null)
                        {
                            if (_isStruct)
                            {
                                ReadValueForStruct(ref reader, ref context.obj, memberIndex, context.readMembers!);
                            }
                            else
                            {
                                context.converter.ReadValue(ref reader, context.obj!, memberIndex, context.readMembers!);
                            }
                        }
                        else if (context.converter.ReadValue(ref reader, memberIndex, context.readMembers!, out object? value))
                        {
                            if (context.converter.ObjectMapping.IsCreatorMember(memberIndex))
                            {
                                AddMemberValue(ref reader, context.creatorValuesByIndex, memberIndex, value);
                            }
                            else
                            {
                                AddMemberValue(ref reader, context.regularValuesByIndex!, memberIndex, value);
                            }
                        }
                    }
                    break;
                case CborObjectFormat.Array:
                    if (context.creatorValuesByIndex == null)
                    {
                        if (_isStruct)
                        {
                            ReadValueForStruct(ref reader, ref context.obj, context.memberIndex, context.readMembers!);
                        }
                        else
                        {
                            context.converter.ReadValue(ref reader, context.obj!, context.memberIndex, context.readMembers!);
                        }
                    }
                    else if (context.converter.ReadValue(ref reader, context.memberIndex, context.readMembers!, out object? value))
                    {
                        if (context.converter.ObjectMapping.IsCreatorMember(context.memberIndex))
                        {
                            AddMemberValue(ref reader, context.creatorValuesByIndex, context.memberIndex, value);
                        }
                        else
                        {
                            AddMemberValue(ref reader, context.regularValuesByIndex!, context.memberIndex, value);
                        }
                    }

                    context.memberIndex++;
                    break;
            }
        }

        private void ReadValueForStruct(ref CborReader reader, ref T instance, ReadOnlySpan<byte> memberName, HashSet<IMemberConverter> readMembers)
        {
            if (_memberConvertersForRead.TryGetValue(memberName, out IMemberConverter? memberConverter))
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }

                ((IMemberConverter<T>)memberConverter).Read(ref reader, ref instance);
            }
            else
            {
                reader.SkipDataItem();
            }
        }

        private void ReadValueForStruct(ref CborReader reader, ref T instance, int memberIndex, HashSet<IMemberConverter> readMembers)
        {
            if (_memberConvertersForReadByIndex.TryGetValue(memberIndex, out IMemberConverter? memberConverter))
            {
                if (readMembers != null)
                {
                    readMembers.Add(memberConverter);
                }

                ((IMemberConverter<T>)memberConverter).Read(ref reader, ref instance);
            }
            else
            {
                reader.SkipDataItem();
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
        /// A document repeating a member reaches a <c>Dictionary.Add</c> here, exactly as a repeated
        /// map key reaches one in the dictionary converters, and has to be refused the same way: as a
        /// <see cref="CborException"/> rather than the <see cref="ArgumentException"/> the dictionary
        /// raises. A type without a creator mapping never reaches this -- its members are assigned, so
        /// the last occurrence wins there and always has.
        /// </remarks>
        private static void AddMemberValue<TKey>(
            ref CborReader reader, Dictionary<TKey, object?> values, TKey key, object? value)
            where TKey : notnull
        {
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
                if (_isStruct)
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

        private bool WriteItem(ref CborWriter writer, ref WriterContext context)
        {
            while (context.memberIndex < context.memberConvertersForWrite.Count)
            {
                IMemberConverter memberConverter = context.memberConvertersForWrite[context.memberIndex++];
                if (_isStruct)
                {
                    IMemberConverter<T> typedMemberConverter = (IMemberConverter<T>)memberConverter;

                    if (typedMemberConverter.ShouldSerialize(ref context.obj, typeof(T)))
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
                else if (memberConverter.ShouldSerialize(context.obj!, typeof(T), context.options))
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

        private void HandleUnknownName(ref CborReader reader, Type type, ReadOnlySpan<byte> rawName)
        {
            if (_options.UnhandledNameMode == UnhandledNameMode.ThrowException)
            {
                throw reader.BuildException($"Unhandled name [{Encoding.ASCII.GetString(rawName)}] in class [{type.Name}] while deserializing.");
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
