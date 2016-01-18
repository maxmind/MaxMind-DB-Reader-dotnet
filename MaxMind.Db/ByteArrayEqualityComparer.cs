#region

using System;
using System.Collections.Generic;

#endregion

namespace MaxMind.Db
{
    internal class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == null || y == null)
            {
                return false;
            }
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x.Length != y.Length)
            {
                return false;
            }
            for (var i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            var result = 17;
            foreach (var b in bytes)
            {
                result = result * 31 + b;
            }
            return result;
        }
    }
}