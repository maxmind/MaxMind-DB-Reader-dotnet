#region

using System;
using System.Linq.Expressions;
using System.Reflection;

#endregion

namespace MaxMind.Db
{
    internal delegate object ObjectActivator(params object?[] args);

    internal static class ReflectionUtil
    {
        // Activator.CreateInstance is extremely slow and ConstructorInfo.Invoke is
        // somewhat slow. This faster alternative (when cached) is largely based off
        // of:
        // http://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
        internal static ObjectActivator CreateActivator(ConstructorInfo constructor)
        {
            if (constructor == null)
            {
                throw new ArgumentNullException(nameof(constructor));
            }
            var paramInfo = constructor.GetParameters();

            var paramExp = Expression.Parameter(typeof(object[]), "args");

            var argsExp = new Expression[paramInfo.Length];
            for (var i = 0; i < paramInfo.Length; i++)
            {
                var index = Expression.Constant(i);
                var paramType = paramInfo[i].ParameterType;
                var accessorExp = Expression.ArrayIndex(paramExp, index);
                var castExp = Expression.Convert(accessorExp, paramType);
                argsExp[i] = castExp;
            }

            var newExp = Expression.New(constructor, argsExp);
            var lambda = Expression.Lambda(typeof(ObjectActivator), newExp, paramExp);
            return (ObjectActivator)lambda.Compile();
        }

        /// <summary>
        ///     Creates a compiled activator that uses <c>MemberInit</c> expressions
        ///     to set properties on an object created via a parameterless constructor.
        ///     This works with <c>init</c>-only setters because <c>init</c> is a
        ///     compiler-only restriction, not enforced by the CLR.
        /// </summary>
        internal static ObjectActivator CreateMemberInitActivator(
            ConstructorInfo parameterlessCtor,
            PropertyInfo[] properties)
        {
            if (parameterlessCtor == null)
            {
                throw new ArgumentNullException(nameof(parameterlessCtor));
            }
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            var paramExp = Expression.Parameter(typeof(object[]), "args");

            var bindings = new MemberBinding[properties.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                var index = Expression.Constant(i);
                var accessorExp = Expression.ArrayIndex(paramExp, index);
                var castExp = Expression.Convert(accessorExp, properties[i].PropertyType);
                bindings[i] = Expression.Bind(properties[i], castExp);
            }

            var newExp = Expression.MemberInit(Expression.New(parameterlessCtor), bindings);
            var lambda = Expression.Lambda(typeof(ObjectActivator), newExp, paramExp);
            return (ObjectActivator)lambda.Compile();
        }

        internal static void CheckType(Type expected, Type from)
        {
            if (!expected.IsAssignableFrom(from))
            {
                throw new DeserializationException($"Could not convert '{from}' to '{expected}'.");
            }
        }
    }
}