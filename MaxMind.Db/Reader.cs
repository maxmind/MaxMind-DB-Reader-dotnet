#region

using System;
using System.IO;
using System.Linq;
using System.Net;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     An enumeration specifying the API to use to read the database
    /// </summary>
    public enum FileAccessMode
    {
        /// <summary>
        ///     Open the file in memory mapped mode. Does not load into real memory.
        /// </summary>
        MemoryMapped,

        /// <summary>
        ///     Load the file into memory.
        /// </summary>
        Memory
    }

    /// <summary>
    ///     Given a MaxMind DB file, this class will retrieve information about an IP address
    /// </summary>
    public sealed class Reader : IDisposable
    {
        private const int DataSectionSeparatorSize = 16;
        private readonly Buffer _database;
        private readonly string _fileName;

        // The property getter was a hotspot during profiling.

        private readonly byte[] _metadataStartMarker =
        {
            0xAB, 0xCD, 0xEF, 77, 97, 120, 77, 105, 110, 100, 46, 99, 111,
            109
        };

        private bool _disposed;
        private int _ipV4Start;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Reader" /> class.
        /// </summary>
        /// <param name="file">The file.</param>
        public Reader(string file) : this(file, FileAccessMode.MemoryMapped)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Reader" /> class.
        /// </summary>
        /// <param name="file">The MaxMind DB file.</param>
        /// <param name="mode">The mode by which to access the DB file.</param>
        public Reader(string file, FileAccessMode mode) : this(BufferForMode(file, mode))
        {
            _fileName = file;
        }

        private static Buffer BufferForMode(string file, FileAccessMode mode)
        {
            switch (mode)
            {
                case FileAccessMode.MemoryMapped:
                    return new MemoryMapBuffer(file);

                case FileAccessMode.Memory:
                    return new ArrayBuffer(file);

                default:
                    throw new ArgumentException("Unknown file access mode");
            }
        }

        /// <summary>
        ///     Initialize with Stream.
        /// </summary>
        /// <param name="stream">The stream to use. It will be used from its current position. </param>
        /// <exception cref="ArgumentNullException"></exception>
        public Reader(Stream stream) : this(new ArrayBuffer(stream))
        {
        }

        private Reader(Buffer buffer)
        {
            _database = buffer;
            var start = FindMetadataStart();
            var metaDecode = new Decoder(_database, start);
            long ignore;
            Metadata = metaDecode.Decode<Metadata>(start, out ignore);
            Decoder = new Decoder(_database, Metadata.SearchTreeSize + DataSectionSeparatorSize);
        }

        /// <summary>
        ///     The metadata for the open database.
        /// </summary>
        /// <value>
        ///     The metadata.
        /// </value>
        public Metadata Metadata { get; }

        private int IPv4Start
        {
            get
            {
                if (_ipV4Start != 0 || Metadata.IPVersion == 4)
                {
                    return _ipV4Start;
                }
                var node = 0;
                for (var i = 0; i < 96 && node < Metadata.NodeCount; i++)
                {
                    node = ReadNode(node, 0);
                }
                _ipV4Start = node;
                return node;
            }
        }

        private Decoder Decoder { get; }

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
        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _database.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        ///     Finds the data related to the specified address.
        /// </summary>
        /// <param name="ipAddress">The IP address.</param>
        /// <param name="injectables">Value to inject during deserialization</param>
        /// <returns>An object containing the IP related data</returns>
        public T Find<T>(IPAddress ipAddress, InjectableValues injectables = null) where T : class
        {
            var pointer = FindAddressInTree(ipAddress);
            return pointer == 0 ? null : ResolveDataPointer<T>(pointer, injectables);
        }

        private T ResolveDataPointer<T>(int pointer, InjectableValues injectables) where T : class
        {
            var resolved = (pointer - Metadata.NodeCount) + Metadata.SearchTreeSize;

            if (resolved >= _database.Length)
            {
                throw new InvalidDatabaseException(
                    "The MaxMind Db file's search tree is corrupt: "
                    + "contains pointer larger than the database.");
            }

            long ignore;
            return Decoder.Decode<T>(resolved, out ignore, injectables);
        }

        private int FindAddressInTree(IPAddress address)
        {
            var rawAddress = address.GetAddressBytes();

            var bitLength = rawAddress.Length * 8;
            var record = StartNode(bitLength);

            for (var i = 0; i < bitLength; i++)
            {
                if (record >= Metadata.NodeCount)
                {
                    break;
                }
                var b = rawAddress[i / 8];
                var bit = 1 & (b >> 7 - (i % 8));
                record = ReadNode(record, bit);
            }
            if (record == Metadata.NodeCount)
            {
                // record is empty
                return 0;
            }
            if (record > Metadata.NodeCount)
            {
                // record is a data pointer
                return record;
            }
            throw new InvalidDatabaseException("Something bad happened");
        }

        private int StartNode(int bitLength)
        {
            // Check if we are looking up an IPv4 address in an IPv6 tree. If this
            // is the case, we can skip over the first 96 nodes.
            if (Metadata.IPVersion == 6 && bitLength == 32)
            {
                return IPv4Start;
            }
            // The first node of the tree is always node 0, at the beginning of the
            // value
            return 0;
        }

        private int FindMetadataStart()
        {
            var buffer = new byte[_metadataStartMarker.Length];

            for (var i = (_database.Length - _metadataStartMarker.Length); i > 0; i--)
            {
                _database.Copy(i, buffer);

                if (!buffer.SequenceEqual(_metadataStartMarker))
                    continue;

                return i + _metadataStartMarker.Length;
            }

            throw new InvalidDatabaseException(
                "Could not find a MaxMind Db metadata marker in this file ("
                + _fileName + "). Is this a valid MaxMind Db file?");
        }

        private int ReadNode(int nodeNumber, int index)
        {
            var baseOffset = nodeNumber * Metadata.NodeByteSize;

            var size = Metadata.RecordSize;

            switch (size)
            {
                case 24:
                    {
                        return _database.ReadInteger(0, baseOffset + index * 3, 3);
                    }
                case 28:
                    {
                        var middle = _database.ReadOne(baseOffset + 3);
                        middle = (index == 0) ? (byte)(middle >> 4) : (byte)(0x0F & middle);

                        return _database.ReadInteger(middle, baseOffset + index * 4, 3);
                    }
                case 32:
                    {
                        return _database.ReadInteger(0, baseOffset + index * 4, 4);
                    }
            }

            throw new InvalidDatabaseException($"Unknown record size: {size}");
        }
    }
}