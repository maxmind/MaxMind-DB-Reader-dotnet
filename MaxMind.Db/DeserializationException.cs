#region

using System;
using System.Runtime.Serialization;
#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Thrown when there is an error deserializing to the provided type.
    /// </summary>
    [Serializable]
    public sealed class DeserializationException : Exception
    {
        /// <summary>
        ///     Construct a DeserializationException
        /// </summary>
        public DeserializationException() : base()
        {
        }

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

        /// <summary>
        ///     Construct a DeserializationException
        /// </summary>
        /// <param name="info">The SerializationInfo with data.</param>
        /// <param name="context">The source for this deserialization.</param>
        private DeserializationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
