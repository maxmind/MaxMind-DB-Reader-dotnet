#region

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

#endregion

namespace MaxMind.Db
{
    internal sealed class MemoryMapBuffer : Buffer
    {
        private static readonly object FileLocker = new object();
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly MemoryMappedViewAccessor _view;
        private bool _disposed;

        public MemoryMapBuffer(string file) : this(file, new FileInfo(file))
        {
        }

        private MemoryMapBuffer(string file, FileInfo fileInfo) : base((int)fileInfo.Length)
        {
            // Ideally we would use the file ID in the mapName, but it is not
            // easily available from C#.
            var mapName = $"{fileInfo.FullName.Replace("\\", "-")}-{Length}";
            lock (FileLocker)
            {
                try
                {
                    _memoryMappedFile = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
                }
                catch (Exception ex) when (ex is IOException || ex is NotImplementedException)
                {
                    using (
                        var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                            FileShare.Delete | FileShare.Read))
                    {
                        _memoryMappedFile = MemoryMappedFile.CreateFromFile(stream, mapName, Length,
                            MemoryMappedFileAccess.Read, null, HandleInheritability.None, false);
                    }
                }
            }

            _view = _memoryMappedFile.CreateViewAccessor(0, Length, MemoryMappedFileAccess.Read);
        }

        public override byte[] Read(long offset, int count)
        {
            var bytes = new byte[count];
            Copy(offset, bytes);
            return bytes;
        }

        public override byte ReadOne(long offset) => _view.ReadByte(offset);

        public override string ReadString(long offset, int count) => Encoding.UTF8.GetString(Read(offset, count));

        public override void Copy(long offset, byte[] bytes)
        {
            // Although not explicitly marked as thread safe, from
            // reviewing the source code, these operations appear to
            // be thread safe as long as only read operations are
            // being done.
            _view.ReadArray(offset, bytes, 0, bytes.Length);
        }

        /// <summary>
        ///     Release resources back to the system.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Release resources back to the system.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _view.Dispose();
                _memoryMappedFile.Dispose();
            }

            _disposed = true;
        }
    }
}