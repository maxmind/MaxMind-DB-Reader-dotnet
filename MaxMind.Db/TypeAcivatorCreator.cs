#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private static readonly TypeActivatorCache _typeConstructors = new();

        // Pre-computed parameter name keys to avoid repeated UTF-8 encoding and ArrayBuffer allocation
        private static readonly ConcurrentDictionary<string, Key> _parameterKeyCache = new();

        internal TypeActivator GetActivator(Type expectedType)
            => _typeConstructors.GetOrAdd(expectedType, ClassActivator);

        private static Key GetParameterKey(string parameterName)
        {
            return _parameterKeyCache.GetOrAdd(parameterName, name =>
            {
                var bytes = Encoding.UTF8.GetBytes(name);
                return new Key(new ArrayBuffer(bytes), 0, bytes.Length);
            });
        }

        private static TypeActivator ClassActivator([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type expectedType)
        {
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
            
            var constructor = ConstructorResolver.ResolveConstructor(expectedType);
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
                        throw ReflectionErrorHandling.CreateDeserializationException(
                            "Unexpected null parameter name",
                            expectedType,
                            param.Name);
                    }
                }
                paramNameTypes.Add(GetParameterKey(name), param);
            }
            var activator = ReflectionUtil.CreateActivator(constructor);
            var clsConstructor = new TypeActivator(activator, paramNameTypes, injectables.ToArray(),
                networkParams.ToArray(), alwaysCreated.ToArray());
            return clsConstructor;
        }

        private static TypeActivator CreateRegisteredActivator(Type expectedType)
        {
            // Use reflection ONCE to get parameter information, but use fast activator for creation
            var constructor = ConstructorResolver.ResolveConstructor(expectedType);
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
                        throw ReflectionErrorHandling.CreateDeserializationException(
                            "Unexpected null parameter name",
                            expectedType,
                            param.Name);
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
                throw ReflectionErrorHandling.CreateDeserializationException(
                    "Failed to create instance using registered activator",
                    expectedType);
            };

            return new TypeActivator(fastActivator, paramNameTypes, injectables.ToArray(),
                networkParams.ToArray(), alwaysCreated.ToArray());
        }
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