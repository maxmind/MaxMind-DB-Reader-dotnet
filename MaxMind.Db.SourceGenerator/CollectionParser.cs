using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MaxMind.Db.SourceGenerator
{
    internal static class CollectionParser
    {
        internal static void Collect(
            ITypeSymbol memberType,
            Location? diagnosticLocation,
            string ownerDescription,
            Compilation compilation,
            bool reportAotDiagnostics,
            SourceProductionContext context,
            IDictionary<string, CollectionSpec> collections
            )
        {
            if (memberType is IArrayTypeSymbol arrayType)
            {
                if (arrayType.Rank == 1 &&
                    arrayType.ElementType.SpecialType == SpecialType.System_Byte)
                {
                    return;
                }

                if (reportAotDiagnostics)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedCollection,
                        diagnosticLocation,
                        arrayType.ToDisplayString(),
                        ownerDescription));
                }
                return;
            }

            if (memberType is not INamedTypeSymbol namedType)
            {
                return;
            }

            var typeName = SymbolHelpers.DisplayType(namedType);
            if (!collections.ContainsKey(typeName))
            {
                var result = Parse(namedType, compilation);
                if (result.IsCollection)
                {
                    if (result.Spec != null)
                    {
                        collections.Add(typeName, result.Spec);
                    }
                    else if (reportAotDiagnostics)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.UnsupportedCollection,
                            diagnosticLocation,
                            namedType.ToDisplayString(),
                            ownerDescription));
                    }
                }
            }

            foreach (var typeArgument in namedType.TypeArguments)
            {
                Collect(
                    typeArgument,
                    diagnosticLocation,
                    ownerDescription,
                    compilation,
                    reportAotDiagnostics,
                    context,
                    collections);
            }
        }

        private static CollectionParseResult Parse(
            INamedTypeSymbol type,
            Compilation compilation
            )
        {
            var dictionaryInterface = FindConstructedInterface(
                type,
                compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2"));
            var readOnlyDictionaryInterface = FindConstructedInterface(
                type,
                compilation.GetTypeByMetadataName(
                    "System.Collections.Generic.IReadOnlyDictionary`2"));
            var dictionaryShape = dictionaryInterface ?? readOnlyDictionaryInterface;
            if (dictionaryShape != null)
            {
                var keyType = dictionaryShape.TypeArguments[0];
                var valueType = dictionaryShape.TypeArguments[1];
                if (!SymbolHelpers.IsTypeAccessible(type, compilation) ||
                    !SymbolHelpers.IsTypeAccessible(keyType, compilation) ||
                    !SymbolHelpers.IsTypeAccessible(valueType, compilation))
                {
                    return CollectionParseResult.Unsupported;
                }
                var dictionaryDefinition = compilation.GetTypeByMetadataName(
                    "System.Collections.Generic.Dictionary`2");
                if (dictionaryDefinition == null)
                {
                    return CollectionParseResult.Unsupported;
                }

                var defaultDictionary = dictionaryDefinition.Construct(keyType, valueType);
                if (HasImplicitConversion(compilation, defaultDictionary, type))
                {
                    return CollectionParseResult.Success(new CollectionSpec(
                        CollectionKind.Dictionary,
                        SymbolHelpers.DisplayType(type),
                        SymbolHelpers.DisplayType(keyType),
                        SymbolHelpers.DisplayType(valueType),
                        SymbolHelpers.DisplayType(defaultDictionary),
                        factoryUsesCapacity: true));
                }

                if (dictionaryInterface != null && CanConstruct(type, compilation))
                {
                    return CollectionParseResult.Success(new CollectionSpec(
                        CollectionKind.Dictionary,
                        SymbolHelpers.DisplayType(type),
                        SymbolHelpers.DisplayType(keyType),
                        SymbolHelpers.DisplayType(valueType),
                        SymbolHelpers.DisplayType(type),
                        factoryUsesCapacity: false));
                }

                return CollectionParseResult.Unsupported;
            }

            var enumerableInterface = FindConstructedInterface(
                type,
                compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1"));
            if (enumerableInterface == null || type.SpecialType == SpecialType.System_String)
            {
                return CollectionParseResult.NotCollection;
            }

            var elementType = enumerableInterface.TypeArguments[0];
            if (!SymbolHelpers.IsTypeAccessible(type, compilation) ||
                !SymbolHelpers.IsTypeAccessible(elementType, compilation))
            {
                return CollectionParseResult.Unsupported;
            }
            var listDefinition = compilation.GetTypeByMetadataName(
                "System.Collections.Generic.List`1");
            var collectionDefinition = compilation.GetTypeByMetadataName(
                "System.Collections.Generic.ICollection`1");
            if (listDefinition == null || collectionDefinition == null)
            {
                return CollectionParseResult.Unsupported;
            }

            var defaultList = listDefinition.Construct(elementType);
            if (HasImplicitConversion(compilation, defaultList, type))
            {
                return CollectionParseResult.Success(new CollectionSpec(
                    CollectionKind.Collection,
                    SymbolHelpers.DisplayType(type),
                    SymbolHelpers.DisplayType(elementType),
                    secondTypeArgument: null,
                    SymbolHelpers.DisplayType(defaultList),
                    factoryUsesCapacity: true));
            }

            var collectionInterface = collectionDefinition.Construct(elementType);
            if (HasImplicitConversion(compilation, type, collectionInterface) &&
                CanConstruct(type, compilation))
            {
                return CollectionParseResult.Success(new CollectionSpec(
                    CollectionKind.Collection,
                    SymbolHelpers.DisplayType(type),
                    SymbolHelpers.DisplayType(elementType),
                    secondTypeArgument: null,
                    SymbolHelpers.DisplayType(type),
                    factoryUsesCapacity: false));
            }

            return CollectionParseResult.Unsupported;
        }

        private static INamedTypeSymbol? FindConstructedInterface(
            INamedTypeSymbol type,
            INamedTypeSymbol? interfaceDefinition
            )
        {
            if (interfaceDefinition == null)
            {
                return null;
            }
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, interfaceDefinition))
            {
                return type;
            }
            return type.AllInterfaces.FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    interfaceDefinition));
        }

        private static bool HasImplicitConversion(
            Compilation compilation,
            ITypeSymbol from,
            ITypeSymbol to
            ) => compilation.ClassifyCommonConversion(from, to).IsImplicit;

        private static bool CanConstruct(INamedTypeSymbol type, Compilation compilation)
        {
            if (type.IsAbstract || !type.IsReferenceType ||
                !SymbolHelpers.IsTypeAccessible(type, compilation))
            {
                return false;
            }

            return type.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0 &&
                SymbolHelpers.IsAccessible(constructor, compilation));
        }

        private readonly struct CollectionParseResult
        {
            private CollectionParseResult(bool isCollection, CollectionSpec? spec)
            {
                IsCollection = isCollection;
                Spec = spec;
            }

            internal bool IsCollection { get; }
            internal CollectionSpec? Spec { get; }

            internal static CollectionParseResult NotCollection { get; } = new(false, null);
            internal static CollectionParseResult Unsupported { get; } = new(true, null);

            internal static CollectionParseResult Success(CollectionSpec spec) =>
                new(true, spec);
        }
    }
}
