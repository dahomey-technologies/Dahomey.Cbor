using System;
using System.Diagnostics;
using System.Text;

namespace Dahomey.Cbor.Util
{
    public struct RawString : IEquatable<RawString>, IComparable<RawString>
    {
        private readonly ReadOnlyMemory<byte> _buffer;
        public ReadOnlyMemory<byte> Buffer => _buffer;

        public RawString(string str, Encoding encoding = null)
        {
            _buffer = (encoding ?? Encoding.UTF8).GetBytes(str);
        }

        public RawString(ReadOnlyMemory<byte> buffer)
        {
            _buffer = buffer;
        }

        public RawString(ReadOnlySpan<byte> buffer)
        {
            _buffer = new ReadOnlyMemory<byte>(buffer.ToArray());
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is RawString rawString))
            {
                return false;
            }
            return Equals(rawString);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (byte b in _buffer.Span)
            {
                hash = 37 * hash + b.GetHashCode();
            }
            return hash;
        }

        public bool Equals(RawString other)
        {
            return _buffer.Span.SequenceEqual(other._buffer.Span);
        }

        public int CompareTo(RawString other)
        {
            return _buffer.Span.SequenceCompareTo(other._buffer.Span);
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(_buffer.Span);
        }
    }
}
