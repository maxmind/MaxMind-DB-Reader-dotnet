#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

#endregion

namespace MaxMind.Db
{
    internal sealed class ListActivatorCreator
    {
        private readonly ConcurrentDictionary<Type, ObjectActivator> _listActivators =
            new ConcurrentDictionary<Type, ObjectActivator>();

        internal ObjectActivator GetActivator(Type expectedType)
            => _listActivators.GetOrAdd(expectedType, ListActivator);

        private static ObjectActivator ListActivator(Type expectedType)
        {
            var genericArgs = expectedType.GetGenericArguments();
            Type argType;
            switch (genericArgs.Length)
            {
                case 0:
                    argType = typeof(object);
                    break;

                case 1:
                    argType = genericArgs[0];
                    break;

                default:
                    throw new DeserializationException(
                        $"Unexpected number of generic arguments for list: {genericArgs.Length}");
            }

            ConstructorInfo constructor;
            var interfaceType = typeof(ICollection<>).MakeGenericType(argType);
            var listType = typeof(List<>).MakeGenericType(argType);
            if (expectedType.IsAssignableFrom(listType))
            {
                constructor = listType.GetConstructor(new[] { typeof(int) });
            }
            else
            {
                ReflectionUtil.CheckType(interfaceType, expectedType);
                constructor = expectedType.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                    throw new DeserializationException($"Unable to find default constructor for {expectedType}");
            }
            var activator = ReflectionUtil.CreateActivator(constructor);
            return activator;
        }
    }
}