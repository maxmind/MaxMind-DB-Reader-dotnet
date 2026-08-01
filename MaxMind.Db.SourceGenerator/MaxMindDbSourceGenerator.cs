using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MaxMind.Db.SourceGenerator
{
    /// <summary>
    ///     Generates reflection-free activators for MaxMind DB model types.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class MaxMindDbSourceGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var types = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => IsModelTypeCandidate(node),
                    static (syntaxContext, cancellationToken) =>
                        syntaxContext.SemanticModel.GetDeclaredSymbol(
                            (TypeDeclarationSyntax)syntaxContext.Node,
                            cancellationToken) as INamedTypeSymbol)
                .Where(static type => type != null)
                .Select(static (type, _) => type!);

            var collectionRoots = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => IsReaderInvocationCandidate(node),
                    static (syntaxContext, cancellationToken) =>
                        GetCollectionRoot(syntaxContext, cancellationToken))
                .Where(static root => root != null)
                .Select(static (root, _) => root!);

            var aotDiagnostics = context.AnalyzerConfigOptionsProvider.Select(
                static (options, _) =>
                    options.GlobalOptions.TryGetValue(
                        "build_property.MaxMindDbAotDiagnostics",
                        out var value) &&
                    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

            var generationInput = types.Collect()
                .Combine(collectionRoots.Collect())
                .Combine(context.CompilationProvider)
                .Combine(aotDiagnostics);
            context.RegisterSourceOutput(generationInput, static (sourceContext, input) =>
                Generate(
                    sourceContext,
                    input.Left.Left.Left,
                    input.Left.Left.Right,
                    input.Left.Right,
                    input.Right));
        }

        private static bool IsModelTypeCandidate(SyntaxNode node)
        {
            if (node is not TypeDeclarationSyntax type)
            {
                return false;
            }

            // A concrete model may inherit annotated properties without containing
            // attributes itself. Otherwise, an MMDB model must have an attributed
            // declaration, member, or constructor parameter.
            if (type.BaseList != null)
            {
                return true;
            }

            // A primary constructor's parameter list is always a direct child of the
            // type declaration. Reading it that way rather than through
            // TypeDeclarationSyntax.ParameterList keeps this compiling against the
            // oldest Roslyn the generator supports, which only exposes that property on
            // records.
            var primaryConstructor = type.ChildNodes()
                .OfType<ParameterListSyntax>()
                .FirstOrDefault();
            if (primaryConstructor != null &&
                (type.AttributeLists.Count > 0 ||
                 primaryConstructor.Parameters.Any(static parameter =>
                     parameter.AttributeLists.Count > 0)))
            {
                // Primary-constructor attributes use the method target and appear on
                // the type declaration rather than a member node.
                return true;
            }

            foreach (var member in type.Members)
            {
                if (member.AttributeLists.Count > 0 ||
                    member is BaseMethodDeclarationSyntax { ParameterList: { } parameterList } &&
                    parameterList.Parameters.Any(static parameter =>
                        parameter.AttributeLists.Count > 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Generate(
            SourceProductionContext context,
            ImmutableArray<INamedTypeSymbol> candidateTypes,
            ImmutableArray<CollectionRoot> collectionRoots,
            Compilation compilation,
            bool reportAotDiagnostics
            )
        {
            if (compilation.GetTypeByMetadataName("MaxMind.Db.SourceGeneratorSupport") == null)
            {
                return;
            }

            var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var specs = new List<TypeSpec>();
            foreach (var candidate in candidateTypes)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!seenTypes.Add(candidate))
                {
                    continue;
                }

                var spec = ModelParser.Parse(
                    candidate, compilation, reportAotDiagnostics, context);
                if (spec != null)
                {
                    specs.Add(spec);
                }
            }

            specs.Sort(static (left, right) =>
                StringComparer.Ordinal.Compare(left.TypeName, right.TypeName));
            var collections = new SortedDictionary<string, CollectionSpec>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                foreach (var member in spec.Members)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (member.InjectableName != null || member.IsNetwork)
                    {
                        continue;
                    }
                    CollectionParser.Collect(
                        member.TypeSymbol,
                        spec.TypeSymbol.Locations.FirstOrDefault(
                            location => location.IsInSource),
                        spec.TypeSymbol.ToDisplayString(),
                        compilation,
                        reportAotDiagnostics,
                        context,
                        collections);
                }
            }

            foreach (var root in collectionRoots)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (SymbolHelpers.ContainsTypeParameter(root.TypeSymbol))
                {
                    if (reportAotDiagnostics && root.TypeSymbol is INamedTypeSymbol)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.UnsupportedCollection,
                            root.Location,
                            root.TypeSymbol.ToDisplayString(),
                            root.Description));
                    }
                    continue;
                }
                CollectionParser.Collect(
                    root.TypeSymbol,
                    root.Location,
                    root.Description,
                    compilation,
                    reportAotDiagnostics,
                    context,
                    collections);
            }

            if (specs.Count == 0 && collections.Count == 0)
            {
                return;
            }
            if (compilation is CSharpCompilation csharpCompilation &&
                csharpCompilation.LanguageVersion < LanguageVersion.CSharp9)
            {
                if (reportAotDiagnostics)
                {
                    var location = specs.Count == 0
                        ? collectionRoots[0].Location
                        : specs[0].TypeSymbol.Locations.FirstOrDefault(
                            candidate => candidate.IsInSource);
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedLanguageVersion,
                        location));
                }
                return;
            }
            context.AddSource(
                "MaxMind.Db.SourceGenerator.g.cs",
                SourceText.From(
                    Render(specs, collections.Values, compilation),
                    Encoding.UTF8));
        }

        private static bool IsReaderInvocationCandidate(SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax invocation)
            {
                return false;
            }

            var genericName = invocation.Expression switch
            {
                GenericNameSyntax name => name,
                MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => name,
                MemberBindingExpressionSyntax { Name: GenericNameSyntax name } => name,
                _ => null,
            };
            return genericName?.Identifier.ValueText is "Find" or "FindAll";
        }

        private static CollectionRoot? GetCollectionRoot(
            GeneratorSyntaxContext context,
            CancellationToken cancellationToken
            )
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not
                    IMethodSymbol { IsGenericMethod: true, TypeArguments.Length: 1 } method ||
                method.Name is not ("Find" or "FindAll"))
            {
                return null;
            }

            var readerType = context.SemanticModel.Compilation.GetTypeByMetadataName(
                "MaxMind.Db.Reader");
            if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, readerType))
            {
                return null;
            }

            return new CollectionRoot(
                method.TypeArguments[0],
                invocation.GetLocation(),
                $"Reader.{method.Name}<T>");
        }

        private static string Render(
            IReadOnlyList<TypeSpec> specs,
            IEnumerable<CollectionSpec> collections,
            Compilation compilation
            )
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("#nullable enable");
            source.AppendLine("#pragma warning disable CS0436 // Local module-initializer polyfill may shadow an inaccessible referenced polyfill");
            source.AppendLine("#pragma warning disable CS0612 // Obsolete member without a message");
            source.AppendLine("#pragma warning disable CS0618 // Obsolete member with a message");
            source.AppendLine();

            var moduleInitializerAttribute = compilation.GetTypeByMetadataName(
                "System.Runtime.CompilerServices.ModuleInitializerAttribute");
            if (moduleInitializerAttribute == null ||
                !compilation.IsSymbolAccessibleWithin(
                    moduleInitializerAttribute,
                    compilation.Assembly))
            {
                source.AppendLine("namespace System.Runtime.CompilerServices");
                source.AppendLine("{");
                source.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]");
                source.AppendLine("    internal sealed class ModuleInitializerAttribute : global::System.Attribute");
                source.AppendLine("    {");
                source.AppendLine("    }");
                source.AppendLine("}");
                source.AppendLine();
            }

            source.AppendLine("namespace MaxMind.Db.Generated");
            source.AppendLine("{");
            source.AppendLine("    internal static class SourceGeneratedRegistration");
            source.AppendLine("    {");
            source.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializerAttribute]");
            source.AppendLine("        internal static void Register()");
            source.AppendLine("        {");

            foreach (var spec in specs)
            {
                RenderRegistration(source, spec);
            }
            foreach (var collection in collections)
            {
                RenderCollectionRegistration(source, collection);
            }

            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString().Replace("\r\n", "\n");
        }

        private static void RenderRegistration(StringBuilder source, TypeSpec spec)
        {
            source.AppendLine("            global::MaxMind.Db.SourceGeneratorSupport.RegisterType(");
            source.Append("                typeof(").Append(spec.TypeName).AppendLine("),");
            RenderActivator(source, spec);
            RenderDefaultsFactory(source, spec);
            RenderMembers(source, spec.Members);
            source.AppendLine("            );");
        }

        private static void RenderCollectionRegistration(
            StringBuilder source,
            CollectionSpec spec
            )
        {
            if (spec.Kind == CollectionKind.Collection)
            {
                source.AppendLine("            global::MaxMind.Db.SourceGeneratorSupport.RegisterCollection(");
                source.Append("                typeof(").Append(spec.TypeName).AppendLine("),");
                source.Append("                typeof(").Append(spec.FirstTypeArgument).AppendLine("),");
                source.Append("                capacity => new ").Append(spec.FactoryTypeName)
                    .Append(spec.FactoryUsesCapacity ? "(capacity)," : "(),")
                    .AppendLine();
                source.Append("                (collection, value) => ((global::System.Collections.Generic.ICollection<")
                    .Append(spec.FirstTypeArgument).Append(">)collection).Add((")
                    .Append(spec.FirstTypeArgument).AppendLine(")value!)");
                source.AppendLine("            );");
                return;
            }

            var valueType = spec.SecondTypeArgument!;
            source.AppendLine("            global::MaxMind.Db.SourceGeneratorSupport.RegisterDictionary(");
            source.Append("                typeof(").Append(spec.TypeName).AppendLine("),");
            source.Append("                typeof(").Append(spec.FirstTypeArgument).AppendLine("),");
            source.Append("                typeof(").Append(valueType).AppendLine("),");
            source.Append("                capacity => new ").Append(spec.FactoryTypeName)
                .Append(spec.FactoryUsesCapacity ? "(capacity)," : "(),")
                .AppendLine();
            source.Append("                (dictionary, key, value) => ((global::System.Collections.Generic.IDictionary<")
                .Append(spec.FirstTypeArgument).Append(", ").Append(valueType)
                .Append(">)dictionary).Add((").Append(spec.FirstTypeArgument)
                .Append(")key!, (").Append(valueType).AppendLine(")value!)");
            source.AppendLine("            );");
        }

        private static void RenderActivator(StringBuilder source, TypeSpec spec)
        {
            source.Append("                values => new ").Append(spec.TypeName);
            if (spec.ActivationKind == ActivationKind.Constructor)
            {
                source.AppendLine("(");
                for (var i = 0; i < spec.Members.Length; i++)
                {
                    var member = spec.Members[i];
                    source.Append("                    (").Append(member.TypeName)
                        .Append(")values[").Append(i).Append("]!");
                    source.AppendLine(i == spec.Members.Length - 1 ? string.Empty : ",");
                }
                source.AppendLine("                ),");
                return;
            }

            source.AppendLine();
            source.AppendLine("                {");
            for (var i = 0; i < spec.Members.Length; i++)
            {
                var member = spec.Members[i];
                source.Append("                    ").Append(member.SourceName)
                    .Append(" = (").Append(member.TypeName).Append(")values[")
                    .Append(i).Append("]!");
                source.AppendLine(i == spec.Members.Length - 1 ? string.Empty : ",");
            }
            source.AppendLine("                },");
        }

        private static void RenderDefaultsFactory(StringBuilder source, TypeSpec spec)
        {
            if (spec.ActivationKind == ActivationKind.Constructor)
            {
                source.AppendLine("                () => new object?[]");
                source.AppendLine("                {");
                foreach (var member in spec.Members)
                {
                    source.Append("                    default(").Append(member.TypeName)
                        .AppendLine("),");
                }
                source.AppendLine("                },");
                return;
            }

            source.AppendLine("                () =>");
            source.AppendLine("                {");
            source.Append("                    var instance = new ").Append(spec.TypeName)
                .AppendLine("();");
            source.AppendLine("                    return new object?[]");
            source.AppendLine("                    {");
            foreach (var member in spec.Members)
            {
                source.Append("                        instance.").Append(member.SourceName)
                    .AppendLine(",");
            }
            source.AppendLine("                    };");
            source.AppendLine("                },");
        }

        private static void RenderMembers(
            StringBuilder source,
            ImmutableArray<MemberSpec> members
            )
        {
            source.AppendLine("                new global::MaxMind.Db.GeneratedMember[]");
            source.AppendLine("                {");
            foreach (var member in members)
            {
                source.Append("                    new(")
                    .Append(SymbolDisplay.FormatLiteral(member.MapKey, quote: true))
                    .Append(", typeof(").Append(member.TypeName).Append("), ");
                source.Append(member.InjectableName == null
                    ? "null"
                    : SymbolDisplay.FormatLiteral(member.InjectableName, quote: true));
                source.Append(", ").Append(member.IsNetwork ? "true" : "false")
                    .Append(", ").Append(member.AlwaysCreate ? "true" : "false")
                    .AppendLine("),");
            }
            source.AppendLine("                }");
        }
    }
}
