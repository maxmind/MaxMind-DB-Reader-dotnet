#region

using System;

#endregion

namespace MaxMind.Db
{
    public class DeserializationException : Exception
    {
        public DeserializationException(string message)
            : base(message)
        {
        }
    }
}