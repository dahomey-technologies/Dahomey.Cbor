using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor
{
    public class CborException : Exception
    {
        /// <summary>
        /// The path segments collected so far, innermost first.
        /// </summary>
        /// <remarks>
        /// The path is assembled as the exception travels back up the converter stack rather than
        /// tracked as the reader descends: each converter knows only its own segment, and only the
        /// converters an actual failure passed through pay anything at all. Reading well-formed data
        /// never allocates this list.
        /// </remarks>
        private List<string>? _segments;
        private string? _path;
        private bool _pathKnown;

        public CborException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Where in the deserialized model the failure occurred, in the notation used by
        /// <c>System.Text.Json</c>: <c>$.Items[7].Name</c>. <c>$</c> alone means the root value itself.
        /// </summary>
        /// <remarks>
        /// Every failure raised while deserializing has a path, down to <c>$</c> for a document that
        /// contradicts the requested type outright. <c>null</c> therefore means the exception did not
        /// come from a read at all - a serialization failure, or a mapping the registry refused to
        /// build - unless a caller has marked it themselves through
        /// <see cref="PrependPathMember(string)"/> or <see cref="PrependPathIndex(int)"/>, either of
        /// which makes a path known even where it adds no segment.
        /// <para>
        /// A path is as precise as the converters it passed through. Converters supplied by the caller
        /// contribute no segment of their own, so a failure inside one is reported against the member
        /// that holds it rather than against anything within.
        /// </para>
        /// </remarks>
        public string? Path
        {
            get
            {
                if (!_pathKnown)
                {
                    return null;
                }

                if (_path == null)
                {
                    StringBuilder pathBuilder = new StringBuilder("$");

                    for (int i = (_segments?.Count ?? 0) - 1; i >= 0; i--)
                    {
                        pathBuilder.Append(_segments![i]);
                    }

                    _path = pathBuilder.ToString();
                }

                return _path;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Overridden rather than baked into the message at construction time because the path is not
        /// known yet when the reader throws: the byte offset is all it has. Everything the base message
        /// said is preserved, and the path is appended to it once the stack has unwound far enough for
        /// the model position to be known.
        /// <para>
        /// It follows that this grows as the exception travels: read while the stack is still
        /// unwinding, it reports the path as far as it is known, not the whole of it. Anything that
        /// captures the text mid-flight - an intercepting <c>catch</c> that logs and rethrows, a
        /// wrapper exception built from it - captures a partial answer. <see cref="Path"/> has the same
        /// property, for the same reason.
        /// </para>
        /// </remarks>
        public override string Message
        {
            get
            {
                string? path = Path;

                if (path == null)
                {
                    return base.Message;
                }

                return $"{base.Message}{SentenceSeparator(base.Message)}Failed to deserialize from \"{path}\".";
            }
        }

        /// <summary>
        /// Records that this failure happened under <paramref name="name"/> - a member of an object or
        /// a key of a map, which read the same way in a path and are not worth distinguishing. Call it
        /// from a converter's <c>catch</c> and rethrow the same exception with a bare <c>throw;</c>:
        /// wrapping it in a new one bakes a half-built path into the new message.
        /// </summary>
        /// <remarks>
        /// Call this from a converter's <c>catch</c> and rethrow the same exception with a bare
        /// <c>throw;</c>. Each frame adds only its own segment, outermost last:
        /// <code>
        /// try
        /// {
        ///     return ReadPayload(ref reader);
        /// }
        /// catch (CborException exception)
        /// {
        ///     exception.PrependPathMember("Payload");
        ///     throw;
        /// }
        /// </code>
        /// <para>
        /// Rethrowing the same exception is not only about the stack trace. <see cref="Message"/> is
        /// composed on demand from the segments collected so far, so wrapping this exception in a new
        /// one - <c>throw new CborException("…" + exception.Message)</c> - freezes a half-built path
        /// into the new message and then appends the rest of it a second time as the stack keeps
        /// unwinding. Read <see cref="Message"/> or <see cref="Path"/> once the read has failed, not
        /// part of the way out of it.
        /// </para>
        /// <para>
        /// Only a converter that decodes a structure of its own needs this. One registered against a
        /// member is already named by the object holding it, and one that delegates to other converters
        /// inherits whatever they contribute.
        /// </para>
        /// <para>
        /// The name may be one the document chose rather than one a type declares - a map key is
        /// arbitrary by definition - so it is bounded in length and escaped, and cannot forge structure
        /// in the message that carries it. A null or empty name is recorded rather than rejected, since
        /// this runs while an exception is already in flight and throwing a second one over a
        /// diagnostic would lose the first; it renders as <c>['']</c>, which a genuinely empty name
        /// also does, so prefer <see cref="PrependPathIndex(int)"/> where a position is known.
        /// </para>
        /// </remarks>
        public void PrependPathMember(string? name)
        {
            PushSegment(FormatName(name ?? string.Empty));
        }

        /// <summary>
        /// Records that this failure happened at position <paramref name="index"/> of a sequence. A
        /// negative index is not a position and contributes no segment, though the failure is still
        /// marked as having been seen here.
        /// </summary>
        /// <inheritdoc cref="PrependPathMember(string)" path="/remarks"/>
        public void PrependPathIndex(int index)
        {
            if (index < 0)
            {
                MarkPathKnown();
                return;
            }

            PushSegment($"[{index}]");
        }

        /// <summary>
        /// <c>.Name</c> for a name that reads unambiguously as one, <c>['Name']</c> for anything else.
        /// </summary>
        /// <remarks>
        /// A name containing a dot, a bracket or a quote would otherwise be indistinguishable from the
        /// path syntax around it: <c>a.b</c> as a single member reads exactly like member <c>b</c> of
        /// member <c>a</c>. The bracketed form is the same answer <c>System.Text.Json</c> gives, and
        /// escaping inside it is what stops a name from closing the quotation the message wraps the
        /// path in.
        /// </remarks>
        private static string FormatName(string name)
        {
            // A name short enough to survive whole and made only of token characters needs neither
            // bracketing nor escaping, so it reads as what it is.
            if (name.Length != 0 && name.Length <= TextTruncation.MaxCharsInMessage && IsSimpleName(name))
            {
                return "." + name;
            }

            return "['" + TextTruncation.Ellipsize(name, escapeApostrophe: true) + "']";
        }

        private static bool IsSimpleName(string name)
        {
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// What to put between the base message and the path. The two are separate sentences, but base
        /// messages are inconsistent about ending in a full stop and adding a second one reads worse
        /// than adding none.
        /// </summary>
        private static string SentenceSeparator(string message)
        {
            if (message.Length == 0)
            {
                return string.Empty;
            }

            char last = message[message.Length - 1];
            return last == '.' || last == '!' || last == '?' ? " " : ". ";
        }

        /// <summary>
        /// Records that the failure was seen by a converter, without naming a position inside it.
        /// </summary>
        /// <remarks>
        /// This is what turns a failure on the root value itself - a document whose very first byte
        /// contradicts the requested type - into the path <c>$</c> rather than no path at all. Without
        /// it, "the failure is at the root" and "no converter on the way up could name a position"
        /// would produce the same empty result, and those are worth telling apart.
        /// </remarks>
        internal void MarkPathKnown()
        {
            _pathKnown = true;
        }

        private void PushSegment(string segment)
        {
            (_segments ??= new List<string>()).Add(segment);
            _pathKnown = true;
            _path = null;
        }
    }
}
