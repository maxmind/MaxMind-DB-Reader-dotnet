using System;
using System.Text;

namespace MaxMind.Db
{
    /// <summary>
    /// Enhanced error handling for reflection operations
    /// </summary>
    internal static class ReflectionErrorHandling
    {
        /// <summary>
        /// Creates a detailed deserialization exception with context information
        /// </summary>
        public static DeserializationException CreateDeserializationException(
            string message,
            Type? targetType = null,
            string? memberName = null,
            Exception? innerException = null)
        {
            var fullMessage = BuildDetailedErrorMessage(message, targetType, memberName);
            return innerException == null
                ? new DeserializationException(fullMessage)
                : new DeserializationException(fullMessage, innerException);
        }

        /// <summary>
        /// Creates a detailed invalid database exception with context information
        /// </summary>
        public static InvalidDatabaseException CreateInvalidDatabaseException(
            string message,
            Type? targetType = null,
            string? memberName = null,
            Exception? innerException = null)
        {
            var fullMessage = BuildDetailedErrorMessage(message, targetType, memberName);
            return innerException == null
                ? new InvalidDatabaseException(fullMessage)
                : new InvalidDatabaseException(fullMessage, innerException);
        }

        /// <summary>
        /// Builds a detailed error message with type and member context
        /// </summary>
        private static string BuildDetailedErrorMessage(string message, Type? targetType, string? memberName)
        {
            var sb = new StringBuilder(message);

            if (targetType != null)
            {
                sb.Append(" (Type: ");
                AppendFriendlyTypeName(sb, targetType);
                sb.Append(')');
            }

            if (memberName != null)
            {
                sb.Append(" (Member: ");
                sb.Append(memberName);
                sb.Append(')');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Appends a user-friendly type name, handling generic types properly
        /// </summary>
        private static void AppendFriendlyTypeName(StringBuilder sb, Type type)
        {
            if (type.IsGenericType)
            {
                var name = type.Name;
                var backtickIndex = name.IndexOf('`');
                if (backtickIndex >= 0)
                {
                    sb.Append(name.AsSpan(0, backtickIndex));
                }
                else
                {
                    sb.Append(name);
                }

                sb.Append('<');
                var genericArgs = type.GetGenericArguments();
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    AppendFriendlyTypeName(sb, genericArgs[i]);
                }
                sb.Append('>');
            }
            else
            {
                sb.Append(type.Name);
            }
        }
    }
}