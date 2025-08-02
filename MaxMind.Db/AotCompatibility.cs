#if NET8_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;

namespace MaxMind.Db
{
    /// <summary>
    /// Provides AOT compatibility checks and utilities
    /// </summary>
    internal static class AotCompatibility
    {
        /// <summary>
        /// Determines if the runtime supports dynamic code generation
        /// </summary>
        public static bool IsDynamicCodeSupported { get; } = RuntimeFeature.IsDynamicCodeSupported;

        /// <summary>
        /// Determines if we should use AOT-optimized paths
        /// </summary>
        public static bool UseAotOptimizations { get; } = !RuntimeFeature.IsDynamicCodeSupported
#if NATIVEAOT
            || true  // Force AOT mode when compiled with NATIVEAOT flag
#endif
            ;

        /// <summary>
        /// Determines if we should prefer source-generated activators over reflection
        /// This is separate from UseAotOptimizations and should be true whenever possible
        /// </summary>
        public static bool PreferSourceGeneratedActivators { get; } = true;

        /// <summary>
        /// Checks if generated type activators are available
        /// </summary>
        internal static bool HasGeneratedActivators()
        {
            try
            {
                // Try to trigger the static constructor to register activators
                Generated.TypeActivatorRegistration.EnsureRegistered();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Try to create an instance using generated activators
        /// </summary>
        internal static bool TryCreateInstanceAot(Type type, object?[] args, out object? instance)
        {
            return SourceGeneratorSupport.TryCreateInstance(type, args, out instance);
        }

        /// <summary>
        /// Check if a type is supported by generated activators
        /// </summary>
        internal static bool IsTypeSupportedAot(Type type)
        {
            return SourceGeneratorSupport.HasActivator(type);
        }
    }
}

#endif