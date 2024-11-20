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
    internal readonly struct TypeActivator
    {
        internal readonly ObjectActivator Activator;
        internal readonly List<ParameterInfo> AlwaysCreatedParameters;
        private readonly object?[] _defaultParameters;
        internal readonly Dictionary<Key, ParameterInfo> DeserializationParameters;
        internal readonly Dictionary<string, ParameterInfo> InjectableParameters;
        internal readonly List<ParameterInfo> NetworkParameters;
        private readonly Type[] ParameterTypes;

        internal TypeActivator(
            ObjectActivator activator,
            Dictionary<Key, ParameterInfo> deserializationParameters,
            Dictionary<string, ParameterInfo> injectables,
            List<ParameterInfo> networkParameters,
            List<ParameterInfo> alwaysCreatedParameters
            ) : this()
        {
            Activator = activator;
            AlwaysCreatedParameters = alwaysCreatedParameters;
            DeserializationParameters = deserializationParameters;
            InjectableParameters = injectables;

            NetworkParameters = networkParameters;
            ParameterTypes =
                deserializationParameters.Values.OrderBy(x => x.Position).Select(x => x.ParameterType).ToArray();
            _defaultParameters = ParameterTypes.Select(DefaultValue).ToArray();
        }

        internal object[] DefaultParameters() => (object[])_defaultParameters.Clone();

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
            var injectables = new Dictionary<string, ParameterInfo>();
            var networkParams = new List<ParameterInfo>();
            var alwaysCreated = new List<ParameterInfo>();
            foreach (var param in parameters)
            {
                var injectableAttribute = param.GetCustomAttributes<InjectAttribute>().FirstOrDefault();
                if (injectableAttribute != null)
                {
                    injectables.Add(injectableAttribute.ParameterName, param);
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
            var clsConstructor = new TypeActivator(activator, paramNameTypes, injectables, networkParams, alwaysCreated);
            return clsConstructor;
        }
    }
}