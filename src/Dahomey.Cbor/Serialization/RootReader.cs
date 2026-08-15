using Dahomey.Cbor.Serialization.Converters;

namespace Dahomey.Cbor.Serialization
{
    /// <summary>
    /// Reads the value at the root of a document, so that a failure on the root itself still has a
    /// position to report: <see cref="CborException.Path"/> of <c>$</c>.
    /// </summary>
    /// <remarks>
    /// The converters that name members, indices and keys only do so from inside a container. A
    /// document that contradicts the requested type outright never reaches one of them, and that is
    /// the failure a caller is least able to place from a byte offset alone. Every entry point that
    /// starts a read goes through here, which is what lets <see cref="CborException.Path"/> promise
    /// that a null value means the exception did not come from a read at all.
    /// <para>
    /// The speculative read behind the <c>PipeReader</c> overloads goes through this too. It throws as
    /// a matter of course while waiting for more of a stream, and marking a path on a failure it is
    /// about to discard costs nothing; the one it does not discard - the failure that stopped the last
    /// attempt on a completed pipe - reaches the caller placed exactly as the same bytes read whole.
    /// </para>
    /// </remarks>
    internal static class RootReader
    {
        public static T Read<T>(ref CborReader reader, ICborConverter<T> converter)
        {
            try
            {
                return converter.Read(ref reader);
            }
            catch (CborException exception)
            {
                exception.MarkPathKnown();
                throw;
            }
        }

        /// <inheritdoc cref="Read{T}(ref CborReader, ICborConverter{T})"/>
        public static object? Read(ref CborReader reader, ICborConverter converter)
        {
            try
            {
                return converter.Read(ref reader);
            }
            catch (CborException exception)
            {
                exception.MarkPathKnown();
                throw;
            }
        }
    }
}
