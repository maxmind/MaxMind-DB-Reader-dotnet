#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        internal readonly Dictionary<Key, ParameterInfo> DeserializationParameters;
        internal readonly KeyValuePair<string, ParameterInfo>[] InjectableParameters;
        internal readonly ParameterInfo[] NetworkParameters;

        internal TypeActivator(
            ObjectActivator activator,
            Dictionary<Key, ParameterInfo> deserializationParameters,
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
        private readonly ConcurrentDictionary<Type, TypeActivator> _typeConstructors =
            new();

        internal TypeActivator GetActivator(Type expectedType)
            => _typeConstructors.GetOrAdd(expectedType, ClassActivator);

        private static TypeActivator ClassActivator(Type expectedType)
        {
#if NET8_0_OR_GREATER
            // Try to use AOT-generated activator first
            if (AotCompatibility.UseAotOptimizations && AotCompatibility.IsTypeSupportedAot(expectedType))
            {
                return CreateAotActivator(expectedType);
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
            var paramNameTypes = new Dictionary<Key, ParameterInfo>();
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
                var bytes = Encoding.UTF8.GetBytes(name);
                paramNameTypes.Add(new Key(new ArrayBuffer(bytes), 0, bytes.Length), param);
            }
            var activator = ReflectionUtil.CreateActivator(constructor);
            var clsConstructor = new TypeActivator(activator, paramNameTypes, injectables.ToArray(),
                networkParams.ToArray(), alwaysCreated.ToArray());
            return clsConstructor;
        }

#if NET8_0_OR_GREATER
        private static TypeActivator CreateAotActivator(Type expectedType)
        {
            // Create a wrapper that uses the AOT-generated activator
            ObjectActivator aotActivator = args =>
            {
                if (AotCompatibility.TryCreateInstanceAot(expectedType, args, out var instance))
                {
                    return instance!;
                }
                throw new InvalidOperationException($"Failed to create instance of {expectedType} using AOT activator");
            };

            // Get parameter information from generated code
            var parameterMap = GetAotParameterMap(expectedType);
            var paramNameTypes = new Dictionary<Key, ParameterInfo>();
            
            foreach (var kvp in parameterMap)
            {
                var bytes = Encoding.UTF8.GetBytes(kvp.Key);
                var paramInfo = new AotParameterInfo(kvp.Key, kvp.Value);
                paramNameTypes.Add(new Key(new ArrayBuffer(bytes), 0, bytes.Length), paramInfo);
            }

            // For AOT, we don't have full parameter info, so we create simplified versions
            return new TypeActivator(
                aotActivator,
                paramNameTypes,
                Array.Empty<KeyValuePair<string, ParameterInfo>>(), // No injectable info in AOT mode
                Array.Empty<ParameterInfo>(), // No network params in AOT mode
                Array.Empty<ParameterInfo>()  // No always created params in AOT mode
            );
        }

        private static Dictionary<string, int> GetAotParameterMap(Type type)
        {
            try
            {
                var generatedType = Type.GetType("MaxMind.Db.Generated.TypeActivators, MaxMind.Db");
                if (generatedType != null)
                {
                    var method = generatedType.GetMethod("GetParameterMap");
                    if (method != null)
                    {
                        return (Dictionary<string, int>)method.Invoke(null, new object[] { type })!;
                    }
                }
            }
            catch
            {
                // Fall back to empty map
            }
            return new Dictionary<string, int>();
        }

        /// <summary>
        /// Minimal ParameterInfo implementation for AOT mode
        /// </summary>
        private class AotParameterInfo : ParameterInfo
        {
            private readonly string _name;
            private readonly int _position;

            public AotParameterInfo(string name, int position)
            {
                _name = name;
                _position = position;
            }

            public override string? Name => _name;
            public override int Position => _position;
            public override Type ParameterType => typeof(object); // We don't have type info in AOT mode
        }
#endif
    }
}