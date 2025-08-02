#if NET8_0_OR_GREATER

using System;
using System.Collections.Concurrent;

namespace MaxMind.Db
{
    /// <summary>
    /// Public API for source-generated type activators to register themselves
    /// </summary>
    public static class SourceGeneratorSupport
    {
        private static readonly ConcurrentDictionary<Type, Func<object?[], object>> RegisteredActivators = new();

        /// <summary>
        /// Register a source-generated activator for a specific type
        /// </summary>
        /// <param name="type">The type this activator creates</param>
        /// <param name="activator">The activator function</param>
        public static void RegisterActivator(Type type, Func<object?[], object> activator)
        {
            RegisteredActivators[type] = activator;
        }

        /// <summary>
        /// Check if a type has a registered source-generated activator
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type has a registered activator</returns>
        public static bool HasActivator(Type type)
        {
            return RegisteredActivators.ContainsKey(type);
        }

        /// <summary>
        /// Try to create an instance using a registered activator
        /// </summary>
        /// <param name="type">The type to create</param>
        /// <param name="args">Constructor arguments</param>
        /// <param name="instance">The created instance if successful</param>
        /// <returns>True if creation was successful</returns>
        public static bool TryCreateInstance(Type type, object?[] args, out object? instance)
        {
            instance = null;
            if (RegisteredActivators.TryGetValue(type, out var activator))
            {
                try
                {
                    instance = activator(args);
                    return true;
                }
                catch
                {
                    // Fall back to reflection
                }
            }
            return false;
        }
    }
}

#endif