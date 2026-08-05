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
            // Abstract types are skipped silently: an annotated abstract base is the
            // supported way to share members, and its concrete derived types are
            // generated instead.
            if (type.IsAbstract || type.IsStatic)
            {
                return null;
            }

            var constructors = type.InstanceConstructors
                .Where(constructor => HasAttribute(constructor, ConstructorAttributeName))
                .ToImmutableArray();
            var properties = GetAnnotatedProperties(type, out var hiddenAnnotatedProperty);

            // Checked before the "nothing annotated" return below, because hiding the
            // only annotated property is one of the ways to reach that state. Only the
            // property path assigns members by name, so this does not apply to a
            // constructor model.
            if (constructors.Length == 0 && hiddenAnnotatedProperty != null)
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.HiddenModelProperty,
                    hiddenAnnotatedProperty,
                    type,
                    type.ToDisplayString(),
                    hiddenAnnotatedProperty.Name);
                return null;
            }

            if (constructors.Length == 0 && properties.Length == 0)
            {
                return null;
            }

            // Reported only once the type is known to be annotated. Candidate discovery
            // admits every type with a base list, so an earlier check here would warn
            // about unrelated declarations.
            if (type.TypeKind != TypeKind.Class)
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.UnsupportedModelKind,
                    type,
                    type,
                    type.ToDisplayString());
                return null;
            }

            if (SymbolHelpers.ContainsTypeParameter(type))
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.OpenGenericModel,
                    type,
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
                    type,
                    type.ToDisplayString());
                return null;
            }

            if (constructors.Length == 1)
            {
                return ParseConstructor(
                    type, constructors[0], compilation, reportAotDiagnostics, context);
            }

            return ParseProperties(
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
                    type,
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
                        type,
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
                    modelType,
                    mapKeyAttribute.AttributeClass?.ToDisplayString() ?? "unknown",
                    modelType.ToDisplayString());
                return null;
            }
            var injectAttribute = GetAttribute(symbol, InjectAttributeName);
            var networkAttribute = GetAttribute(symbol, NetworkAttributeName);
            // An injectable or network member reads no database key, so an explicit
            // MapKey alongside one of them is ambiguous. The reflection fallback reads
            // the key and then overwrites it, which is a behaviour we do not want to
            // reproduce silently. Dropping the model keeps that exact behaviour, since
            // an unregistered model is what the fallback handles.
            if (mapKeyAttribute != null &&
                (injectAttribute != null || networkAttribute != null))
            {
                Report(
                    reportAotDiagnostics,
                    context,
                    Diagnostics.ConflictingMemberAttributes,
                    symbol,
                    modelType,
                    modelType.ToDisplayString(),
                    sourceName);
                return null;
            }
            var mapKey = GetStringArgument(mapKeyAttribute, 0) ?? sourceName;
            var alwaysCreate = GetBooleanArgument(mapKeyAttribute, 1);
            var injectableName = GetStringArgument(injectAttribute, 0);

            return new MemberSpec(
                EscapeIdentifier(sourceName),
                SymbolHelpers.DisplayType(type),
                type,
                mapKey,
                injectableName,
                networkAttribute != null,
                alwaysCreate);
        }

        private static bool IsSupportedMapKeyAttribute(AttributeData attribute)
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();
            return attributeName == MapKeyAttributeName ||
                   attributeName == ParameterAttributeName;
        }

        private static ImmutableArray<IPropertySymbol> GetAnnotatedProperties(
            INamedTypeSymbol type,
            out IPropertySymbol? hiddenAnnotatedProperty
            )
        {
            hiddenAnnotatedProperty = null;
            var propertiesByName = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
            var declaredNames = new HashSet<string>(StringComparer.Ordinal);
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                {
                    if (declaredNames.Add(property.Name))
                    {
                        if (IsAnnotated(property))
                        {
                            propertiesByName.Add(property.Name, property);
                        }
                        continue;
                    }

                    // A more derived declaration already owns this name. Generated code
                    // emits an unqualified member reference, which binds to the most
                    // derived member, so an annotated base property here can never be
                    // the one assigned. Overrides do not reach this branch: GetAttribute
                    // walks the override chain, so an override is annotated and claims
                    // the name itself.
                    if (IsAnnotated(property) &&
                        !propertiesByName.ContainsKey(property.Name))
                    {
                        hiddenAnnotatedProperty = property;
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
                // Only members that reach the decode dictionary can collide. An
                // injectable or network member contributes no key — its MapKey is just
                // the source name it defaulted to — so counting it here reports a
                // collision the runtime would never see.
                if (member.InjectableName != null || member.IsNetwork)
                {
                    continue;
                }
                if (!keys.Add(member.MapKey))
                {
                    Report(
                        reportAotDiagnostics,
                        context,
                        Diagnostics.DuplicateMapKey,
                        type,
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
                    type,
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
            ISymbol fallbackSymbol,
            params object[] messageArguments
            )
        {
            if (!enabled)
            {
                return;
            }

            // An inherited member can come from a referenced assembly, where it has no
            // source location and the diagnostic would land with no file or line. The
            // model type is always declared in this compilation, so it stands in.
            var location = SourceLocation(symbol) ?? SourceLocation(fallbackSymbol);
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                location,
                messageArguments));
        }

        private static Location? SourceLocation(ISymbol symbol) =>
            symbol.Locations.FirstOrDefault(location => location.IsInSource);
    }
}
