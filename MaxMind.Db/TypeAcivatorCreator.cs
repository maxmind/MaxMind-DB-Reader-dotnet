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
    internal struct TypeActivator
    {
        internal readonly ObjectActivator Activator;
        internal readonly List<ParameterInfo> AlwaysCreatedParameters;
        internal readonly object[] _defaultParameters;
        internal readonly Dictionary<byte[], ParameterInfo> DeserializationParameters;
        internal readonly Dictionary<string, ParameterInfo> InjectableParameters;
        internal readonly Type[] ParameterTypes;

        internal TypeActivator(
            ObjectActivator activator,
            Dictionary<byte[], ParameterInfo> deserializationParameters,
            Dictionary<string, ParameterInfo> injectables,
            List<ParameterInfo> alwaysCreatedParameters
            ) : this()
        {
            Activator = activator;
            AlwaysCreatedParameters = alwaysCreatedParameters;
            DeserializationParameters = deserializationParameters;
            InjectableParameters = injectables;
            ParameterTypes =
                deserializationParameters.Values.OrderBy(x => x.Position).Select(x => x.ParameterType).ToArray();
            _defaultParameters = ParameterTypes.Select(DefaultValue).ToArray();
        }

        internal object[] DefaultParameters() => (object[])_defaultParameters.Clone();

        private object DefaultValue(Type type)
        {
            if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                return System.Activator.CreateInstance(type);
            }
            return null;
        }
    }

    internal sealed class TypeAcivatorCreator
    {
        private readonly ConcurrentDictionary<Type, TypeActivator> _typeConstructors =
            new ConcurrentDictionary<Type, TypeActivator>();

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
            var paramNameTypes = new Dictionary<byte[], ParameterInfo>(new ByteArrayEqualityComparer());
            var injectables = new Dictionary<string, ParameterInfo>();
            var alwaysCreated = new List<ParameterInfo>();
            foreach (var param in parameters)
            {
                var injectableAttribute = param.GetCustomAttributes<InjectAttribute>().FirstOrDefault();
                if (injectableAttribute != null)
                {
                    injectables.Add(injectableAttribute.ParameterName, param);
                }
                var paramAttribute = param.GetCustomAttributes<ParameterAttribute>().FirstOrDefault();
                string name;
                if (paramAttribute != null)
                {
                    name = paramAttribute.ParameterName;
                    if (paramAttribute.AlwaysCreate)
                        alwaysCreated.Add(param);
                }
                else
                    name = param.Name;
                paramNameTypes.Add(Encoding.UTF8.GetBytes(name), param);
            }
            var activator = ReflectionUtil.CreateActivator(constructor);
            var clsConstructor = new TypeActivator(activator, paramNameTypes, injectables, alwaysCreated);
            return clsConstructor;
        }
    }
}