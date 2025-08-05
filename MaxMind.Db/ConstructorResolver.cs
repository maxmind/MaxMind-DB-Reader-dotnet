using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace MaxMind.Db
{
    /// <summary>
    /// Enhanced constructor resolution with comprehensive validation
    /// </summary>
    internal static class ConstructorResolver
    {
        /// <summary>
        /// Resolves the constructor for a type with comprehensive validation
        /// </summary>
        public static ConstructorInfo ResolveConstructor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] Type type)
        {
            var constructors = GetEligibleConstructors(type);

            ValidateConstructorCount(type, constructors);

            var constructor = constructors[0];
            ValidateConstructorSignature(type, constructor);

            return constructor;
        }

        /// <summary>
        /// Gets all constructors marked with ConstructorAttribute
        /// </summary>
        private static List<ConstructorInfo> GetEligibleConstructors(Type type)
        {
            try
            {
                return type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(c => c.IsDefined(typeof(ConstructorAttribute), true))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw ReflectionErrorHandling.CreateDeserializationException(
                    "Failed to retrieve constructors",
                    type,
                    innerException: ex);
            }
        }

        /// <summary>
        /// Validates that exactly one constructor is available
        /// </summary>
        private static void ValidateConstructorCount(Type type, List<ConstructorInfo> constructors)
        {
            if (constructors.Count == 0)
            {
                throw ReflectionErrorHandling.CreateDeserializationException(
                    "No constructors found with MaxMind.Db.Constructor attribute",
                    type);
            }

            if (constructors.Count > 1)
            {
                var constructorSignatures = constructors.Select(c => GetConstructorSignature(c));
                var message = $"Multiple constructors found with MaxMind.Db.Constructor attribute. Signatures: {string.Join(", ", constructorSignatures)}";

                throw ReflectionErrorHandling.CreateDeserializationException(message, type);
            }
        }

        /// <summary>
        /// Validates constructor signature for common issues
        /// </summary>
        private static void ValidateConstructorSignature(Type type, ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();

            // Check for duplicate parameter names (case-insensitive to catch common errors)
            var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateNames = new List<string>();

            foreach (var param in parameters)
            {
                var effectiveName = GetEffectiveParameterName(param);
                if (effectiveName != null)
                {
                    if (!parameterNames.Add(effectiveName))
                    {
                        duplicateNames.Add(effectiveName);
                    }
                }
            }

            if (duplicateNames.Count > 0)
            {
                var message = $"Duplicate parameter names found (case-insensitive): {string.Join(", ", duplicateNames)}";
                throw ReflectionErrorHandling.CreateDeserializationException(message, type);
            }

            // Validate parameter attributes
            foreach (var param in parameters)
            {
                ValidateParameterAttributes(type, param);
            }
        }

        /// <summary>
        /// Gets the effective parameter name considering ParameterAttribute
        /// </summary>
        private static string? GetEffectiveParameterName(ParameterInfo parameter)
        {
            var paramAttribute = parameter.GetCustomAttribute<ParameterAttribute>();
            return paramAttribute?.ParameterName ?? parameter.Name;
        }

        /// <summary>
        /// Validates parameter attributes for consistency
        /// </summary>
        private static void ValidateParameterAttributes(Type type, ParameterInfo parameter)
        {
            var attributes = new List<Attribute>();

            var paramAttr = parameter.GetCustomAttribute<ParameterAttribute>();
            var injectAttr = parameter.GetCustomAttribute<InjectAttribute>();
            var networkAttr = parameter.GetCustomAttribute<NetworkAttribute>();

            if (paramAttr != null) attributes.Add(paramAttr);
            if (injectAttr != null) attributes.Add(injectAttr);
            if (networkAttr != null) attributes.Add(networkAttr);

            // Check for conflicting attributes
            if (attributes.Count > 1)
            {
                var attributeNames = attributes.Select(a => a.GetType().Name);
                var message = $"Parameter '{parameter.Name}' has conflicting attributes: {string.Join(", ", attributeNames)}";
                throw ReflectionErrorHandling.CreateDeserializationException(message, type, parameter.Name);
            }

            // Validate ParameterAttribute
            if (paramAttr != null)
            {
                if (string.IsNullOrWhiteSpace(paramAttr.ParameterName))
                {
                    throw ReflectionErrorHandling.CreateDeserializationException(
                        $"Parameter '{parameter.Name}' has ParameterAttribute with null or empty ParameterName",
                        type,
                        parameter.Name);
                }
            }

            // Validate InjectAttribute
            if (injectAttr != null)
            {
                if (string.IsNullOrWhiteSpace(injectAttr.ParameterName))
                {
                    throw ReflectionErrorHandling.CreateDeserializationException(
                        $"Parameter '{parameter.Name}' has InjectAttribute with null or empty ParameterName",
                        type,
                        parameter.Name);
                }
            }
        }

        /// <summary>
        /// Gets a human-readable constructor signature for error messages
        /// </summary>
        private static string GetConstructorSignature(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            var paramStrings = parameters.Select(p => $"{p.ParameterType.Name} {p.Name}");
            return $"{constructor.DeclaringType?.Name}({string.Join(", ", paramStrings)})";
        }
    }
}