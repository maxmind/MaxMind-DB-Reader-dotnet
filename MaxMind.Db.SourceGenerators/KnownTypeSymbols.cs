using System;
using Microsoft.CodeAnalysis;

namespace MaxMind.Db.SourceGenerators
{
    /// <summary>
    /// Cached resolution of commonly used type symbols to avoid repeated metadata lookups.
    /// Inspired by System.Text.Json's type symbol caching strategy for build performance.
    /// </summary>
    internal sealed class KnownTypeSymbols
    {
        public KnownTypeSymbols(Compilation compilation) => Compilation = compilation;

        public Compilation Compilation { get; }

        // MaxMind.Db attribute types
        public INamedTypeSymbol? ConstructorAttributeType => GetOrResolveType(
            "MaxMind.Db.ConstructorAttribute", ref _constructorAttributeType);
        private Option<INamedTypeSymbol?> _constructorAttributeType;

        public INamedTypeSymbol? ParameterAttributeType => GetOrResolveType(
            "MaxMind.Db.ParameterAttribute", ref _parameterAttributeType);
        private Option<INamedTypeSymbol?> _parameterAttributeType;

        public INamedTypeSymbol? NetworkAttributeType => GetOrResolveType(
            "MaxMind.Db.NetworkAttribute", ref _networkAttributeType);
        private Option<INamedTypeSymbol?> _networkAttributeType;

        public INamedTypeSymbol? InjectAttributeType => GetOrResolveType(
            "MaxMind.Db.InjectAttribute", ref _injectAttributeType);
        private Option<INamedTypeSymbol?> _injectAttributeType;

        // MaxMind.Db core types
        public INamedTypeSymbol? SourceGeneratorSupportType => GetOrResolveType(
            "MaxMind.Db.SourceGeneratorSupport", ref _sourceGeneratorSupportType);
        private Option<INamedTypeSymbol?> _sourceGeneratorSupportType;

        public INamedTypeSymbol? TypeActivatorType => GetOrResolveType(
            "MaxMind.Db.TypeActivator", ref _typeActivatorType);
        private Option<INamedTypeSymbol?> _typeActivatorType;

        // Common .NET types
        public INamedTypeSymbol? ObjectType => GetOrResolveType(
            "System.Object", ref _objectType);
        private Option<INamedTypeSymbol?> _objectType;

        public INamedTypeSymbol? StringType => GetOrResolveType(
            "System.String", ref _stringType);
        private Option<INamedTypeSymbol?> _stringType;

        public INamedTypeSymbol? TypeType => GetOrResolveType(
            "System.Type", ref _typeType);
        private Option<INamedTypeSymbol?> _typeType;

        public INamedTypeSymbol? FuncType => GetOrResolveType(
            "System.Func`2", ref _funcType);
        private Option<INamedTypeSymbol?> _funcType;

        public INamedTypeSymbol? DictionaryType => GetOrResolveType(
            "System.Collections.Generic.Dictionary`2", ref _dictionaryType);
        private Option<INamedTypeSymbol?> _dictionaryType;

        public INamedTypeSymbol? FrozenDictionaryType => GetOrResolveType(
            "System.Collections.Frozen.FrozenDictionary`2", ref _frozenDictionaryType);
        private Option<INamedTypeSymbol?> _frozenDictionaryType;

        // MaxMind.Db specific types
        public INamedTypeSymbol? NetworkType => GetOrResolveType(
            "MaxMind.Db.Network", ref _networkType);
        private Option<INamedTypeSymbol?> _networkType;

        public INamedTypeSymbol? InjectableValuesType => GetOrResolveType(
            "MaxMind.Db.InjectableValues", ref _injectableValuesType);
        private Option<INamedTypeSymbol?> _injectableValuesType;

        public INamedTypeSymbol? ReaderType => GetOrResolveType(
            "MaxMind.Db.Reader", ref _readerType);
        private Option<INamedTypeSymbol?> _readerType;

        /// <summary>
        /// Gets or resolves a type symbol with caching using the Option pattern.
        /// This avoids repeated expensive metadata lookups during compilation.
        /// </summary>
        private INamedTypeSymbol? GetOrResolveType(string fullyQualifiedName, ref Option<INamedTypeSymbol?> field)
        {
            if (field.HasValue) return field.Value;

            INamedTypeSymbol? type = Compilation.GetTypeByMetadataName(fullyQualifiedName);
            field = new(type);
            return type;
        }

        /// <summary>
        /// Checks if a type symbol represents the Constructor attribute.
        /// </summary>
        public bool IsConstructorAttribute(INamedTypeSymbol? attributeType)
        {
            return SymbolEqualityComparer.Default.Equals(attributeType, ConstructorAttributeType);
        }

        /// <summary>
        /// Checks if a type symbol represents the Parameter attribute.
        /// </summary>
        public bool IsParameterAttribute(INamedTypeSymbol? attributeType)
        {
            return SymbolEqualityComparer.Default.Equals(attributeType, ParameterAttributeType);
        }

        /// <summary>
        /// Checks if a type symbol represents the Network attribute.
        /// </summary>
        public bool IsNetworkAttribute(INamedTypeSymbol? attributeType)
        {
            return SymbolEqualityComparer.Default.Equals(attributeType, NetworkAttributeType);
        }

        /// <summary>
        /// Checks if a type symbol represents the Inject attribute.
        /// </summary>
        public bool IsInjectAttribute(INamedTypeSymbol? attributeType)
        {
            return SymbolEqualityComparer.Default.Equals(attributeType, InjectAttributeType);
        }

        /// <summary>
        /// Option pattern for lazy-loaded type symbol caching.
        /// Prevents repeated null checks and expensive lookups.
        /// </summary>
        private readonly struct Option<T>
        {
            public readonly bool HasValue;
            public readonly T Value;

            public Option(T value)
            {
                HasValue = true;
                Value = value;
            }
        }
    }
}