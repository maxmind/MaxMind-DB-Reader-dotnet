#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

#endregion

namespace MaxMind.Db
{
    internal sealed class DictionaryActivatorCreator
    {
        private readonly ConcurrentDictionary<Type, ObjectActivator> _dictActivators =
            new ConcurrentDictionary<Type, ObjectActivator>();

        internal ObjectActivator GetActivator(Type expectedType)
            => _dictActivators.GetOrAdd(expectedType, DictionaryActivator);

        private static ObjectActivator DictionaryActivator(Type expectedType)
        {
            var genericArgs = expectedType.GetGenericArguments();
            if (genericArgs.Length != 2)
                throw new DeserializationException(
                    $"Unexpected number of Dictionary generic arguments: {genericArgs.Length}");
            ConstructorInfo constructor;
            if (expectedType.IsInterface)
            {
                var dictType = typeof(Dictionary<,>).MakeGenericType(genericArgs);
                ReflectionUtil.CheckType(expectedType, dictType);
                constructor = dictType.GetConstructor(new[] { typeof(int) });
            }
            else
            {
                ReflectionUtil.CheckType(typeof(IDictionary), expectedType);
                constructor = expectedType.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                    throw new DeserializationException($"Unable to find default constructor for {expectedType}");
            }
            var activator = ReflectionUtil.CreateActivator(constructor);
            return activator;
        }
    }
}