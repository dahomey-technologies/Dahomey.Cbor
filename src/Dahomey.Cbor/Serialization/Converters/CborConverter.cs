namespace Dahomey.Cbor.Serialization.Converters
{
    public interface ICborConverter
    {
    }

    public interface ICborConverter<T> : ICborConverter
    {
        T Read(ref CborReader reader);
        void Write(ref CborWriter writer, T value);
        void Write(ref CborWriter writer, T value, LengthMode lengthMode);
    }

    public abstract class CborConverterBase<T> : ICborConverter<T>
    {
        public abstract T Read(ref CborReader reader);

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
