using System;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public interface IDiscriminatorConvention
    {
        ReadOnlySpan<byte> MemberName { get; }
        Type ReadDiscriminator(ref CborReader reader);
        void WriteDiscriminator(ref CborWriter writer, Type actualType);
        bool TryRegisterType(Type type);
    }
}
