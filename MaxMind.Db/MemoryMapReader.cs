using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace MaxMind.Db
{
    internal class MemoryMapReader : IByteReader
    {
        private static readonly object FileLocker = new object();
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly MemoryMappedViewAccessor _view;
        private bool _disposed;

        public MemoryMapReader(string file)
        {
            var fileInfo = new FileInfo(file);

            Length = (int) fileInfo.Length;

            var mmfName = fileInfo.FullName.Replace("\\", "-");
            lock (FileLocker)
            {
                try
                {
                    _memoryMappedFile = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.Read);
                }
                catch (Exception ex)
                {
                    if (ex is IOException || ex is NotImplementedException)
                    {
                        _memoryMappedFile = MemoryMappedFile.CreateFromFile(file, FileMode.Open,
                            mmfName, fileInfo.Length, MemoryMappedFileAccess.Read);
                    }
                    else
                        throw;
                }
            }

            _view = _memoryMappedFile.CreateViewAccessor(0, Length, MemoryMappedFileAccess.Read);
        }

        public int Length { get; }

        public byte[] Read(int offset, int count)
        {
            var bytes = new byte[count];
            Copy(offset, bytes);
            return bytes;
        }

        public byte ReadOne(int offset) => _view.ReadByte(offset);

        public void Copy(int offset, byte[] bytes)
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
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Release resources back to the system.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
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