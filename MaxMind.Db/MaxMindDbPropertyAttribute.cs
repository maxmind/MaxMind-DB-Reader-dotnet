#region

using System;

#endregion

namespace MaxMind.Db
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class MaxMindDbPropertyAttribute : Attribute
    {
        public string PropertyName { get; set; }

        public MaxMindDbPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}