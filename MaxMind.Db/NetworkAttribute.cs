#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <code>Reader</code> to set the parameter to be the network in CIDR format.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NetworkAttribute : Attribute
    {
        /// <summary>
        ///     Create a new instance of <code>NetworkAttribute</code>.
        /// </summary>
        public NetworkAttribute()
        {
        }
    }
}