#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
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

    internal delegate object ObjectActivator(params object[] args);

    internal struct ClassConstructor
    {
        internal ObjectActivator Activator;
        internal Dictionary<byte[], ParameterInfo> Parameters;
        internal Type[] ParameterTypes;

        public ClassConstructor(ObjectActivator activator, Dictionary<byte[], ParameterInfo> paramsInfo) : this()
        {
            Activator = activator;
            Parameters = paramsInfo;
            ParameterTypes = paramsInfo.Values.OrderBy(x => x.Position).Select(x => x.ParameterType).ToArray();
        }
    }

    internal class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == null || y == null)
            {
                return false;
            }
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x.Length != y.Length)
            {
                return false;
            }
            for (var i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            var result = 17;
            for (var i = 0; i < bytes.Length; i++)
            {
                unchecked
                {
                    result = result * 31 + bytes[i];
                }
            }
            return result;
        }
    }

    /// <summary>
    ///     Given a stream, this class decodes the object graph at a particular location
    /// </summary>
    internal class Decoder
    {
        private readonly ConcurrentDictionary<Type, ClassConstructor> _typeConstructors =
            new ConcurrentDictionary<Type, ClassConstructor>();

        private readonly IByteReader _database;
        private readonly long _pointerBase;
        private readonly int[] _pointerValueOffset = { 0, 0, 1 << 11, (1 << 19) + ((1) << 11), 0 };

        /// <summary>
        ///     Initializes a new instance of the <see cref="Decoder" /> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pointerBase">The base address in the stream.</param>
        internal Decoder(IByteReader database, long pointerBase)
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
        internal T Decode<T>(long offset, out long outOffset) where T : class
        {
            return Decode(typeof(T), offset, out outOffset) as T;
        }

        internal object Decode(Type expectedType, long offset, out long outOffset)
        {
            int size;
            var type = CtrlData(offset, out size, out offset);
            return DecodeByType(expectedType, type, offset, size, out outOffset);
        }

        private ObjectType CtrlData(long offset, out int size, out long outOffset)
        {
            if (offset >= _database.Length)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "pointer larger than the database.");

            var ctrlByte = _database.ReadOne(offset);
            offset++;

            var type = FromControlByte(ctrlByte);

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

            size = SizeFromCtrlByte(ctrlByte, offset, out offset);
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
        /// <returns></returns>
        /// <exception cref="System.Exception">Unable to handle type!</exception>
        private object DecodeByType(Type expectedType, ObjectType type, long offset, int size, out long outOffset)
        {
            outOffset = offset + size;

            switch (type)
            {
                case ObjectType.Pointer:
                    var pointer = DecodePointer(offset, size, out offset);
                    outOffset = offset;
                    if (PointerTestHack)
                    {
                        return pointer;
                    }
                    long ignore;
                    var result = Decode(expectedType, Convert.ToInt32(pointer), out ignore);
                    return result;

                case ObjectType.Map:
                    return DecodeMap(expectedType, offset, size, out outOffset);

                case ObjectType.Array:
                    return DecodeArray(expectedType, size, offset, out outOffset);

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
        private int SizeFromCtrlByte(byte ctrlByte, long offset, out long outOffset)
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
        /// <param name="expectedType"></param>
        /// <param name="size">The size of the structure.</param>
        /// <returns></returns>
        private bool DecodeBoolean(Type expectedType, int size)
        {
            checkType(expectedType, typeof(bool));

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
            checkType(expectedType, typeof(double));

            if (size != 8)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of double.");
            var buffer = _database.Read(offset, size);
            Array.Reverse(buffer);
            return BitConverter.ToDouble(buffer, 0);
        }

        /// <summary>
        ///     Decodes the float.
        /// </summary>
        /// <returns></returns>
        private float DecodeFloat(Type expectedType, long offset, int size)
        {
            checkType(expectedType, typeof(float));

            if (size != 4)
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of float.");
            var buffer = _database.Read(offset, size);
            Array.Reverse(buffer);
            return BitConverter.ToSingle(buffer, 0);
        }

        /// <summary>
        ///     Decodes the string.
        /// </summary>
        /// <returns></returns>
        private string DecodeString(Type expectedType, long offset, int size)
        {
            checkType(expectedType, typeof(string));

            return Encoding.UTF8.GetString(_database.Read(offset, size));
        }

        private byte[] DecodeBytes(Type expectedType, long offset, int size)
        {
            checkType(expectedType, typeof(byte[]));

            return _database.Read(offset, size);
        }

        /// <summary>
        ///     Decodes the map.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="offset">The offset.</param>
        /// <param name="size">The size.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <returns></returns>
        private object DecodeMap(Type expectedType, long offset, int size, out long outOffset)
        {
            // XXX - combine with todict meth
            if (expectedType == typeof(object))
                expectedType = typeof(Dictionary<string, object>);

            // The only generic type we support is Dictionaries.
            if (expectedType.IsGenericType)
            {
                return DecodeMapToDictionary(expectedType, offset, size, out outOffset);
            }

            return DecodeMapToType(expectedType, offset, size, out outOffset);
        }

        private object DecodeMapToDictionary(Type expectedType, long offset, int size, out long outOffset)
        {
            // XXX - cache this
            var genericArgs = expectedType.GetGenericArguments();
            if (genericArgs.Length != 2)
                throw new DeserializationException(
                    $"Unexpected number of Dictionary generic arguments: {genericArgs.Length}");

            var obj = (IDictionary)DictionaryConstructor(expectedType)(size);

            for (var i = 0; i < size; i++)
            {
                var key = Decode(genericArgs[0], offset, out offset);
                var value = Decode(genericArgs[1], offset, out offset);
                obj.Add(key, value);
            }

            outOffset = offset;

            return obj;
        }

        private readonly ConcurrentDictionary<Type, ObjectActivator> _constructorCache =
            new ConcurrentDictionary<Type, ObjectActivator>();

        internal ObjectActivator DictionaryConstructor(Type expectedType)
        {
            if (_constructorCache.ContainsKey(expectedType))
                return _constructorCache[expectedType];

            var genericArgs = expectedType.GetGenericArguments();
            if (genericArgs.Length != 2)
                throw new DeserializationException(
                    $"Unexpected number of Dictionary generic arguments: {genericArgs.Length}");
            ConstructorInfo constructor;
            if (expectedType.IsInterface)
            {
                var dictType = typeof(Dictionary<,>).MakeGenericType(genericArgs);
                checkType(expectedType, dictType);
                constructor = dictType.GetConstructor(new[] { typeof(int) });
            }
            else
            {
                checkType(typeof(IDictionary), expectedType);
                constructor = expectedType.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                    throw new DeserializationException($"Unable to find default constructor for {expectedType}");
            }
            var activator = CreateActivator(constructor);
            _constructorCache.TryAdd(expectedType, activator);
            return activator;
        }

        // Activator.CreateInstance is extremely slow and ConstuctorInfo.Invoke is
        // somewhat slow. This faster alternative (when cached) is largely based off
        // of:
        // http://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
        internal ObjectActivator CreateActivator(ConstructorInfo constructor)
        {
            var paramInfo = constructor.GetParameters();

            var paramExp = Expression.Parameter(typeof(object[]), "args");

            var argsExp = new Expression[paramInfo.Length];
            for (var i = 0; i < paramInfo.Length; i++)
            {
                var index = Expression.Constant(i);
                var paramType = paramInfo[i].ParameterType;
                var accessorExp = Expression.ArrayIndex(paramExp, index);
                var castExp = Expression.Convert(accessorExp, paramType);
                argsExp[i] = castExp;
            }

            var newExp = Expression.New(constructor, argsExp);
            var lambda = Expression.Lambda(typeof(ObjectActivator), newExp, paramExp);
            return (ObjectActivator)lambda.Compile();
        }

        private object DecodeMapToType(Type expectedType, long offset, int size, out long outOffset)
        {
            var constructor = DeserializationConstructor(expectedType);
            var parameters = new object[constructor.Parameters.Count];

            for (var i = 0; i < size; i++)
            {
                var key = DecodeKey(offset, out offset);
                if (constructor.Parameters.ContainsKey(key))
                {
                    var param = constructor.Parameters[key];
                    var paramType = param.ParameterType;
                    var value = Decode(paramType, offset, out offset);
                    parameters[param.Position] = value;
                }
                else
                {
                    offset = NextValueOffset(offset, 1);
                }
            }
            outOffset = offset;
            return constructor.Activator(parameters);
        }

        private ClassConstructor DeserializationConstructor(Type expectedType)
        {
            if (_typeConstructors.ContainsKey(expectedType))
            {
                return _typeConstructors[expectedType];
            }
            var constructors =
                expectedType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(c => c.IsDefined(typeof(MaxMindDbConstructorAttribute), true))
                    .ToList();
            if (constructors.Count == 0)
            {
                throw new DeserializationException(
                    $"No constructors found for {expectedType} found with MaxMindDbConstructor attribute");
            }
            if (constructors.Count > 1)
            {
                throw new DeserializationException(
                    $"More than one constructor found for {expectedType} found with MaxMindDbConstructor attribute");
            }

            var constructor = constructors[0];
            var paramNameTypes = constructor.GetParameters()
                .ToDictionary(MapPropertyName, x => x, new ByteArrayEqualityComparer());

            var activator = CreateActivator(constructor);
            var clsConstructor = new ClassConstructor(activator, paramNameTypes);
            _typeConstructors.TryAdd(expectedType, clsConstructor);
            return clsConstructor;
        }

        private byte[] MapPropertyName(ParameterInfo paramInfo)
        {
            var attribute = paramInfo.GetCustomAttributes<MaxMindDbPropertyAttribute>().FirstOrDefault();
            var s = attribute == null ? paramInfo.Name : attribute.PropertyName;
            return Encoding.UTF8.GetBytes(s);
        }

        private byte[] DecodeKey(long offset, out long outOffset)
        {
            int size;
            var type = CtrlData(offset, out size, out offset);
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
            if (numberToSkip == 0)
            {
                return offset;
            }
            int size;
            var type = CtrlData(offset, out size, out offset);
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
            return NextValueOffset(offset, numberToSkip - 1);
        }

        /// <summary>
        ///     Decodes the long.
        /// </summary>
        /// <returns></returns>
        private long DecodeLong(Type expectedType, long offset, int size)
        {
            checkType(expectedType, typeof(long));

            long val = 0;
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | _database.ReadOne(offset + i);
            }
            return val;
        }

        /// <summary>
        ///     Decodes the array.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <returns></returns>
        private object DecodeArray(Type expectedType, int size, long offset, out long outOffset)
        {
            var genericArgs = expectedType.GetGenericArguments();
            var argType = genericArgs.Length == 0 ? typeof(object) : genericArgs[0];

            object array;
            var interfaceType = typeof(ICollection<>).MakeGenericType(argType);
            var listType = typeof(List<>).MakeGenericType(argType);
            if (expectedType.IsAssignableFrom(listType))
            {
                array = Activator.CreateInstance(listType, size);
            }
            else
            {
                checkType(interfaceType, expectedType);
                array = Activator.CreateInstance(expectedType);
            }
            for (var i = 0; i < size; i++)
            {
                var r = Decode(argType, offset, out offset);
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
            checkType(expectedType, typeof(ulong));

            ulong val = 0;
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | _database.ReadOne(offset + i);
            }
            return val;
        }

        /// <summary>
        ///     Decodes the big integer.
        /// </summary>
        /// <returns></returns>
        private BigInteger DecodeBigInteger(Type expectedType, long offset, int size)
        {
            checkType(expectedType, typeof(BigInteger));

            var buffer = _database.Read(offset, size);
            Array.Reverse(buffer);

            //Pad with a 0 in case we're on a byte boundary
            Array.Resize(ref buffer, buffer.Length + 1);
            buffer[buffer.Length - 1] = 0x0;

            return new BigInteger(buffer);
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
            var packed = DecodeInteger(b, offset, pointerSize);
            outOffset = offset + pointerSize;
            return packed + _pointerBase + _pointerValueOffset[pointerSize];
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        private int DecodeInteger(Type expectedType, long offset, int size)
        {
            checkType(expectedType, typeof(int));

            return DecodeInteger(0, offset, size);
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        private int DecodeInteger(long offset, int size)
        {
            return DecodeInteger(0, offset, size);
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        internal int DecodeInteger(int val, long offset, int size)
        {
            for (var i = 0; i < size; i++)
            {
                val = (val << 8) | _database.ReadOne(offset + i);
            }
            return val;
        }

        private void checkType(Type expected, Type from)
        {
            if (!expected.IsAssignableFrom(from))
            {
                throw new DeserializationException($"Could not convert '{from}' to '{expected}'.");
            }
        }
    }
}