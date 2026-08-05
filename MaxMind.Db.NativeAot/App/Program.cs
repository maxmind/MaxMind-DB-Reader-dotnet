using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using MaxMind.Db;
using MaxMind.Db.NativeAot.Models;
using MaxMind.Db.NetStandard.TestModels;

namespace MaxMind.Db.NativeAot.App
{
    internal static class Program
    {
        private static readonly IPAddress _decoderAddress = IPAddress.Parse("1.1.1.1");
        private static readonly IPAddress _cityAddress = IPAddress.Parse("81.2.69.160");

        private static void Main()
        {
            Check(!RuntimeFeature.IsDynamicCodeSupported, "The integration test must run as NativeAOT.");
            Check(
                typeof(CityResponse).Assembly != typeof(Reader).Assembly,
                "Models must be generated in a separate assembly.");

            foreach (var mode in new[] { FileAccessMode.MemoryMapped, FileAccessMode.Memory })
            {
                TestDecoderDatabase(mode);
                TestCityDatabase(mode);
                TestReflectionFallbackFailsWithGuidance(mode);
            }

            Console.WriteLine("NativeAOT integration tests passed.");
        }

        private static void TestDecoderDatabase(FileAccessMode mode)
        {
            var database = Path.Combine(AppContext.BaseDirectory, "MaxMind-DB-test-decoder.mmdb");
            using var reader = new Reader(database, mode);

            Check(reader.Metadata.DatabaseType == "MaxMind DB Decoder Test", "Unexpected metadata.");

            var dictionary = reader.Find<Dictionary<string, object>>(_decoderAddress) ??
                throw new InvalidOperationException("Dictionary lookup returned null.");
            Check(
                (string)dictionary["utf8_string"] == "unicode! ☯ - ♫",
                "Dictionary string did not decode.");

            var concurrentDictionary =
                reader.Find<ConcurrentDictionary<string, object>>(_decoderAddress) ??
                throw new InvalidOperationException(
                    "ConcurrentDictionary lookup returned null.");
            Check(
                (string)concurrentDictionary["utf8_string"] == "unicode! ☯ - ♫",
                "Source-generated top-level dictionary did not decode.");

            var constructorModel = reader.Find<DecoderConstructorModel>(_decoderAddress) ??
                throw new InvalidOperationException("Constructor model lookup returned null.");
            Check(
                constructorModel.Utf8String == "unicode! ☯ - ♫",
                "Constructor model string did not decode.");
            Check(
                SequenceEquals(constructorModel.Array, 1, 2, 3),
                "IReadOnlyList did not decode.");
            Check(
                NestedStringEquals(constructorModel.Map, "hello"),
                "IReadOnlyDictionary did not decode.");

            var netStandardModel = reader.Find<NetStandardModel>(_decoderAddress) ??
                throw new InvalidOperationException(".NET Standard model lookup returned null.");
            Check(
                netStandardModel.Utf8String == "unicode! ☯ - ♫",
                ".NET Standard model did not decode.");

            var propertyModel = reader.Find<DecoderPropertyModel>(_decoderAddress) ??
                throw new InvalidOperationException("Property model lookup returned null.");
            Check(SequenceEquals(propertyModel.Array, 1, 2, 3), "ICollection did not decode.");
            Check(
                NestedStringEquals(propertyModel.Map, "hello"),
                "Dictionary did not decode.");

            var concreteModel = reader.Find<DecoderConcreteCollectionModel>(_decoderAddress) ??
                throw new InvalidOperationException("Concrete collection model lookup returned null.");
            Check(SequenceEquals(concreteModel.Array, 1, 2, 3), "LinkedList did not decode.");
            Check(
                NestedStringEquals(concreteModel.Map, "hello"),
                "Concrete dictionary did not decode.");

            var count = 0;
            foreach (var _ in reader.FindAll<DecoderConstructorModel>())
            {
                count++;
            }
            Check(count == 26, $"FindAll returned {count} records instead of 26.");
        }

        private static void TestCityDatabase(FileAccessMode mode)
        {
            var database = Path.Combine(AppContext.BaseDirectory, "GeoIP2-City-Test.mmdb");
            var injectables = new InjectableValues();
            injectables.AddValue("locales", (IReadOnlyList<string>)["en"]);
            using var reader = new Reader(database, mode);

            var response = reader.Find<CityResponse>(_cityAddress, injectables) ??
                throw new InvalidOperationException("City lookup returned null.");
            Check(response.City.Name == "London", "City name did not decode.");
            Check(response.Subdivisions.Count == 1, "Subdivisions did not decode.");
            Check(
                response.Subdivisions[0].Name == "England",
                "Subdivision name did not decode.");
            Check(
                response.Traits.Network?.ToString() == "81.2.69.160/27",
                "Network did not decode.");
        }

        // ReflectionFallbackModel has no generated registration, so this is the only
        // coverage of what the reflection fallback does once trimmed and published as
        // NativeAOT; no analyzer reports that path. Full trimming removes the members
        // reflection needs, so the lookup fails before Expression.Compile is ever
        // reached. What matters is that the failure stays actionable.
        private static void TestReflectionFallbackFailsWithGuidance(FileAccessMode mode)
        {
            var database = Path.Combine(AppContext.BaseDirectory, "MaxMind-DB-test-decoder.mmdb");
            using var reader = new Reader(database, mode);

            try
            {
                reader.Find<ReflectionFallbackModel>(_decoderAddress);
            }
            catch (DeserializationException ex)
            {
                Check(
                    ex.Message.Contains(nameof(ReflectionFallbackModel)),
                    "The fallback failure must name the model.");
                Check(
                    ex.Message.Contains("rebuild the assembly that declares the model"),
                    "The fallback failure must point at the source generator.");
                return;
            }

            throw new InvalidOperationException(
                "Decoding a model without a generated registration must fail once trimmed.");
        }

        private static bool SequenceEquals(IEnumerable<long> values, params long[] expected)
        {
            var index = 0;
            foreach (var value in values)
            {
                if (index >= expected.Length || value != expected[index])
                {
                    return false;
                }
                index++;
            }
            return index == expected.Length;
        }

        private static bool NestedStringEquals(
            IReadOnlyDictionary<string, object> map,
            string expected
            )
            => map.TryGetValue("mapX", out var nested) &&
                nested is IReadOnlyDictionary<string, object> inner &&
                inner.TryGetValue("utf8_stringX", out var value) &&
                (value as string) == expected;

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
