#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <c>Reader</c> to map database key to constructor parameter.
    /// </summary>
    /// <param name="parameterName">The name to use for the property.</param>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class InjectAttribute(string parameterName) : Attribute
    {
        /// <summary>
        ///     The name to use for the property.
        /// </summary>
        public string ParameterName { get; } = parameterName;
    }
}