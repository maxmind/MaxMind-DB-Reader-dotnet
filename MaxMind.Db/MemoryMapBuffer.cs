﻿#region

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

            string? mapName = $"{objectNamespace}\\{fileInfo.FullName.Replace("\\", "-")}-{Length}";
            lock (FileLocker)
            {
                try
                {
                    _memoryMappedFile = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
                }
#if !NETSTANDARD2_0 && !NETSTANDARD2_1 && !NET5_0
                catch (Exception ex) when (ex is IOException || ex is NotImplementedException)
#else           // Note that PNSE is only required by .NetStandard1.0, see the subsequent comment for more context
                catch (Exception ex) when (ex is IOException || ex is NotImplementedException || ex is PlatformNotSupportedException)
#endif
                {
#if !NETSTANDARD2_0 && !NETSTANDARD2_1 && !NET5_0
                    var security = new MemoryMappedFileSecurity();
                    security.AddAccessRule(
                        new System.Security.AccessControl.AccessRule<MemoryMappedFileRights>(
                            new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
                            MemoryMappedFileRights.Read,
                            System.Security.AccessControl.AccessControlType.Allow));

                    _memoryMappedFile = MemoryMappedFile.CreateFromFile(stream, mapName, Length,
                            MemoryMappedFileAccess.Read, security, HandleInheritability.None, false);
#else

                    // In .NET Core, named maps are not supported for Unices yet: https://github.com/dotnet/corefx/issues/1329
                    // When executed on unsupported platform, we get the PNSE. In which case, we consruct the memory map by
                    // setting mapName to null.
                    if (ex is PlatformNotSupportedException)
                        mapName = null;

                    // In NetStandard1.0 (docs: http://bit.ly/1TOKXEw) and since .Net46 (https://msdn.microsoft.com/en-us/library/dn804422.aspx)
                    // CreateFromFile has a new overload with six arguments (modulo MemoryMappedFileSecurity). While the one with seven arguments
                    // is still available in .Net46, that has been removed from netstandard1.0.
                    _memoryMappedFile = MemoryMappedFile.CreateFromFile(stream, mapName, Length,
                            MemoryMappedFileAccess.Read, HandleInheritability.None, false);
#endif
                }
            }

            _view = _memoryMappedFile.CreateViewAccessor(0, Length, MemoryMappedFileAccess.Read);
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
            if (offset + count > _view.Capacity) {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    "Attempt to read beyond the end of the MemoryMappedFile.");
            }
            unsafe
            {
                byte* ptr = (byte*) 0;
                try {
                    _view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                    return Encoding.UTF8.GetString(ptr + offset, count);
                } finally {
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
