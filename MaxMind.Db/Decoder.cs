#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;

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
    internal sealed class Decoder
    {
        private readonly Buffer _database;
        private readonly long _pointerBase;
        private readonly bool _followPointers;
        private readonly int[] _pointerValueOffset = { 0, 0, 1 << 11, (1 << 19) + (1 << 11), 0 };

        private readonly DictionaryActivatorCreator _dictionaryActivatorCreator;
        private readonly ListActivatorCreator _listActivatorCreator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Decoder" /> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pointerBase">The base address in the stream.</param>
        /// <param name="followPointers">Whether to follow pointers. For testing.</param>
        internal Decoder(Buffer database, long pointerBase, bool followPointers = true)
        {
            _pointerBase = pointerBase;
            _database = database;
            _followPointers = followPointers;
            _listActivatorCreator = new ListActivatorCreator();
            _dictionaryActivatorCreator = new DictionaryActivatorCreator();
            _typeAcivatorCreator = new TypeAcivatorCreator();
        }

        /// <summary>
        ///     Decodes the object at the specified offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset</param>
        /// <param name="injectables"></param>
        /// <param name="network"></param>
        /// <returns>An object containing the data read from the stream</returns>
        internal T Decode<T>(long offset, out long outOffset, InjectableValues? injectables = null, Network? network = default) where T : class
        {
            var decoded = Decode(typeof(T), offset, out outOffset, injectables, network) as T;
            if (decoded == null)
            {
                throw new InvalidDatabaseException("The value cannot be decoded as " + typeof(T));
            }
            return decoded;
        }

        private object Decode(Type expectedType, long offset, out long outOffset, InjectableValues? injectables = null, Network? network = null)
        {
            var type = CtrlData(offset, out var size, out offset);
            return DecodeByType(expectedType, type, offset, size, out outOffset, injectables, network);
        }

        private ObjectType CtrlData(long offset, out int size, out long outOffset)
        {
            if (offset >= _database.Length)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "pointer larger than the database.");

            var ctrlByte = _database.ReadOne(offset);
            offset++;

            var type = (ObjectType)(ctrlByte >> 5);

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

            // The size calculation is inlined as it is hot code
            size = ctrlByte & 0x1f;
            if (size >= 29)
            {
                var bytesToRead = size - 28;
                var i = _database.ReadInteger(0, offset, bytesToRead);
                offset = offset + bytesToRead;
                switch (size)
                {
                    case 29:
                        size = 29 + i;
                        break;

                    case 30:
                        size = 285 + i;
                        break;

                    default:
                        size = 65821 + (i & (0x0FFFFFFF >> (32 - 8 * bytesToRead)));
                        break;
                }
            }
            outOffset = offset;
            return type;
        }

        /// <summary>
        ///     Decodes the type of the by.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="type">The type.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="size">The size.</param>
        /// <param name="outOffset">The out offset</param>
        /// <param name="injectables"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Unable to handle type!</exception>
        private object DecodeByType(
            Type expectedType,
            ObjectType type,
            long offset,
            int size,
            out long outOffset,
            InjectableValues? injectables,
            Network? network
            )
        {
            outOffset = offset + size;

            switch (type)
            {
                case ObjectType.Pointer:
                    var pointer = DecodePointer(offset, size, out offset);
                    outOffset = offset;
                    if (!_followPointers)
                    {
                        return pointer;
                    }

                    var result = Decode(expectedType, Convert.ToInt32(pointer), out _, injectables, network);
                    return result;

                case ObjectType.Map:
                    return DecodeMap(expectedType, offset, size, out outOffset, injectables, network);

                case ObjectType.Array:
                    return DecodeArray(expectedType, size, offset, out outOffset, injectables, network);

                case ObjectType.Boolean:
                    outOffset = offset;
                    return DecodeBoolean(expectedType, size);

                case ObjectType.Utf8String:
                    return DecodeString(expectedType, offset, size);

                case ObjectType.Double:
                    return DecodeDouble(expectedType, offset, size);

                case ObjectType.Float:
                    return DecodeFloat(expectedType, offset, size);

                case ObjectType.Bytes:
                    return DecodeBytes(expectedType, offset, size);

                case ObjectType.Uint16:
                    return DecodeInteger(expectedType, offset, size);

                case ObjectType.Uint32:
                    return DecodeLong(expectedType, offset, size);

                case ObjectType.Int32:
                    return DecodeInteger(expectedType, offset, size);

                case ObjectType.Uint64:
                    return DecodeUInt64(expectedType, offset, size);

                case ObjectType.Uint128:
                    return DecodeBigInteger(expectedType, offset, size);

                default:
                    throw new InvalidDatabaseException("Unable to handle type:" + type);
            }
        }

        /// <summary>
        ///     Decodes the boolean.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="size">The size of the structure.</param>
        /// <returns></returns>
        private static bool DecodeBoolean(Type expectedType, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(bool));

            switch (size)
            {
                case 0:
                    return false;

                case 1:
                    return true;

                default:
                    throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                       + "invalid size of boolean.");
            }
        }

        /// <summary>
        ///     Decodes the double.
        /// </summary>
        /// <returns></returns>
        private double DecodeDouble(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(double));

            if (size != 8)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of double.");
            return _database.ReadDouble(offset);
        }

        /// <summary>
        ///     Decodes the float.
        /// </summary>
        /// <returns></returns>
        private float DecodeFloat(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(float));

            if (size != 4)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of float.");
            return _database.ReadFloat(offset);
        }

        /// <summary>
        ///     Decodes the string.
        /// </summary>
        /// <returns></returns>
        private string DecodeString(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(string));

            return _database.ReadString(offset, size);
        }

        private byte[] DecodeBytes(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(byte[]));

            return _database.Read(offset, size);
        }

        /// <summary>
        ///     Decodes the map.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="offset">The offset.</param>
        /// <param name="size">The size.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <param name="injectables"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        private object DecodeMap(
            Type expectedType,
            long offset,
            int size,
            out long outOffset,
            InjectableValues? injectables,
            Network? network
            )
        {
            var objDictType = typeof(Dictionary<string, object>);
            if (!expectedType.GetTypeInfo().IsGenericType && expectedType.IsAssignableFrom(objDictType))
                expectedType = objDictType;

            // Currently we don't support non-dict generic types
            if (expectedType.GetTypeInfo().IsGenericType)
            {
                return DecodeMapToDictionary(expectedType, offset, size, out outOffset, injectables, network);
            }

            return DecodeMapToType(expectedType, offset, size, out outOffset, injectables, network);
        }

        private object DecodeMapToDictionary(Type expectedType, long offset, int size, out long outOffset,
            InjectableValues? injectables, Network? network)
        {
            var genericArgs = expectedType.GetGenericArguments();
            if (genericArgs.Length != 2)
                throw new DeserializationException(
                    $"Unexpected number of Dictionary generic arguments: {genericArgs.Length}");

            var obj = (IDictionary)_dictionaryActivatorCreator.GetActivator(expectedType)(size);

            for (var i = 0; i < size; i++)
            {
                var key = Decode(genericArgs[0], offset, out offset);
                var value = Decode(genericArgs[1], offset, out offset, injectables, network);
                obj.Add(key, value);
            }

            outOffset = offset;

            return obj;
        }

        private object DecodeMapToType(
            Type expectedType,
            long offset,
            int size,
            out long outOffset,
            InjectableValues? injectables,
            Network? network
            )
        {
            var constructor = _typeAcivatorCreator.GetActivator(expectedType);
            var parameters = constructor.DefaultParameters();

            for (var i = 0; i < size; i++)
            {
                var key = DecodeKey(offset, out offset);
                if (constructor.DeserializationParameters.ContainsKey(key))
                {
                    var param = constructor.DeserializationParameters[key];
                    var paramType = param.ParameterType;
                    var value = Decode(paramType, offset, out offset, injectables, network);
                    parameters[param.Position] = value;
                }
                else
                {
                    offset = NextValueOffset(offset, 1);
                }
            }

            SetInjectables(constructor, parameters, injectables);
            SetNetwork(constructor, parameters, network);
            SetAlwaysCreatedParams(constructor, parameters, injectables, network);

            outOffset = offset;
            return constructor.Activator(parameters);
        }

        private void SetAlwaysCreatedParams(
            TypeActivator constructor,
            object[] parameters,
            InjectableValues? injectables,
            Network? network
            )
        {
            foreach (var param in constructor.AlwaysCreatedParameters)
            {
                if (parameters[param.Position] != null) continue;

                var activator = _typeAcivatorCreator.GetActivator(param.ParameterType);
                var cstorParams = activator.DefaultParameters();
                SetInjectables(activator, cstorParams, injectables);
                SetNetwork(activator, cstorParams, network);
                SetAlwaysCreatedParams(activator, cstorParams, injectables, network);
                parameters[param.Position] = activator.Activator(cstorParams);
            }
        }

        private static void SetInjectables(TypeActivator constructor, object[] parameters, InjectableValues? injectables)
        {
            foreach (var item in constructor.InjectableParameters)
            {
                if (injectables == null || !injectables.Values.ContainsKey(item.Key))
                    throw new DeserializationException($"No injectable value found for {item.Key}");

                parameters[item.Value.Position] = injectables.Values[item.Key];
            }
        }

        private static void SetNetwork(TypeActivator constructor, object?[] parameters, Network? network)
        {
            foreach (var item in constructor.NetworkParameters)
            {
                // We don't check that we have a non-null network as we want to
                // allow enumeration to use the same models as normal lookups. We
                // cannot support the network field for enumeration as the objects
                // are cached.
                parameters[item.Position] = network;
            }
        }

        private readonly TypeAcivatorCreator _typeAcivatorCreator;

        private byte[] DecodeKey(long offset, out long outOffset)
        {
            var type = CtrlData(offset, out var size, out offset);
            switch (type)
            {
                case ObjectType.Pointer:
                    offset = DecodePointer(offset, size, out outOffset);
                    return DecodeKey(offset, out offset);

                case ObjectType.Utf8String:
                    outOffset = offset + size;
                    return _database.Read(offset, size);

                default:
                    throw new InvalidDatabaseException($"Database contains a non-string as map key: {type}");
            }
        }

        private long NextValueOffset(long offset, int numberToSkip)
        {
            while (true)
            {
                if (numberToSkip == 0)
                {
                    return offset;
                }

                var type = CtrlData(offset, out var size, out offset);
                switch (type)
                {
                    case ObjectType.Pointer:
                        DecodePointer(offset, size, out offset);
                        break;

                    case ObjectType.Map:
                        numberToSkip += 2 * size;
                        break;

                    case ObjectType.Array:
                        numberToSkip += size;
                        break;

                    case ObjectType.Boolean:
                        break;

                    default:
                        offset += size;
                        break;
                }

                numberToSkip = numberToSkip - 1;
            }
        }

        /// <summary>
        ///     Decodes the long.
        /// </summary>
        /// <returns></returns>
        private long DecodeLong(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(long));
            return _database.ReadLong(offset, size);
        }

        /// <summary>
        ///     Decodes the array.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <param name="injectables"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        private object DecodeArray(Type expectedType, int size, long offset, out long outOffset,
            InjectableValues? injectables, Network? network)
        {
            var genericArgs = expectedType.GetGenericArguments();
            var argType = genericArgs.Length == 0 ? typeof(object) : genericArgs[0];
            var interfaceType = typeof(ICollection<>).MakeGenericType(argType);

            var array = _listActivatorCreator.GetActivator(expectedType)(size);
            for (var i = 0; i < size; i++)
            {
                var r = Decode(argType, offset, out offset, injectables, network);
                interfaceType.GetMethod("Add").Invoke(array, new[] { r });
            }

            outOffset = offset;
            return array;
        }

        /// <summary>
        ///     Decodes the uint64.
        /// </summary>
        /// <returns></returns>
        private ulong DecodeUInt64(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(ulong));
            return _database.ReadULong(offset, size);
        }

        /// <summary>
        ///     Decodes the big integer.
        /// </summary>
        /// <returns></returns>
        private BigInteger DecodeBigInteger(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(BigInteger));
            return _database.ReadBigInteger(offset, size);
        }

        /// <summary>
        ///     Decodes the pointer.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="size"></param>
        /// <param name="outOffset">The resulting offset</param>
        /// <returns></returns>
        private long DecodePointer(long offset, int size, out long outOffset)
        {
            var pointerSize = ((size >> 3) & 0x3) + 1;
            var b = pointerSize == 4 ? 0 : size & 0x7;
            var packed = _database.ReadInteger(b, offset, pointerSize);
            outOffset = offset + pointerSize;
            return packed + _pointerBase + _pointerValueOffset[pointerSize];
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        private int DecodeInteger(Type expectedType, long offset, int size)
        {
            ReflectionUtil.CheckType(expectedType, typeof(int));

            return _database.ReadInteger(0, offset, size);
        }
    }
}