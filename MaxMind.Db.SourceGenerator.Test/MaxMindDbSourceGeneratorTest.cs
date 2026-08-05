using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using MaxMind.Db.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace MaxMind.Db.SourceGenerator.Test
{
    public class MaxMindDbSourceGeneratorTest
    {
        [Fact]
        public void GeneratesConstructorMetadataDeterministically()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class ConstructorModel
                {
                    [Constructor]
                    internal ConstructorModel(
                        [MapKey("city", true)] string city,
                        [Inject("locales")] string[] locales,
                        [Network] Network network)
                    {
                    }
                }
                """;

            var first = RunGenerator(modelSource, aotDiagnostics: true);
            var second = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Equal(first.Source, second.Source);
            Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializerAttribute]", first.Source);
            Assert.Contains("new global::Models.ConstructorModel(", first.Source);
            Assert.Contains("new global::MaxMind.Db.GeneratedMember[]", first.Source);
            Assert.Contains(
                "global::MaxMind.Db.GeneratedMember.Mapped(\"city\", typeof(global::System.String), true)",
                first.Source);
            Assert.Contains(
                "global::MaxMind.Db.GeneratedMember.Injected(\"locales\", typeof(global::System.String[]))",
                first.Source);
            Assert.Contains(
                "global::MaxMind.Db.GeneratedMember.Networked(typeof(global::MaxMind.Db.Network))",
                first.Source);
            Assert.DoesNotContain(
                first.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG008");
            Assert.Empty(first.Errors);
        }

        [Fact]
        public void GeneratesPositionalRecordConstructor()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                [method: Constructor]
                internal sealed record PositionalModel(string value);
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains("new global::Models.PositionalModel(", result.Source);
            Assert.Contains(
                "global::MaxMind.Db.GeneratedMember.Mapped(\"value\", typeof(global::System.String), false)",
                result.Source);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesPrimaryClassConstructor()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                [method: Constructor]
                internal sealed class PrimaryModel(
                    [MapKey("database_value")] string value)
                {
                    internal string Value { get; } = value;
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains("new global::Models.PrimaryModel(", result.Source);
            Assert.Contains(
                "global::MaxMind.Db.GeneratedMember.Mapped(\"database_value\", typeof(global::System.String), false)",
                result.Source);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesConcretePropertyModelWithInheritedAttributes()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal abstract record CityResponseBase
                {
                    [MapKey("city")]
                    public string? City { get; init; }

                    [Inject("locales")]
                    public string[] Locales { get; init; } = [];
                }

                internal sealed record CityResponse : CityResponseBase;
                """;

            var result = RunGenerator(modelSource);

            Assert.Contains("new global::Models.CityResponse", result.Source);
            Assert.Contains("City = (global::System.String)values[0]!", result.Source);
            Assert.Contains("Locales = (global::System.String[])values[1]!", result.Source);
            Assert.Contains("var instance = new global::Models.CityResponse();", result.Source);
            Assert.DoesNotContain("CityResponseBase),", result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void SupportsDeprecatedParameterAttribute()
        {
            const string modelSource = """
                #pragma warning disable CS0618
                using MaxMind.Db;

                namespace Models;

                internal sealed class DeprecatedModel
                {
                    [Constructor]
                    internal DeprecatedModel([Parameter("database_name", true)] string value)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource);

            Assert.Contains(
                "global::MaxMind.Db.GeneratedMember.Mapped(\"database_name\", typeof(global::System.String), true)",
                result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsDerivedMapKeyAttributesForAotAndFallsBackOtherwise()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class PrefixedKeyAttribute : MapKeyAttribute
                {
                    internal PrefixedKeyAttribute(string name)
                        : base("prefix_" + name, true)
                    {
                    }
                }

                internal sealed class DerivedAttributeModel
                {
                    [Constructor]
                    internal DerivedAttributeModel([PrefixedKey("city")] string value)
                    {
                    }
                }

                internal sealed class DerivedAttributePropertyModel
                {
                    [PrefixedKey("country")]
                    internal string? Value { get; set; }
                }
                """;

            var normalResult = RunGenerator(modelSource);
            var aotResult = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Empty(normalResult.Source);
            Assert.Empty(normalResult.Diagnostics);
            Assert.Empty(normalResult.Errors);
            Assert.Equal(
                2,
                aotResult.Diagnostics.Count(diagnostic => diagnostic.Id == "MMDBSG011"));
            Assert.Empty(aotResult.Source);
            Assert.Empty(aotResult.Errors);
        }

        [Fact]
        public void ReportsAotDiagnosticsOnlyWhenEnabled()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class InaccessibleModel
                {
                    [Constructor]
                    private InaccessibleModel(string value)
                    {
                    }
                }
                """;

            var normalResult = RunGenerator(modelSource);
            var aotResult = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.DoesNotContain(
                normalResult.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG002");
            Assert.Contains(
                aotResult.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG002");
        }

        [Fact]
        public void FallsBackBeforeCSharp9()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models
                {
                    internal sealed class ConstructorModel
                    {
                        [Constructor]
                        internal ConstructorModel(string value)
                        {
                        }
                    }
                }
                """;

            var normalResult = RunGenerator(
                modelSource,
                languageVersion: LanguageVersion.CSharp8);
            var aotResult = RunGenerator(
                modelSource,
                aotDiagnostics: true,
                languageVersion: LanguageVersion.CSharp8);

            Assert.Empty(normalResult.Source);
            Assert.Empty(normalResult.Errors);
            Assert.Contains(
                aotResult.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG009");
            Assert.Empty(aotResult.Errors);
        }

        [Fact]
        public void ReportsCollectionOnlyGenerationBeforeCSharp9()
        {
            const string modelSource = """
                using System.Collections.Generic;
                using MaxMind.Db;

                internal sealed class Lookup
                {
                    internal void Run(Reader reader)
                    {
                        _ = reader.FindAll<List<long>>();
                    }
                }
                """;

            var result = RunGenerator(
                modelSource,
                aotDiagnostics: true,
                languageVersion: LanguageVersion.CSharp8);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG009");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesRegistrationInCSharp9()
        {
            const string modelSource = """
                using MaxMind.Db;

                internal sealed class ConstructorModel
                {
                    [Constructor]
                    internal ConstructorModel(string value)
                    {
                    }
                }
                """;

            var result = RunGenerator(
                modelSource,
                aotDiagnostics: true,
                languageVersion: LanguageVersion.CSharp9);

            Assert.Contains("new global::MaxMind.Db.GeneratedMember[]", result.Source);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsFileLocalModelsForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                file sealed class FileModel
                {
                    [Constructor]
                    internal FileModel(string value)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG001");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsHiddenAnnotatedPropertyForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal class BaseModel
                {
                    [MapKey("utf8_string")]
                    internal string? Value { get; init; }
                }

                internal sealed class ShadowModel : BaseModel
                {
                    internal new int Value { get; init; }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG014");
            Assert.DoesNotContain("Models.ShadowModel", result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesOverriddenAnnotatedPropertyWithoutHidingDiagnostic()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal class OverrideBaseModel
                {
                    [MapKey("utf8_string")]
                    internal virtual string? Value { get; set; }
                }

                internal sealed class OverrideModel : OverrideBaseModel
                {
                    internal override string? Value { get; set; }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG014");
            Assert.Contains("new global::Models.OverrideModel", result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsMapKeyCombinedWithInjectForAot()
        {
            const string modelSource = """
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class ConflictModel
                {
                    [Constructor]
                    internal ConflictModel(
                        [MapKey("ip")] [Inject("ip_address")] IPAddress? address = null)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG013");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsMapKeyCombinedWithNetworkForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed record NetworkConflictModel
                {
                    [MapKey("net")]
                    [Network]
                    internal Network? Value { get; init; }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG013");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void InjectableMemberNamesDoNotCollideWithMapKeys()
        {
            const string modelSource = """
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class InjectModel
                {
                    [Constructor]
                    internal InjectModel(
                        [MapKey("city")] string? name = null,
                        [Inject("ip_address")] IPAddress? city = null)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG005");
            Assert.Contains("new global::Models.InjectModel(", result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsStructModelsForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal struct StructModel
                {
                    [Constructor]
                    internal StructModel([MapKey("utf8_string")] string value)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG012");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsRecordStructPropertyModelsForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal record struct RecordStructModel
                {
                    [MapKey("utf8_string")]
                    internal string? Value { get; init; }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG012");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void SkipsStructModelsWithoutAotDiagnostics()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal struct StructModel
                {
                    [Constructor]
                    internal StructModel([MapKey("utf8_string")] string value)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource);

            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG012");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsPositionalRecordWithoutConstructorAttributeForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed record PositionalModel([MapKey("v")] string Value);
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG016");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void IgnoresUnannotatedTypesWithoutDiagnostics()
        {
            const string modelSource = """
                using System;
                using System.Collections.Generic;

                namespace Models;

                internal sealed class Unrelated : List<string>, IDisposable
                {
                    internal Unrelated(string name)
                    {
                        Name = name;
                    }

                    internal string Name { get; }

                    public void Dispose()
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsRequiredFieldsForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class RequiredFieldModel
                {
                    [Constructor]
                    internal RequiredFieldModel([MapKey("utf8_string")] string value)
                    {
                    }

                    internal required string Extra;
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG010");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsInaccessibleInheritedPropertyForAot()
        {
            const string baseSource = """
                using MaxMind.Db;

                namespace ExternalModels
                {
                    public abstract class ExternalBase
                    {
                        [MapKey("value")]
                        public string? Value { get; internal init; }
                    }
                }
                """;
            const string modelSource = """
                namespace Models;

                internal sealed class DerivedModel : ExternalModels.ExternalBase;
                """;
            var baseReference = CompileReference(baseSource);

            var result = RunGenerator(
                modelSource,
                aotDiagnostics: true,
                additionalReferences: [baseReference]);

            var diagnostic = Assert.Single(
                result.Diagnostics,
                candidate => candidate.Id == "MMDBSG003");
            // The offending property lives in a referenced assembly, so without a
            // fallback the warning lands at Location.None with no file or line.
            Assert.True(diagnostic.Location.IsInSource);
            Assert.Contains("DerivedModel", diagnostic.Location.SourceTree!.ToString());
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsOpenGenericModelsForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class GenericModel<T>
                {
                    [Constructor]
                    internal GenericModel(T value)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG004");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsModelsNestedInGenericTypesForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class Outer<T>
                {
                    internal sealed class NestedModel
                    {
                        [Constructor]
                        internal NestedModel(string value)
                        {
                        }
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Equal(
                1,
                result.Diagnostics.Count(diagnostic => diagnostic.Id == "MMDBSG004"));
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsDuplicateMapKeysForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class DuplicateModel
                {
                    [Constructor]
                    internal DuplicateModel(
                        [MapKey("value")] string first,
                        [MapKey("value")] string second)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG005");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsMissingParameterlessConstructorsForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class PropertyModel
                {
                    internal PropertyModel(string value)
                    {
                    }

                    [MapKey("value")]
                    internal string? Value { get; set; }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG006");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsMultipleDeserializationConstructorsForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class AmbiguousModel
                {
                    [Constructor]
                    internal AmbiguousModel(string value)
                    {
                    }

                    [Constructor]
                    internal AmbiguousModel(long value)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG007");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsRequiredMembersWithoutSetsRequiredMembersForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class RequiredModel
                {
                    [MapKey("value")]
                    public required string Value { get; init; }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG010");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesRequiredMembersWithSetsRequiredMembersConstructor()
        {
            const string modelSource = """
                using System.Diagnostics.CodeAnalysis;
                using MaxMind.Db;

                namespace Models;

                internal sealed class RequiredModel
                {
                    [SetsRequiredMembers]
                    internal RequiredModel()
                    {
                        Value = string.Empty;
                    }

                    [MapKey("value")]
                    public required string Value { get; init; }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains("new global::Models.RequiredModel", result.Source);
            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG010");
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesCollectionFactoriesAndAddDelegates()
        {
            const string modelSource = """
                using System.Collections.Generic;
                using MaxMind.Db;

                namespace Models;

                internal sealed class CustomList<T> : List<T>;
                internal sealed class CustomDictionary<TKey, TValue> : Dictionary<TKey, TValue>
                    where TKey : notnull;
                internal sealed class NonGenericDictionary : Dictionary<string, long>;

                internal sealed class CollectionModel
                {
                    [Constructor]
                    internal CollectionModel(
                        ICollection<long> collection,
                        IReadOnlyList<long> readOnly,
                        LinkedList<long> linked,
                        CustomList<long> concrete,
                        IDictionary<string, long> dictionary,
                        IReadOnlyDictionary<string, long> readOnlyDictionary,
                        CustomDictionary<string, long> concreteDictionary,
                        NonGenericDictionary nonGenericDictionary)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource);

            Assert.Contains("RegisterCollection<", result.Source);
            Assert.Contains("RegisterCollection<global::System.Collections.Generic.IReadOnlyList<global::System.Int64>, global::System.Int64>(", result.Source);
            Assert.Contains("capacity => new global::System.Collections.Generic.List<global::System.Int64>(capacity)", result.Source);
            Assert.Contains("capacity => new global::System.Collections.Generic.LinkedList<global::System.Int64>()", result.Source);
            Assert.Contains("capacity => new global::Models.CustomList<global::System.Int64>()", result.Source);
            Assert.Contains("RegisterDictionary<", result.Source);
            Assert.Contains("RegisterDictionary<global::System.Collections.Generic.IReadOnlyDictionary<global::System.String, global::System.Int64>, global::System.String, global::System.Int64>(", result.Source);
            Assert.Contains("capacity => new global::Models.CustomDictionary<global::System.String, global::System.Int64>()", result.Source);
            Assert.Contains(
                "RegisterDictionary<global::Models.NonGenericDictionary, global::System.String, global::System.Int64>(",
                result.Source);
            Assert.Contains("capacity => new global::Models.NonGenericDictionary()", result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesCollectionsUsedByReaderCalls()
        {
            const string modelSource = """
                using System.Collections.Concurrent;
                using System.Collections.Generic;
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class Lookup
                {
                    internal void Run(Reader reader, IPAddress address)
                    {
                        _ = reader.Find<ConcurrentDictionary<string, object>>(address);
                        _ = reader.FindAll<LinkedList<long>>();
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                "RegisterDictionary<global::System.Collections.Concurrent.ConcurrentDictionary<global::System.String, global::System.Object>, global::System.String, global::System.Object>(",
                result.Source);
            Assert.Contains(
                "RegisterCollection<global::System.Collections.Generic.LinkedList<global::System.Int64>, global::System.Int64>(",
                result.Source);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void GeneratesCollectionsUsedByConditionalReaderCalls()
        {
            const string modelSource = """
                using System.Collections.Concurrent;
                using System.Collections.Generic;
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class Lookup
                {
                    internal void Run(Reader? reader, IPAddress address)
                    {
                        _ = reader?.Find<ConcurrentDictionary<string, object>>(address);
                        _ = reader?.FindAll<LinkedList<long>>();
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                "RegisterDictionary<global::System.Collections.Concurrent.ConcurrentDictionary<global::System.String, global::System.Object>, global::System.String, global::System.Object>(",
                result.Source);
            Assert.Contains(
                "RegisterCollection<global::System.Collections.Generic.LinkedList<global::System.Int64>, global::System.Int64>(",
                result.Source);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsClosedCollectionsContainingTypeParametersForAot()
        {
            const string modelSource = """
                using System.Collections.Generic;
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class Lookup
                {
                    internal void Run<T>(Reader reader, IPAddress address)
                    {
                        _ = reader.Find<Dictionary<string, T>>(address);
                        _ = reader.FindAll<List<T>>();
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            // MMDBSG015 rather than MMDBSG008: the cause is the unresolved type
            // parameter, and Dictionary<string, T> being a collection is incidental.
            Assert.Equal(
                2,
                result.Diagnostics.Count(diagnostic => diagnostic.Id == "MMDBSG015"));
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsCollectionsWithInaccessibleTypeArgumentsForAot()
        {
            const string modelSource = """
                using System.Collections.Generic;
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class Lookup
                {
                    private sealed class PrivateModel;

                    internal void Run(Reader reader, IPAddress address)
                    {
                        _ = reader.Find<IReadOnlyList<PrivateModel>>(address);
                        _ = reader.Find<IReadOnlyDictionary<string, PrivateModel>>(address);
                        _ = reader.Find<Outer<PrivateModel>.Values>(address);
                    }

                    internal sealed class Outer<T>
                    {
                        internal sealed class Values : List<long>
                        {
                        }
                    }
                }
                """;

            var normalResult = RunGenerator(modelSource);
            var aotResult = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Empty(normalResult.Source);
            Assert.Empty(normalResult.Diagnostics);
            Assert.Empty(normalResult.Errors);
            Assert.Equal(
                3,
                aotResult.Diagnostics.Count(diagnostic => diagnostic.Id == "MMDBSG008"));
            Assert.Empty(aotResult.Source);
            Assert.Empty(aotResult.Errors);
        }

        [Fact]
        public void IgnoresGenericReaderWrappersWithoutGeneratingInvalidCode()
        {
            const string modelSource = """
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class Lookup
                {
                    internal T? Run<T>(Reader reader, IPAddress address) where T : class
                    {
                        return reader.Find<T>(address);
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Empty(result.Source);
            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG015");
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void IgnoresGenericReaderWrappersSilentlyWithoutAotDiagnostics()
        {
            const string modelSource = """
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class Lookup
                {
                    internal T? Run<T>(Reader reader, IPAddress address) where T : class
                    {
                        return reader.Find<T>(address);
                    }
                }
                """;

            var result = RunGenerator(modelSource);

            Assert.Empty(result.Source);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsUnsupportedCollectionsUsedByReaderCalls()
        {
            const string modelSource = """
                using System.Net;
                using MaxMind.Db;

                namespace Models;

                internal sealed class Lookup
                {
                    internal void Run(Reader reader, IPAddress address)
                    {
                        _ = reader.Find<long[]>(address);
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG008");
            Assert.Empty(result.Source);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void IgnoresUnrelatedGenericFindMethods()
        {
            const string modelSource = """
                using System.Collections.Concurrent;

                namespace Models;

                internal sealed class Lookup
                {
                    private static T Find<T>() where T : class => null!;

                    internal void Run()
                    {
                        _ = Find<ConcurrentDictionary<string, object>>();
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Empty(result.Source);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsValueTypeCollectionForAot()
        {
            const string modelSource = """
                using System.Collections.Immutable;
                using MaxMind.Db;

                namespace Models;

                internal sealed class ImmutableModel
                {
                    [Constructor]
                    internal ImmutableModel(ImmutableArray<long> values)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG008");
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void AllowsByteArrayWithoutDiagnostic()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class BytesModel
                {
                    [Constructor]
                    internal BytesModel(byte[] value)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG008");
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ReportsUnsupportedArrayForAot()
        {
            const string modelSource = """
                using MaxMind.Db;

                namespace Models;

                internal sealed class ArrayModel
                {
                    [Constructor]
                    internal ArrayModel(long[] values)
                    {
                    }
                }
                """;

            var result = RunGenerator(modelSource, aotDiagnostics: true);

            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Id == "MMDBSG008");
        }

        [Fact]
        public void GeneratedCodeCanPopulateObsoleteProperties()
        {
            const string modelSource = """
                using System;
                using MaxMind.Db;

                namespace Models;

                internal sealed record ObsoletePropertyModel
                {
                    [Obsolete("Kept for database compatibility.")]
                    [MapKey("legacy")]
                    public string? Legacy { get; init; }
                }
                """;

            var result = RunGenerator(modelSource, warningsAsErrors: true);

            Assert.Contains("#pragma warning disable CS0436", result.Source);
            Assert.Contains("#pragma warning disable CS0618", result.Source);
            Assert.Contains("\"legacy\"", result.Source);
            Assert.Contains("Legacy =", result.Source);
            Assert.Empty(result.Errors);
        }

        private static GeneratorResult RunGenerator(
            string source,
            bool aotDiagnostics = false,
            bool warningsAsErrors = false,
            LanguageVersion languageVersion = LanguageVersion.CSharp12,
            IEnumerable<MetadataReference>? additionalReferences = null
            )
        {
            var parseOptions = new CSharpParseOptions(languageVersion);
            var references = additionalReferences == null
                ? References
                : References.AddRange(additionalReferences);
            var compilation = CSharpCompilation.Create(
                "GeneratorTests",
                [CSharpSyntaxTree.ParseText(source, parseOptions)],
                references,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable,
                    generalDiagnosticOption: warningsAsErrors
                        ? ReportDiagnostic.Error
                        : ReportDiagnostic.Default));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new MaxMindDbSourceGenerator().AsSourceGenerator()],
                parseOptions: parseOptions,
                optionsProvider: new TestAnalyzerConfigOptionsProvider(aotDiagnostics));

            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

            var runResult = driver.GetRunResult();
            var generatedSource = runResult.GeneratedTrees.Length == 0
                ? string.Empty
                : Assert.Single(runResult.GeneratedTrees).GetText().ToString();
            var errors = outputCompilation.GetDiagnostics()
                .Concat(generatorDiagnostics)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();
            return new GeneratorResult(
                generatedSource,
                errors,
                runResult.Diagnostics);
        }

        private static MetadataReference CompileReference(string source)
        {
            var compilation = CSharpCompilation.Create(
                "GeneratorTestReference_" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(
                    source,
                    new CSharpParseOptions(LanguageVersion.CSharp12))],
                References,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(
                result.Success,
                string.Join(Environment.NewLine, result.Diagnostics));
            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        private static readonly ImmutableArray<MetadataReference> References =
            CreateReferences();

        private static ImmutableArray<MetadataReference> CreateReferences()
        {
            var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
                .Split(Path.PathSeparator) ?? [];
            var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths.Append(typeof(ConstructorAttribute).Assembly.Location))
            {
                references[path] = MetadataReference.CreateFromFile(path);
            }
            return references.Values.ToImmutableArray();
        }

        private sealed class GeneratorResult
        {
            internal GeneratorResult(
                string source,
                ImmutableArray<Diagnostic> errors,
                ImmutableArray<Diagnostic> diagnostics
                )
            {
                Source = source;
                Errors = errors;
                Diagnostics = diagnostics;
            }

            internal ImmutableArray<Diagnostic> Diagnostics { get; }
            internal ImmutableArray<Diagnostic> Errors { get; }
            internal string Source { get; }
        }

        private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly AnalyzerConfigOptions _globalOptions;

            internal TestAnalyzerConfigOptionsProvider(bool aotDiagnostics)
            {
                _globalOptions = new TestAnalyzerConfigOptions(aotDiagnostics);
            }

            public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
                EmptyAnalyzerConfigOptions.Instance;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
                EmptyAnalyzerConfigOptions.Instance;
        }

        private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            private readonly bool _aotDiagnostics;

            internal TestAnalyzerConfigOptions(bool aotDiagnostics)
            {
                _aotDiagnostics = aotDiagnostics;
            }

            public override bool TryGetValue(string key, out string value)
            {
                if (key == "build_property.MaxMindDbAotDiagnostics")
                {
                    value = _aotDiagnostics ? "true" : "false";
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }

        private sealed class EmptyAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            internal static EmptyAnalyzerConfigOptions Instance { get; } = new();

            public override bool TryGetValue(string key, out string value)
            {
                value = string.Empty;
                return false;
            }
        }
    }
}
