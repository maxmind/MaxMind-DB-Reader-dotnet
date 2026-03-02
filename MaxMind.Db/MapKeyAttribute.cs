#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Instruct <c>Reader</c> to map a MaxMind DB map key to a constructor
    ///     parameter or property.
    /// </summary>
    /// <param name="name">The key name in the MaxMind DB map.</param>
    /// <param name="alwaysCreate">
    ///     Whether to create the object even if the key
    ///     is not present in the database. If this is false, the default value will be used
    ///     (null for nullable types).
    /// </param>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public class MapKeyAttribute(string name, bool alwaysCreate = false) : Attribute
    {
        /// <summary>
        ///     The key name in the MaxMind DB map.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        ///     Whether to create the object even if the key is not present in
        ///     the database. If this is false, the default value will be used
        ///     (null for nullable types).
        /// </summary>
        public bool AlwaysCreate { get; } = alwaysCreate;
    }

    /// <summary>
    ///     Deprecated. Use <see cref="MapKeyAttribute"/> instead.
    /// </summary>
    /// <param name="parameterName">The key name in the MaxMind DB map.</param>
    /// <param name="alwaysCreate">
    ///     Whether to create the object even if the key
    ///     is not present in the database. If this is false, the default value will be used
    ///     (null for nullable types).
    /// </param>
    [Obsolete("Use MapKeyAttribute instead.")]
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class ParameterAttribute(string parameterName, bool alwaysCreate = false)
        : MapKeyAttribute(parameterName, alwaysCreate);
}
