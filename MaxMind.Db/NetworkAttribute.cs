#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <c>Reader</c> to set the parameter or property to be the network in CIDR format.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class NetworkAttribute : Attribute;

}