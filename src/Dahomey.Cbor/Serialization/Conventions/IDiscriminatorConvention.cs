using Dahomey.Cbor.Serialization.Converters;
using System;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public interface IDiscriminatorConvention
    {
        ReadOnlySpan<byte> MemberName { get; }
        Type ReadDiscriminator(ref CborReader reader);
        void WriteDiscriminator<T>(ref CborWriter writer, Type actualType) where T : class;
    }
}
