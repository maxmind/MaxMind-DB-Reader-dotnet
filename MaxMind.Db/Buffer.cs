#region

using System;
using System.Numerics;

#endregion

namespace MaxMind.Db
{
    internal abstract class Buffer : IDisposable
    {
        public readonly int Length;

        public abstract byte[] Read(long offset, int count);

        public abstract string ReadString(long offset, int count);

        public abstract byte ReadOne(long offset);

        public abstract void Copy(long offset, byte[] array);

        public abstract void Dispose();

        protected Buffer(int length)
        {
            Length = length;
        }

        /// <summary>
        ///     Read a big integer from the buffer.
        /// </summary>
        public BigInteger ReadBigInteger(long offset, int size)
        {
            // This could be optimized if it ever matters
            var buffer = Read(offset, size);
            Array.Reverse(buffer);

            // The integer will always be positive. We need to make sure
            // the last bit is 0.
            if (buffer.Length > 0 && (buffer[buffer.Length - 1] & 0x80) > 0)
            {
                Array.Resize(ref buffer, buffer.Length + 1);
            }
            return new BigInteger(buffer);
        }

        /// <summary>
        ///     Read an integer from the buffer.
        /// </summary>
        public int ReadInteger(int val, long offset, int size)
        {
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | ReadOne(offset + i);
            }
            return val;
        }

        /// <summary>
        ///     Read a long from the buffer.
        /// </summary>
        public long ReadLong(long offset, int size)
        {
            long val = 0;
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | ReadOne(offset + i);
            }
            return val;
        }

        /// <summary>
        ///     Read a uint64 from the buffer.
        /// </summary>
        public ulong ReadULong(long offset, int size)
        {
            ulong val = 0;
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | ReadOne(offset + i);
            }
            return val;
        }
    }
}