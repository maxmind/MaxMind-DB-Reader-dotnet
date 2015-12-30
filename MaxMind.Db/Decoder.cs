#region

using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Numerics;
using System.Text;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Enumeration representing the types of objects read from the database
    /// </summary>
    internal enum ObjectType
    {
        Extended,
        Pointer,
        Utf8String,
        Double,
        Bytes,
        Uint16,
        Uint32,
        Map,
        Int32,
        Uint64,
        Uint128,
        Array,
        Container,
        EndMarker,
        Boolean,
        Float
    }

    /// <summary>
    ///     Given a stream, this class decodes the object graph at a particular location
    /// </summary>
    internal class Decoder
    {
        private readonly IByteReader _database;
        private readonly int _pointerBase;
        private readonly int[] _pointerValueOffset = { 0, 0, 1 << 11, (1 << 19) + ((1) << 11), 0 };

        /// <summary>
        ///     Initializes a new instance of the <see cref="Decoder" /> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pointerBase">The base address in the stream.</param>
        internal Decoder(IByteReader database, int pointerBase)
        {
            _pointerBase = pointerBase;
            _database = database;
        }

        internal bool PointerTestHack { get; set; }

        /// <summary>
        ///     Decodes the object at the specified offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset</param>
        /// <returns>An object containing the data read from the stream</returns>
        internal JToken Decode(int offset, out int outOffset)
        {
            if (offset >= _database.Length)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "pointer larger than the database.");

            var ctrlByte = _database.ReadOne(offset);
            offset++;

            var type = FromControlByte(ctrlByte);

            if (type == ObjectType.Pointer)
            {
                long pointer = DecodePointer(ctrlByte, offset, out offset);
                outOffset = offset;
                if (PointerTestHack)
                {
                    return new JValue(pointer);
                }
                int ignore;
                var result = Decode(Convert.ToInt32(pointer), out ignore);
                return result;
            }

            if (type == ObjectType.Extended)
            {
                int nextByte = _database.ReadOne(offset);
                var typeNum = nextByte + 7;
                if (typeNum < 8)
                    throw new InvalidDatabaseException(
                        "Something went horribly wrong in the decoder. An extended type "
                        + "resolved to a type number < 8 (" + typeNum
                        + ")");
                type = (ObjectType)typeNum;
                offset++;
            }

            var size = SizeFromCtrlByte(ctrlByte, offset, out offset);

            return DecodeByType(type, offset, size, out outOffset);
        }

        /// <summary>
        ///     Decodes the type of the by.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="size">The size.</param>
        /// <param name="outOffset">The out offset</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Unable to handle type!</exception>
        private JToken DecodeByType(ObjectType type, int offset, int size, out int outOffset)
        {
            outOffset = offset + size;
            var buffer = type == ObjectType.Boolean ? null : _database.Read(offset, size);

            switch (type)
            {
                case ObjectType.Map:
                    return DecodeMap(size, offset, out outOffset);

                case ObjectType.Array:
                    return DecodeArray(size, offset, out outOffset);

                case ObjectType.Boolean:
                    outOffset = offset;
                    return DecodeBoolean(size);

                case ObjectType.Utf8String:
                    return DecodeString(buffer);

                case ObjectType.Double:
                    return DecodeDouble(buffer);

                case ObjectType.Float:
                    return DecodeFloat(buffer);

                case ObjectType.Bytes:
                    return new JValue(buffer);

                case ObjectType.Uint16:
                    return DecodeIntegerToJValue(buffer);

                case ObjectType.Uint32:
                    return DecodeLong(buffer);

                case ObjectType.Int32:
                    return DecodeIntegerToJValue(buffer);

                case ObjectType.Uint64:
                    return DecodeUInt64(buffer);

                case ObjectType.Uint128:
                    return DecodeBigInteger(buffer);

                default:
                    throw new InvalidDatabaseException("Unable to handle type:" + type);
            }
        }

        /// <summary>
        ///     From the control byte.
        /// </summary>
        /// <param name="b">The attribute.</param>
        /// <returns></returns>
        private ObjectType FromControlByte(byte b)
        {
            var p = b >> 5;
            return (ObjectType)p;
        }

        /// <summary>
        ///     Sizes from control byte.
        /// </summary>
        /// <param name="ctrlByte">The control byte.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <returns></returns>
        private int SizeFromCtrlByte(byte ctrlByte, int offset, out int outOffset)
        {
            var size = ctrlByte & 0x1f;
            var bytesToRead = size < 29 ? 0 : size - 28;

            if (size == 29)
            {
                var buffer = _database.Read(offset, bytesToRead);
                var i = DecodeInteger(buffer);
                size = 29 + i;
            }
            else if (size == 30)
            {
                var buffer = _database.Read(offset, bytesToRead);
                var i = DecodeInteger(buffer);
                size = 285 + i;
            }
            else if (size > 30)
            {
                var buffer = _database.Read(offset, bytesToRead);
                var i = DecodeInteger(buffer) & (0x0FFFFFFF >> (32 - (8 * bytesToRead)));
                size = 65821 + i;
            }

            outOffset = offset + bytesToRead;
            return size;
        }

        /// <summary>
        ///     Decodes the boolean.
        /// </summary>
        /// <param name="size">The size of the structure.</param>
        /// <returns></returns>
        private JValue DecodeBoolean(int size)
        {
            switch (size)
            {
                case 0:
                    return new JValue(false);

                case 1:
                    return new JValue(true);

                default:
                    throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                       + "invalid size of boolean.");
            }
        }

        /// <summary>
        ///     Decodes the double.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private JValue DecodeDouble(byte[] buffer)
        {
            if (buffer.Length != 8)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of double.");

            Array.Reverse(buffer);
            return new JValue(BitConverter.ToDouble(buffer, 0));
        }

        /// <summary>
        ///     Decodes the float.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private JValue DecodeFloat(byte[] buffer)
        {
            if (buffer.Length != 4)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of float.");
            Array.Reverse(buffer);
            return new JValue(BitConverter.ToSingle(buffer, 0));
        }

        /// <summary>
        ///     Decodes the string.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private JValue DecodeString(byte[] buffer)
        {
            return new JValue(Encoding.UTF8.GetString(buffer));
        }

        /// <summary>
        ///     Decodes the map.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <returns></returns>
        private JToken DecodeMap(int size, int offset, out int outOffset)
        {
            var obj = new JObject();

            for (var i = 0; i < size; i++)
            {
                var key = Decode(offset, out offset);
                var value = Decode(offset, out offset);
                obj.Add(key.Value<string>(), value);
            }

            outOffset = offset;
            return obj;
        }

        /// <summary>
        ///     Decodes the long.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private JValue DecodeLong(byte[] buffer)
        {
            var integer = buffer.Aggregate<byte, long>(0, (current, t) => (current << 8) | t);
            return new JValue(integer);
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private JValue DecodeIntegerToJValue(byte[] buffer)
        {
            return new JValue(DecodeInteger(buffer));
        }

        /// <summary>
        ///     Decodes the array.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <returns></returns>
        private JToken DecodeArray(int size, int offset, out int outOffset)
        {
            var array = new JArray();

            for (var i = 0; i < size; i++)
            {
                var r = Decode(offset, out offset);
                array.Add(r);
            }

            outOffset = offset;
            return array;
        }

        /// <summary>
        ///     Decodes the uint64.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private JValue DecodeUInt64(byte[] buffer)
        {
            var integer = buffer.Aggregate<byte, ulong>(0, (current, t) => (current << 8) | t);
            return new JValue(integer);
        }

        /// <summary>
        ///     Decodes the big integer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        private JToken DecodeBigInteger(byte[] buffer)
        {
            Array.Reverse(buffer);

            //Pad with a 0 in case we're on a byte boundary
            Array.Resize(ref buffer, buffer.Length + 1);
            buffer[buffer.Length - 1] = 0x0;

            return new JValue(new BigInteger(buffer));
        }

        /// <summary>
        ///     Decodes the pointer.
        /// </summary>
        /// <param name="ctrlByte">The control byte.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The resulting offset</param>
        /// <returns></returns>
        private int DecodePointer(int ctrlByte, int offset, out int outOffset)
        {
            var pointerSize = ((ctrlByte >> 3) & 0x3) + 1;
            var b = pointerSize == 4 ? 0 : ctrlByte & 0x7;
            var buffer = _database.Read(offset, pointerSize);
            var packed = DecodeInteger(b, buffer);
            outOffset = offset + pointerSize;
            return packed + _pointerBase + _pointerValueOffset[pointerSize];
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        internal static int DecodeInteger(byte[] buffer)
        {
            return DecodeInteger(0, buffer);
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <param name="baseValue">The base value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        internal static int DecodeInteger(int baseValue, byte[] buffer)
        {
            return buffer.Aggregate(baseValue, (current, t) => (current << 8) | t);
        }
    }
}