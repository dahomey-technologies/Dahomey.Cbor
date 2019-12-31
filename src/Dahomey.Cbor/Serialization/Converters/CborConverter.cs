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
        void Write(ref CborWriter writer, T value, LengthMode lengthMode);
    }

    public abstract class CborConverterBase<T> : ICborConverter<T>
    {
        object ICborConverter.Read(ref CborReader reader)
        {
            return Read(ref reader);
        }

        public abstract T Read(ref CborReader reader);

        void ICborConverter.Write(ref CborWriter writer, object value)
        {
            Write(ref writer, (T)value);
        }

        public virtual void Write(ref CborWriter writer, T value)
        {
            Write(ref writer, value, LengthMode.Default);
        }

        public virtual void Write(ref CborWriter writer, T value, LengthMode lengthMode)
        {
            Write(ref writer, value);
        }
    }
}
