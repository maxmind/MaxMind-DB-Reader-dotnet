using MaxMind.Db.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace MaxMind.Db.SourceGenerators
{
    /// <summary>
    /// Parses type declarations and extracts metadata for source generation.
    /// Uses immutable model types and known type symbols for optimal incremental compilation.
    /// </summary>
    internal sealed class TypeActivatorParser
    {
        private readonly KnownTypeSymbols _knownTypes;

        public TypeActivatorParser(KnownTypeSymbols knownTypes)
        {
            _knownTypes = knownTypes;
        }

        /// <summary>
        /// Parses a class declaration to extract type activator specification.
        /// Returns null if the class doesn't have a Constructor attribute.
        /// </summary>
        public TypeActivatorSpec? ParseTypeActivatorSpec(
            ClassDeclarationSyntax classDeclaration,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;
            if (typeSymbol == null) return null;

            // Skip abstract classes - they cannot be instantiated
            if (typeSymbol.IsAbstract) return null;

            // Skip private types - they cannot be accessed from generated code
            if (typeSymbol.DeclaredAccessibility == Accessibility.Private) return null;

            // Find constructor with Constructor attribute
            var constructorWithAttr = typeSymbol.Constructors
                .FirstOrDefault(ctor => HasConstructorAttribute(ctor));

            if (constructorWithAttr == null) return null;

            // Parse constructor parameters
            var parameters = ParseParameters(constructorWithAttr, cancellationToken);

            // Create type reference
            var typeRef = CreateTypeRef(typeSymbol);

            // Determine namespace information
            var namespaceSymbol = typeSymbol.ContainingNamespace;
            bool isGlobalNamespace = namespaceSymbol.IsGlobalNamespace;
            string namespaceName = isGlobalNamespace ? "<global>" : namespaceSymbol.ToDisplayString();

            return new TypeActivatorSpec
            {
                TypeRef = typeRef,
                ActivatorMethodName = $"Create{typeRef.Name}Activator",
                Parameters = new ImmutableEquatableArray<ParameterSpec>(parameters),
                Accessibility = GetConstructorAccessibility(constructorWithAttr),
                Namespace = namespaceName,
                IsGlobalNamespace = isGlobalNamespace
            };
        }

        /// <summary>
        /// Checks if a constructor has the Constructor attribute.
        /// </summary>
        private bool HasConstructorAttribute(IMethodSymbol constructor)
        {
            return constructor.GetAttributes()
                .Any(attr => _knownTypes.IsConstructorAttribute(attr.AttributeClass));
        }

        /// <summary>
        /// Parses constructor parameters and extracts all relevant metadata.
        /// </summary>
        private ImmutableArray<ParameterSpec> ParseParameters(
            IMethodSymbol constructor,
            CancellationToken cancellationToken)
        {
            var parameters = ImmutableArray.CreateBuilder<ParameterSpec>();

            foreach (var param in constructor.Parameters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var paramSpec = new ParameterSpec
                {
                    Name = param.Name,
                    Type = CreateTypeRef(param.Type),
                    Position = param.Ordinal,
                    IsNullable = param.Type.CanBeReferencedByName && param.NullableAnnotation == NullableAnnotation.Annotated,
                    DatabaseParameterName = GetDatabaseParameterName(param),
                    IsNetwork = HasNetworkAttribute(param),
                    IsAlwaysCreated = GetAlwaysCreatedValue(param),
                    IsInjectable = HasInjectAttribute(param),
                    InjectableParameterName = GetInjectableParameterName(param)
                };

                parameters.Add(paramSpec);
            }

            return parameters.ToImmutable();
        }

        /// <summary>
        /// Gets the database parameter name from Parameter attribute or uses the parameter name.
        /// </summary>
        private string? GetDatabaseParameterName(IParameterSymbol parameter)
        {
            var paramAttr = parameter.GetAttributes()
                .FirstOrDefault(attr => _knownTypes.IsParameterAttribute(attr.AttributeClass));

            if (paramAttr?.ConstructorArguments.Length > 0)
            {
                return paramAttr.ConstructorArguments[0].Value?.ToString();
            }

            return parameter.Name;
        }

        /// <summary>
        /// Checks if a parameter has the Network attribute.
        /// </summary>
        private bool HasNetworkAttribute(IParameterSymbol parameter)
        {
            return parameter.GetAttributes()
                .Any(attr => _knownTypes.IsNetworkAttribute(attr.AttributeClass));
        }

        /// <summary>
        /// Gets the AlwaysCreate value from Parameter attribute.
        /// </summary>
        private bool GetAlwaysCreatedValue(IParameterSymbol parameter)
        {
            var paramAttr = parameter.GetAttributes()
                .FirstOrDefault(attr => _knownTypes.IsParameterAttribute(attr.AttributeClass));

            if (paramAttr != null)
            {
                // Look for AlwaysCreate named argument
                var alwaysCreateArg = paramAttr.NamedArguments
                    .FirstOrDefault(kvp => kvp.Key == "AlwaysCreate");

                if (alwaysCreateArg.Value.Value is bool alwaysCreate)
                    return alwaysCreate;
            }

            return false;
        }

        /// <summary>
        /// Checks if a parameter has the Inject attribute.
        /// </summary>
        private bool HasInjectAttribute(IParameterSymbol parameter)
        {
            return parameter.GetAttributes()
                .Any(attr => _knownTypes.IsInjectAttribute(attr.AttributeClass));
        }

        /// <summary>
        /// Gets the injectable parameter name from Inject attribute.
        /// </summary>
        private string? GetInjectableParameterName(IParameterSymbol parameter)
        {
            var injectAttr = parameter.GetAttributes()
                .FirstOrDefault(attr => _knownTypes.IsInjectAttribute(attr.AttributeClass));

            if (injectAttr?.ConstructorArguments.Length > 0)
            {
                return injectAttr.ConstructorArguments[0].Value?.ToString();
            }

            return null;
        }

        /// <summary>
        /// Creates a TypeRef from a type symbol for immutable caching.
        /// </summary>
        private TypeRef CreateTypeRef(ISymbol typeSymbol)
        {
            var fullyQualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";
            bool isGlobalNamespace = typeSymbol.ContainingNamespace?.IsGlobalNamespace ?? true;

            // Handle generic types
            var genericArguments = ImmutableArray<TypeRef>.Empty;
            bool isGeneric = false;

            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                isGeneric = true;
                var genericArgs = ImmutableArray.CreateBuilder<TypeRef>();
                foreach (var typeArg in namedType.TypeArguments)
                {
                    genericArgs.Add(CreateTypeRef(typeArg));
                }
                genericArguments = genericArgs.ToImmutable();
            }

            return new TypeRef
            {
                Name = typeSymbol.Name,
                FullyQualifiedName = fullyQualifiedName,
                Namespace = namespaceName,
                IsGlobalNamespace = isGlobalNamespace,
                IsGeneric = isGeneric,
                GenericArguments = genericArguments
            };
        }

        /// <summary>
        /// Gets the constructor accessibility level for code generation decisions.
        /// </summary>
        private static ConstructorAccessibility GetConstructorAccessibility(IMethodSymbol constructor)
        {
            return constructor.DeclaredAccessibility switch
            {
                Accessibility.Public => ConstructorAccessibility.Public,
                Accessibility.Internal => ConstructorAccessibility.Internal,
                Accessibility.Protected => ConstructorAccessibility.Protected,
                Accessibility.Private => ConstructorAccessibility.Private,
                _ => ConstructorAccessibility.Internal
            };
        }
    }
}