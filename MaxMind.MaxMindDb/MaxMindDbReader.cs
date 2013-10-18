using System;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MaxMind.MaxMindDb
{
    using Winterdom.IO.FileMap;

    /// <summary>
    /// An enumeration specifying the API to use to read the database
    /// </summary>
    public enum FileAccessMode
    {
        MemoryMapped,
        Memory
    }

    /// <summary>
    /// Given a MaxMind DB file, this class will retrieve information about an IP address
    /// </summary>
    public class MaxMindDbReader : IDisposable
    {
        /// <summary>
        /// Gets the metadata.
        /// </summary>
        /// <value>
        /// The metadata.
        /// </value>
        public Metadata Metadata { get; private set; }

        #region Private

        private int DATA_SECTION_SEPARATOR_SIZE = 16;

        private byte[] METADATA_START_MARKER = new byte[] { (byte)0xAB, (byte)0xCD, (byte)0xEF, 77, 97, 120, 77, 105, 110, 100, 46, 99, 111, 109 };

        private string FileName { get; set; }

        private int FileSize { get; set; }

        private int _ipV4Start;
        private int ipV4Start
        {
            get
            {
                if (_ipV4Start == 0 || this.Metadata.IpVersion == 4)
                {
                    int node = 0;
                    for (int i = 0; i < 96 && node < this.Metadata.NodeCount; i++)
                    {
                        node = this.ReadNode(node, 0);
                    }
                    this._ipV4Start = node;
                }
                return _ipV4Start;
            }
        }

        private Stream fs { get; set; }

        private Decoder Decoder { get; set; }

        private MemoryMappedFile memoryMappedFile;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="MaxMindDbReader"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        public MaxMindDbReader(string file) : this(file, FileAccessMode.MemoryMapped) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaxMindDbReader"/> class.
        /// </summary>
        /// <param name="file">The MaxMind DB file.</param>
        /// <param name="mode">The mode by which to access the DB file.</param>
        public MaxMindDbReader(string file, FileAccessMode mode)
        {
            this.FileName = file;

            if (mode == FileAccessMode.Memory) this.fs = new MemoryStream(File.ReadAllBytes(this.FileName));
            else
            {
                var fileLength = (int)new FileInfo(file).Length;
                memoryMappedFile = MemoryMappedFile.Create(this.FileName, MapProtection.PageReadOnly, fileLength);
                fs = memoryMappedFile.MapView(MapAccess.FileMapRead, 0, fileLength);
            }

            int start = this.FindMetadataStart();
            Decoder meta_decode = new Decoder(fs, start);
            Result result = meta_decode.Decode(start);
            this.Metadata = Deserialize<Metadata>(result.Node);
            this.Decoder = new Decoder(fs, this.Metadata.SearchTreeSize + DATA_SECTION_SEPARATOR_SIZE);
        }

        /// <summary>
        /// Finds the data related to the specified address.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <returns>An object containing the IP related data</returns>
        public JToken Find(string address)
        {
            return Find(IPAddress.Parse(address));
        }

        /// <summary>
        /// Finds the data related to the specified address.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <returns>An object containing the IP related data</returns>
        public JToken Find(IPAddress address)
        {
            int pointer = this.FindAddressInTree(address);
            if (pointer == 0)
                return null;

            return ResolveDataPointer(pointer);
        }

        #region Private

        private JToken ResolveDataPointer(int pointer)
        {
            int resolved = (int)((pointer - this.Metadata.NodeCount) + this.Metadata.SearchTreeSize);

            if (resolved >= this.fs.Length)
            {
                throw new InvalidDatabaseException(
                        "The MaxMind DB file's search tree is corrupt: "
                                + "contains pointer larger than the database.");
            }

            return this.Decoder.Decode(resolved).Node;
        }

        private int FindAddressInTree(IPAddress address)
        {
            byte[] rawAddress = address.GetAddressBytes();

            int bitLength = rawAddress.Length * 8;
            int record = this.StartNode(bitLength);

            for (int i = 0; i < bitLength; i++)
            {
                if (record >= this.Metadata.NodeCount)
                {
                    break;
                }
                int b = 0xFF & rawAddress[i / 8];
                int bit = 1 & (b >> 7 - (i % 8));
                record = this.ReadNode(record, bit);
            }
            if (record == this.Metadata.NodeCount)
            {
                // record is empty
                return 0;
            }
            else if (record > this.Metadata.NodeCount)
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
            if (this.Metadata.IpVersion == 6 && bitLength == 32)
            {
                return this.ipV4Start;
            }
            // The first node of the tree is always node 0, at the beginning of the
            // value
            return 0;
        }

        private T Deserialize<T>(JToken value)
        {
            var serializer = new JsonSerializer();
            return serializer.Deserialize<T>(new JTokenReader(value));
        }

        private int FindMetadataStart()
        {
            this.FileSize = (int)fs.Length;
            byte[] buffer = new byte[METADATA_START_MARKER.Length];

            for (int i = (this.FileSize - METADATA_START_MARKER.Length); i > 0; i--)
            {
                fs.Seek(i, SeekOrigin.Begin);
                long pos = fs.Position;
                fs.Read(buffer, 0, buffer.Length);

                if (!buffer.SequenceEqual(METADATA_START_MARKER))
                    continue;

                return i + METADATA_START_MARKER.Length;
            }

            return -1;
        }

        private int ReadNode(int nodeNumber, int index)
        {
            int baseOffset = (int)(nodeNumber * this.Metadata.NodeByteSize);

            int size = (int)this.Metadata.RecordSize;

            if (size == 24)
            {
                byte[] buffer = ReadMany(baseOffset + index * 3, 3);
                return Decoder.DecodeInteger(buffer);
            }
            else if (size == 28)
            {
                int middle = ReadOne(baseOffset + 3);
                middle = (index == 0) ? (0xF0 & middle) >> 4 : 0x0F & middle;

                byte[] buffer = ReadMany(baseOffset + index * 4, 3);
                return Decoder.DecodeInteger(middle, buffer);
            }
            else if (size == 32)
            {
                byte[] buffer = ReadMany(baseOffset + index * 4, 4);
                return Decoder.DecodeInteger(buffer);
            }

            throw new InvalidDatabaseException("Unknown record size: "
                    + size);
        }

        private int ReadOne(int position)
        {
            lock (fs)
            {
                fs.Seek(position, SeekOrigin.Begin);
                return fs.ReadByte();
            }
        }

        private byte[] ReadMany(int position, int size)
        {
            lock (fs)
            {
                byte[] buffer = new byte[size];
                fs.Seek(position, SeekOrigin.Begin);
                fs.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                this.fs.Dispose();
                memoryMappedFile.Dispose();
            }
            catch { }
        }

        ~MaxMindDbReader()
        {
            Dispose();
        }

        #endregion
    }
}