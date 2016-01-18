#region

using System.Collections.Generic;

#endregion

namespace MaxMind.Db
{
    /// <summary>
    ///     Values to be injected into classes during deserialization.
    /// </summary>
    public class InjectableValues
    {
        internal IDictionary<string, object> Values { get; } = new Dictionary<string, object>();

        /// <summary>
        ///     Add a value to be injected into the class during serialization
        /// </summary>
        /// <param name="key">
        ///     The key name as set with the <c>InectAttribute</c> used to determine
        ///     where to inject the value.
        /// </param>
        /// <param name="value">The value to be injected.</param>
        public void AddValue(string key, object value)
        {
            Values.Add(key, value);
        }
    }
}