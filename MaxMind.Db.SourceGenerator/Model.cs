using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MaxMind.Db.SourceGenerator
{
    internal enum ActivationKind
    {
        Constructor,
        Properties,
    }

    internal sealed class TypeSpec
    {
        internal TypeSpec(
            string typeName,
            ActivationKind activationKind,
            ImmutableArray<MemberSpec> members,
            INamedTypeSymbol typeSymbol
            )
        {
            TypeName = typeName;
            ActivationKind = activationKind;
            Members = members;
            TypeSymbol = typeSymbol;
        }

        internal ActivationKind ActivationKind { get; }
        internal ImmutableArray<MemberSpec> Members { get; }
        internal string TypeName { get; }
        internal INamedTypeSymbol TypeSymbol { get; }
    }

    internal sealed class MemberSpec
    {
        internal MemberSpec(
            string sourceName,
            string typeName,
            ITypeSymbol typeSymbol,
            string mapKey,
            string? injectableName,
            bool isNetwork,
            bool alwaysCreate
            )
        {
            SourceName = sourceName;
            TypeName = typeName;
            TypeSymbol = typeSymbol;
            MapKey = mapKey;
            InjectableName = injectableName;
            IsNetwork = isNetwork;
            AlwaysCreate = alwaysCreate;
        }

        internal bool AlwaysCreate { get; }
        internal string? InjectableName { get; }
        internal bool IsNetwork { get; }
        internal string MapKey { get; }
        internal string SourceName { get; }
        internal string TypeName { get; }
        internal ITypeSymbol TypeSymbol { get; }
    }

    internal enum CollectionKind
    {
        Collection,
        Dictionary,
    }

    internal sealed class CollectionSpec
    {
        internal CollectionSpec(
            CollectionKind kind,
            string typeName,
            string firstTypeArgument,
            string? secondTypeArgument,
            string factoryTypeName,
            bool factoryUsesCapacity
            )
        {
            Kind = kind;
            TypeName = typeName;
            FirstTypeArgument = firstTypeArgument;
            SecondTypeArgument = secondTypeArgument;
            FactoryTypeName = factoryTypeName;
            FactoryUsesCapacity = factoryUsesCapacity;
        }

        internal string FactoryTypeName { get; }
        internal bool FactoryUsesCapacity { get; }
        internal string FirstTypeArgument { get; }
        internal CollectionKind Kind { get; }
        internal string? SecondTypeArgument { get; }
        internal string TypeName { get; }
    }

    internal sealed class CollectionRoot
    {
        internal CollectionRoot(
            ITypeSymbol typeSymbol,
            Location location,
            string description
            )
        {
            TypeSymbol = typeSymbol;
            Location = location;
            Description = description;
        }

        internal string Description { get; }
        internal Location Location { get; }
        internal ITypeSymbol TypeSymbol { get; }
    }
}
