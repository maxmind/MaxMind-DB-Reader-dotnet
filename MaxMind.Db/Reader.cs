#region

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
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
        private readonly IByteReader _database;
        private readonly string _fileName;

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
        public Reader(string file, FileAccessMode mode)
        {
            _fileName = file;

            switch (mode)
            {
                case FileAccessMode.MemoryMapped:
                    _database = new MemoryMapReader(file);
                    break;

                case FileAccessMode.Memory:
                    _database = new ArrayReader(file);
                    break;

                default:
                    throw new ArgumentException("Unknown file access mode");
            }

            InitMetaData();
        }

        /// <summary>
        ///     Initialize with Stream.
        /// </summary>
        /// <param name="stream">The stream to use. It will be used from its current position. </param>
        /// <exception cref="ArgumentNullException"></exception>
        public Reader(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "The database stream must not be null.");
            }
            byte[] fileBytes;

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            if (fileBytes.Length == 0)
            {
                throw new InvalidDatabaseException(
                    "There are zero bytes left in the stream. Perhaps you need to reset the stream's position.");
            }

            _database = new ArrayReader(fileBytes);
            InitMetaData();
        }

        /// <summary>
        ///     The metadata for the open database.
        /// </summary>
        /// <value>
        ///     The metadata.
        /// </value>
        public Metadata Metadata { get; private set; }

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

        private Decoder Decoder { get; set; }

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

        private void InitMetaData()
        {
            var start = FindMetadataStart();
            var metaDecode = new Decoder(_database, start);
            int ignore;
            var result = metaDecode.Decode<ReadOnlyDictionary<string, object>>(start, out ignore);
            Metadata = Deserialize<Metadata>(JObject.FromObject(result));
            Decoder = new Decoder(_database, Metadata.SearchTreeSize + DataSectionSeparatorSize);
        }

        /// <summary>
        ///     Finds the data related to the specified address.
        /// </summary>
        /// <param name="ipAddress">The IP address.</param>
        /// <returns>An object containing the IP related data</returns>
        public T Find<T>(string ipAddress) where T : class
        {
            return Find<T>(IPAddress.Parse(ipAddress));
        }

        /// <summary>
        ///     Finds the data related to the specified address.
        /// </summary>
        /// <param name="ipAddress">The IP address.</param>
        /// <returns>An object containing the IP related data</returns>
        public T Find<T>(IPAddress ipAddress) where T : class
        {
            var pointer = FindAddressInTree(ipAddress);
            return pointer == 0 ? null : ResolveDataPointer<T>(pointer);
        }

        private T ResolveDataPointer<T>(int pointer) where T : class
        {
            var resolved = (pointer - Metadata.NodeCount) + Metadata.SearchTreeSize;

            if (resolved >= _database.Length)
            {
                throw new InvalidDatabaseException(
                    "The MaxMind Db file's search tree is corrupt: "
                    + "contains pointer larger than the database.");
            }

            int ignore;
            return Decoder.Decode<T>(resolved, out ignore);
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

        private T Deserialize<T>(JToken value)
        {
            var serializer = new JsonSerializer();
            using (var reader = new JTokenReader(value))
            {
                return serializer.Deserialize<T>(reader);
            }
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
                        return DecodeInteger(0, baseOffset + index * 3, 3);
                    }
                case 28:
                    {
                        var middle = _database.ReadOne(baseOffset + 3);
                        middle = (index == 0) ? (byte)(middle >> 4) : (byte)(0x0F & middle);

                        return DecodeInteger(middle, baseOffset + index * 4, 3);
                    }
                case 32:
                    {
                        return DecodeInteger(0, baseOffset + index * 4, 4);
                    }
            }

            throw new InvalidDatabaseException("Unknown record size: "
                                               + size);
        }

        // XXX share again
        private int DecodeInteger(int val, int offset, int size)
        {
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | _database.ReadOne(offset + i);
            }
            return val;
        }
    }
}