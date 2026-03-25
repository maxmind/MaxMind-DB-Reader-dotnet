#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <c>Reader</c> to inject a runtime value into a constructor
    ///     parameter or property during deserialization.
    /// </summary>
    /// <param name="name">The injectable value name.</param>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class InjectAttribute(string name) : Attribute
    {
        /// <summary>
        ///     The injectable value name.
        /// </summary>
        public string Name { get; } = name;
    }
}