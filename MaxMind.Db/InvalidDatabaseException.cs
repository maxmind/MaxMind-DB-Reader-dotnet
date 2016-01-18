#region

using System;
using System.Runtime.Serialization;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Thrown when the MaxMind database file is incorrectly formatted
    /// </summary>
    [Serializable]
    public sealed class InvalidDatabaseException : Exception
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="InvalidDatabaseException" /> class.
        /// </summary>
        /// <param name="message">A message that describes the error.</param>
        public InvalidDatabaseException(string message)
            : base(message)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="InvalidDatabaseException" /> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">
        ///     The exception that is the cause of the current exception. If the
        ///     <paramref name="innerException" /> parameter is not a null reference, the current exception is raised in a catch
        ///     block that handles the inner exception.
        /// </param>
        public InvalidDatabaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        ///     Constructor for deserialization.
        /// </summary>
        /// <param name="info">The SerializationInfo with data.</param>
        /// <param name="context">The source for this deserialization.</param>
        private InvalidDatabaseException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}