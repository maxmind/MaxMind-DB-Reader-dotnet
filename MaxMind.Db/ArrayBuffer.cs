#region

using System;
using System.Text;

#endregion

namespace MaxMind.Db
{
    internal sealed class ArrayBuffer : Buffer
    {
        private readonly byte[] _fileBytes;

        public ArrayBuffer(byte[] array)
        {
            Length = array.LongLength;
            _fileBytes = array;
        }

        public override ReadOnlySpan<byte> AsSpan(long offset, int count)
        {
            return _fileBytes.AsSpan().Slice((int)offset, count);
        }

        public override ReadOnlyMemory<byte> AsMemory(long offset, int count)
        {
            return _fileBytes.AsMemory().Slice((int)offset, count);
        }

        public override byte[] Read(long offset, int count)
        {
            var bytes = new byte[count];

            if (bytes.Length > 0)
            {
                Array.Copy(_fileBytes, offset, bytes, 0, bytes.Length);
            }

            return bytes;
        }

        public override byte ReadOne(long offset) => _fileBytes[offset];

        public override string ReadString(long offset, int count)
            => Encoding.UTF8.GetString(_fileBytes, (int)offset, count);

        /// <summary>
        ///     Read an int from the buffer.
        /// </summary>
        public override int ReadInt(long offset)
        {
            return _fileBytes[offset] << 24 |
                   _fileBytes[offset + 1] << 16 |
                   _fileBytes[offset + 2] << 8 |
                   _fileBytes[offset + 3];
        }

        /// <summary>
        ///     Read a variable-sized int from the buffer.
        /// </summary>
        public override int ReadVarInt(long offset, int count)
        {
            return count switch
            {
                0 => 0,
                1 => _fileBytes[offset],
                2 => _fileBytes[offset] << 8 |
                     _fileBytes[offset + 1],
                3 => _fileBytes[offset] << 16 |
                     _fileBytes[offset + 1] << 8 |
                     _fileBytes[offset + 2],
                4 => ReadInt(offset),
                _ => throw new InvalidDatabaseException($"Unexpected int32 of size {count}"),
            };
        }
    }
}