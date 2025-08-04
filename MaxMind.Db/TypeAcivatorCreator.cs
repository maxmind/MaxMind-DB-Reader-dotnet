#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq;
using System.Reflection;
using System.Text;

#endregion

namespace MaxMind.Db
{
    internal class TypeActivator
    {
        internal readonly ObjectActivator Activator;
        internal readonly ParameterInfo[] AlwaysCreatedParameters;
        internal readonly object?[] DefaultParameters;
        internal readonly IParameterDictionary DeserializationParameters;
        internal readonly KeyValuePair<string, ParameterInfo>[] InjectableParameters;
        internal readonly ParameterInfo[] NetworkParameters;

        internal TypeActivator(
            ObjectActivator activator,
            IParameterDictionary deserializationParameters,
            KeyValuePair<string, ParameterInfo>[] injectables,
            ParameterInfo[] networkParameters,
            ParameterInfo[] alwaysCreatedParameters
            )
        {
            Activator = activator;
            AlwaysCreatedParameters = alwaysCreatedParameters;
            DeserializationParameters = deserializationParameters;
            InjectableParameters = injectables;

            NetworkParameters = networkParameters;
            Type[] parameterTypes = deserializationParameters.Values.OrderBy(x => x.Position).Select(x => x.ParameterType).ToArray();
            DefaultParameters = parameterTypes.Select(DefaultValue).ToArray();
        }

        private static object? DefaultValue(Type type)
        {
            if (type.GetTypeInfo().IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                return System.Activator.CreateInstance(type);
            }
            return null;
        }
    }

    internal sealed class TypeAcivatorCreator
    {
        // Use sliding expiration cache for automatic memory management in long-running applications
        private static readonly SlidingExpirationCache<Type, TypeActivator> _typeConstructors =
            new(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));

        // Pre-computed parameter name keys to avoid repeated UTF-8 encoding and ArrayBuffer allocation
        private static readonly ConcurrentDictionary<string, Key> _parameterKeyCache = new();

        internal TypeActivator GetActivator(Type expectedType)
        {
#if DEBUG
            // Track cache performance in debug builds
            bool wasInCache = TypeActivatorCache.CacheCount > 0;
            var result = _typeConstructors.GetOrAdd(expectedType, ClassActivator);

            if (wasInCache)
                TypeActivatorCache.PerformanceCounters.RecordCacheHit();
            else
                TypeActivatorCache.PerformanceCounters.RecordCacheMiss();

            return result;
#else
            return _typeConstructors.GetOrAdd(expectedType, ClassActivator);
#endif
        }

        private static Key GetParameterKey(string parameterName)
        {
            return _parameterKeyCache.GetOrAdd(parameterName, name =>
            {
                var bytes = Encoding.UTF8.GetBytes(name);
                return new Key(new ArrayBuffer(bytes), 0, bytes.Length);
            });
        }

#if NET8_0_OR_GREATER
        private static TypeActivator ClassActivator([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type expectedType)
#else
        private static TypeActivator ClassActivator(Type expectedType)
#endif
        {
#if NET8_0_OR_GREATER
            // Ensure generated activators are registered
            try
            {
                Generated.TypeActivatorRegistration.EnsureRegistered();
            }
            catch
            {
                // Ignore if not available - means no generated types in this compilation
            }

            // Try to use registered source-generated activator first
            if (SourceGeneratorSupport.HasActivator(expectedType))
            {
                return CreateRegisteredActivator(expectedType);
            }
#endif
            var constructors =
                expectedType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(c => c.IsDefined(typeof(ConstructorAttribute), true))
                    .ToList();
            if (constructors.Count == 0)
            {
                throw new DeserializationException(
                    $"No constructors found for {expectedType} found with MaxMind.Db.Constructor attribute");
            }
            if (constructors.Count > 1)
            {
                throw new DeserializationException(
                    $"More than one constructor found for {expectedType} found with MaxMind.Db/Constructor attribute");
            }

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            var paramNameTypes = new SmallParameterDictionary();
            var injectables = new List<KeyValuePair<string, ParameterInfo>>();
            var networkParams = new List<ParameterInfo>();
            var alwaysCreated = new List<ParameterInfo>();
            foreach (var param in parameters)
            {
                var injectableAttribute = param.GetCustomAttributes<InjectAttribute>().FirstOrDefault();
                if (injectableAttribute != null)
                {
                    injectables.Add(new KeyValuePair<string, ParameterInfo>(injectableAttribute.ParameterName, param));
                }
                var networkAttribute = param.GetCustomAttributes<NetworkAttribute>().FirstOrDefault();
                if (networkAttribute != null)
                {
                    networkParams.Add(param);
                }
                var paramAttribute = param.GetCustomAttributes<ParameterAttribute>().FirstOrDefault();
                string? name;
                if (paramAttribute != null)
                {
                    name = paramAttribute.ParameterName;
                    if (paramAttribute.AlwaysCreate)
                        alwaysCreated.Add(param);
                }
                else
                {
                    name = param.Name;
                    if (name == null)
                    {
                        throw new DeserializationException("Unexpected null parameter name");
                    }
                }
                paramNameTypes.Add(GetParameterKey(name), param);
            }
            var activator = ReflectionUtil.CreateActivator(constructor);
            var clsConstructor = new TypeActivator(activator, paramNameTypes, injectables.ToArray(),
                networkParams.ToArray(), alwaysCreated.ToArray());
            return clsConstructor;
        }

#if NET8_0_OR_GREATER
        private static TypeActivator CreateRegisteredActivator(Type expectedType)
        {
            // Use reflection ONCE to get parameter information, but use fast activator for creation
            var constructors =
                expectedType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(c => c.IsDefined(typeof(ConstructorAttribute), true))
                    .ToList();

            if (constructors.Count != 1)
            {
                throw new DeserializationException(
                    $"Expected exactly one constructor with [Constructor] attribute for {expectedType}, found {constructors.Count}");
            }

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            var paramNameTypes = new SmallParameterDictionary();
            var injectables = new List<KeyValuePair<string, ParameterInfo>>();
            var networkParams = new List<ParameterInfo>();
            var alwaysCreated = new List<ParameterInfo>();

            foreach (var param in parameters)
            {
                var injectableAttribute = param.GetCustomAttributes<InjectAttribute>().FirstOrDefault();
                if (injectableAttribute != null)
                {
                    injectables.Add(new KeyValuePair<string, ParameterInfo>(injectableAttribute.ParameterName, param));
                }
                var networkAttribute = param.GetCustomAttributes<NetworkAttribute>().FirstOrDefault();
                if (networkAttribute != null)
                {
                    networkParams.Add(param);
                }
                var paramAttribute = param.GetCustomAttributes<ParameterAttribute>().FirstOrDefault();
                string? name;
                if (paramAttribute != null)
                {
                    name = paramAttribute.ParameterName;
                    if (paramAttribute.AlwaysCreate)
                        alwaysCreated.Add(param);
                }
                else
                {
                    name = param.Name;
                    if (name == null)
                    {
                        throw new DeserializationException("Unexpected null parameter name");
                    }
                }
                paramNameTypes.Add(GetParameterKey(name), param);
            }

            // Create a wrapper that uses the registered fast activator
            ObjectActivator fastActivator = args =>
            {
                if (SourceGeneratorSupport.TryCreateInstance(expectedType, args, out var instance))
                {
                    return instance!;
                }
                throw new InvalidOperationException($"Failed to create instance of {expectedType} using registered activator");
            };

            return new TypeActivator(fastActivator, paramNameTypes, injectables.ToArray(),
                networkParams.ToArray(), alwaysCreated.ToArray());
        }
#endif
    }

    /// <summary>
    /// Interface for parameter dictionary to allow different implementations
    /// </summary>
    internal interface IParameterDictionary
    {
        bool TryGetValue(Key key, out ParameterInfo value);
        void Add(Key key, ParameterInfo value);
        IEnumerable<ParameterInfo> Values { get; }
    }

    /// <summary>
    /// Optimized parameter dictionary for small parameter sets (typical MaxMind types have less than 16 parameters)
    /// Uses linear search which is faster than Dictionary overhead for small collections
    /// </summary>
    internal sealed class SmallParameterDictionary : IParameterDictionary
    {
        private const int MaxLinearSearchSize = 16;
        private readonly List<KeyValuePair<Key, ParameterInfo>> _items = new();
        private Dictionary<Key, ParameterInfo>? _fallbackDict;

        public void Add(Key key, ParameterInfo value)
        {
            if (_fallbackDict != null)
            {
                _fallbackDict.Add(key, value);
            }
            else if (_items.Count < MaxLinearSearchSize)
            {
                _items.Add(new KeyValuePair<Key, ParameterInfo>(key, value));
            }
            else
            {
                // Convert to Dictionary for large parameter sets
                _fallbackDict = new Dictionary<Key, ParameterInfo>(_items);
                _fallbackDict.Add(key, value);
                _items.Clear(); // Free memory
            }
        }

        public bool TryGetValue(Key key, out ParameterInfo value)
        {
            if (_fallbackDict != null)
            {
                return _fallbackDict.TryGetValue(key, out value!);
            }

            // Linear search is faster than Dictionary for small collections
            foreach (var item in _items)
            {
                if (item.Key.Equals(key))
                {
                    value = item.Value;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public IEnumerable<ParameterInfo> Values =>
            _fallbackDict?.Values ?? _items.Select(kvp => kvp.Value);
    }
}