using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Util;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Messages for the ways adding a map entry can fail, so every read path words them the same.
    /// </summary>
    /// <remarks>
    /// The backing dictionary reports all of these as <see cref="System.ArgumentException"/>, and
    /// <see cref="System.ArgumentNullException"/> derives from it, so the exception type alone does not
    /// say which happened. Each case is distinguished before a message is chosen rather than assuming
    /// the common one: a null key is not a duplicate, and a caller-supplied <c>IDictionary</c> may
    /// reject an entry for reasons of its own.
    /// </remarks>
    internal static class MapKeyErrors
    {
        public static string NullKey()
        {
            return "A map key cannot be null.";
        }

        public static string Duplicate(object key)
        {
            return $"Duplicate map key: {Describe(key)}";
        }

        /// <summary>
        /// The dictionary refused the entry but the key is not already present, so it is not a
        /// duplicate. Says what actually happened rather than asserting the usual cause.
        /// </summary>
        public static string Rejected(object key, string reason)
        {
            return $"Map key rejected: {Describe(key)} - {reason}";
        }

        /// <remarks>
        /// A text key is described by its text, whatever it arrived as. <see cref="CborString"/>
        /// quotes itself in <c>ToString()</c>, so the object model handed this the six characters
        /// <c>"a"</c> where every other decode target hands it the one character <c>a</c> - and
        /// <see cref="TextTruncation.Ellipsize"/> then escaped those quotes as document text, since
        /// it cannot know they were added by the description rather than present in the key. The same
        /// document then read differently depending on what it was being read into, which is the
        /// thing routing every target through one helper was meant to prevent.
        /// <para>
        /// Only the string case is unwrapped. A number, a boolean or a container key has no quoting
        /// of its own to undo, and <c>CborValue.ToString()</c> is the right rendering for it.
        /// </para>
        /// </remarks>
        private static string Describe(object key)
        {
            string text = key is CborString cborString
                ? cborString.Value<string>()
                : key.ToString() ?? string.Empty;

            return TextTruncation.Ellipsize(text);
        }
    }
}
