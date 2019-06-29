namespace Dahomey.Cbor.Serialization
{
    public enum CborMajorType : byte
    {
        PositiveInteger = 0,
        NegativeInteger = 1,
        ByteString = 2,
        TextString = 3,
        Array = 4,
        Map = 5,
        SemanticTag = 6,
        Primitive = 7,
        Max = Primitive
    }
}
