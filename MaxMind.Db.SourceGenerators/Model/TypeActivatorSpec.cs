using System;
using System.Collections.Immutable;
using System.Linq;

namespace MaxMind.Db.SourceGenerators.Model
{
    /// <summary>
    /// Immutable specification for a type that needs a source-generated activator.
    /// Implements structural equality for efficient incremental compilation.
    /// </summary>
    internal sealed record TypeActivatorSpec
    {
        public required TypeRef TypeRef { get; init; }
        public required string ActivatorMethodName { get; init; }
        public required ImmutableEquatableArray<ParameterSpec> Parameters { get; init; }
        public required ConstructorAccessibility Accessibility { get; init; }
        public required string Namespace { get; init; }
        public required bool IsGlobalNamespace { get; init; }
    }

    /// <summary>
    /// Immutable parameter specification with all metadata needed for code generation.
    /// </summary>
    internal sealed record ParameterSpec
    {
        public required string Name { get; init; }
        public required TypeRef Type { get; init; }
        public required string? DatabaseParameterName { get; init; }
        public required int Position { get; init; }
        public required bool IsNullable { get; init; }
        public required bool IsNetwork { get; init; }
        public required bool IsAlwaysCreated { get; init; }
        public required bool IsInjectable { get; init; }
        public required string? InjectableParameterName { get; init; }
    }

    /// <summary>
    /// Immutable type reference that can be safely used across compilation units.
    /// </summary>
    internal sealed record TypeRef
    {
        public required string Name { get; init; }
        public required string FullyQualifiedName { get; init; }
        public required string Namespace { get; init; }
        public required bool IsGlobalNamespace { get; init; }
        public required bool IsGeneric { get; init; }
        public required ImmutableArray<TypeRef> GenericArguments { get; init; } = ImmutableArray<TypeRef>.Empty;
    }

    /// <summary>
    /// Constructor accessibility levels for code generation decisions.
    /// </summary>
    internal enum ConstructorAccessibility
    {
        Public,
        Internal,
        Protected,
        Private
    }

    /// <summary>
    /// Immutable array wrapper that implements value-based equality.
    /// Essential for incremental source generation caching.
    /// </summary>
    internal readonly struct ImmutableEquatableArray<T> : IEquatable<ImmutableEquatableArray<T>>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _array;

        public ImmutableEquatableArray(ImmutableArray<T> array)
        {
            _array = array.IsDefault ? ImmutableArray<T>.Empty : array;
        }

        public ImmutableArray<T> AsImmutableArray() => _array;

        public int Length => _array.Length;
        public T this[int index] => _array[index];

        public bool Equals(ImmutableEquatableArray<T> other)
        {
            return _array.SequenceEqual(other._array);
        }

        public override bool Equals(object? obj)
        {
            return obj is ImmutableEquatableArray<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (_array.IsEmpty) return 0;

            var hash = new HashCode();
            foreach (var item in _array)
            {
                hash.Add(item);
            }
            return hash.ToHashCode();
        }

        public static implicit operator ImmutableArray<T>(ImmutableEquatableArray<T> array) => array._array;
        public static implicit operator ImmutableEquatableArray<T>(ImmutableArray<T> array) => new(array);
    }
}