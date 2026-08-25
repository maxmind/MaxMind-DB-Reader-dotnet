#region

using System;
#if !NETSTANDARD2_0
using System.Buffers;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

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
        private readonly MemoryMapBuffer _database;
        private readonly long _pointerBase;
        private readonly bool _followPointers;
        private readonly int[] _pointerValueOffset = [0, 0, 1 << 11, (1 << 19) + (1 << 11), 0];

        // Per-lookup decode limits recommended by the MaxMind DB specification.
        // The depth limit stops pointer cycles and over-deep data before the
        // stack overflows (a StackOverflowException cannot be caught in .NET).
        // The value limit stops a pointer fan-out, where nested pointers to
        // shared targets would otherwise cost 2**depth decode operations. The
        // running depth and value budget are passed through the decode call, so
        // a single Decoder stays safe for concurrent lookups with no shared
        // mutable state. The largest real records decode a few hundred values.
        private const int MaxDepth = 512;
        private const int MaxDecodedValues = 1 << 16;
        // Each data-structure level uses several managed frames. Some runtimes
        // have less stack space than others, so the format-level limit alone
        // cannot guarantee that there is enough CLR stack to reach it. Real
        // records are much shallower; delay the runtime probe until this point
        // to keep it completely off their decode path.
        private const int RuntimeStackCheckDepth = 32;

        private static bool HasSufficientExecutionStack()
        {
#if NETSTANDARD2_0
            // netstandard2.0 exposes only the throwing form of the runtime
            // stack probe. Keep the exception path limited to malformed data.
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
                return true;
            }
            catch (InsufficientExecutionStackException)
            {
                return false;
            }
#else
            return RuntimeHelpers.TryEnsureSufficientExecutionStack();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckDepth(int depth)
        {
            // Preserve the original single-comparison fast path. The runtime
            // probe and the second comparison are reached only by structures
            // far deeper than real database records.
            if (depth < RuntimeStackCheckDepth)
            {
                return;
            }

            if (depth > MaxDepth || !HasSufficientExecutionStack())
            {
                throw new InvalidDatabaseException(
                    "The MaxMind DB file's data section exceeds the maximum depth.");
            }
        }

        // Applied once per container: bounds nesting depth and charges the
        // container's declared size against the value budget before its
        // elements are read, so an oversized declared size is rejected up front
        // and a re-decoded (fanned-out) container drains the budget.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckContainer(int depth, int valueCount, ref int budget)
        {
            CheckDepth(depth);
            budget -= valueCount;
            if (budget < 0)
            {
                throw new InvalidDatabaseException(
                    "The MaxMind DB file's data section exceeds the maximum number of values.");
            }
        }

        // A pointer field is charged by its enclosing container. Its target is
        // another decoded value and must be charged each time it is followed.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConsumePointerTarget(ref int budget)
        {
            budget--;
            if (budget < 0)
            {
                throw new InvalidDatabaseException(
                    "The MaxMind DB file's data section exceeds the maximum number of values.");
            }
        }

        private readonly DictionaryActivatorCreator _dictionaryActivatorCreator;
        private readonly ListActivatorCreator _listActivatorCreator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Decoder" /> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="pointerBase">The base address in the stream.</param>
        /// <param name="followPointers">Whether to follow pointers. For testing.</param>
        internal Decoder(MemoryMapBuffer database, long pointerBase, bool followPointers = true)
        {
            _pointerBase = pointerBase;
            _database = database;
            _followPointers = followPointers;
            _listActivatorCreator = new ListActivatorCreator();
            _dictionaryActivatorCreator = new DictionaryActivatorCreator();
            _typeActivatorCreator = new TypeActivatorCreator();
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
            // The per-lookup decode limits are threaded as parameters (a value
            // depth and a shared remaining-value budget) rather than stored on
            // the shared Decoder, so they add no thread-local access and keep the
            // decoder safe for concurrent reads.
            var budget = MaxDecodedValues;
            return DecodeNested<T>(offset, out outOffset, 0, ref budget, injectables, network);
        }

        private T DecodeNested<T>(long offset, out long outOffset, int depth, ref int budget, InjectableValues? injectables, Network? network) where T : class
        {
            if (Decode(typeof(T), offset, out outOffset, depth, ref budget, injectables, network) is not T decoded)
            {
                throw new InvalidDatabaseException("The value cannot be decoded as " + typeof(T));
            }
            return decoded;
        }

        private object Decode(Type expectedType, long offset, out long outOffset, int depth, ref int budget, InjectableValues? injectables = null, Network? network = null)
        {
            // Scalars decode with no guard overhead; the depth and value limits
            // are applied only where the structure nests (containers and
            // pointers), which keeps the per-value path free.
            var type = CtrlData(offset, out var size, out offset);
            return DecodeByType(expectedType, type, offset, size, out outOffset, depth, ref budget, injectables, network);
        }

        private ObjectType CtrlData(long offset, out int size, out long outOffset)
        {
            if (offset >= _database.Length)
            {
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "pointer larger than the database.");
            }

            var ctrlByte = _database.ReadOne(offset);
            offset++;

            var type = (ObjectType)(ctrlByte >> 5);

            if (type == ObjectType.Extended)
            {
                int nextByte = _database.ReadOne(offset);
                var typeNum = nextByte + 7;
                if (typeNum < 8)
                {
                    throw new InvalidDatabaseException(
                        "Something went horribly wrong in the decoder. An extended type "
                        + "resolved to a type number < 8 (" + typeNum
                        + ")");
                }

                type = (ObjectType)typeNum;
                offset++;
            }

            // The size calculation is inlined as it is hot code
            size = ctrlByte & 0x1f;
            if (size >= 29)
            {
                var bytesToRead = size - 28;
                size = size switch
                {
                    29 => 29 + _database.ReadOne(offset),
                    30 => 285 + _database.ReadVarInt(offset, bytesToRead),
                    _ => 65821 + _database.ReadVarInt(offset, bytesToRead),
                };
                offset += bytesToRead;
            }
            outOffset = offset;
            return type;
        }

        /// <summary>
        ///     Decodes the value by type.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="type">The type.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="size">The size.</param>
        /// <param name="outOffset">The out offset</param>
        /// <param name="depth">The current nesting depth.</param>
        /// <param name="budget">The remaining number of values that may be decoded.</param>
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
            int depth,
            ref int budget,
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

                    CheckDepth(depth);
                    ConsumePointerTarget(ref budget);
                    return Decode(expectedType, pointer, out _, depth + 1, ref budget, injectables, network);

                case ObjectType.Map:
                    // A map entry decodes a key and a value, so it costs two values.
                    CheckContainer(depth, size * 2, ref budget);
                    return DecodeMap(expectedType, offset, size, out outOffset, depth, ref budget, injectables, network);

                case ObjectType.Array:
                    CheckContainer(depth, size, ref budget);
                    return DecodeArray(expectedType, size, offset, out outOffset, depth, ref budget, injectables, network);

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
                    throw new InvalidDatabaseException("Unable to handle type: " + type);
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
            if (expectedType != typeof(bool) && expectedType != typeof(bool?))
            {
                ReflectionUtil.CheckType(expectedType, typeof(bool));
            }

            return size switch
            {
                0 => false,
                1 => true,
                _ => throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                     + "invalid size of boolean."),
            };
        }

        /// <summary>
        ///     Decodes the double.
        /// </summary>
        /// <returns></returns>
        private double DecodeDouble(Type expectedType, long offset, int size)
        {
            if (expectedType != typeof(double) && expectedType != typeof(double?))
            {
                ReflectionUtil.CheckType(expectedType, typeof(double));
            }

            if (size != 8)
            {
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of double.");
            }

            return _database.ReadDouble(offset);
        }

        /// <summary>
        ///     Decodes the float.
        /// </summary>
        /// <returns></returns>
        private float DecodeFloat(Type expectedType, long offset, int size)
        {
            if (expectedType != typeof(float) && expectedType != typeof(float?))
            {
                ReflectionUtil.CheckType(expectedType, typeof(float));
            }

            if (size != 4)
            {
                throw new InvalidDatabaseException("The MaxMind DB file's data section contains bad data: "
                                                   + "invalid size of float.");
            }

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
        /// <param name="depth">The current nesting depth.</param>
        /// <param name="budget">The remaining number of values that may be decoded.</param>
        /// <param name="injectables"></param>
        /// <param name="network"></param>
        /// <returns></returns>
        private object DecodeMap(
            Type expectedType,
            long offset,
            int size,
            out long outOffset,
            int depth,
            ref int budget,
            InjectableValues? injectables,
            Network? network
            )
        {
            var objDictType = typeof(Dictionary<string, object>);
            if (!expectedType.IsGenericType && expectedType.IsAssignableFrom(objDictType))
                expectedType = objDictType;

            // Currently we don't support non-dict generic types. A non-generic type only
            // decodes as a dictionary if one was registered for it, and the flag keeps
            // that lookup off the path every model map takes: probing unconditionally
            // measured ~2% slower on a City lookup.
            if (expectedType.IsGenericType ||
                (SourceGeneratorSupport.HasNonGenericDictionaryRegistration &&
                 SourceGeneratorSupport.TryGetDictionaryRegistration(expectedType, out _)))
            {
                return DecodeMapToDictionary(expectedType, offset, size, out outOffset, depth, ref budget, injectables, network);
            }

            return DecodeMapToType(expectedType, offset, size, out outOffset, depth, ref budget, injectables, network);
        }

        private object DecodeMapToDictionary(Type expectedType, long offset, int size, out long outOffset,
            int depth, ref int budget, InjectableValues? injectables, Network? network)
        {
            // Fast path for Dictionary<string, string> (and parents).
            if (expectedType.IsAssignableFrom(typeof(Dictionary<string, string>)))
            {
                Dictionary<string, string> dic = new(size);
                for (var i = 0; i < size; i++)
                {
                    var key = DecodeNested<string>(offset, out offset, depth + 1, ref budget, null, null);
                    var value = DecodeNested<string>(offset, out offset, depth + 1, ref budget, injectables, network);
                    dic.Add(key, value);
                }

                outOffset = offset;
                return dic;
            }

            // Fast path for Dictionary<string, object> (and parents).
            if (expectedType.IsAssignableFrom(typeof(Dictionary<string, object>)))
            {
                Dictionary<string, object> dic = new(size);
                for (var i = 0; i < size; i++)
                {
                    var key = DecodeNested<string>(offset, out offset, depth + 1, ref budget, null, null);
                    var value = DecodeNested<object>(offset, out offset, depth + 1, ref budget, injectables, network);
                    dic.Add(key, value);
                }

                outOffset = offset;
                return dic;
            }

            if (SourceGeneratorSupport.TryGetDictionaryRegistration(
                    expectedType, out var registration))
            {
                var generatedDictionary = registration.Factory(size);
                for (var i = 0; i < size; i++)
                {
                    var key = Decode(registration.KeyType, offset, out offset, depth + 1, ref budget);
                    var value = Decode(
                        registration.ValueType, offset, out offset, depth + 1, ref budget, injectables, network);
                    registration.Add(generatedDictionary, key, value);
                }

                outOffset = offset;
                return generatedDictionary;
            }

            var genericArgs = expectedType.GetGenericArguments();
            if (genericArgs.Length != 2)
            {
                throw new DeserializationException(
                    $"Unexpected number of Dictionary generic arguments: {genericArgs.Length}");
            }

            var obj = (IDictionary)_dictionaryActivatorCreator.GetActivator(expectedType)(size);
            for (var i = 0; i < size; i++)
            {
                var key = Decode(genericArgs[0], offset, out offset, depth + 1, ref budget);
                var value = Decode(genericArgs[1], offset, out offset, depth + 1, ref budget, injectables, network);
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
            int depth,
            ref int budget,
            InjectableValues? injectables,
            Network? network
            )
        {
            var constructor = _typeActivatorCreator.GetActivator(expectedType);

#if !NETSTANDARD2_0
            // N.B. Rent can return larger arrays. This is fine because both constructor invocations and
            // MemberInit activators only access elements up to their parameter/property count.
            object?[] parameters = ArrayPool<object?>.Shared.Rent(constructor.DefaultParameters.Length);
            try
            {
#else
            object?[] parameters = new object?[constructor.DefaultParameters.Length];
#endif
            constructor.DefaultParameters.CopyTo(parameters, 0);

            for (var i = 0; i < size; i++)
            {
                var key = DecodeKey(offset, out offset, depth + 1, ref budget);
                if (constructor.DeserializationParameters.TryGetValue(key, out var v))
                {
                    var param = v;
                    var paramType = param.MemberType;
                    var value = Decode(paramType, offset, out offset, depth + 1, ref budget, injectables, network);
                    parameters[param.Position] = value;
                }
                else
                {
                    offset = NextValueOffset(offset, 1, depth + 1, ref budget);
                }
            }

            SetInjectables(constructor, parameters, injectables);
            SetNetwork(constructor, parameters, network);
            SetAlwaysCreatedParams(constructor, parameters, injectables, network);

            outOffset = offset;
            object obj = constructor.Activator(parameters);

            return obj;
#if !NETSTANDARD2_0
            }
            finally
            {
                ArrayPool<object?>.Shared.Return(parameters, clearArray: true);
            }
#endif
        }

        private void SetAlwaysCreatedParams(
            TypeActivator constructor,
            object?[] parameters,
            InjectableValues? injectables,
            Network? network
            )
        {
            foreach (var param in constructor.AlwaysCreatedParameters)
            {
                if (parameters[param.Position] != null) continue;

                var activator = _typeActivatorCreator.GetActivator(param.MemberType);

#if !NETSTANDARD2_0
                object?[] cstorParams = ArrayPool<object?>.Shared.Rent(activator.DefaultParameters.Length);
                try
                {
#else
                object?[] cstorParams = new object?[activator.DefaultParameters.Length];
#endif
                activator.DefaultParameters.CopyTo(cstorParams, 0);

                SetInjectables(activator, cstorParams, injectables);
                SetNetwork(activator, cstorParams, network);
                SetAlwaysCreatedParams(activator, cstorParams, injectables, network);
                parameters[param.Position] = activator.Activator(cstorParams);
#if !NETSTANDARD2_0
                }
                finally
                {
                    ArrayPool<object?>.Shared.Return(cstorParams, clearArray: true);
                }
#endif
            }
        }

        private static void SetInjectables(TypeActivator constructor, object?[] parameters, InjectableValues? injectables)
        {
            foreach (var item in constructor.InjectableParameters)
            {
                if (injectables == null || !injectables.Values.TryGetValue(item.Key, out var value))
                    throw new DeserializationException($"No injectable value found for {item.Key}");

                parameters[item.Value.Position] = value;
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

        private readonly TypeActivatorCreator _typeActivatorCreator;

        private Key DecodeKey(long offset, out long outOffset, int depth, ref int budget)
        {
            var type = CtrlData(offset, out var size, out offset);
            switch (type)
            {
                case ObjectType.Pointer:
                    // A key can only be a string, so it cannot fan out and needs
                    // no value budget. It can still point at another pointer, so
                    // guard the depth to stop a pointer cycle from overflowing
                    // the stack with an uncatchable StackOverflowException.
                    CheckDepth(depth);
                    offset = DecodePointer(offset, size, out outOffset);
                    ConsumePointerTarget(ref budget);
                    return DecodeKey(offset, out _, depth + 1, ref budget);

                case ObjectType.Utf8String:
                    outOffset = offset + size;
                    return new Key(_database, offset, size);

                default:
                    throw new InvalidDatabaseException($"Database contains a non-string as map key: {type}");
            }
        }

        // The enclosing container already charged the values in numberToSkip.
        // Keep sibling values in a loop and recurse only into containers, where
        // their children must be charged and their structural depth checked.
        private long NextValueOffset(long offset, int numberToSkip, int depth, ref int budget)
        {
            while (numberToSkip > 0)
            {
                var type = CtrlData(offset, out var size, out offset);
                switch (type)
                {
                    case ObjectType.Pointer:
                        // While skipping values, only pointer byte-length matters.
                        offset += ((size >> 3) & 0x3) + 1;
                        break;

                    case ObjectType.Map:
                        CheckContainer(depth, 2 * size, ref budget);
                        offset = NextValueOffset(offset, 2 * size, depth + 1, ref budget);
                        break;

                    case ObjectType.Array:
                        CheckContainer(depth, size, ref budget);
                        offset = NextValueOffset(offset, size, depth + 1, ref budget);
                        break;

                    case ObjectType.Boolean:
                        break;

                    default:
                        offset += size;
                        break;
                }

                numberToSkip--;
            }

            return offset;
        }

        /// <summary>
        ///     Decodes the long.
        /// </summary>
        /// <returns></returns>
        private long DecodeLong(Type expectedType, long offset, int size)
        {
            if (expectedType != typeof(long) && expectedType != typeof(long?))
            {
                ReflectionUtil.CheckType(expectedType, typeof(long));
            }
            return _database.ReadLong(offset, size);
        }

        /// <summary>
        ///     Decodes the array.
        /// </summary>
        /// <param name="expectedType"></param>
        /// <param name="size">The size.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="outOffset">The out offset.</param>
        /// <param name="depth">The current nesting depth.</param>
        /// <param name="budget">The remaining number of values that may be decoded.</param>
        /// <param name="injectables"></param>
        /// <param name="network"></param>
        /// <returns></returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
            "AOT",
            "IL3050",
            Justification = "Generated collection registrations return before this runtime generic construction path. This path serves only the documented fallback for unregistered collection types, which is unsupported in NativeAOT applications.")]
#endif
        private object DecodeArray(Type expectedType, int size, long offset, out long outOffset,
            int depth, ref int budget, InjectableValues? injectables, Network? network)
        {
            // Fast path for List<string> (and parents).
            if (expectedType != typeof(object) && expectedType.IsAssignableFrom(typeof(List<string>)))
            {
                List<string> list = new(size);
                for (var i = 0; i < size; i++)
                {
                    var r = DecodeNested<string>(offset, out offset, depth + 1, ref budget, injectables, network);
                    list.Add(r);
                }

                outOffset = offset;
                return list;
            }

            // Database values decoded as object use List<object>.
            if (expectedType == typeof(object) || expectedType.IsAssignableFrom(typeof(List<object>)))
            {
                List<object> list = new(size);
                for (var i = 0; i < size; i++)
                {
                    var value = DecodeNested<object>(offset, out offset, depth + 1, ref budget, injectables, network);
                    list.Add(value);
                }

                outOffset = offset;
                return list;
            }

            if (SourceGeneratorSupport.TryGetCollectionRegistration(
                    expectedType, out var registration))
            {
                var generatedCollection = registration.Factory(size);
                for (var i = 0; i < size; i++)
                {
                    var value = Decode(
                        registration.ElementType, offset, out offset, depth + 1, ref budget, injectables, network);
                    registration.Add(generatedCollection, value);
                }

                outOffset = offset;
                return generatedCollection;
            }

            var genericArgs = expectedType.GetGenericArguments();
            var argType = genericArgs.Length == 0 ? typeof(object) : genericArgs[0];
            var interfaceType = typeof(ICollection<>).MakeGenericType(argType);
            var addMethod = interfaceType.GetMethod("Add");
            if (addMethod == null)
            {
                throw new DeserializationException("Missing Add method when decoding array");
            }

            var array = _listActivatorCreator.GetActivator(expectedType)(size);
            for (var i = 0; i < size; i++)
            {
                var value = Decode(argType, offset, out offset, depth + 1, ref budget, injectables, network);
                addMethod.Invoke(array, [value]);
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
            if (expectedType != typeof(ulong) && expectedType != typeof(ulong?))
            {
                ReflectionUtil.CheckType(expectedType, typeof(ulong));
            }
            return _database.ReadULong(offset, size);
        }

        /// <summary>
        ///     Decodes the big integer.
        /// </summary>
        /// <returns></returns>
        private BigInteger DecodeBigInteger(Type expectedType, long offset, int size)
        {
            if (expectedType != typeof(BigInteger) && expectedType != typeof(BigInteger?))
            {
                ReflectionUtil.CheckType(expectedType, typeof(BigInteger));
            }
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
            // Cast through uint so that 4-byte values >= 2^31 are
            // zero-extended to long rather than sign-extended.
            var packed = ((long)b << (8 * pointerSize)) | (long)(uint)_database.ReadVarInt(offset, pointerSize);
            outOffset = offset + pointerSize;
            return packed + _pointerBase + _pointerValueOffset[pointerSize];
        }

        /// <summary>
        ///     Decodes the integer.
        /// </summary>
        /// <returns></returns>
        private int DecodeInteger(Type expectedType, long offset, int size)
        {
            if (expectedType != typeof(int) && expectedType != typeof(int?))
            {
                ReflectionUtil.CheckType(expectedType, typeof(int));
            }

            return _database.ReadVarInt(offset, size);
        }
    }
}
