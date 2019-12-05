#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

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
        ///     Open the file in global memory mapped mode. Requires the 'create global objects' right. Does not load into real memory.
        /// </summary>
        /// <remarks>
        ///     For information on the 'create global objects' right, see: https://docs.microsoft.com/en-us/windows/security/threat-protection/security-policy-settings/create-global-objects
        /// </remarks>
        MemoryMappedGlobal,

        /// <summary>
        ///     Load the file into memory.
        /// </summary>
        Memory,
    }

    /// <summary>
    ///     Given a MaxMind DB file, this class will retrieve information about an IP address
    /// </summary>
    public sealed class Reader : IDisposable
    {
        /// <summary>
        /// A node from the reader iterator
        /// </summary>
        public struct ReaderIteratorNode<T>
        {
            /// <summary>
            /// Internal constructor
            /// </summary>
            /// <param name="start">Start ip</param>
            /// <param name="prefixLength">Prefix length</param>
            /// <param name="data">Data</param>
            internal ReaderIteratorNode(IPAddress start, int prefixLength, T data)
            {
                Start = start;
                PrefixLength = prefixLength;
                Data = data;
            }

            /// <summary>
            /// Start ip address
            /// </summary>
            public IPAddress Start { get; }

            /// <summary>
            /// Prefix/mask length
            /// </summary>
            public int PrefixLength { get; }

            /// <summary>
            /// Data
            /// </summary>
            public T Data { get; }
        }

        private struct NetNode
        {
            public byte[] IPBytes { get; set; }
            public int Bit { get; set; }
            public int Pointer { get; set; }
        }

        private const int DataSectionSeparatorSize = 16;
        private readonly Buffer _database;
        private readonly string? _fileName;

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
        public Reader(string file, FileAccessMode mode) : this(BufferForMode(file, mode), file)
        {
        }

        /// <summary>
        ///     Asynchronously initializes a new instance of the <see cref="Reader" /> class by loading the specified file into memory.
        /// </summary>
        /// <param name="file">The file.</param>
        public static async Task<Reader> CreateAsync(string file)
        {
            return new Reader(await ArrayBuffer.CreateAsync(file).ConfigureAwait(false), file);
        }

        private static Buffer BufferForMode(string file, FileAccessMode mode)
        {
            switch (mode)
            {
                case FileAccessMode.MemoryMapped:
                    return new MemoryMapBuffer(file, false);

                case FileAccessMode.MemoryMappedGlobal:
                    return new MemoryMapBuffer(file, true);

                case FileAccessMode.Memory:
                    return new ArrayBuffer(file);

                default:
                    throw new ArgumentException("Unknown file access mode");
            }
        }

        /// <summary>
        ///     Initialize with <c>Stream</c>. The current position of the
        ///     string must point to the start of the database. The content
        ///     between the current position and the end of the stream must
        ///     be a valid MaxMind DB.
        /// </summary>
        /// <param name="stream">The stream to use. It will be used from its
        ///                      current position. </param>
        /// <exception cref="ArgumentNullException"></exception>
        public Reader(Stream stream) : this(new ArrayBuffer(stream), null)
        {
        }

        /// <summary>
        ///     Asynchronously initialize with Stream.
        /// </summary>
        /// <param name="stream">The stream to use. It will be used from its current position. </param>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<Reader> CreateAsync(Stream stream)
        {
            return new Reader(await ArrayBuffer.CreateAsync(stream).ConfigureAwait(false), null);
        }

        private Reader(Buffer buffer, string? file)
        {
            _fileName = file;
            _database = buffer;
            var start = FindMetadataStart();
            var metaDecode = new Decoder(_database, start);
            Metadata = metaDecode.Decode<Metadata>(start, out _);
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
        public T? Find<T>(IPAddress ipAddress, InjectableValues? injectables = null) where T : class
        {
            return Find<T>(ipAddress, out _, injectables);
        }

        /// <summary>
        /// Get an enumerator that iterates all data nodes in the database. Do not modify the object as it may be cached.
        /// 
        /// Note that due to caching, the Network attribute on constructor parameters will be ignored.
        /// </summary>
        /// <param name="injectables">Value to inject during deserialization</param>
        /// <param name="cacheSize">The size of the data cache. This can greatly speed enumeration at the cost of memory usage.</param>
        /// <returns>Enumerator for all data nodes</returns>
        public IEnumerable<ReaderIteratorNode<T>> FindAll<T>(InjectableValues? injectables = null, int cacheSize = 16384) where T : class
        {
            var byteCount = Metadata.IPVersion == 6 ? 16 : 4;
            var nodes = new List<NetNode>();
            var root = new NetNode { IPBytes = new byte[byteCount] };
            nodes.Add(root);
            var dataCache = new CachedDictionary<int, T>(cacheSize, null);
            while (nodes.Count > 0)
            {
                var node = nodes[nodes.Count - 1];
                nodes.RemoveAt(nodes.Count - 1);
                while (true)
                {
                    if (node.Pointer < Metadata.NodeCount)
                    {
                        var ipRight = new byte[byteCount];
                        Array.Copy(node.IPBytes, ipRight, ipRight.Length);
                        if (ipRight.Length <= node.Bit >> 3)
                        {
                            throw new InvalidDataException("Invalid search tree, bad bit " + node.Bit);
                        }
                        ipRight[node.Bit >> 3] |= (byte)(1 << (7 - (node.Bit % 8)));
                        var rightPointer = ReadNode(node.Pointer, 1);
                        node.Bit++;
                        nodes.Add(new NetNode { Pointer = rightPointer, IPBytes = ipRight, Bit = node.Bit });
                        node.Pointer = ReadNode(node.Pointer, 0);
                    }
                    else
                    {
                        if (node.Pointer > Metadata.NodeCount)
                        {
                            // data node, we are done with this branch
                            if (!dataCache.TryGetValue(node.Pointer, out var data))
                            {
                                data = ResolveDataPointer<T>(node.Pointer, injectables, null);
                                dataCache.Add(node.Pointer, data);
                            }
                            var isIPV4 = true;
                            for (var i = 0; i < node.IPBytes.Length - 4; i++)
                            {
                                if (node.IPBytes[i] == 0) continue;

                                isIPV4 = false;
                                break;
                            }
                            if (!isIPV4 || node.IPBytes.Length == 4)
                            {
                                yield return new ReaderIteratorNode<T>(new IPAddress(node.IPBytes), node.Bit, data);
                            }
                            else
                            {
                                yield return new ReaderIteratorNode<T>(new IPAddress(node.IPBytes.Skip(12).Take(4).ToArray()), node.Bit - 96, data);
                            }
                        }
                        // else node is an empty node (terminator node), we are done with this branch
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     Finds the data related to the specified address.
        /// </summary>
        /// <param name="ipAddress">The IP address.</param>
        /// <param name="prefixLength">The network prefix length for the network record in the database containing the IP address looked up.</param>
        /// <param name="injectables">Value to inject during deserialization</param>
        /// <returns>An object containing the IP related data</returns>
        public T? Find<T>(IPAddress ipAddress, out int prefixLength, InjectableValues? injectables = null) where T : class
        {
            var pointer = FindAddressInTree(ipAddress, out prefixLength);
            var network = new Network(ipAddress, prefixLength);
            return pointer == 0 ? null : ResolveDataPointer<T>(pointer, injectables, network);
        }

        private T ResolveDataPointer<T>(int pointer, InjectableValues? injectables, Network? network) where T : class
        {
            var resolved = pointer - Metadata.NodeCount + Metadata.SearchTreeSize;

            if (resolved >= _database.Length)
            {
                throw new InvalidDatabaseException(
                    "The MaxMind Db file's search tree is corrupt: "
                    + "contains pointer larger than the database.");
            }

            return Decoder.Decode<T>(resolved, out _, injectables, network);
        }

        private int FindAddressInTree(IPAddress address, out int prefixLength)
        {
            var rawAddress = address.GetAddressBytes();

            var bitLength = rawAddress.Length * 8;
            var record = StartNode(bitLength);
            var nodeCount = Metadata.NodeCount;

            var i = 0;
            for (; i < bitLength && record < nodeCount; i++)
            {
                var bit = 1 & (rawAddress[i >> 3] >> (7 - (i % 8)));
                record = ReadNode(record, bit);
            }
            prefixLength = i;
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

            for (var i = _database.Length - _metadataStartMarker.Length; i > 0; i--)
            {
                _database.Copy(i, buffer);

                if (!buffer.SequenceEqual(_metadataStartMarker))
                    continue;

                return i + _metadataStartMarker.Length;
            }

            throw new InvalidDatabaseException(
                $"Could not find a MaxMind Db metadata marker in this file ({_fileName}). Is this a valid MaxMind Db file?");
        }

        private int ReadNode(int nodeNumber, int index)
        {
            var baseOffset = nodeNumber * Metadata.NodeByteSize;

            var size = Metadata.RecordSize;

            switch (size)
            {
                case 24:
                    {
                        var offset = baseOffset + index * 3;
                        return _database.ReadOne(offset) << 16 |
                            _database.ReadOne(offset + 1) << 8 |
                            _database.ReadOne(offset + 2);
                    }
                case 28:
                    {
                        if (index == 0)
                        {
                            return ((_database.ReadOne(baseOffset + 3) & 0xF0) << 20) |
                                    (_database.ReadOne(baseOffset) << 16) |
                                    (_database.ReadOne(baseOffset + 1) << 8) |
                                    _database.ReadOne(baseOffset + 2);
                        }
                        return ((_database.ReadOne(baseOffset + 3) & 0x0F) << 24) |
                                (_database.ReadOne(baseOffset + 4) << 16) |
                                (_database.ReadOne(baseOffset + 5) << 8) |
                                _database.ReadOne(baseOffset + 6);
                    }
                case 32:
                    {
                        var offset = baseOffset + index * 4;
                        return _database.ReadOne(offset) << 24 |
                               _database.ReadOne(offset + 1) << 16 |
                               _database.ReadOne(offset + 2) << 8 |
                               _database.ReadOne(offset + 3);
                    }
            }

            throw new InvalidDatabaseException($"Unknown record size: {size}");
        }
    }
}