#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <c>Reader</c> to use the constructor when deserializing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class ConstructorAttribute : Attribute
    {
    }
}