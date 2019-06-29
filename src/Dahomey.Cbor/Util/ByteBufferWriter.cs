using System;
using System.Buffers;
using System.Diagnostics;

namespace Dahomey.Cbor.Util
{
    public class ByteBufferWriter : IBufferWriter<byte>
    {
        private readonly byte[] _emptyBuffer = new byte[0];

        private byte[] _buffer;
        private int _index;

        private const int DefaultInitialBufferSize = 256;

        public int Capacity => _buffer.Length;
        public int FreeCapacity => _buffer.Length - _index;
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _index);

        public ByteBufferWriter()
        {
            _buffer = _emptyBuffer;
            _index = 0;
        }

        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentException(nameof(count));

            if (_index > _buffer.Length - count)
                throw new InvalidOperationException();

            _index += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            Debug.Assert(_buffer.Length > _index);
            return _buffer.AsMemory(_index);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            Debug.Assert(_buffer.Length > _index);
            return _buffer.AsSpan(_index);
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentException(nameof(sizeHint));

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (sizeHint > FreeCapacity)
            {
                int growBy = Math.Max(sizeHint, _buffer.Length);

                if (_buffer.Length == 0)
                {
                    growBy = Math.Max(growBy, DefaultInitialBufferSize);
                }

                int newSize = checked(_buffer.Length + growBy);

                Array.Resize(ref _buffer, newSize);
            }

            Debug.Assert(FreeCapacity > 0 && FreeCapacity >= sizeHint);
        }
    }
}
