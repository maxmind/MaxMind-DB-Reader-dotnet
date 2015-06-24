using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace MaxMind.Db
{
    internal class MemoryMapReader : IByteReader
    {
        private static readonly object FileLocker = new object();
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly MemoryMappedViewAccessor _view;

        public int Length { get; }

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

        public byte[] Read(int offset, int count)
        {
            var bytes = new byte[count];
            Copy(offset, bytes);
            return bytes;
        }

        public byte ReadOne(int offset) => _view.ReadByte(offset);

        public void Copy(int offset, byte[] bytes)
        {
            // Doing an unsafe read improves performance by 10%. Probably
            // not worth the increased support overhead from people asking
            // why we use unsafe.
            _view.ReadArray(offset, bytes, 0, bytes.Length);
        }

        public void Dispose()
        {
            _view.Dispose();
            _memoryMappedFile.Dispose();
        }
    }
}