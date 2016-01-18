#region

using System;
using System.IO;
using System.Text;

#endregion

namespace MaxMind.Db
{
    internal sealed class ArrayBuffer : Buffer
    {
        private readonly byte[] _fileBytes;

        public ArrayBuffer(byte[] array) : base(array.Length)
        {
            _fileBytes = array;
        }

        public ArrayBuffer(string file) : this(File.ReadAllBytes(file))
        {
        }

        public ArrayBuffer(Stream stream) : this(BytesFromStream(stream))
        {
        }

        private static byte[] BytesFromStream(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "The database stream must not be null.");
            }
            byte[] bytes;

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                bytes = memoryStream.ToArray();
            }

            if (bytes.Length == 0)
            {
                throw new InvalidDatabaseException(
                    "There are zero bytes left in the stream. Perhaps you need to reset the stream's position.");
            }
            return bytes;
        }

        public override byte[] Read(long offset, int count)
        {
            // Not using an ArraySegment as you can cast it into an IList
            // in .NET 4. I also looked into ImmutableArray, but it appears
            // that it does a copy when creating a subarray.
            var bytes = new byte[count];
            Copy(offset, bytes);
            return bytes;
        }

        public override byte ReadOne(long offset) => _fileBytes[offset];

        public override string ReadString(long offset, int count)
            => Encoding.UTF8.GetString(_fileBytes, (int)offset, count);

        public override void Copy(long offset, byte[] bytes)
        {
            if (bytes.Length > 0)
            {
                Array.Copy(_fileBytes, (int)offset, bytes, 0, bytes.Length);
            }
        }

        public override void Dispose()
        {
        }
    }
}