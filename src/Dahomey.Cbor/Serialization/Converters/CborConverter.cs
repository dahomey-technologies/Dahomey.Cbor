namespace Dahomey.Cbor.Serialization.Converters
{
    public interface ICborConverter
    {
        object Read(ref CborReader reader);
        void Write(ref CborWriter writer, object value);
    }

    public interface ICborConverter<T> : ICborConverter
    {
        new T Read(ref CborReader reader);
        void Write(ref CborWriter writer, T value);
    }

    public abstract class CborConverterBase<T> : ICborConverter<T>
    {
        public abstract T Read(ref CborReader reader);
        public abstract void Write(ref CborWriter writer, T value);

        object ICborConverter.Read(ref CborReader reader)
        {
            return Read(ref reader);
        }

        void ICborConverter.Write(ref CborWriter writer, object value)
        {
            Write(ref writer, (T)value);
        }
    }
}
