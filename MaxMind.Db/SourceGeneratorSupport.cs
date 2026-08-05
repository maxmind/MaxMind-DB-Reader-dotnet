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
        /// <remarks>
        ///     Process-wide and write-once, like the registries themselves, which are
        ///     populated from module initializers and never reset. Setting it cannot
        ///     change the outcome for any other type: decoding still requires a
        ///     registration keyed by the exact type, so for every unrelated type the
        ///     lookup misses and the branch behaves as if this were still
        ///     <see langword="false"/>. It only decides whether that lookup happens at
        ///     all, which is why a test registering a non-generic dictionary cannot
        ///     perturb tests that run after it.
        /// </remarks>
        internal static bool HasNonGenericDictionaryRegistration
            => _hasNonGenericDictionaryRegistration;

        /// <summary>
        ///     Registers source-generated deserialization metadata for a model type.
        /// </summary>
        /// <typeparam name="T">The model type being registered.</typeparam>
        /// <param name="activator">Creates an instance from the ordered member values.</param>
        /// <param name="defaultsFactory">Creates the ordered default member values.</param>
        /// <param name="members">The ordered deserialization member metadata.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when any argument is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     Thrown when a member was not created by one of the
        ///     <see cref="GeneratedMember"/> factory methods.
        /// </exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterType<T>(
            Func<object?[], T> activator,
            Func<object?[]> defaultsFactory,
            GeneratedMember[] members
            )
            where T : class
        {
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
                if (registeredMembers[i].MemberType == null)
                {
                    throw new ArgumentException(
                        "Members must be created with a GeneratedMember factory method.",
                        nameof(members));
                }
            }

            // N.B. Validation here is limited to what cannot vary by type. Generated
            // registrations all run from a single module initializer, so anything
            // thrown from this method leaves the generated registration class
            // permanently uninitializable and disables generated activation for every
            // type in that assembly. Consistency checks that involve the member set as
            // a whole belong in GeneratedTypeActivatorRegistration, where they run on
            // first use and fail only the offending type.
            TypeRegistrations.TryAdd(typeof(T), new GeneratedTypeActivatorRegistration(
                typeof(T),
                args => activator(args),
                defaultsFactory,
                registeredMembers));
        }

        /// <summary>
        ///     Registers source-generated creation and mutation delegates for a
        ///     collection type.
        /// </summary>
        /// <typeparam name="TCollection">The declared collection type.</typeparam>
        /// <typeparam name="TElement">The collection element type.</typeparam>
        /// <param name="factory">
        ///     Creates a collection using the decoded item count as a capacity hint.
        /// </param>
        /// <param name="add">
        ///     Adds a decoded element to the collection. This stays untyped so that the
        ///     cast lives in generated code rather than in a wrapper delegate on the
        ///     per-element decode path.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when any argument is <see langword="null"/>.
        /// </exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterCollection<TCollection, TElement>(
            Func<int, TCollection> factory,
            Action<object, object?> add
            )
            where TCollection : class
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            if (add == null)
            {
                throw new ArgumentNullException(nameof(add));
            }

            CollectionRegistrations.TryAdd(
                typeof(TCollection),
                new GeneratedCollectionRegistration(
                    typeof(TElement), capacity => factory(capacity), add));
        }

        /// <summary>
        ///     Registers source-generated creation and mutation delegates for a
        ///     dictionary type.
        /// </summary>
        /// <typeparam name="TDictionary">The declared dictionary type.</typeparam>
        /// <typeparam name="TKey">The dictionary key type.</typeparam>
        /// <typeparam name="TValue">The dictionary value type.</typeparam>
        /// <param name="factory">
        ///     Creates a dictionary using the decoded item count as a capacity hint.
        /// </param>
        /// <param name="add">
        ///     Adds a decoded key and value to the dictionary. This stays untyped for
        ///     the same reason as the collection overload.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when any argument is <see langword="null"/>.
        /// </exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterDictionary<TDictionary, TKey, TValue>(
            Func<int, TDictionary> factory,
            Action<object, object?, object?> add
            )
            where TDictionary : class
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            if (add == null)
            {
                throw new ArgumentNullException(nameof(add));
            }

            if (DictionaryRegistrations.TryAdd(
                    typeof(TDictionary),
                    new GeneratedDictionaryRegistration(
                        typeof(TKey),
                        typeof(TValue),
                        capacity => factory(capacity),
                        add)) &&
                !typeof(TDictionary).IsGenericType)
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
    ///     Which source of data supplies a source-generated member's value.
    /// </summary>
    internal enum GeneratedMemberKind
    {
        Mapped,
        Injected,
        Networked,
    }

    /// <summary>
    ///     Describes one member used by source-generated MaxMind DB deserialization.
    ///     A member draws its value from exactly one source, so instances are created
    ///     through <see cref="Mapped"/>, <see cref="Injected"/> or
    ///     <see cref="Networked"/> rather than a constructor that could express a
    ///     combination none of the decode paths can resolve.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct GeneratedMember
    {
        private GeneratedMember(
            GeneratedMemberKind kind,
            string? mapKey,
            Type memberType,
            string? injectableName,
            bool alwaysCreate
            )
        {
            Kind = kind;
            MapKey = mapKey;
            MemberType = memberType;
            InjectableName = injectableName;
            AlwaysCreate = alwaysCreate;
        }

        /// <summary>
        ///     Creates metadata for a member read from a database map key.
        /// </summary>
        /// <param name="mapKey">The database map key for the member.</param>
        /// <param name="memberType">The type of the member.</param>
        /// <param name="alwaysCreate">
        ///     Whether the member is created even when the key is absent.
        /// </param>
        /// <returns>The member metadata.</returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="mapKey"/> or <paramref name="memberType"/>
        ///     is <see langword="null"/>.
        /// </exception>
        public static GeneratedMember Mapped(
            string mapKey,
            Type memberType,
            bool alwaysCreate
            ) => new(
                GeneratedMemberKind.Mapped,
                mapKey ?? throw new ArgumentNullException(nameof(mapKey)),
                memberType ?? throw new ArgumentNullException(nameof(memberType)),
                null,
                alwaysCreate);

        /// <summary>
        ///     Creates metadata for a member supplied from injectable values.
        /// </summary>
        /// <param name="injectableName">The injectable name for the member.</param>
        /// <param name="memberType">The type of the member.</param>
        /// <returns>The member metadata.</returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="injectableName"/> or
        ///     <paramref name="memberType"/> is <see langword="null"/>.
        /// </exception>
        public static GeneratedMember Injected(
            string injectableName,
            Type memberType
            ) => new(
                GeneratedMemberKind.Injected,
                null,
                memberType ?? throw new ArgumentNullException(nameof(memberType)),
                injectableName ?? throw new ArgumentNullException(nameof(injectableName)),
                false);

        /// <summary>
        ///     Creates metadata for a member that receives the matched network.
        /// </summary>
        /// <param name="memberType">The type of the member.</param>
        /// <returns>The member metadata.</returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="memberType"/> is <see langword="null"/>.
        /// </exception>
        public static GeneratedMember Networked(Type memberType) => new(
            GeneratedMemberKind.Networked,
            null,
            memberType ?? throw new ArgumentNullException(nameof(memberType)),
            null,
            false);

        internal bool AlwaysCreate { get; }
        internal string? InjectableName { get; }
        internal GeneratedMemberKind Kind { get; }
        internal string? MapKey { get; }
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
                switch (registeredMember.Kind)
                {
                    case GeneratedMemberKind.Injected:
                        injectables.Add(
                            new KeyValuePair<string, DeserializationMember>(
                                registeredMember.InjectableName!, member));
                        break;
                    case GeneratedMemberKind.Networked:
                        networkParameters.Add(member);
                        break;
                    default:
                        var key = new Key(
                            Encoding.UTF8.GetBytes(registeredMember.MapKey!));
                        if (deserializationParameters.ContainsKey(key))
                        {
                            throw new DeserializationException(
                                $"Source-generated metadata for {_type} contains the "
                                + $"duplicate map key '{registeredMember.MapKey}'.");
                        }
                        deserializationParameters.Add(key, member);
                        if (registeredMember.AlwaysCreate)
                        {
                            alwaysCreatedParameters.Add(member);
                        }
                        break;
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
