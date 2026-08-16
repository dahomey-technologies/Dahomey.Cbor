using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Util;
using System;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    /// <summary>
    /// The struct counterpart of <see cref="DelegateMemberMapping{T, TM}"/>: an
    /// <see cref="IMemberMapping"/> for a member of a value type, whose accessors are supplied as
    /// <c>ref</c>-taking delegates rather than discovered from a <see cref="System.Reflection.MemberInfo"/>.
    /// </summary>
    /// <remarks>
    /// Structs need <see cref="StructMemberGetterDelegate{T, TP}"/> /
    /// <see cref="StructMemberSetterDelegate{T, TP}"/> so the instance is passed by reference and
    /// mutations are not lost on a copy. Reflection-free and AOT-safe.
    /// </remarks>
    /// <typeparam name="T">The declaring struct.</typeparam>
    /// <typeparam name="TM">The member type.</typeparam>
    public class DelegateStructMemberMapping<T, TM> : IMemberMapping
        where T : struct
    {
        private readonly CborConverterRegistry _converterRegistry;
        private readonly StructMemberGetterDelegate<T, TM>? _memberGetter;
        private readonly StructMemberSetterDelegate<T, TM>? _memberSetter;
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

        /// <summary>
        /// Always null. <see cref="StructMemberConverter{T, TM}"/> does not consult a
        /// ShouldSerialize method, mirroring the reflection path for structs.
        /// </summary>
        public Func<object, bool>? ShouldSerializeMethod => null;

        public LengthMode LengthMode { get; private set; }
        public RequirementPolicy RequirementPolicy { get; private set; }

        public DelegateStructMemberMapping(
            CborConverterRegistry converterRegistry,
            StructMemberGetterDelegate<T, TM>? memberGetter,
            StructMemberSetterDelegate<T, TM>? memberSetter)
        {
            _converterRegistry = converterRegistry ?? throw new ArgumentNullException(nameof(converterRegistry));
            _memberGetter = memberGetter;
            _memberSetter = memberSetter;
            DefaultValue = default(TM);
        }

        public DelegateStructMemberMapping<T, TM> SetMemberName(string memberName)
        {
            MemberName = memberName;
            return this;
        }

        public DelegateStructMemberMapping<T, TM> SetMemberIndex(int memberIndex)
        {
            MemberIndex = memberIndex;
            return this;
        }

        public DelegateStructMemberMapping<T, TM> SetConverter(ICborConverter<TM> converter)
        {
            _converter = converter;
            return this;
        }

        public DelegateStructMemberMapping<T, TM> SetDefaultValue(TM defaultValue)
        {
            DefaultValue = defaultValue;
            return this;
        }

        public DelegateStructMemberMapping<T, TM> SetIgnoreIfDefault(bool ignoreIfDefault)
        {
            IgnoreIfDefault = ignoreIfDefault;
            return this;
        }

        public DelegateStructMemberMapping<T, TM> SetLengthMode(LengthMode lengthMode)
        {
            LengthMode = lengthMode;
            return this;
        }

        public DelegateStructMemberMapping<T, TM> SetRequired(RequirementPolicy requirementPolicy)
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

            return new StructMemberConverter<T, TM>(
                MemberName,
                MemberIndex,
                ResolveConverter,
                _memberGetter,
                _memberSetter,
                (TM)DefaultValue!,
                IgnoreIfDefault,
                RequirementPolicy);
        }

        /// <summary>
        /// Called by <see cref="ObjectMapping{T}"/> with the converter currently being constructed.
        /// Mirrors <see cref="DelegateMemberMapping{T, TM}.GetMemberNameForConverter"/>; a struct
        /// cannot contain itself, so the recursion break is defensive symmetry rather than a
        /// reachable case.
        /// </summary>
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
