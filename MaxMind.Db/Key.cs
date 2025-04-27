﻿using System;

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

            var code = 17;
            for (var i = 0; i < size; i++)
            {
                code = (31 * code) + buffer.ReadOne(offset + i);
            }
            hashCode = code;
        }

        public bool Equals(Key other)
        {
            if (size != other.size)
            {
                return false;
            }

            for (var i = 0; i < size; i++)
            {
                if (buffer.ReadOne(offset + i) != other.buffer.ReadOne(other.offset + i))
                {
                    return false;
                }
            }

            return true;
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
