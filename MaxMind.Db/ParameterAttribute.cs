#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <code>Reader</code> to map database key to constructor parameter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ParameterAttribute : Attribute
    {
        /// <summary>
        ///     The name to use for the property.
        /// </summary>
        public string ParameterName { get; }

        /// <summary>
        ///     Whether to create the object even if the key is not present in
        ///     the database. If this is false, the default value will be used
        ///     (null for nullable types).
        /// </summary>
        public bool AlwaysCreate { get; }

        /// <summary>
        ///     Create a new instance of <code>ParameterAttribute</code>.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="alwaysCreate">
        ///     Whether to create the object even if the key
        ///     is not present in the database. If this is false, the default value will be used
        ///     (null for nullable types)
        /// </param>
        public ParameterAttribute(string parameterName, bool alwaysCreate = false)
        {
            ParameterName = parameterName;
            AlwaysCreate = alwaysCreate;
        }
    }
}