using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Dahomey.Cbor.Util
{
    public class ByteBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private readonly byte[] _emptyBuffer = new byte[0];

        private byte[] _buffer;
        private int _size;

        private const int DefaultInitialBufferSize = 256;

        public int Capacity => _buffer.Length;
        public int FreeCapacity => _buffer.Length - _size;
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _size);

        public ByteBufferWriter()
        {
            _buffer = _emptyBuffer;
            _size = 0;
        }

        public void Dispose()
        {
            if (!ReferenceEquals(_buffer, _emptyBuffer))
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }

            _buffer = null;
            _size = 0;
        }

        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentException(nameof(count));

            if (_size > _buffer.Length - count)
                throw new InvalidOperationException();

            _size += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            Debug.Assert(_buffer.Length > _size);
            return _buffer.AsMemory(_size);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            Debug.Assert(_buffer.Length > _size);
            return _buffer.AsSpan(_size);
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

                byte[] backup = _buffer;
                _buffer = ArrayPool<byte>.Shared.Rent(newSize);

                if (!ReferenceEquals(backup, _emptyBuffer))
                {
                    backup.AsSpan().CopyTo(_buffer);
                    ArrayPool<byte>.Shared.Return(backup);
                }
            }

            Debug.Assert(FreeCapacity > 0 && FreeCapacity >= sizeHint);
        }

        public Task CopyToAsync(Stream stream)
        {
            return stream.WriteAsync(_buffer, 0, _size);
        }
    }
}
