#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    /// Thrown when there is an error deserializing to the provided type.
    /// </summary>
    public class DeserializationException : Exception
    {
        /// <summary>
        /// Construct a DeserializationException
        /// </summary>
        /// <param name="message"></param>
        public DeserializationException(string message)
            : base(message)
        {
        }
    }
}