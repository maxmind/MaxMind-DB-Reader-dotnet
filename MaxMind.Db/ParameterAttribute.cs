#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <c>Reader</c> to map database key to constructor parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="alwaysCreate">
    ///     Whether to create the object even if the key
    ///     is not present in the database. If this is false, the default value will be used
    ///     (null for nullable types)
    /// </param>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ParameterAttribute(string parameterName, bool alwaysCreate = false) : Attribute
    {
        /// <summary>
        ///     The name to use for the property.
        /// </summary>
        public string ParameterName { get; } = parameterName;

        /// <summary>
        ///     Whether to create the object even if the key is not present in
        ///     the database. If this is false, the default value will be used
        ///     (null for nullable types).
        /// </summary>
        public bool AlwaysCreate { get; } = alwaysCreate;
    }
}