#region

using System;
#if !NETSTANDARD1_4
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
#endif
#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Thrown when there is an error deserializing to the provided type.
    /// </summary>
#if !NETSTANDARD1_4
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors")]
    [Serializable]
#endif
    public sealed class DeserializationException : Exception
    {
        /// <summary>
        ///     Construct a DeserializationException
        /// </summary>
        /// <param name="message"></param>
        public DeserializationException(string message)
            : base(message)
        {
        }

        /// <summary>
        ///     Construct a DeserializationException
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException">The underlying exception that caused this one.</param>
        public DeserializationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if !NETSTANDARD1_4
        /// <summary>
        ///     Constructor for deserialization.
        /// </summary>
        /// <param name="info">The SerializationInfo with data.</param>
        /// <param name="context">The source for this deserialization.</param>
        private DeserializationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}
