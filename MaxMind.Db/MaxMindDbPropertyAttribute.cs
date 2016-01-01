#region

using System;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    /// Instruct <code>Reader</code> to map database key to constructor parameter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class MaxMindDbPropertyAttribute : Attribute
    {
        /// <summary>
        /// The name to use for the property.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Create a new instance of <code>MaxMindDbPropertyAttribute</code>.
        /// </summary>
        /// <param name="propertyName"></param>
        public MaxMindDbPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}