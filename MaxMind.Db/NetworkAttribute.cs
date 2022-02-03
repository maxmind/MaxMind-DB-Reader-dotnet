#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <c>Reader</c> to set the parameter to be the network in CIDR format.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NetworkAttribute : Attribute
    {
        /// <summary>
        ///     Create a new instance of <c>NetworkAttribute</c>.
        /// </summary>
        public NetworkAttribute()
        {
        }
    }
}