#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        internal T Decode<T>(int offset, out int outOffset) where T : class
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
                    return pointer as T;
                }
                int ignore;
                var result = Decode<T>(Convert.ToInt32(pointer), out ignore);
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

            return DecodeByType<T>(type, offset, size, out outOffset);
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
        private T DecodeByType<T>(ObjectType type, int offset, int size, out int outOffset) where T : class
        {
            outOffset = offset + size;

            switch (type)
            {
                case ObjectType.Map:
                    return DecodeMap<T>(size, offset, out outOffset);

                case ObjectType.Array:
                    return DecodeArray<T>(size, offset, out outOffset);

                case ObjectType.Boolean:
                    outOffset = offset;
                    return DecodeBoolean<T>(size);

                case ObjectType.Utf8String:
                    return DecodeString<T>(offset, size);

                case ObjectType.Double:
                    return DecodeDouble<T>(offset, size);

                case ObjectType.Float:
                    return DecodeFloat<T>(offset, size);

                case ObjectType.Bytes:
                    return DecodeBytes<T>(offset, size);

                case ObjectType.Uint16:
                    return DecodeIntegerAsT<T>(offset, size);

                case ObjectType.Uint32:
                    return DecodeLong<T>(offset, size);

                case ObjectType.Int32:
                    return DecodeIntegerAsT<T>(offset, size);

                case ObjectType.Uint64:
                    return DecodeUInt64<T>(offset, size);

                case ObjectType.Uint128:
                    return DecodeBigInteger<T>(offset, size);

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
                var i = DecodeInteger(offset, bytesToRead);
                size = 29 + i;
            }
            else if (size == 30)
            {
                var i = DecodeInteger(offset, bytesToRead);
                size = 285 + i;
            }
            else if (size > 30)
            {
                var i = DecodeInteger(offset, bytesToRead) & (0x0FFFFFFF >> (32 - (8 * bytesToRead)));
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
        private T DecodeBoolean<T>(int size) where T : class
        {
            checkType<T>(typeof(bool));

            switch (size)
            {
                case 0:
                    return false as T;

                case 1:
                    return true as T;

                default:
                    throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                       + "invalid size of boolean.");
            }
        }

        /// <summary>
        ///     Decodes the double.
        /// </summary>
        /// <returns></returns>
        private T DecodeDouble<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(double));

            if (size != 8)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of double.");
            var buffer = _database.Read(offset, size);
            Array.Reverse(buffer);
            return BitConverter.ToDouble(buffer, 0) as T;
        }

        /// <summary>
        ///     Decodes the float.
        /// </summary>
        /// <returns></returns>
        private T DecodeFloat<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(float));

            if (size != 4)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of float.");
            var buffer = _database.Read(offset, size);
            Array.Reverse(buffer);
            return BitConverter.ToSingle(buffer, 0) as T;
        }

        /// <summary>
        ///     Decodes the string.
        /// </summary>
        /// <returns></returns>
        private T DecodeString<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(string));

            return Encoding.UTF8.GetString(_database.Read(offset, size)) as T;
        }

        private T DecodeBytes<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(byte[]));

            return _database.Read(offset, size) as T;
        }

        /// <summary>
        ///     Decodes the map.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <returns></returns>
        private T DecodeMap<T>(int size, int offset, out int outOffset) where T : class
        {
            checkType<T>(typeof(ReadOnlyDictionary<string, object>));

            var obj = new Dictionary<string, object>(size);

            for (var i = 0; i < size; i++)
            {
                var key = Decode<string>(offset, out offset);
                var value = Decode<object>(offset, out offset);
                obj.Add(key, value);
            }

            outOffset = offset;
            return new ReadOnlyDictionary<string, object>(obj) as T;
        }

        /// <summary>
        ///     Decodes the long.
        /// </summary>
        /// <returns></returns>
        private T DecodeLong<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(long));

            long val = 0;
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | _database.ReadOne(offset + i);
            }
            return val as T;
        }

        /// <summary>
        ///     Decodes the array.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <returns></returns>
        private T DecodeArray<T>(int size, int offset, out int outOffset) where T : class
        {
            checkType<T>(typeof(ReadOnlyCollection<object>));

            var array = new List<object>(size);

            for (var i = 0; i < size; i++)
            {
                var r = Decode<object>(offset, out offset);
                array.Add(r);
            }

            outOffset = offset;
            return array.AsReadOnly() as T;
        }

        /// <summary>
        ///     Decodes the uint64.
        /// </summary>
        /// <returns></returns>
        private T DecodeUInt64<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(ulong));

            ulong val = 0;
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | _database.ReadOne(offset + i);
            }
            return val as T;
        }

        /// <summary>
        ///     Decodes the big integer.
        /// </summary>
        /// <returns></returns>
        private T DecodeBigInteger<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(BigInteger));

            var buffer = _database.Read(offset, size);
            Array.Reverse(buffer);

            //Pad with a 0 in case we're on a byte boundary
            Array.Resize(ref buffer, buffer.Length + 1);
            buffer[buffer.Length - 1] = 0x0;

            return new BigInteger(buffer) as T;
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
            var packed = DecodeInteger(b, offset, pointerSize);
            outOffset = offset + pointerSize;
            return packed + _pointerBase + _pointerValueOffset[pointerSize];
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        private T DecodeIntegerAsT<T>(int offset, int size) where T : class
        {
            checkType<T>(typeof(int));

            return DecodeInteger(0, offset, size) as T;
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        private int DecodeInteger(int offset, int size)
        {
            return DecodeInteger(0, offset, size);
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        private int DecodeInteger(int val, int offset, int size)
        {
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | _database.ReadOne(offset + i);
            }
            return val;
        }

        private void checkType<T>(Type from)
        {
            if (!typeof(T).IsAssignableFrom(from))
            {
                throw new DeserializationException($"Could not convert '{from}' to '{typeof(T)}'.");
            }
        }
    }
}