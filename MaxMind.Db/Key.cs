using System;

namespace MaxMind.Db
{
    internal readonly struct Key : IEquatable<Key>
    {
        private readonly Buffer buffer;
        private readonly long offset;
        private readonly int size;
        private readonly int hashCode;

        public Key(Buffer buffer, long offset, int size)
        {
            this.buffer = buffer;
            this.offset = offset;
            this.size = size;
            hashCode = buffer.HashBytes(offset, size);
        }

        public bool Equals(Key other)
        {
            if (size != other.size)
            {
                return false;
            }

            return buffer.EqualsBytes(offset, other.buffer, other.offset, size);
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
            return hashCode;
        }
    }
}
