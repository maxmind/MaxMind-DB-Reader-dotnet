using System;

namespace MaxMind.MaxMindDb
{
    /// <summary>
    /// Thrown when the MaxMind database file is incorrectly formatted
    /// </summary>
    public class InvalidDatabaseException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidDatabaseException"/> class.
        /// </summary>
        /// <param name="message">A message that describes the error.</param>
        public InvalidDatabaseException(string message) : base(message)
        {
            
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidDatabaseException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception. If the <paramref name="innerException" /> parameter is not a null reference, the current exception is raised in a catch block that handles the inner exception.</param>
        public InvalidDatabaseException(string message, Exception innerException) : base(message, innerException)
        {
            
        }
    }
}