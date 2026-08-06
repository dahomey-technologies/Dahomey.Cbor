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
        /// <summary>
        /// How much of a key is worth repeating. A key comes from the document being decoded, which for
        /// anything reading untrusted frames means an attacker chooses it, and exception messages end
        /// up in logs.
        /// </summary>
        private const int MaxKeyCharsInMessage = 64;

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

        private static string Describe(object key)
        {
            string text = key.ToString() ?? string.Empty;

            return text.Length <= MaxKeyCharsInMessage
                ? text
                : text.Substring(0, MaxKeyCharsInMessage) + $"... ({text.Length} characters)";
        }
    }
}
