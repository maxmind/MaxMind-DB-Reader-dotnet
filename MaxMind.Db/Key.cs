using System;

namespace MaxMind.Db
{
    internal readonly struct Key : IEquatable<Key>
    {
        private readonly MemoryMapBuffer? _buffer;
        private readonly byte[]? _bytes;
        private readonly long _offset;
        private readonly int _size;
        private readonly int _hashCode;

        public Key(MemoryMapBuffer buffer, long offset, int size)
        {
            _buffer = buffer;
            _bytes = null;
            _offset = offset;
            _size = size;
            _hashCode = buffer.HashBytes(offset, size);
        }

        public Key(byte[] bytes)
        {
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

            if (_buffer != null)
            {
                if (other._buffer != null)
                {
                    return _buffer.EqualsBytes(_offset, other._buffer, other._offset, _size);
                }

                return other._bytes != null && _buffer.EqualsBytes(_offset, other._bytes, 0, _size);
            }

            if (_bytes == null)
            {
                return other._buffer == null && other._bytes == null;
            }

            if (other._buffer != null)
            {
                return other._buffer.EqualsBytes(other._offset, _bytes, 0, _size);
            }

            return other._bytes != null && BytesEqual(_bytes, other._bytes, _size);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || typeof(Key) != obj.GetType())
            {
                return false;
            }

            return Equals((Key)obj);
        }

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
    }
}
