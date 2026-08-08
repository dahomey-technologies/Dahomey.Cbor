namespace Dahomey.Cbor.Util
{
    /// <summary>
    /// How much text taken from the document being decoded may be repeated back in an exception.
    /// </summary>
    /// <remarks>
    /// Map keys, member names and the paths built out of them all come from the document, which for
    /// anything reading untrusted frames means an attacker chooses them - and exception messages end up
    /// in logs. One policy, applied everywhere such text is quoted, keeps a message's length a function
    /// of the document's shape rather than of its contents.
    /// </remarks>
    internal static class TextTruncation
    {
        public const int MaxCharsInMessage = 64;

        public static string Ellipsize(string text)
        {
            return text.Length <= MaxCharsInMessage
                ? text
                : text.Substring(0, MaxCharsInMessage) + $"... ({text.Length} characters)";
        }
    }
}
