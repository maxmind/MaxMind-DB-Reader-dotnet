#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Numerics;
using System.Text;
using MaxMind.Db.ReflectionFallback.TestModels;
using MaxMind.Db.Test.Helper;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    public class SourceGeneratorSupportTest
    {
        private readonly string _testDataRoot =
            Path.Combine(TestUtils.TestDirectory, "TestData", "MaxMind-DB", "test-data");

        [Fact]
        public void SourceGeneratedTestModelsAreRegistered()
        {
            Assert.True(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(TypeHolder), out _));
            Assert.True(SourceGeneratorSupport.TryGetCollectionRegistration(
                typeof(ICollection<long>), out _));
        }

        [Fact]
        public void ReflectionFallbackDeserializesModelsFromAssemblyWithoutGenerator()
        {
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(ReflectionConstructorModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(ReflectionPropertyModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetCollectionRegistration(
                typeof(FallbackList<long>), out _));

            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var address = IPAddress.Parse("1.1.1.1");

            var constructorModel = reader.Find<ReflectionConstructorModel>(address);
            Assert.NotNull(constructorModel);
            Assert.Equal("unicode! ☯ - ♫", constructorModel.Utf8String);
            Assert.Equal([1, 2, 3], constructorModel.Values);

            var propertyModel = reader.Find<ReflectionPropertyModel>(address);
            Assert.NotNull(propertyModel);
            Assert.Equal("unicode! ☯ - ♫", propertyModel.Utf8String);
            Assert.Equal([1, 2, 3], propertyModel.Values);
            Assert.Equal("preserved default", propertyModel.Missing);
        }

        [Fact]
        public void ReflectionFallbackDecodesEveryTypeHolderMember()
        {
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(ReflectionTypeHolder), out _));
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");

            var record = reader.Find<ReflectionTypeHolder>(
                IPAddress.Parse("1.1.1.1"), injectables);

            Assert.NotNull(record);
            Assert.True(record.Boolean);
            Assert.Equal([0, 0, 0, 42], record.Bytes);
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);
            Assert.Equal(new List<long> { 1, 2, 3 }, record.Array);
            Assert.Equal(42.123456, record.Double, 9);
            Assert.Equal(1.1F, record.Float, 5);
            Assert.Equal(-268435456, record.Int32);
            Assert.Equal(100, record.Uint16);
            Assert.Equal(268435456, record.Uint32);
            Assert.Equal(1152921504606846976UL, record.Uint64);
            Assert.Equal(
                BigInteger.Parse("1329227995784915872903807060280344576"),
                record.Uint128);

            var mapX = record.Map.MapX;
            Assert.Equal("hello", mapX.Utf8StringX);
            Assert.Equal(new List<long> { 7, 8, 9 }, mapX.ArrayX);
            Assert.Equal("1.1.1.0/24", mapX.Network.ToString());

            // AlwaysCreate through reflection, including injection and network on a
            // member whose parent is absent from the database.
            Assert.Equal("injected string", record.Nonexistant.Injected);
            Assert.Equal("1.1.1.0/24", record.Nonexistant.Network.ToString());
            Assert.Equal(
                "injected string",
                record.Nonexistant.InnerNonexistant.Injected);
            Assert.Equal(
                "1.1.1.0/24",
                record.Nonexistant.InnerNonexistant.Network.ToString());
        }

        [Fact]
        public void ReflectionFallbackDecodesEveryPropertyHolderMember()
        {
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(ReflectionPropTypeHolder), out _));
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");

            var record = reader.Find<ReflectionPropTypeHolder>(
                IPAddress.Parse("1.1.1.1"), injectables);

            Assert.NotNull(record);
            Assert.True(record.Boolean);
            Assert.Equal([0, 0, 0, 42], record.Bytes);
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);
            Assert.Equal(new List<long> { 1, 2, 3 }, record.Array);
            Assert.Equal(42.123456, record.Double, 9);
            Assert.Equal(1.1F, record.Float, 5);
            Assert.Equal(-268435456, record.Int32);
            Assert.Equal(100, record.Uint16);
            Assert.Equal(268435456, record.Uint32);
            Assert.Equal(1152921504606846976UL, record.Uint64);
            Assert.Equal(
                BigInteger.Parse("1329227995784915872903807060280344576"),
                record.Uint128);
            Assert.Equal("injected string", record.Injected);
            Assert.Equal("1.1.1.0/24", record.Network?.ToString());

            var mapX = record.Map?.MapX;
            Assert.NotNull(mapX);
            Assert.Equal("hello", mapX.Utf8StringX);
            Assert.Equal(new List<long> { 7, 8, 9 }, mapX.ArrayX);
            Assert.Equal("1.1.1.0/24", mapX.Network?.ToString());
        }

        [Fact]
        public void ReflectionAlwaysCreateLeavesValueTypeMembersAtTheirDefault()
        {
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var address = IPAddress.Parse("1.1.1.1");

            var constructorModel =
                reader.Find<ReflectionAlwaysCreateConstructorModel>(address);
            Assert.NotNull(constructorModel);
            Assert.Equal(0, constructorModel.AbsentValueType);
            Assert.NotNull(constructorModel.AbsentModel);

            var propertyModel = reader.Find<ReflectionAlwaysCreatePropertyModel>(address);
            Assert.NotNull(propertyModel);
            Assert.Equal(0, propertyModel.AbsentValueType);
            Assert.NotNull(propertyModel.AbsentModel);
        }

        [Fact]
        public void GeneratedAlwaysCreateLeavesValueTypeMembersAtTheirDefault()
        {
            SourceGeneratorSupport.RegisterType(
                typeof(AlwaysCreateValueTypeGeneratedModel),
                values => new AlwaysCreateValueTypeGeneratedModel((long)values[0]!),
                () => [default(long)],
                [new GeneratedMember("no_such_key", typeof(long), null, false, true)]);
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));

            var model = reader.Find<AlwaysCreateValueTypeGeneratedModel>(
                IPAddress.Parse("1.1.1.1"));

            Assert.NotNull(model);
            Assert.Equal(0, model.Value);
        }

        [Fact]
        public void DerivedMapKeyAttributesUseReflectionFallbackSemantics()
        {
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(DerivedMapKeyFallbackModel), out _));
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(DerivedMapKeyPropertyFallbackModel), out _));

            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var address = IPAddress.Parse("1.1.1.1");
            var constructorModel = reader.Find<DerivedMapKeyFallbackModel>(address);
            var propertyModel = reader.Find<DerivedMapKeyPropertyFallbackModel>(address);

            Assert.NotNull(constructorModel);
            Assert.Equal("unicode! ☯ - ♫", constructorModel.Utf8String);
            Assert.NotNull(propertyModel);
            Assert.Equal("unicode! ☯ - ♫", propertyModel.Utf8String);
            Assert.NotNull(propertyModel.AlwaysCreated);
        }

        [Fact]
        public void RegisteredActivatorTakesPrecedenceOverReflection()
        {
            SourceGeneratorSupport.RegisterType(
                typeof(GeneratedAlwaysCreated),
                values => new GeneratedAlwaysCreated((string)values[0]!),
                () => [null],
                [new GeneratedMember("injected", typeof(string), "injected", false, false)]);
            SourceGeneratorSupport.RegisterType(
                typeof(GeneratedModel),
                values => new GeneratedModel(
                    (string)values[0]!,
                    (string)values[1]!,
                    (Network?)values[2],
                    (GeneratedAlwaysCreated)values[3]!),
                () => [null, null, null, null],
                [
                    new GeneratedMember("utf8_string", typeof(string), null, false, false),
                    new GeneratedMember("injected", typeof(string), "injected", false, false),
                    new GeneratedMember("network", typeof(Network), null, true, false),
                    new GeneratedMember(
                        "missing", typeof(GeneratedAlwaysCreated), null, false, true),
                ]);

            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));

            var model = reader.Find<GeneratedModel>(IPAddress.Parse("1.1.1.1"), injectables);

            Assert.NotNull(model);
            Assert.Equal("unicode! ☯ - ♫", model.Utf8String);
            Assert.Equal("injected string", model.Injected);
            Assert.Equal("1.1.1.0/24", model.Network?.ToString());
            Assert.Equal("injected string", model.AlwaysCreated.Injected);
        }

        [Fact]
        public void RegisterTypeRejectsDefaultGeneratedMember()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                SourceGeneratorSupport.RegisterType(
                    typeof(InvalidGeneratedModel),
                    _ => new InvalidGeneratedModel(),
                    () => [null],
                    [default]));

            Assert.Equal("members", exception.ParamName);
            Assert.False(SourceGeneratorSupport.TryGetTypeRegistration(
                typeof(InvalidGeneratedModel), out _));
        }

        [Fact]
        public void RegisterTypeDefensivelyCopiesMembers()
        {
            var members = new[]
            {
                new GeneratedMember("original", typeof(string), null, false, false),
            };
            SourceGeneratorSupport.RegisterType(
                typeof(CopiedMembersGeneratedModel),
                _ => new CopiedMembersGeneratedModel(),
                () => [null],
                members);

            members[0] = new GeneratedMember("changed", typeof(long), null, false, false);
            var activator = new TypeActivatorCreator()
                .GetActivator(typeof(CopiedMembersGeneratedModel));

            Assert.True(activator.DeserializationParameters.ContainsKey(
                new Key(Encoding.UTF8.GetBytes("original"))));
            Assert.Equal(
                typeof(string),
                Assert.Single(activator.DeserializationParameters).Value.MemberType);
        }

        [Fact]
        public void GeneratedDefaultsMustMatchMemberCount()
        {
            SourceGeneratorSupport.RegisterType(
                typeof(MismatchedDefaultsGeneratedModel),
                _ => new MismatchedDefaultsGeneratedModel(),
                () => [],
                [new GeneratedMember("value", typeof(string), null, false, false)]);

            var exception = Assert.Throws<DeserializationException>(() =>
                new TypeActivatorCreator()
                    .GetActivator(typeof(MismatchedDefaultsGeneratedModel)));

            Assert.Contains("match the registered member count", exception.Message);
        }

        [Fact]
        public void GeneratedDuplicateMapKeysAreRejectedOnFirstUse()
        {
            SourceGeneratorSupport.RegisterType(
                typeof(DuplicateMapKeyGeneratedModel),
                _ => new DuplicateMapKeyGeneratedModel(),
                () => [null, null],
                [
                    new GeneratedMember("value", typeof(string), null, false, false),
                    new GeneratedMember("value", typeof(string), null, false, false),
                ]);

            var exception = Assert.Throws<DeserializationException>(() =>
                new TypeActivatorCreator()
                    .GetActivator(typeof(DuplicateMapKeyGeneratedModel)));

            Assert.Contains(nameof(DuplicateMapKeyGeneratedModel), exception.Message);
            Assert.Contains("duplicate map key 'value'", exception.Message);
        }

        [Fact]
        public void DuplicateTypeRegistrationKeepsTheFirstRegistration()
        {
            SourceGeneratorSupport.RegisterType(
                typeof(DuplicateRegistrationGeneratedModel),
                _ => new DuplicateRegistrationGeneratedModel(),
                () => [null],
                [new GeneratedMember("first", typeof(string), null, false, false)]);
            SourceGeneratorSupport.RegisterType(
                typeof(DuplicateRegistrationGeneratedModel),
                _ => new DuplicateRegistrationGeneratedModel(),
                () => [null],
                [new GeneratedMember("second", typeof(string), null, false, false)]);

            var activator = new TypeActivatorCreator()
                .GetActivator(typeof(DuplicateRegistrationGeneratedModel));

            Assert.True(activator.DeserializationParameters.ContainsKey(
                new Key(Encoding.UTF8.GetBytes("first"))));
            Assert.False(activator.DeserializationParameters.ContainsKey(
                new Key(Encoding.UTF8.GetBytes("second"))));
        }

        [Fact]
        public void GeneratedDefaultFactoryExceptionsHaveDeserializationContext()
        {
            var inner = new InvalidOperationException("Default construction failed.");
            SourceGeneratorSupport.RegisterType(
                typeof(ThrowingDefaultsGeneratedModel),
                _ => new ThrowingDefaultsGeneratedModel(),
                () => throw inner,
                []);
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));

            var exception = Assert.Throws<DeserializationException>(() =>
                reader.Find<ThrowingDefaultsGeneratedModel>(IPAddress.Parse("1.1.1.1")));

            Assert.Contains(nameof(ThrowingDefaultsGeneratedModel), exception.Message);
            Assert.Same(inner, exception.InnerException);
        }

        [Fact]
        public void GeneratedActivatorMetadataIsReusedAcrossCreators()
        {
            var defaultsFactoryCalls = 0;
            SourceGeneratorSupport.RegisterType(
                typeof(CachedMetadataGeneratedModel),
                values => new CachedMetadataGeneratedModel(values[0]!),
                () =>
                {
                    defaultsFactoryCalls++;
                    return [new object()];
                },
                [new GeneratedMember("value", typeof(object), null, false, false)]);

            var first = new TypeActivatorCreator()
                .GetActivator(typeof(CachedMetadataGeneratedModel));
            var second = new TypeActivatorCreator()
                .GetActivator(typeof(CachedMetadataGeneratedModel));

            Assert.NotSame(first, second);
            Assert.Same(first.DeserializationParameters, second.DeserializationParameters);
            Assert.NotSame(first.DefaultParameters, second.DefaultParameters);
            Assert.NotSame(first.DefaultParameters[0], second.DefaultParameters[0]);
            Assert.Equal(2, defaultsFactoryCalls);
        }

        [Fact]
        public void GeneratedRuntimeMembersAreNotDatabaseParameters()
        {
            SourceGeneratorSupport.RegisterType(
                typeof(GeneratedInjectedCollectionModel),
                values => new GeneratedInjectedCollectionModel((string[])values[0]!),
                () => [null],
                [new GeneratedMember(
                    "locales", typeof(string[]), "locales", false, false)]);

            var activator = new TypeActivatorCreator()
                .GetActivator(typeof(GeneratedInjectedCollectionModel));

            Assert.Empty(activator.DeserializationParameters);
            Assert.Single(activator.InjectableParameters);
        }

        [Fact]
        public void RegisteredCollectionsTakePrecedenceOverReflection()
        {
            SourceGeneratorSupport.RegisterCollection(
                typeof(IReadOnlyList<long>),
                typeof(long),
                capacity => new List<long>(capacity),
                (collection, value) => ((ICollection<long>)collection).Add((long)value!));
            SourceGeneratorSupport.RegisterDictionary(
                typeof(GeneratedDictionary<string, object>),
                typeof(string),
                typeof(object),
                _ => new GeneratedDictionary<string, object>(),
                (dictionary, key, value) =>
                    ((IDictionary<string, object>)dictionary).Add((string)key!, value!));
            SourceGeneratorSupport.RegisterType(
                typeof(GeneratedCollectionModel),
                values => new GeneratedCollectionModel(
                    (IReadOnlyList<long>)values[0]!,
                    (GeneratedDictionary<string, object>)values[1]!),
                () => [null, null],
                [
                    new GeneratedMember(
                        "array", typeof(IReadOnlyList<long>), null, false, false),
                    new GeneratedMember(
                        "map",
                        typeof(GeneratedDictionary<string, object>),
                        null,
                        false,
                        false),
                ]);

            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));

            var model = reader.Find<GeneratedCollectionModel>(IPAddress.Parse("1.1.1.1"));

            Assert.NotNull(model);
            Assert.Equal([1L, 2L, 3L], model.Array);
            Assert.Single(model.Map);
            Assert.IsType<Dictionary<string, object>>(model.Map["mapX"]);
        }

        [Fact]
        public void RegisteredNonGenericDictionaryTakesPrecedenceOverModelActivation()
        {
            SourceGeneratorSupport.RegisterDictionary(
                typeof(GeneratedNonGenericDictionary),
                typeof(string),
                typeof(object),
                _ => new GeneratedNonGenericDictionary(),
                (dictionary, key, value) =>
                    ((IDictionary<string, object>)dictionary).Add((string)key!, value!));
            SourceGeneratorSupport.RegisterType(
                typeof(GeneratedNonGenericDictionary),
                _ => throw new InvalidOperationException("Model activator should not be used."),
                () => [],
                []);
            SourceGeneratorSupport.RegisterType(
                typeof(GeneratedNonGenericDictionaryModel),
                values => new GeneratedNonGenericDictionaryModel(
                    (GeneratedNonGenericDictionary)values[0]!),
                () => [null],
                [new GeneratedMember(
                    "map", typeof(GeneratedNonGenericDictionary), null, false, false)]);
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));

            var model = reader.Find<GeneratedNonGenericDictionaryModel>(
                IPAddress.Parse("1.1.1.1"));

            Assert.NotNull(model);
            Assert.Single(model.Map);
            Assert.IsType<Dictionary<string, object>>(model.Map["mapX"]);
        }

        private sealed class GeneratedAlwaysCreated
        {
            internal GeneratedAlwaysCreated(string injected)
            {
                Injected = injected;
            }

            internal string Injected { get; }
        }

        private sealed class GeneratedModel
        {
            internal GeneratedModel(
                string utf8String,
                string injected,
                Network? network,
                GeneratedAlwaysCreated alwaysCreated
                )
            {
                Utf8String = utf8String;
                Injected = injected;
                Network = network;
                AlwaysCreated = alwaysCreated;
            }

            internal GeneratedAlwaysCreated AlwaysCreated { get; }
            internal string Injected { get; }
            internal Network? Network { get; }
            internal string Utf8String { get; }
        }

        private sealed class InvalidGeneratedModel
        {
        }

        private sealed class CopiedMembersGeneratedModel
        {
        }

        private sealed class MismatchedDefaultsGeneratedModel
        {
        }

        private sealed class AlwaysCreateValueTypeGeneratedModel
        {
            internal AlwaysCreateValueTypeGeneratedModel(long value)
            {
                Value = value;
            }

            internal long Value { get; }
        }

        private sealed class DuplicateMapKeyGeneratedModel
        {
        }

        private sealed class DuplicateRegistrationGeneratedModel
        {
        }

        private sealed class ThrowingDefaultsGeneratedModel
        {
        }

        private sealed class CachedMetadataGeneratedModel
        {
            internal CachedMetadataGeneratedModel(object value)
            {
                Value = value;
            }

            internal object Value { get; }
        }

        private sealed class GeneratedCollectionModel
        {
            internal GeneratedCollectionModel(
                IReadOnlyList<long> array,
                GeneratedDictionary<string, object> map
                )
            {
                Array = array;
                Map = map;
            }

            internal IReadOnlyList<long> Array { get; }
            internal GeneratedDictionary<string, object> Map { get; }
        }

        private sealed class GeneratedInjectedCollectionModel
        {
            internal GeneratedInjectedCollectionModel(string[] locales)
            {
                Locales = locales;
            }

            internal string[] Locales { get; }
        }

        private sealed class GeneratedNonGenericDictionary : Dictionary<string, object>
        {
        }

        private sealed class GeneratedNonGenericDictionaryModel
        {
            internal GeneratedNonGenericDictionaryModel(GeneratedNonGenericDictionary map)
            {
                Map = map;
            }

            internal GeneratedNonGenericDictionary Map { get; }
        }

        private sealed class GeneratedDictionary<TKey, TValue> : Dictionary<TKey, TValue>
            where TKey : notnull
        {
        }
    }

    internal sealed class Utf8KeyAttribute : MapKeyAttribute
    {
        internal Utf8KeyAttribute(string name)
            : base("utf8_" + name, true)
        {
        }
    }

    internal sealed class DerivedMapKeyFallbackModel
    {
        [Constructor]
        internal DerivedMapKeyFallbackModel([Utf8Key("string")] string utf8String)
        {
            Utf8String = utf8String;
        }

        internal string Utf8String { get; }
    }

    internal sealed class DerivedMapKeyPropertyFallbackModel
    {
        [Utf8Key("missing")]
        internal DerivedMapKeyAlwaysCreated? AlwaysCreated { get; set; }

        [Utf8Key("string")]
        internal string? Utf8String { get; set; }
    }

    internal sealed class DerivedMapKeyAlwaysCreated
    {
        [MapKey("unused")]
        internal string? Value { get; set; }
    }
}
