using System;

namespace MaxMind.Db
{
    internal readonly struct Key : IEquatable<Key>
    {
        private readonly bool _isBufferBacked;
        private readonly MemoryMapBuffer? _buffer;
        private readonly byte[]? _bytes;
        private readonly long _offset;
        private readonly int _size;
        private readonly int _hashCode;

        public Key(MemoryMapBuffer buffer, long offset, int size)
        {
            _isBufferBacked = true;
            _buffer = buffer;
            _bytes = null;
            _offset = offset;
            _size = size;
            _hashCode = buffer.HashBytes(offset, size);
        }

        public Key(byte[] bytes)
        {
            _isBufferBacked = false;
            _buffer = null;
            _bytes = bytes;
            _offset = 0;
            _size = bytes.Length;
            _hashCode = HashBytes(bytes);
        }

        public bool Equals(Key other)
        {
            if (_size != other._size)
            {
                return false;
            }

            if (_isBufferBacked)
            {
                var buffer = _buffer!;
                if (other._isBufferBacked)
                {
                    return buffer.EqualsBytes(_offset, other._buffer!, other._offset, _size);
                }

                return other._bytes != null && buffer.EqualsBytes(_offset, other._bytes, 0, _size);
            }

            var bytes = _bytes;
            if (bytes == null)
            {
                return !other._isBufferBacked && other._bytes == null;
            }

            if (other._isBufferBacked)
            {
                return other._buffer!.EqualsBytes(other._offset, bytes, 0, _size);
            }

#if NETSTANDARD2_0
            return other._bytes != null && BytesEqual(bytes, other._bytes, _size);
#else
            return other._bytes != null && bytes.AsSpan(0, _size).SequenceEqual(other._bytes.AsSpan(0, _size));
#endif
        }

        public override bool Equals(object? obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private static int HashBytes(byte[] bytes)
        {
            var code = 17;
            for (var i = 0; i < bytes.Length; i++)
            {
                code = (31 * code) + bytes[i];
            }

            return code;
        }

#if NETSTANDARD2_0
        private static bool BytesEqual(byte[] bytes1, byte[] bytes2, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (bytes1[i] != bytes2[i])
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }
}
