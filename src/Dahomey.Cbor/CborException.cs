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
        /// <c>System.Text.Json</c>: <c>$.Items[7].Name</c>. <c>$</c> alone means the root value, and
        /// <c>null</c> means the path is unknown - the failure happened outside any converter that
        /// contributes a segment, which includes converters supplied by the caller.
        /// </summary>
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
        /// </remarks>
        public override string Message
        {
            get
            {
                string? path = Path;
                return path == null
                    ? base.Message
                    : $"{base.Message} Failed to deserialize from \"{path}\".";
            }
        }

        /// <summary>Records that this failure happened inside the member <paramref name="name"/>.</summary>
        /// <remarks>
        /// The name is the one the document used, not necessarily one the type declares - an unknown
        /// member rejected by <see cref="UnhandledNameMode"/> fails under whatever name it arrived with
        /// - so it is truncated on the same terms as any other quoted document text.
        /// </remarks>
        internal void PushMember(string name)
        {
            PushSegment("." + TextTruncation.Ellipsize(name));
        }

        /// <summary>Records that this failure happened at position <paramref name="index"/> of an array.</summary>
        internal void PushIndex(int index)
        {
            PushSegment($"[{index}]");
        }

        /// <summary>Records that this failure happened under the map key <paramref name="key"/>.</summary>
        internal void PushKey(string? key)
        {
            PushSegment($"['{TextTruncation.Ellipsize(key ?? string.Empty)}']");
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
