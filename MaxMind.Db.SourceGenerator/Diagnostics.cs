using Microsoft.CodeAnalysis;

namespace MaxMind.Db.SourceGenerator
{
    internal static class Diagnostics
    {
        private const string Category = "MaxMind.Db.SourceGenerator";

        internal static readonly DiagnosticDescriptor InaccessibleType = new(
            "MMDBSG001",
            "Model type is inaccessible to the MaxMind DB source generator",
            "Type '{0}' is not accessible from source-generated code",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor InaccessibleConstructor = new(
            "MMDBSG002",
            "Model constructor is inaccessible to the MaxMind DB source generator",
            "The deserialization constructor for '{0}' is not accessible from source-generated code",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor InaccessibleProperty = new(
            "MMDBSG003",
            "Model property cannot be assigned by the MaxMind DB source generator",
            "Property '{0}' on '{1}' must be an instance, non-indexed property with a getter and setter accessible from source-generated code",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor OpenGenericModel = new(
            "MMDBSG004",
            "Open generic MaxMind DB models are not supported",
            "Type '{0}' is an open generic model and cannot use source-generated deserialization",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor DuplicateMapKey = new(
            "MMDBSG005",
            "MaxMind DB model contains a duplicate map key",
            "Type '{0}' maps more than one member to the database key '{1}'",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor MissingParameterlessConstructor = new(
            "MMDBSG006",
            "Property model needs an accessible parameterless constructor",
            "Type '{0}' needs a parameterless constructor accessible from source-generated code",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor MultipleDeserializationConstructors = new(
            "MMDBSG007",
            "Model has multiple MaxMind DB constructors",
            "Type '{0}' has more than one constructor marked with ConstructorAttribute",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor UnsupportedCollection = new(
            "MMDBSG008",
            "Collection type is not supported by source-generated deserialization",
            "Collection type '{0}' used by '{1}' cannot be created and populated by source-generated code",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor UnsupportedLanguageVersion = new(
            "MMDBSG009",
            "MaxMind DB source generation requires C# 9 or later",
            "MaxMind DB source generation requires C# 9 or later because generated registrations use module initializers",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor RequiredMembers = new(
            "MMDBSG010",
            "Required model members need a SetsRequiredMembers constructor",
            "Model '{0}' has required member '{1}', so its deserialization constructor must be marked with SetsRequiredMembersAttribute",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor DerivedMapKeyAttribute = new(
            "MMDBSG011",
            "Derived MapKey attributes are not supported by source-generated deserialization",
            "Attribute '{0}' on model '{1}' derives from MapKeyAttribute and cannot be evaluated by source-generated deserialization",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor MissingDeserializationConstructor = new(
            "MMDBSG016",
            "Annotated model has no deserialization constructor",
            "Type '{0}' has annotated constructor parameters but no constructor marked with ConstructorAttribute, so source-generated deserialization cannot activate it. A positional record needs the attribute on its primary constructor, as in [method: Constructor].",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor UnresolvableLookupType = new(
            "MMDBSG015",
            "Lookup result type is not statically resolvable",
            "The type argument '{0}' of {1} still contains a type parameter, so no registration can be generated for this call",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor HiddenModelProperty = new(
            "MMDBSG014",
            "Model property is hidden by a more derived member",
            "Property '{1}' on model '{0}' is annotated but hidden by a more derived member of the same name, so source-generated deserialization cannot assign it",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor ConflictingMemberAttributes = new(
            "MMDBSG013",
            "MapKey cannot be combined with Inject or Network",
            "Member '{1}' on model '{0}' combines MapKey with Inject or Network, so source-generated deserialization cannot tell which supplies its value",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor UnsupportedModelKind = new(
            "MMDBSG012",
            "MaxMind DB models must be classes or records",
            "Type '{0}' is annotated for MaxMind DB deserialization but is not a class or record, so it cannot use source-generated deserialization",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
