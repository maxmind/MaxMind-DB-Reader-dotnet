using Microsoft.CodeAnalysis;

namespace MaxMind.Db.SourceGenerator
{
    internal static class SymbolHelpers
    {
        private static readonly SymbolDisplayFormat TypeDisplayFormat =
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        internal static string DisplayType(ITypeSymbol type) =>
            type.ToDisplayString(TypeDisplayFormat);

        internal static bool IsTypeAccessible(
            ITypeSymbol type,
            Compilation compilation
            )
        {
            if (type is IArrayTypeSymbol arrayType)
            {
                return IsTypeAccessible(arrayType.ElementType, compilation);
            }
            if (type is not INamedTypeSymbol namedType)
            {
                return type is not ITypeParameterSymbol;
            }

            if (namedType.IsFileLocal || !IsAccessible(namedType, compilation))
            {
                return false;
            }
            if (namedType.ContainingType != null &&
                !IsTypeAccessible(namedType.ContainingType, compilation))
            {
                return false;
            }

            foreach (var typeArgument in namedType.TypeArguments)
            {
                if (!IsTypeAccessible(typeArgument, compilation))
                {
                    return false;
                }
            }
            return true;
        }
        internal static bool IsAccessible(ISymbol symbol, Compilation compilation)
        {
            if (symbol.DeclaredAccessibility == Accessibility.Public)
            {
                return true;
            }

            return SymbolEqualityComparer.Default.Equals(
                       symbol.ContainingAssembly,
                       compilation.Assembly) &&
                   symbol.DeclaredAccessibility is Accessibility.Internal or
                       Accessibility.ProtectedOrInternal;
        }


        internal static bool ContainsTypeParameter(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol)
            {
                return true;
            }
            if (type is IArrayTypeSymbol arrayType)
            {
                return ContainsTypeParameter(arrayType.ElementType);
            }
            if (type is not INamedTypeSymbol namedType)
            {
                return false;
            }
            if (namedType.ContainingType != null &&
                ContainsTypeParameter(namedType.ContainingType))
            {
                return true;
            }
            foreach (var typeArgument in namedType.TypeArguments)
            {
                if (ContainsTypeParameter(typeArgument))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
