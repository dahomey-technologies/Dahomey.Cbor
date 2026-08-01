using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Util;
using System;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    /// <summary>
    /// An <see cref="IMemberMapping"/> whose accessors are supplied as delegates rather than
    /// discovered from a <see cref="System.Reflection.MemberInfo"/>.
    /// </summary>
    /// <remarks>
    /// This is the reflection-free counterpart of <see cref="MemberMapping{T}"/>, and the building
    /// block a source generator (or hand-written AOT-safe configuration) emits. Because the getter
    /// and setter arrive as ordinary delegates — typically compiler-generated lambdas such as
    /// <c>o => o.Name</c> and <c>(o, v) => o.Name = v</c> — nothing here needs
    /// <c>Expression.Compile</c>, <c>MakeGenericType</c> or <c>Activator.CreateInstance</c>.
    /// <para>
    /// The member converter is resolved lazily through <see cref="CborConverterRegistry.Lookup{T}()"/>,
    /// which is generic and therefore AOT-safe. Laziness also lets self-referential types map without
    /// infinite recursion, matching the behaviour of <see cref="MemberMapping{T}"/>.
    /// </para>
    /// Use with <see cref="ObjectMapping{T}.SetMemberMappings"/> or
    /// <see cref="ObjectMapping{T}.AddMemberMappings"/> — do not call
    /// <see cref="ObjectMapping{T}.AutoMap"/>, which is the reflection path.
    /// </remarks>
    /// <typeparam name="T">The declaring class.</typeparam>
    /// <typeparam name="TM">The member type.</typeparam>
    public class DelegateMemberMapping<T, TM> : IMemberMapping
    {
        private readonly CborConverterRegistry _converterRegistry;
        private readonly Func<T, TM>? _memberGetter;
        private readonly Action<T, TM>? _memberSetter;
        private ICborConverter<TM>? _converter;

        /// <summary>Always null: this mapping is defined by delegates, not by reflection.</summary>
        public MemberInfo? MemberInfo => null;

        public Type MemberType => typeof(TM);
        public string? MemberName { get; private set; }
        public int? MemberIndex { get; private set; }

        public ICborConverter? Converter => ResolveConverter();

        public bool CanBeDeserialized => _memberSetter != null;
        public bool CanBeSerialized => _memberGetter != null;

        public object? DefaultValue { get; private set; }
        public bool IgnoreIfDefault { get; private set; }
        public Func<object, bool>? ShouldSerializeMethod { get; private set; }
        public LengthMode LengthMode { get; private set; }
        public RequirementPolicy RequirementPolicy { get; private set; }

        /// <param name="converterRegistry">Registry used to resolve the converter for <typeparamref name="TM"/>.</param>
        /// <param name="memberGetter">Reads the member. Null makes the member write-only.</param>
        /// <param name="memberSetter">Writes the member. Null makes the member read-only.</param>
        public DelegateMemberMapping(
            CborConverterRegistry converterRegistry,
            Func<T, TM>? memberGetter,
            Action<T, TM>? memberSetter)
        {
            _converterRegistry = converterRegistry ?? throw new ArgumentNullException(nameof(converterRegistry));
            _memberGetter = memberGetter;
            _memberSetter = memberSetter;
            DefaultValue = default(TM);
        }

        public DelegateMemberMapping<T, TM> SetMemberName(string memberName)
        {
            MemberName = memberName;
            return this;
        }

        public DelegateMemberMapping<T, TM> SetMemberIndex(int memberIndex)
        {
            MemberIndex = memberIndex;
            return this;
        }

        public DelegateMemberMapping<T, TM> SetConverter(ICborConverter<TM> converter)
        {
            _converter = converter;
            return this;
        }

        public DelegateMemberMapping<T, TM> SetDefaultValue(TM defaultValue)
        {
            DefaultValue = defaultValue;
            return this;
        }

        public DelegateMemberMapping<T, TM> SetIgnoreIfDefault(bool ignoreIfDefault)
        {
            IgnoreIfDefault = ignoreIfDefault;
            return this;
        }

        public DelegateMemberMapping<T, TM> SetShouldSerializeMethod(Func<object, bool> shouldSerializeMethod)
        {
            ShouldSerializeMethod = shouldSerializeMethod;
            return this;
        }

        public DelegateMemberMapping<T, TM> SetLengthMode(LengthMode lengthMode)
        {
            LengthMode = lengthMode;
            return this;
        }

        public DelegateMemberMapping<T, TM> SetRequired(RequirementPolicy requirementPolicy)
        {
            RequirementPolicy = requirementPolicy;
            return this;
        }

        public IMemberConverter GenerateMemberConverter()
        {
            if (MemberName == null && !MemberIndex.HasValue)
            {
                throw new CborException(
                    $"Either a member name or a member index must be set on member of type {typeof(TM)} in {typeof(T)}.");
            }

            if (MemberName != null && MemberIndex.HasValue)
            {
                throw new CborException(
                    $"MemberName and MemberIndex cannot coexist in member {MemberName} of {typeof(T)}.");
            }

            return new MemberConverter<T, TM>(
                MemberName != null ? MemberName.AsBinaryMemory() : ReadOnlyMemory<byte>.Empty,
                MemberIndex,
                ResolveConverter(),
                _memberGetter,
                _memberSetter,
                (TM)DefaultValue!,
                IgnoreIfDefault,
                ShouldSerializeMethod,
                LengthMode,
                RequirementPolicy);
        }

        /// <summary>
        /// Called by <see cref="ObjectMapping{T}"/> with the converter currently being constructed.
        /// </summary>
        /// <remarks>
        /// This is also the recursion break for self-referential types (upstream #147/#151): when a
        /// member's type is the very type the parent converter is being built for, reuse that
        /// converter instead of asking the registry for it — the registry has not finished adding it
        /// yet, so a lookup would re-enter converter creation and recurse forever.
        /// The <c>is ICborConverter&lt;TM&gt;</c> test does this without reflecting over generic
        /// arguments, so it stays AOT-safe.
        /// </remarks>
        public string? GetMemberNameForConverter(ICborConverter converter)
        {
            if (_converter == null && converter is ICborConverter<TM> selfConverter)
            {
                _converter = selfConverter;
            }

            return MemberName;
        }

        private ICborConverter<TM> ResolveConverter()
        {
            // Lookup<TM>() is generic, so no MakeGenericType is involved.
            return _converter ??= _converterRegistry.Lookup<TM>();
        }
    }
}
