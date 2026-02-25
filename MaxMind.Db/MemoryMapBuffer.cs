#region

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace MaxMind.Db
{
    internal sealed class MemoryMapBuffer : Buffer
    {
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly MemoryMappedViewAccessor _view;
        private bool _disposed;

        // Creates a named memory-mapped file backed directly by the file on
        // disk, suitable for cross-process sharing.
        internal MemoryMapBuffer(string file, bool useGlobalNamespace) : this(file, useGlobalNamespace, new FileInfo(file))
        {
        }

        private MemoryMapBuffer(string file, bool useGlobalNamespace, FileInfo fileInfo)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                                              FileShare.Delete | FileShare.Read);
            Length = stream.Length;
            // Ideally we would use the file ID in the mapName, but it is not
            // easily available from C#.
            var objectNamespace = useGlobalNamespace ? "Global" : "Local";

            // We create a sha256 here as there are limitations on mutex names.
            using var sha256 = SHA256.Create();
            var suffixTxt = $"{fileInfo.FullName.Replace("\\", "-")}-{Length}";
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(suffixTxt));
            var suffix = BitConverter.ToString(hashBytes).Replace("-", "");

            var mapName = $"{objectNamespace}\\{suffix}";
            var mutexName = $"{mapName}-Mutex";

            using (var mutex = new Mutex(false, mutexName))
            {
                var hasHandle = false;

                try
                {
                    hasHandle = mutex.WaitOne(TimeSpan.FromSeconds(10), false);
                    if (!hasHandle)
                    {
                        throw new TimeoutException("Timeout waiting for mutex.");
                    }

                    _memoryMappedFile = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
                }
                catch (Exception ex) when (ex is IOException or NotImplementedException or PlatformNotSupportedException)
                {
                    // In .NET Core, named maps are not supported for Unices yet: https://github.com/dotnet/corefx/issues/1329
                    // When executed on unsupported platform, we get the PNSE. In which case, we construct the memory map by
                    // setting mapName to null.
                    if (ex is PlatformNotSupportedException)
                        mapName = null;

                    _memoryMappedFile = MemoryMappedFile.CreateFromFile(stream, mapName, Length,
                            MemoryMappedFileAccess.Read, HandleInheritability.None, false);
                }
                finally
                {
                    if (hasHandle)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }

            _view = _memoryMappedFile.CreateViewAccessor(0, Length, MemoryMappedFileAccess.Read);
        }

        // Reads the file into an anonymous memory-mapped region that is
        // private to this process.
        internal MemoryMapBuffer(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                                              FileShare.Delete | FileShare.Read);
            Length = stream.Length;

            (_memoryMappedFile, _view) = CreateMmapFromStream(stream, Length);
        }

        // Reads the stream into an anonymous memory-mapped region that is
        // private to this process.
        internal MemoryMapBuffer(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "The database stream must not be null.");
            }

            if (stream.CanSeek)
            {
                Length = stream.Length - stream.Position;

                (_memoryMappedFile, _view) = CreateMmapFromStream(stream, Length);
                return;
            }

            var tempFile = Path.GetTempFileName();
            try
            {
                using (var tempStream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.CopyTo(tempStream);
                    Length = tempStream.Length;

                    tempStream.Position = 0;
                    (_memoryMappedFile, _view) = CreateMmapFromStream(tempStream, Length);
                }
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Best-effort cleanup. If deletion fails, the temp
                    // file is orphaned but the mmap may already be valid.
                    // Letting this exception propagate would turn a
                    // successful construction into a failure and leak the
                    // mmap resources.
                }
            }
        }

        private MemoryMapBuffer(MemoryMappedFile memoryMappedFile, MemoryMappedViewAccessor view, long length)
        {
            Length = length;
            _memoryMappedFile = memoryMappedFile;
            _view = view;
        }

        internal static async Task<MemoryMapBuffer> CreateAsync(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                                              FileShare.Delete | FileShare.Read, 4096, true);
            return await CreateAsync(stream).ConfigureAwait(false);
        }

        internal static async Task<MemoryMapBuffer> CreateAsync(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "The database stream must not be null.");
            }

            if (stream.CanSeek)
            {
                var length = stream.Length - stream.Position;

                var (memoryMappedFile, view) = await CreateMmapFromStreamAsync(stream, length).ConfigureAwait(false);

                return new MemoryMapBuffer(memoryMappedFile, view, length);
            }

            var tempFile = Path.GetTempFileName();
            try
            {
                using (var tempStream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, true))
                {
                    await stream.CopyToAsync(tempStream).ConfigureAwait(false);
                    var length = tempStream.Length;

                    tempStream.Position = 0;
                    var (memoryMappedFile, view) = await CreateMmapFromStreamAsync(tempStream, length).ConfigureAwait(false);

                    return new MemoryMapBuffer(memoryMappedFile, view, length);
                }
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Best-effort cleanup. If deletion fails, the temp
                    // file is orphaned but the mmap may already be valid.
                    // Letting this exception propagate would turn a
                    // successful construction into a failure and leak the
                    // mmap resources.
                }
            }
        }

        private static (MemoryMappedFile File, MemoryMappedViewAccessor View) CreateMmapFromStream(Stream source, long length)
        {
            if (length == 0)
            {
                throw new InvalidDatabaseException("The database is empty.");
            }

            var memoryMappedFile = MemoryMappedFile.CreateNew(null, length);
            try
            {
                using (var viewStream = memoryMappedFile.CreateViewStream(0, length, MemoryMappedFileAccess.Write))
                {
                    source.CopyTo(viewStream);
                }
                var view = memoryMappedFile.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
                return (memoryMappedFile, view);
            }
            catch
            {
                memoryMappedFile.Dispose();
                throw;
            }
        }

        private static async Task<(MemoryMappedFile File, MemoryMappedViewAccessor View)> CreateMmapFromStreamAsync(Stream source, long length)
        {
            if (length == 0)
            {
                throw new InvalidDatabaseException("The database is empty.");
            }

            var memoryMappedFile = MemoryMappedFile.CreateNew(null, length);
            try
            {
                using (var viewStream = memoryMappedFile.CreateViewStream(0, length, MemoryMappedFileAccess.Write))
                {
                    await source.CopyToAsync(viewStream).ConfigureAwait(false);
                }
                var view = memoryMappedFile.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
                return (memoryMappedFile, view);
            }
            catch
            {
                memoryMappedFile.Dispose();
                throw;
            }
        }

        public override ReadOnlySpan<byte> AsSpan(long offset, int count)
        {
            var bytes = new byte[count];
            _view.ReadArray(offset, bytes, 0, bytes.Length);
            return bytes.AsSpan();
        }

        public override ReadOnlyMemory<byte> AsMemory(long offset, int count)
        {
            var bytes = new byte[count];
            _view.ReadArray(offset, bytes, 0, bytes.Length);
            return bytes.AsMemory();
        }

        public override byte[] Read(long offset, int count)
        {
            var bytes = new byte[count];

            // Although not explicitly marked as thread safe, from
            // reviewing the source code, these operations appear to
            // be thread safe as long as only read operations are
            // being done.
            _view.ReadArray(offset, bytes, 0, bytes.Length);

            return bytes;
        }

        public override byte ReadOne(long offset) => _view.ReadByte(offset);

        public override string ReadString(long offset, int count)
        {
            if (offset + count > _view.Capacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    "Attempt to read beyond the end of the MemoryMappedFile.");
            }
            unsafe
            {
                var ptr = (byte*)0;
                try
                {
                    _view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                    return Encoding.UTF8.GetString(ptr + offset, count);
                }
                finally
                {
                    _view.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }

        /// <summary>
        ///     Read an int from the buffer.
        /// </summary>
        public override int ReadInt(long offset)
        {
            return _view.ReadByte(offset) << 24 |
                   _view.ReadByte(offset + 1) << 16 |
                   _view.ReadByte(offset + 2) << 8 |
                   _view.ReadByte(offset + 3);
        }

        /// <summary>
        ///     Read a variable-sized int from the buffer.
        /// </summary>
        public override int ReadVarInt(long offset, int count)
        {
            return count switch
            {
                0 => 0,
                1 => _view.ReadByte(offset),
                2 => _view.ReadByte(offset) << 8 |
                     _view.ReadByte(offset + 1),
                3 => _view.ReadByte(offset) << 16 |
                     _view.ReadByte(offset + 1) << 8 |
                     _view.ReadByte(offset + 2),
                4 => ReadInt(offset),
                _ => throw new InvalidDatabaseException($"Unexpected int32 of size {count}"),
            };
        }

        /// <summary>
        ///     Release resources back to the system.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
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
