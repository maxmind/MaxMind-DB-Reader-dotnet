using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;

namespace MaxMind.Db
{
    /// <summary>
    /// Public API for source-generated type activators to register themselves
    /// </summary>
    public static class SourceGeneratorSupport
    {
        private static readonly ConcurrentDictionary<Type, Func<object?[], object>> RegisteredActivators = new();

        /// <summary>
        /// Register a complete source-generated activator with all metadata for optimal performance
        /// </summary>
        /// <param name="type">The type this activator creates</param>
        /// <param name="activator">The activator function</param>
        /// <param name="parameterMappings">Pre-computed parameter mappings</param>
        /// <param name="injectableMappings">Injectable parameter mappings</param>
        /// <param name="networkParameterPositions">Network parameter positions</param>
        /// <param name="alwaysCreatedParameterPositions">Always created parameter positions</param>
        public static void RegisterCompleteActivator(
            Type type,
            Func<object?[], object> activator,
            FrozenDictionary<string, (int Position, Type ParameterType)> parameterMappings,
            FrozenDictionary<string, int> injectableMappings,
            int[] networkParameterPositions,
            int[] alwaysCreatedParameterPositions)
        {
            // For now, just register the activator - the complete metadata could be used for further optimizations
            RegisteredActivators[type] = activator;

            // Metadata could be used for future optimizations if needed
        }

        /// <summary>
        /// Check if a type has a registered source-generated activator
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type has a registered activator</returns>
        public static bool HasActivator(Type type)
        {
#if DEBUG
            if (AreSourceGeneratorsDisabled)
                return false;
#endif
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

#if DEBUG
        private static readonly ThreadLocal<bool> DisableSourceGenerators = new ThreadLocal<bool>(() => false);

        /// <summary>
        /// Get a snapshot of currently registered activators for testing purposes
        /// </summary>
        /// <returns>Dictionary of registered activators</returns>
        internal static Dictionary<Type, Func<object?[], object>> GetRegisteredActivators()
        {
            return new Dictionary<Type, Func<object?[], object>>(RegisteredActivators);
        }

        /// <summary>
        /// Clear all registered activators for testing purposes
        /// </summary>
        internal static void ClearRegisteredActivators()
        {
            RegisteredActivators.Clear();
        }

        /// <summary>
        /// Restore registered activators for testing purposes
        /// </summary>
        /// <param name="activators">Activators to restore</param>
        internal static void RestoreRegisteredActivators(Dictionary<Type, Func<object?[], object>> activators)
        {
            RegisteredActivators.Clear();
            foreach (var kvp in activators)
            {
                RegisteredActivators[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Temporarily disable source generators for the current thread
        /// </summary>
        /// <returns>Disposable that restores the previous state</returns>
        internal static IDisposable DisableSourceGeneratorsForCurrentThread()
        {
            var previousValue = DisableSourceGenerators.Value;
            DisableSourceGenerators.Value = true;
            return new SourceGeneratorDisabler(previousValue);
        }

        private sealed class SourceGeneratorDisabler : IDisposable
        {
            private readonly bool _previousValue;

            public SourceGeneratorDisabler(bool previousValue)
            {
                _previousValue = previousValue;
            }

            public void Dispose()
            {
                DisableSourceGenerators.Value = _previousValue;
            }
        }

        /// <summary>
        /// Check if source generators are disabled for the current thread
        /// </summary>
        internal static bool AreSourceGeneratorsDisabled => DisableSourceGenerators.Value;
#endif
    }
}