using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Newtonsoft.Json;

namespace MaxMind.GeoIP2
{
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

        private Stream fs { get; set; }

        private MaxMindDbDecoder Decoder { get; set; }

        #endregion

        public MaxMindDbReader(string file) : this(file, FileAccessMode.MEMORY_MAPPED) { }

        public MaxMindDbReader(string file, FileAccessMode mode)
        {
            this.FileName = file;

            if (mode == FileAccessMode.MEMORY)
                this.fs = new FileStream(this.FileName, FileMode.Open, FileAccess.Read);
            else
                this.fs = new MemoryStream(File.ReadAllBytes(this.FileName));

            int start = this.FindMetadataStart();
            MaxMindDbDecoder meta_decode = new MaxMindDbDecoder(fs, 0);
            MaxMindDbResult result = meta_decode.Decode(start);
            this.Metadata = Deserialize<Metadata>(result.ToJson());
            this.Decoder = new MaxMindDbDecoder(fs, this.Metadata.SearchTreeSize + DATA_SECTION_SEPARATOR_SIZE);
        }

        /// <summary>
        /// Finds the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public Response Find(string address)
        {
            return Find(IPAddress.Parse(address));
        }

        /// <summary>
        /// Finds the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public Response Find(IPAddress address)
        {
            int pointer = this.FindAddressInTree(address);
            if (pointer == 0)
                return null;

            MaxMindDbResult result = ResolveDataPointer(pointer);
            return Deserialize<Response>(result.ToJson());
        }

        #region Private

        /// <summary>
        /// Resolves the data pointer.
        /// </summary>
        /// <param name="pointer">The pointer.</param>
        /// <returns></returns>
        private MaxMindDbResult ResolveDataPointer(int pointer)
        {
            int resolved = (int)((pointer - this.Metadata.NodeCount) + this.Metadata.SearchTreeSize);
            return this.Decoder.Decode(resolved);
        }

        /// <summary>
        /// Finds the address information tree.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        private int FindAddressInTree(IPAddress address)
        {
            byte[] rawAddress = address.GetAddressBytes();

            //if (BitConverter.IsLittleEndian)
            //    Array.Reverse(rawAddress);

            bool isIp4AddressInIp6Db = rawAddress.Length == 4 && this.Metadata.IpVersion == 6;
            int ipStartBit = isIp4AddressInIp6Db ? 96 : 0;

            int nodeNum = 0;

            for (int i = 0; i < rawAddress.Length * 8 + ipStartBit; i++)
            {
                int bit = 0;
                if (i >= ipStartBit)
                {
                    int b = 0xFF & rawAddress[(i - ipStartBit) / 8];
                    bit = 1 & (b >> 7 - (i % 8));
                }
                int record = this.ReadNode(nodeNum, bit);

                if (record == this.Metadata.NodeCount)
                    return 0;
                else if (record > this.Metadata.NodeCount)
                    return record;

                nodeNum = record;
            }

            return 0;
        }

        /// <summary>
        /// Deserializes the specified value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        private T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value);
        }

        /// <summary>
        /// Finds the metadata start.
        /// </summary>
        /// <returns></returns>
        private int FindMetadataStart()
        {
            this.FileSize = (int)fs.Length;
            byte[] buffer = new byte[METADATA_START_MARKER.Length];

            for (int i = (this.FileSize - METADATA_START_MARKER.Length); i > 0; i--)
            {
                fs.Seek(i, SeekOrigin.Begin);
                long pos = fs.Position;
                fs.Read(buffer, 0, buffer.Length);

                if (!ByteArrayEqual(buffer, METADATA_START_MARKER))
                    continue;

                return i + METADATA_START_MARKER.Length;
            }

            return -1;
        }

        /// <summary>
        /// Reads the node.
        /// </summary>
        /// <param name="nodeNumber">The node number.</param>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        private int ReadNode(int nodeNumber, int index)
        {
            int baseOffset = (int)(nodeNumber * this.Metadata.NodeByteSize);

            int size = (int)this.Metadata.RecordSize;

            if (size == 24)
            {
                byte[] buffer = ReadMany(baseOffset + index * 3, 3);
                return MaxMindDbDecoder.DecodeInteger(buffer);
            }
            else if (size == 28)
            {
                int middle = ReadOne(baseOffset + 3);
                middle = (index == 0) ? (0xF0 & middle) >> 4 : 0x0F & middle;

                byte[] buffer = ReadMany(baseOffset + index * 4, 3);
                return MaxMindDbDecoder.DecodeInteger(middle, buffer);
            }
            else if (size == 32)
            {
                byte[] buffer = ReadMany(baseOffset + index * 4, 4);
                return MaxMindDbDecoder.DecodeInteger(buffer);
            }

            return 1;
        }

        /// <summary>
        /// Reads the one.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns></returns>
        private int ReadOne(int position)
        {
            lock (fs)
            {
                fs.Seek(position, SeekOrigin.Begin);
                return fs.ReadByte();
            }
        }

        /// <summary>
        /// Reads the many.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Bytes the array equal.
        /// </summary>
        /// <param name="a">The aggregate.</param>
        /// <param name="b">The attribute.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">The lenght of both arrays should be equal</exception>
        private bool ByteArrayEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                throw new Exception("The lenght of both arrays should be equal");

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Bytes the array automatic string.
        /// </summary>
        /// <param name="a">The aggregate.</param>
        /// <returns></returns>
        private string ByteArrayToString(byte[] a)
        {
            return String.Join(",", a.Select(o => o.ToString()).ToArray());
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            try { this.fs.Dispose(); }
            catch { }
        }

        ~MaxMindDbReader()
        {
            Dispose();
        }

        #endregion
    }

    public enum FileAccessMode
    {
        MEMORY_MAPPED,
        MEMORY
    }
}
