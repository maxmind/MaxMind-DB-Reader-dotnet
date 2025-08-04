using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using MaxMind.Db.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MaxMind.Db.SourceGenerators
{
    /// <summary>
    /// Enhanced source generator using incremental compilation patterns for optimal build performance.
    /// Features advanced caching, immutable model types, and efficient attribute discovery.
    /// </summary>
    [Generator]
    public class EnhancedTypeActivatorGenerator : IIncrementalGenerator
    {
        private const string ConstructorAttributeFullName = "MaxMind.Db.ConstructorAttribute";

        /// <summary>
        /// Initializes the incremental generator with enhanced System.Text.Json-inspired patterns.
        /// </summary>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Cache known types for efficient symbol resolution across compilation
            IncrementalValueProvider<KnownTypeSymbols> knownTypes = context.CompilationProvider
                .Select(static (compilation, _) => new KnownTypeSymbols(compilation))
                .WithTrackingName("KnownTypes");

            // Use ForAttributeWithMetadataName for efficient incremental attribute discovery
            // This is significantly faster than broad syntax providers
            // NOTE: Constructor attribute is on constructor methods, not classes
            IncrementalValuesProvider<TypeActivatorSpec?> typeSpecs = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ConstructorAttributeFullName,
                    predicate: static (node, _) => node is ConstructorDeclarationSyntax,
                    transform: static (context, _) =>
                    {
                        var constructor = (ConstructorDeclarationSyntax)context.TargetNode;
                        var containingClass = constructor.Parent as ClassDeclarationSyntax;
                        return (Class: containingClass, context.SemanticModel);
                    })
                .Where(static x => x.Class != null)
                .Combine(knownTypes)
                .Select(static (tuple, ct) =>
                {
                    var ((classDecl, semanticModel), knownTypes) = tuple;
                    if (classDecl == null) return null;
                    var parser = new TypeActivatorParser(knownTypes);
                    return parser.ParseTypeActivatorSpec(classDecl, semanticModel, ct);
                })
                .Where(static spec => spec != null)
                .WithTrackingName("TypeActivatorSpecs");

            // Register source output with collected specs for batch processing
            context.RegisterSourceOutput(typeSpecs.Collect(), GenerateActivators);
        }

        /// <summary>
        /// Generates source code for all discovered type activator specifications.
        /// Uses context-aware generation with parameter name deduplication.
        /// </summary>
        private static void GenerateActivators(
            SourceProductionContext context,
            ImmutableArray<TypeActivatorSpec?> typeSpecs)
        {
            try
            {
                var validSpecs = typeSpecs.Where(spec => spec != null).Cast<TypeActivatorSpec>().ToList();
                if (validSpecs.Count == 0) return;

                // Group by namespace for organized code generation
                var specsByNamespace = validSpecs.GroupBy(spec => spec.Namespace).ToList();

                // Create global parameter mapping cache for deduplication
                var parameterMappingCache = CreateParameterMappingCache(validSpecs);

                // Generate activators for each namespace
                foreach (var namespaceGroup in specsByNamespace)
                {
                    GenerateNamespaceActivators(
                        context,
                        namespaceGroup.Key,
                        namespaceGroup.ToList(),
                        parameterMappingCache);
                }

                // Generate optimized coordinator class
                GenerateOptimizedCoordinator(context, specsByNamespace.Select(g => g.Key).ToArray());
            }
            catch (Exception ex)
            {
                // Generate error information for debugging
                GenerateErrorFile(context, ex);
            }
        }

        /// <summary>
        /// Creates a global parameter mapping cache to avoid duplication across types.
        /// This improves both build performance and runtime efficiency.
        /// </summary>
        private static ImmutableDictionary<string, int> CreateParameterMappingCache(
            IReadOnlyList<TypeActivatorSpec> typeSpecs)
        {
            var parameterNames = typeSpecs
                .SelectMany(spec => spec.Parameters.AsImmutableArray())
                .Select(param => param.DatabaseParameterName ?? param.Name)
                .Distinct()
                .OrderBy(name => name)
                .Select((name, index) => new { name, index })
                .ToImmutableDictionary(x => x.name, x => x.index);

            return parameterNames;
        }

        /// <summary>
        /// Generates activators for a specific namespace with optimized code patterns.
        /// </summary>
        private static void GenerateNamespaceActivators(
            SourceProductionContext context,
            string namespaceName,
            IReadOnlyList<TypeActivatorSpec> specs,
            ImmutableDictionary<string, int> parameterMappingCache)
        {
            var code = new StringBuilder();

            code.AppendLine("// <auto-generated/>");
            code.AppendLine($"// Generated at: {DateTime.UtcNow:O}");
            code.AppendLine($"// Found {specs.Count} types with [Constructor] attribute in namespace '{namespaceName}'");
            foreach (var spec in specs)
            {
                code.AppendLine($"//   {spec.TypeRef.FullyQualifiedName}");
            }
            code.AppendLine();
            code.AppendLine("#nullable enable");
            code.AppendLine();
            code.AppendLine("using global::System;");
            code.AppendLine("using global::System.Collections.Generic;");
            code.AppendLine("using global::System.Collections.Frozen;");
            code.AppendLine("using global::MaxMind.Db;");
            code.AppendLine();

            // Handle global namespace
            bool isGlobalNamespace = namespaceName == "<global>";
            string actualNamespace = isGlobalNamespace ? "MaxMind.Db.Generated.Global" : namespaceName;

            code.AppendLine($"namespace {actualNamespace}");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>");
            code.AppendLine("    /// Auto-registers source-generated activators for MaxMind.Db types");
            code.AppendLine("    /// </summary>");
            code.AppendLine("    internal static class TypeActivatorRegistration");
            code.AppendLine("    {");

            if (specs.Count > 0)
            {
                GenerateStaticConstructor(code, specs, parameterMappingCache);
            }

            GenerateEnsureRegisteredMethod(code);

            code.AppendLine("    }");
            code.AppendLine("}");

            // Generate file with safe namespace name
            var safeNamespace = isGlobalNamespace ? "Global" :
                namespaceName.Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(" ", "_");

            context.AddSource(
                $"TypeActivatorRegistration_{safeNamespace}.g.cs",
                SourceText.From(code.ToString(), Encoding.UTF8));
        }

        /// <summary>
        /// Generates the static constructor that registers all activators.
        /// Uses advanced code patterns for optimal runtime performance.
        /// </summary>
        private static void GenerateStaticConstructor(
            StringBuilder code,
            IReadOnlyList<TypeActivatorSpec> specs,
            ImmutableDictionary<string, int> parameterMappingCache)
        {
            code.AppendLine("        static TypeActivatorRegistration()");
            code.AppendLine("        {");

            foreach (var spec in specs)
            {
                GenerateOptimizedActivator(code, spec, parameterMappingCache);
            }

            code.AppendLine("        }");
            code.AppendLine();
        }

        /// <summary>
        /// Generates an optimized activator for a specific type with pre-computed metadata.
        /// </summary>
        private static void GenerateOptimizedActivator(
            StringBuilder code,
            TypeActivatorSpec spec,
            ImmutableDictionary<string, int> parameterMappingCache)
        {
            var typeName = spec.TypeRef.FullyQualifiedName;

            code.AppendLine($"        // Register optimized activator for {typeName}");
            code.AppendLine("        {");

            // Generate efficient activator function
            code.AppendLine($"            global::System.Func<object?[], object> activator = args => new {typeName}(");

            var parameters = spec.Parameters.AsImmutableArray();
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                string cast = $"({param.Type.FullyQualifiedName})args[{i}]!";
                string comma = i < parameters.Length - 1 ? "," : "";
                code.AppendLine($"                {cast}{comma}");
            }

            code.AppendLine("            );");
            code.AppendLine();

            // Generate pre-computed parameter mappings
            GenerateParameterMappings(code, parameters, parameterMappingCache);

            // Generate parameter position arrays
            GenerateParameterPositionArrays(code, parameters);

            // Register with complete activator
            code.AppendLine($"            global::MaxMind.Db.SourceGeneratorSupport.RegisterCompleteActivator(");
            code.AppendLine($"                typeof({typeName}),");
            code.AppendLine($"                activator,");
            code.AppendLine($"                parameterMappings,");
            code.AppendLine($"                injectableMappings,");
            code.AppendLine($"                networkParameterPositions,");
            code.AppendLine($"                alwaysCreatedParameterPositions);");
            code.AppendLine("        }");
            code.AppendLine();
        }

        /// <summary>
        /// Generates pre-computed parameter mappings dictionary.
        /// </summary>
        private static void GenerateParameterMappings(
            StringBuilder code,
            ImmutableArray<ParameterSpec> parameters,
            ImmutableDictionary<string, int> parameterMappingCache)
        {
            code.AppendLine("            var parameterMappings = new global::System.Collections.Generic.Dictionary<string, (int Position, global::System.Type ParameterType)>");
            code.AppendLine("            {");

            foreach (var param in parameters)
            {
                var dbName = param.DatabaseParameterName ?? param.Name;
                var typeForTypeof = param.Type.FullyQualifiedName.EndsWith("?")
                    ? param.Type.FullyQualifiedName[..^1]
                    : param.Type.FullyQualifiedName;

                code.AppendLine($"                {{ \"{dbName}\", ({param.Position}, typeof({typeForTypeof})) }},");
            }

            code.AppendLine("            }.ToFrozenDictionary();");
            code.AppendLine();

            // Generate injectable mappings
            code.AppendLine("            var injectableMappings = new global::System.Collections.Generic.Dictionary<string, int>");
            code.AppendLine("            {");

            foreach (var param in parameters.Where(p => p.IsInjectable))
            {
                var injectName = param.InjectableParameterName ?? param.Name;
                code.AppendLine($"                {{ \"{injectName}\", {param.Position} }},");
            }

            code.AppendLine("            }.ToFrozenDictionary();");
            code.AppendLine();
        }

        /// <summary>
        /// Generates parameter position arrays for fast runtime access.
        /// </summary>
        private static void GenerateParameterPositionArrays(
            StringBuilder code,
            ImmutableArray<ParameterSpec> parameters)
        {
            var networkPositions = parameters
                .Where(p => p.IsNetwork)
                .Select(p => p.Position.ToString())
                .ToArray();

            var alwaysCreatedPositions = parameters
                .Where(p => p.IsAlwaysCreated)
                .Select(p => p.Position.ToString())
                .ToArray();

            code.AppendLine($"            var networkParameterPositions = new int[] {{ {string.Join(", ", networkPositions)} }};");
            code.AppendLine($"            var alwaysCreatedParameterPositions = new int[] {{ {string.Join(", ", alwaysCreatedPositions)} }};");
            code.AppendLine();
        }

        /// <summary>
        /// Generates the EnsureRegistered method.
        /// </summary>
        private static void GenerateEnsureRegisteredMethod(StringBuilder code)
        {
            code.AppendLine("        /// <summary>");
            code.AppendLine("        /// Ensures that all type activators are registered. This method is called automatically.");
            code.AppendLine("        /// </summary>");
            code.AppendLine("        public static void EnsureRegistered()");
            code.AppendLine("        {");
            code.AppendLine("            // Static constructor will run when this method is called");
            code.AppendLine("        }");
        }

        /// <summary>
        /// Generates the coordinator class that calls all namespace registrations.
        /// </summary>
        private static void GenerateOptimizedCoordinator(
            SourceProductionContext context,
            string[] namespaces)
        {
            var code = new StringBuilder();

            code.AppendLine("// <auto-generated/>");
            code.AppendLine($"// Generated at: {DateTime.UtcNow:O}");
            code.AppendLine($"// Coordinator for {namespaces.Length} namespaces");
            code.AppendLine();
            code.AppendLine("#nullable enable");
            code.AppendLine();
            code.AppendLine("using global::System;");
            code.AppendLine();
            code.AppendLine("namespace MaxMind.Db.Generated");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>");
            code.AppendLine("    /// Auto-registers source-generated activators for MaxMind.Db types");
            code.AppendLine("    /// </summary>");
            code.AppendLine("    internal static class TypeActivatorRegistration");
            code.AppendLine("    {");
            code.AppendLine("        /// <summary>");
            code.AppendLine("        /// Ensures that all type activators are registered. This method is called automatically.");
            code.AppendLine("        /// </summary>");
            code.AppendLine("        public static void EnsureRegistered()");
            code.AppendLine("        {");

            foreach (var ns in namespaces)
            {
                bool isGlobalNs = ns == "<global>";
                string targetNamespace = isGlobalNs ? "MaxMind.Db.Generated.Global" : ns;
                code.AppendLine($"            global::{targetNamespace}.TypeActivatorRegistration.EnsureRegistered();");
            }

            code.AppendLine("        }");
            code.AppendLine("    }");
            code.AppendLine("}");

            context.AddSource(
                "MaxMind.Db.Generated.TypeActivatorRegistration.g.cs",
                SourceText.From(code.ToString(), Encoding.UTF8));
        }

        /// <summary>
        /// Generates an error file for debugging generator issues.
        /// </summary>
        private static void GenerateErrorFile(SourceProductionContext context, Exception ex)
        {
            var errorCode = $@"// <auto-generated/>
// Source generator error: {ex.Message}
// Stack trace: {ex.StackTrace?.Replace("\n", "\n// ")}

namespace MaxMind.Db.Generated
{{
    internal static class TypeActivatorRegistration
    {{
        public static void EnsureRegistered() {{ }}
    }}
}}";

            context.AddSource(
                "MaxMind.Db.Generated.TypeActivatorRegistration.Error.g.cs",
                SourceText.From(errorCode, Encoding.UTF8));
        }
    }
}