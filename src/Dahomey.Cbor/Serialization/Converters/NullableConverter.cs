namespace Dahomey.Cbor.Serialization.Converters
{
    public sealed class NullableConverter<T> : CborConverterBase<T?> where T : struct
    {
        private readonly ICborConverter<T> _cborConverter;

        public NullableConverter(CborOptions options)
        {
            this._cborConverter = options.Registry.ConverterRegistry.Lookup<T>();
        }

        /// <summary>
        /// Reads the underlying value, or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <c>ReadNull</c> skips a semantic tag before looking at the item, which loses the tag for an
        /// underlying converter that dispatches on it -- <see cref="BigIntegerConverter"/> reading a
        /// bignum saw a bare byte string and rejected it. So on a tagged item the tag is taken under a
        /// bookmark and handed back when the item turns out not to be null. A tagged null still reads
        /// as null, which is what it did before. Only a tagged item pays for the bookmark; the common
        /// untagged case is the same two branches it always was.
        /// <para>
        /// This narrows one case. Skipping the tag here and again in the underlying converter made
        /// <c>T?</c> accept two stacked tags where <c>T</c> accepts one, so <c>C1 C1 0C</c> read as 12
        /// into an <c>int?</c> and threw into an <c>int</c>. Handing the tag back leaves exactly one
        /// skip, so a nullable now accepts what its underlying type accepts and no more. The leniency
        /// was a side effect of the double skip rather than a decision, and keeping it is not possible
        /// alongside the fix: whether the tag belongs to the underlying converter is not knowable from
        /// here.
        /// </para>
        /// </remarks>
        public override T? Read(ref CborReader reader)
        {
            if (reader.IsSemanticTag())
            {
                CborReaderBookmark bookmark = reader.GetBookmark();

                if (reader.ReadNull())
                {
                    return default;
                }

                reader.ReturnToBookmark(bookmark);
                return this._cborConverter.Read(ref reader);
            }

            if (reader.ReadNull())
            {
                return default;
            }

            return this._cborConverter.Read(ref reader);
        }


        public override void Write(ref CborWriter writer, T? value)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            this._cborConverter.Write(ref writer, value.Value);
        }
    }
}
