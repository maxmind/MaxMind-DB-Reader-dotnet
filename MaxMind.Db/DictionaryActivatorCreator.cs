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
            new();

        internal ObjectActivator GetActivator(Type expectedType)
            => _dictActivators.GetOrAdd(expectedType, DictionaryActivator);

#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
            "AOT",
            "IL3050",
            Justification = "Generated dictionary registrations return before this runtime generic construction path. This path serves only the documented fallback for unregistered dictionary types, which is unsupported in NativeAOT applications.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
            "Trimming",
            "IL2070",
            Justification = "Generated dictionary registrations return before this reflection path. This path serves only the documented fallback for unregistered dictionary types, which is unsupported in trimmed applications.")]
#endif
        private static ObjectActivator DictionaryActivator(Type expectedType)
        {
            var genericArgs = expectedType.GetGenericArguments();
            ConstructorInfo? constructor;
            if (expectedType.GetTypeInfo().IsInterface)
            {
                var dictType = typeof(Dictionary<,>).MakeGenericType(genericArgs);
                ReflectionUtil.CheckType(expectedType, dictType);
                constructor = dictType.GetConstructor([typeof(int)]);
            }
            else
            {
                ReflectionUtil.CheckType(typeof(IDictionary), expectedType);
                constructor = expectedType.GetConstructor(Type.EmptyTypes);
            }
            if (constructor == null)
                throw new DeserializationException($"Unable to find default constructor for {expectedType}");
            return ReflectionUtil.CreateActivator(constructor);
        }
    }
}
