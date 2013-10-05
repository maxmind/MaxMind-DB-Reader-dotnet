using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MaxMind.MaxMindDb
{
    public enum ObjectType
    {
        EXTENDED, POINTER, UTF8_STRING, DOUBLE, BYTES, UINT16, UINT32, MAP, INT32, UINT64, UINT128, ARRAY, CONTAINER, END_MARKER, BOOLEAN, FLOAT
    }

    public class Decoder
    {
        #region Private

        private Stream fs = null;

        private long pointerBase = -1;

        private int[] pointerValueOffset = { 0, 0, 1 << 11, (1 << 19) + ((1) << 11), 0 };

        #endregion

        public Decoder(Stream stream, long pointerBase)
        {
            this.pointerBase = pointerBase;
            this.fs = stream;
        }

        /// <summary>
        /// Decodes the specified offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public MaxMindDbResult Decode(int offset)
        {
            int ctrlByte = 0xFF & ReadOne(offset);
            offset++;

            ObjectType type = FromControlByte(ctrlByte);

            if (type == ObjectType.POINTER)
            {
                MaxMindDbResult pointer = this.decodePointer(ctrlByte, offset);
                MaxMindDbResult result = this.Decode(Convert.ToInt32(pointer.Node.Value));
                result.Offset = pointer.Offset;
                return result;
            }

            if (type == ObjectType.EXTENDED)
            {
                int nextByte = ReadOne(offset);
                int typeNum = nextByte + 7;
                type = GetObjectType(typeNum);
                offset++;
            }

            int[] sizeArray = this.SizeFromCtrlByte(ctrlByte, offset);
            int size = sizeArray[0];
            offset = sizeArray[1];

            return DecodeByType(type, offset, size);
        }

        #region Private

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
        /// Decodes the type of the by.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Unable to handle type!</exception>
        private MaxMindDbResult DecodeByType(ObjectType type, int offset, int size)
        {
            int new_offset = offset + size;
            byte[] buffer = ReadMany(offset, size);

            switch (type)
            {
                case ObjectType.MAP:
                    return decodeMap(size, offset);
                case ObjectType.ARRAY:
                    return decodeArray(size, offset);
                case ObjectType.BOOLEAN:
                    return MaxMindDbResult.Create<bool>(decodeBoolean(buffer), offset);
                case ObjectType.UTF8_STRING:
                    return MaxMindDbResult.Create<string>(decodeString(buffer), new_offset);
                case ObjectType.DOUBLE:
                    return MaxMindDbResult.Create<double>(decodeDouble(buffer), new_offset);
                case ObjectType.FLOAT:
                    return MaxMindDbResult.Create<float>(decodeFloat(buffer), new_offset);
                case ObjectType.BYTES:
                    return MaxMindDbResult.Create<byte[]>(buffer, new_offset);
                case ObjectType.UINT16:
                    return MaxMindDbResult.Create<int>(decodeInteger(buffer), new_offset);
                case ObjectType.UINT32:
                    return MaxMindDbResult.Create<long>(decodeLong(buffer), new_offset);
                case ObjectType.INT32:
                    return MaxMindDbResult.Create<int>(decodeInteger(buffer), new_offset);
                case ObjectType.UINT64:
                    return MaxMindDbResult.Create<long>(decodeUint64(buffer), new_offset);
                case ObjectType.UINT128:
                    return MaxMindDbResult.Create<BigInteger>(decodeBigInteger(buffer), new_offset);
                default:
                    throw new Exception("Unable to handle type!");
            }
        }

        /// <summary>
        /// Froms the control byte.
        /// </summary>
        /// <param name="b">The attribute.</param>
        /// <returns></returns>
        private ObjectType FromControlByte(int b)
        {
            byte p = (byte)((0xFF & b) >> 5);
            string[] names = Enum.GetNames(typeof(ObjectType));
            return (ObjectType)Enum.Parse(typeof(ObjectType), names[p]);
        }

        /// <summary>
        /// Gets the type of the object.
        /// </summary>
        /// <param name="b">The attribute.</param>
        /// <returns></returns>
        private ObjectType GetObjectType(int b)
        {
            string[] names = Enum.GetNames(typeof(ObjectType));
            return (ObjectType)Enum.Parse(typeof(ObjectType), names[b]);
        }

        /// <summary>
        /// Sizes from control byte.
        /// </summary>
        /// <param name="ctrlByte">The control byte.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        private int[] SizeFromCtrlByte(int ctrlByte, int offset)
        {
            int size = ctrlByte & 0x1f;
            int bytesToRead = size < 29 ? 0 : size - 28;

            if (size == 29) {
                byte[] buffer = ReadMany(offset, bytesToRead);
                int i = this.decodeInteger(buffer);
                size = 29 + i;
            } else if (size == 30) {
                byte[] buffer = ReadMany(offset, bytesToRead);
                int i = this.decodeInteger(buffer);
                size = 285 + i;
            }
            else if (size > 30)
            {
                byte[] buffer = ReadMany(offset, bytesToRead);
                int i = this.decodeInteger(buffer) & (0x0FFFFFFF >> (32 - (8 * bytesToRead)));
                size = 65821 + i;
            }

            return new int[] { size, offset + bytesToRead };
        }

        #region Convert

        /// <summary>
        /// Decodes the boolean.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private bool decodeBoolean(byte[] buffer)
        {
            return BitConverter.ToBoolean(buffer, 0);
        }

        /// <summary>
        /// Decodes the double.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private double decodeDouble(byte[] buffer)
        {
            return BitConverter.ToDouble(buffer, 0);
        }

        /// <summary>
        /// Decodes the float.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private float decodeFloat(byte[] buffer)
        {
            return (float)BitConverter.ToDouble(buffer, 0);
        }

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private string decodeString(byte[] buffer)
        {
            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Decodes the map.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        private MaxMindDbResult decodeMap(int size, int offset)
        {
            Dictionary<MaxMindDbResultNode, MaxMindDbResultNode> dict = new Dictionary<MaxMindDbResultNode, MaxMindDbResultNode>();
            MaxMindDbResultNode key = null;
            MaxMindDbResultNode value = null;

            for (int i = 0; i < size; i++)
            {
                MaxMindDbResult left = this.Decode(offset);
                key = left.Node;
                offset = left.Offset;
                MaxMindDbResult right = this.Decode(offset);
                value = right.Node;
                offset = right.Offset;
                dict.Add(key, value);
            }

            return MaxMindDbResult.Create<Dictionary<MaxMindDbResultNode, MaxMindDbResultNode>>(dict, offset);
        }

        /// <summary>
        /// Decodes the long.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private long decodeLong(byte[] buffer)
        {
            long integer = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                integer = (integer << 8) | (buffer[i] & 0xFF);
            }
            return integer;
        }

        /// <summary>
        /// Decodes the integer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private int decodeInteger(byte[] buffer)
        {
            int integer = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                integer = (integer << 8) | (buffer[i] & 0xFF);
            }
            return integer;
        }

        /// <summary>
        /// Decodes the integer.
        /// </summary>
        /// <param name="b">The attribute.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private int decodeInteger(int b, byte[] buffer)
        {
            int integer = b;
            for (int i = 0; i < buffer.Length; i++)
            {
                integer = (integer << 8) | (buffer[i] & 0xFF);
            }
            return integer;
        }

        /// <summary>
        /// Decodes the array.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        private MaxMindDbResult decodeArray(int size, int offset)
        {
            List<MaxMindDbResultNode> list = new List<MaxMindDbResultNode>();

            for (int i = 0; i < size; i++)
            {
                MaxMindDbResult r = this.Decode(offset);
                offset = r.Offset;
                list.Add(r.Node);
            }

            return MaxMindDbResult.Create<List<MaxMindDbResultNode>>(list, offset);
        }

        /// <summary>
        /// Decodes the uint64.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private long decodeUint64(byte[] buffer)
        {
            return BigInteger.ToInt64(new BigInteger(buffer));
        }

        /// <summary>
        /// Decodes the big integer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private BigInteger decodeBigInteger(byte[] buffer)
        {
            return new BigInteger(buffer);
        }

        /// <summary>
        /// Decodes the pointer.
        /// </summary>
        /// <param name="ctrlByte">The control byte.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        private MaxMindDbResult decodePointer(int ctrlByte, int offset)
        {
            int pointerSize = ((ctrlByte >> 3) & 0x3) + 1;
            int b = pointerSize == 4 ? (byte)0 : (byte)(ctrlByte & 0x7);
            byte[] buffer = ReadMany(offset, pointerSize);
            int packed = this.decodeInteger(b, buffer);
            long pointer = packed + this.pointerBase + this.pointerValueOffset[pointerSize];
            return MaxMindDbResult.Create<long>(pointer, offset + pointerSize);
        }

        #endregion

        #endregion

        #region static

        /// <summary>
        /// Decodes the integer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static int DecodeInteger(byte[] buffer)
        {
            int integer = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                integer = (integer << 8) | (buffer[i] & 0xFF);
            }
            return integer;
        }

        /// <summary>
        /// Decodes the integer.
        /// </summary>
        /// <param name="baseValue">The base value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static int DecodeInteger(int baseValue, byte[] buffer)
        {
            int integer = baseValue;
            for (int i = 0; i < buffer.Length; i++)
            {
                integer = (integer << 8) | (buffer[i] & 0xFF);
            }
            return integer;
        }

        #endregion
    }
}