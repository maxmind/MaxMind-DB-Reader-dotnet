#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Infrastructure used by the MaxMind DB source generator.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SourceGeneratorSupport
    {
        private static readonly ConcurrentDictionary<Type, GeneratedTypeActivatorRegistration>
            TypeRegistrations = new();
        private static readonly ConcurrentDictionary<Type, GeneratedCollectionRegistration>
            CollectionRegistrations = new();
        private static readonly ConcurrentDictionary<Type, GeneratedDictionaryRegistration>
            DictionaryRegistrations = new();
        private static volatile bool _hasNonGenericDictionaryRegistration;

        /// <summary>
        ///     Whether any non-generic dictionary type has been registered. Generic
        ///     dictionaries are already routed by their arity, so decoding only needs to
        ///     look for a registration when this is <see langword="true"/>.
        /// </summary>
        internal static bool HasNonGenericDictionaryRegistration
            => _hasNonGenericDictionaryRegistration;

        /// <summary>
        ///     Registers source-generated deserialization metadata for a type.
        /// </summary>
        /// <param name="type">The type being registered.</param>
        /// <param name="activator">Creates an instance from the ordered member values.</param>
        /// <param name="defaultsFactory">Creates the ordered default member values.</param>
        /// <param name="members">The ordered deserialization member metadata.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when any argument is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     Thrown when a member contains a <see langword="null"/> map key or
        ///     member type.
        /// </exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterType(
            Type type,
            Func<object?[], object> activator,
            Func<object?[]> defaultsFactory,
            GeneratedMember[] members
            )
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (activator == null)
            {
                throw new ArgumentNullException(nameof(activator));
            }
            if (defaultsFactory == null)
            {
                throw new ArgumentNullException(nameof(defaultsFactory));
            }
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            var registeredMembers = (GeneratedMember[])members.Clone();
            for (var i = 0; i < registeredMembers.Length; i++)
            {
                if (registeredMembers[i].MapKey == null)
                {
                    throw new ArgumentException(
                        "Members must not contain null map keys.", nameof(members));
                }
                if (registeredMembers[i].MemberType == null)
                {
                    throw new ArgumentException(
                        "Members must not contain null member types.", nameof(members));
                }
            }

            // N.B. Validation here is limited to what cannot vary by type. Generated
            // registrations all run from a single module initializer, so anything
            // thrown from this method leaves the generated registration class
            // permanently uninitializable and disables generated activation for every
            // type in that assembly. Consistency checks that involve the member set as
            // a whole belong in GeneratedTypeActivatorRegistration, where they run on
            // first use and fail only the offending type.
            TypeRegistrations.TryAdd(type, new GeneratedTypeActivatorRegistration(
                type,
                activator,
                defaultsFactory,
                registeredMembers));
        }

        /// <summary>
        ///     Registers source-generated creation and mutation delegates for a collection type.
        /// </summary>
        /// <param name="type">The declared collection type.</param>
        /// <param name="elementType">The collection element type.</param>
        /// <param name="factory">
        ///     Creates a collection using the decoded item count as a capacity hint.
        /// </param>
        /// <param name="add">Adds an element to the collection.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when any argument is <see langword="null"/>.
        /// </exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterCollection(
            Type type,
            Type elementType,
            Func<int, object> factory,
            Action<object, object?> add
            )
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (elementType == null)
            {
                throw new ArgumentNullException(nameof(elementType));
            }
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            if (add == null)
            {
                throw new ArgumentNullException(nameof(add));
            }

            CollectionRegistrations.TryAdd(
                type,
                new GeneratedCollectionRegistration(elementType, factory, add));
        }

        /// <summary>
        ///     Registers source-generated creation and mutation delegates for a dictionary type.
        /// </summary>
        /// <param name="type">The declared dictionary type.</param>
        /// <param name="keyType">The dictionary key type.</param>
        /// <param name="valueType">The dictionary value type.</param>
        /// <param name="factory">
        ///     Creates a dictionary using the decoded item count as a capacity hint.
        /// </param>
        /// <param name="add">Adds a key and value to the dictionary.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when any argument is <see langword="null"/>.
        /// </exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterDictionary(
            Type type,
            Type keyType,
            Type valueType,
            Func<int, object> factory,
            Action<object, object?, object?> add
            )
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (keyType == null)
            {
                throw new ArgumentNullException(nameof(keyType));
            }
            if (valueType == null)
            {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            if (add == null)
            {
                throw new ArgumentNullException(nameof(add));
            }

            if (DictionaryRegistrations.TryAdd(
                    type,
                    new GeneratedDictionaryRegistration(keyType, valueType, factory, add)) &&
                !type.IsGenericType)
            {
                _hasNonGenericDictionaryRegistration = true;
            }
        }

        internal static bool TryGetCollectionRegistration(
            Type type,
            out GeneratedCollectionRegistration registration
            ) => CollectionRegistrations.TryGetValue(type, out registration!);

        internal static bool TryGetDictionaryRegistration(
            Type type,
            out GeneratedDictionaryRegistration registration
            ) => DictionaryRegistrations.TryGetValue(type, out registration!);

        internal static bool TryGetTypeRegistration(
            Type type,
            out GeneratedTypeActivatorRegistration registration
            ) => TypeRegistrations.TryGetValue(type, out registration!);
    }

    /// <summary>
    ///     Describes one member used by source-generated MaxMind DB deserialization.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct GeneratedMember
    {
        /// <summary>
        ///     Initializes source-generated deserialization metadata for one member.
        /// </summary>
        /// <param name="mapKey">The database map key for the member.</param>
        /// <param name="memberType">The type of the member.</param>
        /// <param name="injectableName">
        ///     The injectable name for the member, or <see langword="null"/>.
        /// </param>
        /// <param name="isNetwork">Whether the member receives the network.</param>
        /// <param name="alwaysCreate">Whether the member is always created.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="mapKey"/> or <paramref name="memberType"/>
        ///     is <see langword="null"/>.
        /// </exception>
        public GeneratedMember(
            string mapKey,
            Type memberType,
            string? injectableName,
            bool isNetwork,
            bool alwaysCreate
            )
        {
            MapKey = mapKey ?? throw new ArgumentNullException(nameof(mapKey));
            MemberType = memberType ?? throw new ArgumentNullException(nameof(memberType));
            InjectableName = injectableName;
            IsNetwork = isNetwork;
            AlwaysCreate = alwaysCreate;
        }

        internal bool AlwaysCreate { get; }
        internal string? InjectableName { get; }
        internal bool IsNetwork { get; }
        internal string MapKey { get; }
        internal Type MemberType { get; }
    }

    internal sealed class GeneratedCollectionRegistration
    {
        internal GeneratedCollectionRegistration(
            Type elementType,
            Func<int, object> factory,
            Action<object, object?> add
            )
        {
            ElementType = elementType;
            Factory = factory;
            Add = add;
        }

        internal Action<object, object?> Add { get; }
        internal Type ElementType { get; }
        internal Func<int, object> Factory { get; }
    }

    internal sealed class GeneratedDictionaryRegistration
    {
        internal GeneratedDictionaryRegistration(
            Type keyType,
            Type valueType,
            Func<int, object> factory,
            Action<object, object?, object?> add
            )
        {
            KeyType = keyType;
            ValueType = valueType;
            Factory = factory;
            Add = add;
        }

        internal Action<object, object?, object?> Add { get; }
        internal Func<int, object> Factory { get; }
        internal Type KeyType { get; }
        internal Type ValueType { get; }
    }

    internal sealed class GeneratedTypeActivatorRegistration
    {
        private readonly ObjectActivator _activator;
        private readonly Func<object?[]> _defaultsFactory;
        private readonly GeneratedMember[] _members;
        private readonly Type _type;
        private ActivatorMetadata? _cachedMetadata;

        internal GeneratedTypeActivatorRegistration(
            Type type,
            Func<object?[], object> activator,
            Func<object?[]> defaultsFactory,
            GeneratedMember[] members
            )
        {
            _type = type;
            _activator = args => activator(args);
            _defaultsFactory = defaultsFactory;
            _members = members;
        }

        internal TypeActivator CreateActivator()
        {
            object?[] defaultParameters;
            try
            {
                defaultParameters = (object?[])_defaultsFactory().Clone();
            }
            catch (Exception ex)
            {
                throw new DeserializationException(
                    $"The source-generated default value factory for {_type} threw an exception",
                    ex);
            }
            if (defaultParameters.Length != _members.Length)
            {
                throw new DeserializationException(
                    "Source-generated default member values must match the registered member count.");
            }

            var metadata = GetOrCreateMetadata();
            // Nulling the slot is what makes SetAlwaysCreatedParams construct the
            // member. A non-nullable value type has no model to construct, and the
            // reflection path leaves its default in place, so nulling it here would
            // send the decoder off to activate something like System.Int32 as a model.
            foreach (var member in metadata.AlwaysCreatedParameters)
            {
                if (!TypeActivator.IsNonNullableValueType(member.MemberType))
                {
                    defaultParameters[member.Position] = null;
                }
            }

            return new TypeActivator(
                _activator,
                metadata.DeserializationParameters,
                metadata.Injectables,
                metadata.NetworkParameters,
                metadata.AlwaysCreatedParameters,
                defaultParameters);
        }

        private ActivatorMetadata GetOrCreateMetadata()
        {
            var cachedMetadata = Volatile.Read(ref _cachedMetadata);
            if (cachedMetadata != null)
            {
                return cachedMetadata;
            }

            var metadata = BuildMetadata();
            return Interlocked.CompareExchange(ref _cachedMetadata, metadata, null) ?? metadata;
        }

        private ActivatorMetadata BuildMetadata()
        {
            var deserializationParameters =
                new Dictionary<Key, DeserializationMember>(_members.Length);
            var injectables = new List<KeyValuePair<string, DeserializationMember>>();
            var networkParameters = new List<DeserializationMember>();
            var alwaysCreatedParameters = new List<DeserializationMember>();

            for (var i = 0; i < _members.Length; i++)
            {
                var registeredMember = _members[i];
                var member = new DeserializationMember(i, registeredMember.MemberType);
                if (registeredMember.InjectableName == null && !registeredMember.IsNetwork)
                {
                    var key = new Key(Encoding.UTF8.GetBytes(registeredMember.MapKey));
                    if (deserializationParameters.ContainsKey(key))
                    {
                        throw new DeserializationException(
                            $"Source-generated metadata for {_type} contains the duplicate "
                            + $"map key '{registeredMember.MapKey}'.");
                    }
                    deserializationParameters.Add(key, member);
                }
                if (registeredMember.InjectableName != null)
                {
                    injectables.Add(
                        new KeyValuePair<string, DeserializationMember>(
                            registeredMember.InjectableName, member));
                }
                if (registeredMember.IsNetwork)
                {
                    networkParameters.Add(member);
                }
                if (registeredMember.AlwaysCreate)
                {
                    alwaysCreatedParameters.Add(member);
                }
            }

            return new ActivatorMetadata(
                deserializationParameters,
                injectables.ToArray(),
                networkParameters.ToArray(),
                alwaysCreatedParameters.ToArray());
        }

        private sealed class ActivatorMetadata
        {
            internal ActivatorMetadata(
                Dictionary<Key, DeserializationMember> deserializationParameters,
                KeyValuePair<string, DeserializationMember>[] injectables,
                DeserializationMember[] networkParameters,
                DeserializationMember[] alwaysCreatedParameters
                )
            {
                DeserializationParameters = deserializationParameters;
                Injectables = injectables;
                NetworkParameters = networkParameters;
                AlwaysCreatedParameters = alwaysCreatedParameters;
            }

            internal DeserializationMember[] AlwaysCreatedParameters { get; }
            internal Dictionary<Key, DeserializationMember> DeserializationParameters { get; }
            internal KeyValuePair<string, DeserializationMember>[] Injectables { get; }
            internal DeserializationMember[] NetworkParameters { get; }
        }
    }
}
