using System;
using System.Text;

namespace Dahomey.Cbor.Util
{
    /// <summary>
    /// How text taken from the document being decoded may be repeated back in an exception.
    /// </summary>
    /// <remarks>
    /// Map keys, member names and the paths built out of them all come from the document, which for
    /// anything reading untrusted frames means an attacker chooses them - and exception messages end up
    /// in logs. One policy, applied everywhere such text is quoted, keeps a message bounded in length
    /// and free of characters that would let the quoted text pass itself off as the structure around
    /// it.
    /// </remarks>
    internal static class TextTruncation
    {
        /// <summary>
        /// The budget, counted in characters of the rendered result rather than of the source.
        /// </summary>
        /// <remarks>
        /// Counting the source instead would make the real bound six times this, since one control
        /// character renders as six - which is exactly the length a hostile name would choose.
        /// </remarks>
        public const int MaxCharsInMessage = 64;

        /// <param name="escapeApostrophe">
        /// Set where the result is wrapped in <c>'…'</c>, as a path segment is. Left off elsewhere so
        /// that ordinary prose is not littered with backslashes.
        /// </param>
        public static string Ellipsize(string text, bool escapeApostrophe = false)
        {
            if (text.Length <= MaxCharsInMessage && IsPlain(text, escapeApostrophe))
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(MaxCharsInMessage + 24);
            int consumed = 0;

            while (consumed < text.Length)
            {
                // A surrogate pair is one character and is kept whole; truncating between its halves
                // would emit a lone surrogate, which is not valid UTF-16 and can break a log sink.
                bool isPair = char.IsHighSurrogate(text[consumed])
                    && consumed + 1 < text.Length
                    && char.IsLowSurrogate(text[consumed + 1]);

                if (builder.Length + (isPair ? 2 : EscapedLength(text[consumed], escapeApostrophe)) > MaxCharsInMessage)
                {
                    break;
                }

                if (isPair)
                {
                    builder.Append(text[consumed]).Append(text[consumed + 1]);
                    consumed += 2;
                }
                else
                {
                    AppendEscaped(builder, text[consumed], escapeApostrophe);
                    consumed++;
                }
            }

            if (consumed < text.Length)
            {
                builder.Append($"... ({text.Length} characters)");
            }

            return builder.ToString();
        }

        private static bool IsPlain(string text, bool escapeApostrophe)
        {
            foreach (char c in text)
            {
                if (EscapedLength(c, escapeApostrophe) != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static int EscapedLength(char c, bool escapeApostrophe)
        {
            if (NeedsBackslash(c, escapeApostrophe))
            {
                return 2;
            }

            // A surrogate reaching here is unpaired: the caller keeps valid pairs whole.
            return IsOpaque(c) ? 6 : 1;
        }

        private static void AppendEscaped(StringBuilder builder, char c, bool escapeApostrophe)
        {
            if (NeedsBackslash(c, escapeApostrophe))
            {
                builder.Append('\\').Append(c);
            }
            else if (IsOpaque(c))
            {
                builder.Append("\\u").Append(((int)c).ToString("x4"));
            }
            else
            {
                builder.Append(c);
            }
        }

        private static bool NeedsBackslash(char c, bool escapeApostrophe)
        {
            return c == '\\' || c == '"' || (escapeApostrophe && c == '\'');
        }

        private static bool IsOpaque(char c)
        {
            return c < ' ' || c == '\u007f' || char.IsSurrogate(c);
        }
    }
}
