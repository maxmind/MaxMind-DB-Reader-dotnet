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
    /// <summary>
    ///     Wraps either a <see cref="ParameterInfo"/> (constructor-based activation)
    ///     or a <see cref="PropertyInfo"/> (property-based activation) so the decoder
    ///     can treat both uniformly.
    /// </summary>
    internal sealed class DeserializationMember
    {
        internal int Position { get; }
        internal Type MemberType { get; }
        internal string? Name { get; }

        internal DeserializationMember(ParameterInfo param)
        {
            Position = param.Position;
            MemberType = param.ParameterType;
            Name = param.Name;
        }

        internal DeserializationMember(int position, Type memberType, string? name)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            Position = position;
            MemberType = memberType ?? throw new ArgumentNullException(nameof(memberType));
            Name = name;
        }
    }

    internal class TypeActivator
    {
        internal readonly ObjectActivator Activator;
        internal readonly DeserializationMember[] AlwaysCreatedParameters;
        internal readonly object?[] DefaultParameters;
        internal readonly Dictionary<Key, DeserializationMember> DeserializationParameters;
        internal readonly KeyValuePair<string, DeserializationMember>[] InjectableParameters;
        internal readonly DeserializationMember[] NetworkParameters;

        internal TypeActivator(
            ObjectActivator activator,
            Dictionary<Key, DeserializationMember> deserializationParameters,
            KeyValuePair<string, DeserializationMember>[] injectables,
            DeserializationMember[] networkParameters,
            DeserializationMember[] alwaysCreatedParameters,
            object?[]? defaultParameters = null
            )
        {
            Activator = activator;
            AlwaysCreatedParameters = alwaysCreatedParameters;
            DeserializationParameters = deserializationParameters;
            InjectableParameters = injectables;
            NetworkParameters = networkParameters;

            // The deserializationParameters dictionary must contain ALL members
            // (MapKey + Inject + Network) so that DefaultParameters.Length correctly
            // sizes the parameter array used by both constructor and MemberInit activators.
            if (defaultParameters != null)
            {
                DefaultParameters = defaultParameters;
            }
            else
            {
                Type[] parameterTypes = deserializationParameters.Values
                    .OrderBy(x => x.Position)
                    .Select(x => x.MemberType)
                    .ToArray();
                DefaultParameters = parameterTypes.Select(DefaultValue).ToArray();
            }
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

    internal sealed class TypeActivatorCreator
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

            if (constructors.Count > 1)
            {
                throw new DeserializationException(
                    $"More than one constructor found for {expectedType} with the MaxMind.Db.Constructor attribute");
            }

            if (constructors.Count == 1)
            {
                return ConstructorBasedActivator(constructors[0]);
            }

            return PropertyBasedActivator(expectedType);
        }

        private static TypeActivator ConstructorBasedActivator(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            var paramNameTypes = new Dictionary<Key, DeserializationMember>();
            var injectables = new List<KeyValuePair<string, DeserializationMember>>();
            var networkParams = new List<DeserializationMember>();
            var alwaysCreated = new List<DeserializationMember>();
            foreach (var param in parameters)
            {
                var member = new DeserializationMember(param);

                var injectableAttribute = param.GetCustomAttributes<InjectAttribute>().FirstOrDefault();
                if (injectableAttribute != null)
                {
                    injectables.Add(new KeyValuePair<string, DeserializationMember>(injectableAttribute.Name, member));
                }
                var networkAttribute = param.GetCustomAttributes<NetworkAttribute>().FirstOrDefault();
                if (networkAttribute != null)
                {
                    networkParams.Add(member);
                }
                var paramAttribute = param.GetCustomAttributes<MapKeyAttribute>().FirstOrDefault();
                string? name;
                if (paramAttribute != null)
                {
                    name = paramAttribute.Name;
                    if (paramAttribute.AlwaysCreate)
                    {
                        alwaysCreated.Add(member);
                    }
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
                paramNameTypes.Add(new Key(bytes), member);
            }
            var activator = ReflectionUtil.CreateActivator(constructor);
            return new TypeActivator(activator, paramNameTypes, injectables.ToArray(),
                networkParams.ToArray(), alwaysCreated.ToArray());
        }

        private static TypeActivator PropertyBasedActivator(Type expectedType)
        {
            var parameterlessCtor = expectedType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);

            if (parameterlessCtor == null)
            {
                throw new DeserializationException(
                    $"No constructor found for {expectedType} with the MaxMind.Db.Constructor attribute "
                    + "and no parameterless constructor found for property-based activation");
            }

            var properties = expectedType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.IsDefined(typeof(MapKeyAttribute), true)
                         || p.IsDefined(typeof(InjectAttribute), true)
                         || p.IsDefined(typeof(NetworkAttribute), true))
                .OrderBy(p => p.Name)
                .ToArray();

            if (properties.Length == 0)
            {
                throw new DeserializationException(
                    $"No properties found on {expectedType} with the MapKey, Inject, or Network "
                    + "attributes for property-based activation");
            }

            var paramNameTypes = new Dictionary<Key, DeserializationMember>();
            var injectables = new List<KeyValuePair<string, DeserializationMember>>();
            var networkParams = new List<DeserializationMember>();
            var alwaysCreated = new List<DeserializationMember>();
            var orderedProperties = new List<PropertyInfo>();

            var position = 0;
            foreach (var prop in properties)
            {
                if (!prop.CanWrite)
                {
                    throw new DeserializationException(
                        $"Property {prop.Name} on {expectedType} must have a setter or init accessor "
                        + "for property-based activation");
                }

                var member = new DeserializationMember(position, prop.PropertyType, prop.Name);

                var injectableAttribute = prop.GetCustomAttributes<InjectAttribute>().FirstOrDefault();
                if (injectableAttribute != null)
                {
                    injectables.Add(new KeyValuePair<string, DeserializationMember>(injectableAttribute.Name, member));
                }
                var networkAttribute = prop.GetCustomAttributes<NetworkAttribute>().FirstOrDefault();
                if (networkAttribute != null)
                {
                    networkParams.Add(member);
                }
                var paramAttribute = prop.GetCustomAttributes<MapKeyAttribute>().FirstOrDefault();
                string? name;
                if (paramAttribute != null)
                {
                    name = paramAttribute.Name;
                    if (paramAttribute.AlwaysCreate)
                    {
                        alwaysCreated.Add(member);
                    }
                }
                else
                {
                    name = prop.Name;
                }
                var bytes = Encoding.UTF8.GetBytes(name);
                paramNameTypes.Add(new Key(bytes), member);
                orderedProperties.Add(prop);
                position++;
            }

            // Compute defaults from a temporary instance so property initializers
            // are preserved (e.g., = new Dictionary<string,string>()).
            object? tempInstance;
            try
            {
                tempInstance = parameterlessCtor.Invoke(null);
            }
            catch (TargetInvocationException ex)
            {
                throw new DeserializationException(
                    $"The parameterless constructor for {expectedType} threw an exception "
                    + "during property-based activation default value detection",
                    ex.InnerException ?? ex);
            }
            var defaultParameters = new object?[orderedProperties.Count];
            for (var i = 0; i < orderedProperties.Count; i++)
            {
                defaultParameters[i] = orderedProperties[i].GetValue(tempInstance);
            }
            // Override AlwaysCreate defaults to null so SetAlwaysCreatedParams triggers.
            foreach (var ac in alwaysCreated)
            {
                defaultParameters[ac.Position] = null;
            }

            var activator = ReflectionUtil.CreateMemberInitActivator(
                parameterlessCtor, orderedProperties.ToArray());
            return new TypeActivator(activator, paramNameTypes, injectables.ToArray(),
                networkParams.ToArray(), alwaysCreated.ToArray(), defaultParameters);
        }
    }
}
