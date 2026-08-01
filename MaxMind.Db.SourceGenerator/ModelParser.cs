using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MaxMind.Db.SourceGenerator
{
    internal static class ModelParser
    {
        private const string ConstructorAttributeName = "MaxMind.Db.ConstructorAttribute";
        private const string InjectAttributeName = "MaxMind.Db.InjectAttribute";
        private const string MapKeyAttributeName = "MaxMind.Db.MapKeyAttribute";
        private const string NetworkAttributeName = "MaxMind.Db.NetworkAttribute";
        private const string ParameterAttributeName = "MaxMind.Db.ParameterAttribute";
        private const string SetsRequiredMembersAttributeName =
            "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

        internal static TypeSpec? Parse(
            INamedTypeSymbol type,
            Compilation compilation,
            bool reportAotDiagnostics,
            SourceProductionContext context
            )
        {
            if (type.IsAbstract || type.IsStatic || type.TypeKind != TypeKind.Class)
            {
                return null;
            }

            var constructors = type.InstanceConstructors
                .Where(constructor => HasAttribute(constructor, ConstructorAttributeName))
                .ToImmutableArray();
            var properties = GetAnnotatedProperties(type);
            if (constructors.Length == 0 && properties.Length == 0)
            {
                return null;
            }

            if (SymbolHelpers.ContainsTypeParameter(type))
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.OpenGenericModel,
                    type,
                    type.ToDisplayString());
                return null;
            }

            if (!SymbolHelpers.IsTypeAccessible(type, compilation))
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.InaccessibleType,
                    type,
                    type.ToDisplayString());
                return null;
            }

            if (constructors.Length > 1)
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.MultipleDeserializationConstructors,
                    type,
                    type.ToDisplayString());
                return null;
            }

            return constructors.Length == 1
                ? ParseConstructor(
                    type, constructors[0], compilation, reportAotDiagnostics, context)
                : ParseProperties(
                    type, properties, compilation, reportAotDiagnostics, context);
        }

        private static TypeSpec? ParseConstructor(
            INamedTypeSymbol type,
            IMethodSymbol constructor,
            Compilation compilation,
            bool reportAotDiagnostics,
            SourceProductionContext context
            )
        {
            if (!SymbolHelpers.IsAccessible(constructor, compilation))
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.InaccessibleConstructor,
                    constructor,
                    type.ToDisplayString());
                return null;
            }
            if (!SupportsRequiredMembers(
                    type, constructor, reportAotDiagnostics, context))
            {
                return null;
            }

            var members = ImmutableArray.CreateBuilder<MemberSpec>(constructor.Parameters.Length);
            foreach (var parameter in constructor.Parameters)
            {
                var member = CreateMember(
                    parameter,
                    parameter.Type,
                    parameter.Name,
                    type,
                    reportAotDiagnostics,
                    context);
                if (member == null)
                {
                    return null;
                }
                members.Add(member);
            }

            if (!HasUniqueMapKeys(type, members, reportAotDiagnostics, context))
            {
                return null;
            }

            return new TypeSpec(
                SymbolHelpers.DisplayType(type),
                ActivationKind.Constructor,
                members.ToImmutable(),
                type);
        }

        private static TypeSpec? ParseProperties(
            INamedTypeSymbol type,
            ImmutableArray<IPropertySymbol> properties,
            Compilation compilation,
            bool reportAotDiagnostics,
            SourceProductionContext context
            )
        {
            var constructor = type.InstanceConstructors.FirstOrDefault(candidate =>
                candidate.Parameters.Length == 0 &&
                SymbolHelpers.IsAccessible(candidate, compilation));
            if (constructor == null)
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.MissingParameterlessConstructor,
                    type,
                    type.ToDisplayString());
                return null;
            }
            if (!SupportsRequiredMembers(
                    type, constructor, reportAotDiagnostics, context))
            {
                return null;
            }

            var members = ImmutableArray.CreateBuilder<MemberSpec>(properties.Length);
            foreach (var property in properties)
            {
                if (property.IsIndexer || property.IsStatic ||
                    property.GetMethod == null || property.SetMethod == null ||
                    !SymbolHelpers.IsAccessible(property.GetMethod, compilation) ||
                    !SymbolHelpers.IsAccessible(property.SetMethod, compilation))
                {
                    Report(
                        reportAotDiagnostics,
                        context,
                        Diagnostics.InaccessibleProperty,
                        property,
                        property.Name,
                        type.ToDisplayString());
                    return null;
                }

                var member = CreateMember(
                    property,
                    property.Type,
                    property.Name,
                    type,
                    reportAotDiagnostics,
                    context);
                if (member == null)
                {
                    return null;
                }
                members.Add(member);
            }

            if (!HasUniqueMapKeys(type, members, reportAotDiagnostics, context))
            {
                return null;
            }

            return new TypeSpec(
                SymbolHelpers.DisplayType(type),
                ActivationKind.Properties,
                members.ToImmutable(),
                type);
        }

        private static MemberSpec? CreateMember(
            ISymbol symbol,
            ITypeSymbol type,
            string sourceName,
            INamedTypeSymbol modelType,
            bool reportAotDiagnostics,
            SourceProductionContext context
            )
        {
            var mapKeyAttribute = GetAttribute(symbol, MapKeyAttributeName);
            if (mapKeyAttribute != null && !IsSupportedMapKeyAttribute(mapKeyAttribute))
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.DerivedMapKeyAttribute,
                    symbol,
                    mapKeyAttribute.AttributeClass?.ToDisplayString() ?? "unknown",
                    modelType.ToDisplayString());
                return null;
            }
            var injectAttribute = GetAttribute(symbol, InjectAttributeName);
            var mapKey = GetStringArgument(mapKeyAttribute, 0) ?? sourceName;
            var alwaysCreate = GetBooleanArgument(mapKeyAttribute, 1);
            var injectableName = GetStringArgument(injectAttribute, 0);

            return new MemberSpec(
                EscapeIdentifier(sourceName),
                SymbolHelpers.DisplayType(type),
                type,
                mapKey,
                injectableName,
                GetAttribute(symbol, NetworkAttributeName) != null,
                alwaysCreate);
        }

        private static bool IsSupportedMapKeyAttribute(AttributeData attribute)
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();
            return attributeName == MapKeyAttributeName ||
                   attributeName == ParameterAttributeName;
        }

        private static ImmutableArray<IPropertySymbol> GetAnnotatedProperties(INamedTypeSymbol type)
        {
            var propertiesByName = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                {
                    if (!propertiesByName.ContainsKey(property.Name) && IsAnnotated(property))
                    {
                        propertiesByName.Add(property.Name, property);
                    }
                }
            }

            return propertiesByName.Values
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static bool IsAnnotated(IPropertySymbol property) =>
            GetAttribute(property, MapKeyAttributeName) != null ||
            GetAttribute(property, InjectAttributeName) != null ||
            GetAttribute(property, NetworkAttributeName) != null;

        private static AttributeData? GetAttribute(ISymbol symbol, string metadataName)
        {
            for (var current = symbol; current != null; current = GetOverriddenSymbol(current))
            {
                var attribute = current.GetAttributes().FirstOrDefault(candidate =>
                    IsAttribute(candidate.AttributeClass, metadataName));
                if (attribute != null)
                {
                    return attribute;
                }
            }
            return null;
        }

        private static ISymbol? GetOverriddenSymbol(ISymbol symbol) => symbol switch
        {
            IPropertySymbol property => property.OverriddenProperty,
            _ => null,
        };

        private static bool HasAttribute(ISymbol symbol, string metadataName) =>
            GetAttribute(symbol, metadataName) != null;

        private static bool IsAttribute(INamedTypeSymbol? attribute, string metadataName)
        {
            for (var current = attribute; current != null; current = current.BaseType)
            {
                if (current.ToDisplayString() == metadataName)
                {
                    return true;
                }
            }
            return false;
        }

        private static string? GetStringArgument(AttributeData? attribute, int position)
        {
            if (attribute == null || attribute.ConstructorArguments.Length <= position)
            {
                return null;
            }
            return attribute.ConstructorArguments[position].Value as string;
        }

        private static bool GetBooleanArgument(AttributeData? attribute, int position)
        {
            if (attribute == null || attribute.ConstructorArguments.Length <= position)
            {
                return false;
            }
            return attribute.ConstructorArguments[position].Value is bool value && value;
        }

        private static bool HasUniqueMapKeys(
            INamedTypeSymbol type,
            ImmutableArray<MemberSpec>.Builder members,
            bool reportAotDiagnostics,
            SourceProductionContext context
            )
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in members)
            {
                if (!keys.Add(member.MapKey))
                {
                    Report(
                        reportAotDiagnostics,
                        context,
                        Diagnostics.DuplicateMapKey,
                        type,
                        type.ToDisplayString(),
                        member.MapKey);
                    return false;
                }
            }
            return true;
        }

        private static bool SupportsRequiredMembers(
            INamedTypeSymbol type,
            IMethodSymbol constructor,
            bool reportAotDiagnostics,
            SourceProductionContext context
            )
        {
            if (HasAttribute(constructor, SetsRequiredMembersAttributeName))
            {
                return true;
            }

            for (var current = type; current != null; current = current.BaseType)
            {
                var requiredProperty = current.GetMembers()
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault(property => property.IsRequired);
                if (requiredProperty == null)
                {
                    continue;
                }

                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.RequiredMembers,
                    requiredProperty,
                    type.ToDisplayString(),
                    requiredProperty.Name);
                return false;
            }
            return true;
        }

        private static string EscapeIdentifier(string identifier) =>
            SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
            SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None
                ? identifier
                : "@" + identifier;

        private static void Report(
            bool enabled,
            SourceProductionContext context,
            DiagnosticDescriptor descriptor,
            ISymbol symbol,
            params object[] messageArguments
            )
        {
            if (!enabled)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                symbol.Locations.FirstOrDefault(location => location.IsInSource),
                messageArguments));
        }
    }
}
