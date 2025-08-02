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
        /// Checks if generated type activators are available
        /// </summary>
        internal static bool HasGeneratedActivators()
        {
#if NATIVEAOT
            // When compiled for AOT, check if the generated type exists
            var generatedType = Type.GetType("MaxMind.Db.Generated.TypeActivators, MaxMind.Db");
            return generatedType != null;
#else
            return false;
#endif
        }

        /// <summary>
        /// Try to create an instance using generated activators
        /// </summary>
        internal static bool TryCreateInstanceAot(Type type, object?[] args, out object? instance)
        {
            instance = null;
#if NATIVEAOT
            try
            {
                var generatedType = Type.GetType("MaxMind.Db.Generated.TypeActivators, MaxMind.Db");
                if (generatedType != null)
                {
                    var method = generatedType.GetMethod("CreateInstance");
                    if (method != null)
                    {
                        instance = method.Invoke(null, new object[] { type, args });
                        return true;
                    }
                }
            }
            catch
            {
                // Fall back to reflection-based approach
            }
#endif
            return false;
        }

        /// <summary>
        /// Check if a type is supported by generated activators
        /// </summary>
        internal static bool IsTypeSupportedAot(Type type)
        {
#if NATIVEAOT
            try
            {
                var generatedType = Type.GetType("MaxMind.Db.Generated.TypeActivators, MaxMind.Db");
                if (generatedType != null)
                {
                    var method = generatedType.GetMethod("IsTypeSupported");
                    if (method != null)
                    {
                        return (bool)method.Invoke(null, new object[] { type })!;
                    }
                }
            }
            catch
            {
                // Type not supported
            }
#endif
            return false;
        }
    }
}

#endif