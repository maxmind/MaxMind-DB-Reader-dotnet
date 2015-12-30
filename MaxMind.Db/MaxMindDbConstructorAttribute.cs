#region

using System;

#endregion

namespace MaxMind.Db
{
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class MaxMindDbConstructorAttribute : Attribute
    {
    }
}