#region

using MaxMind.Db.Test.Helper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    public class ReaderTest
    {
        private readonly string _testDataRoot =
            Path.Combine(TestUtils.TestDirectory, "TestData", "MaxMind-DB", "test-data");

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
        [InlineData(FileAccessMode.Memory)]
        public void Test(FileAccessMode mode)
        {
            foreach (var recordSize in new[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");
                    using var reader = new Reader(file, mode);
                    TestMetadata(reader, ipVersion);

                    if (ipVersion == 4)
                    {
                        TestIPV4(reader, file);
                    }
                    else
                    {
                        TestIPV6(reader, file);
                    }
                }
            }
        }

        [Fact]
        public async Task TestAsync()
        {
            foreach (var recordSize in new[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");
                    var reader = await Reader.CreateAsync(file);
                    using (reader)
                    {
                        TestMetadata(reader, ipVersion);

                        if (ipVersion == 4)
                        {
                            TestIPV4(reader, file);
                        }
                        else
                        {
                            TestIPV6(reader, file);
                        }
                    }
                }
            }
        }

        [Fact]
        public void TestStream()
        {
            foreach (var recordSize in new[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");
                    using var streamReader = File.OpenText(file);
                    using var reader = new Reader(streamReader.BaseStream);
                    TestMetadata(reader, ipVersion);

                    if (ipVersion == 4)
                    {
                        TestIPV4(reader, file);
                    }
                    else
                    {
                        TestIPV6(reader, file);
                    }
                }
            }
        }

        [Fact]
        public async Task TestStreamAsync()
        {
            foreach (var recordSize in new[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");
                    using var streamReader = File.OpenText(file);
                    using var reader = await Reader.CreateAsync(streamReader.BaseStream);
                    TestMetadata(reader, ipVersion);

                    if (ipVersion == 4)
                    {
                        TestIPV4(reader, file);
                    }
                    else
                    {
                        TestIPV6(reader, file);
                    }
                }
            }
        }

        [Fact]
        public void TestNonSeekableStream()
        {
            foreach (var recordSize in new[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");

                    using var stream = new NonSeekableStreamWrapper(File.OpenRead(file));
                    using var reader = new Reader(stream);
                    TestMetadata(reader, ipVersion);

                    if (ipVersion == 4)
                    {
                        TestIPV4(reader, file);
                    }
                    else
                    {
                        TestIPV6(reader, file);
                    }
                }
            }
        }

        [Fact]
        public async Task TestNonSeekableStreamAsync()
        {
            foreach (var recordSize in new[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");

                    using var stream = new NonSeekableStreamWrapper(File.OpenRead(file));
                    using var reader = await Reader.CreateAsync(stream);
                    TestMetadata(reader, ipVersion);

                    if (ipVersion == 4)
                    {
                        TestIPV4(reader, file);
                    }
                    else
                    {
                        TestIPV6(reader, file);
                    }
                }
            }
        }

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        [Fact]
        public void NullStreamThrowsArgumentNullException()
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            var ex = Assert.Throws<ArgumentNullException>(
                () => new Reader((Stream)null));
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            Assert.Contains("The database stream must not be null", ex.Message);
        }

        [Fact]
        public async Task NullStreamThrowsArgumentNullExceptionAsync()
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await Reader.CreateAsync((Stream)null));
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            Assert.Contains("The database stream must not be null", ex.Message);
        }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        [Fact]
        public void TestEmptyStream()
        {
            using var stream = new MemoryStream();
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => new Reader(stream));
            Assert.Contains("The database is empty.", ex.Message);
        }

        [Fact]
        public async Task TestEmptyStreamAsync()
        {
            using var stream = new MemoryStream();
            var ex = await Assert.ThrowsAsync<InvalidDatabaseException>(
                async () => await Reader.CreateAsync(stream));
            Assert.Contains("The database is empty.", ex.Message);
        }

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
#pragma warning disable CS0618 // Verify deprecated value still functions
        [InlineData(FileAccessMode.MemoryMappedGlobal)]
#pragma warning restore CS0618
        [InlineData(FileAccessMode.Memory)]
        public void TestEmptyFile(FileAccessMode mode)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var ex = Assert.Throws<InvalidDatabaseException>(
                    () => new Reader(tempFile, mode));
                Assert.Contains("The database is empty.", ex.Message);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void MetadataPointer()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-metadata-pointers.mmdb"));
            Assert.Equal("Lots of pointers in metadata", reader.Metadata.DatabaseType);
        }

        [Fact]
        public void NoIPV4SearchTree()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-no-ipv4-search-tree.mmdb"));
            Assert.Equal("::/64", reader.Find<string>(IPAddress.Parse("1.1.1.1")));
            Assert.Equal("::/64", reader.Find<string>(IPAddress.Parse("192.1.1.1")));
        }

        [Theory]
        [InlineData("1.1.1.1", "MaxMind-DB-test-ipv6-32.mmdb", 8, false)]
        [InlineData("::1:ffff:ffff", "MaxMind-DB-test-ipv6-24.mmdb", 128, true)]
        [InlineData("::2:0:1", "MaxMind-DB-test-ipv6-24.mmdb", 122, true)]
        [InlineData("1.1.1.1", "MaxMind-DB-test-ipv4-24.mmdb", 32, true)]
        [InlineData("1.1.1.3", "MaxMind-DB-test-ipv4-24.mmdb", 31, true)]
        [InlineData("1.1.1.3", "MaxMind-DB-test-decoder.mmdb", 24, true)]
        [InlineData("::ffff:1.1.1.128", "MaxMind-DB-test-decoder.mmdb", 120, true)]
        [InlineData("::1.1.1.128", "MaxMind-DB-test-decoder.mmdb", 120, true)]
        [InlineData("200.0.2.1", "MaxMind-DB-no-ipv4-search-tree.mmdb", 0, true)]
        [InlineData("::200.0.2.1", "MaxMind-DB-no-ipv4-search-tree.mmdb", 64, true)]
        [InlineData("0:0:0:0:ffff:ffff:ffff:ffff", "MaxMind-DB-no-ipv4-search-tree.mmdb", 64, true)]
        [InlineData("ef00::", "MaxMind-DB-no-ipv4-search-tree.mmdb", 1, false)]
        public void TestFindPrefixLength(string ipStr, string dbFile, int expectedPrefixLength, bool expectedOK)
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, dbFile));
            var ip = IPAddress.Parse(ipStr);
            var record = reader.Find<object>(ip, out var prefixLength);

            Assert.Equal(expectedPrefixLength, prefixLength);

            if (expectedOK)
            {
                Assert.NotNull(record);
            }
            else
            {
                Assert.Null(record);
            }
        }

        [Fact]
        public void TestDecodingToDictionary()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var record = reader.Find<Dictionary<string, object>>(IPAddress.Parse("::1.1.1.0"));
            TestDecodingTypes(record);
        }

        [Fact]
        public void TestDecodingToGenericIDictionary()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var record = reader.Find<IDictionary<string, object>>(IPAddress.Parse("::1.1.1.0"));
            TestDecodingTypes(record);
        }

        [Fact]
        public void TestDecodingToConcurrentDictionary()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var record = reader.Find<ConcurrentDictionary<string, object>>(IPAddress.Parse("::1.1.1.0"));
            TestDecodingTypes(record);
        }

        private static void TestNode<T>(
            Reader reader,
            Reader.ReaderIteratorNode<T> node,
            InjectableValues? injectables = null
            ) where T : class
        {
            var lengthBits = node.Start.GetAddressBytes().Length * 8;
            Assert.True(lengthBits >= node.PrefixLength);

            // ensure a lookup back into the db produces correct results
            var find = reader.Find<T>(node.Start, injectables);
            Assert.NotNull(find);
            var find2 = reader.Find<T>(node.Start, injectables);
            Assert.NotNull(find2);
            Assert.Equivalent(find, find2);
            Assert.Equivalent(node.Data, find);
        }

        [Fact]
        public void TestEnumerateCountryDatabase()
        {
            var count = 0;
            using (var reader = new Reader(Path.Combine(_testDataRoot, "GeoIP2-Country-Test.mmdb")))
            {
                foreach (var node in reader.FindAll<Dictionary<string, object>>())
                {
                    TestNode(reader, node);
                    count++;
                }
            }

            Assert.True(count >= 397);
        }

        [Fact]
        public void TestEnumerateDecoderDatabase()
        {
            var count = 0;
            var injectables = new InjectableValues();
            injectables.AddValue("injectable", "injectable_value");
            injectables.AddValue("injected", "injected_value");
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb")))
            {
                foreach (var node in reader.FindAll<NoNetworkTypeHolder>(injectables))
                {
                    TestNode(reader, node, injectables);
                    count++;
                }
            }
            Assert.Equal(26, count);
        }

        private static void TestDecodingTypes(IDictionary<string, object>? record)
        {
            if (record == null)
            {
                throw new Xunit.Sdk.XunitException("unexpected null record value");
            }
            Assert.True((bool)record["boolean"]);

            Assert.Equal([0, 0, 0, 42], (byte[])record["bytes"]);

            Assert.Equal("unicode! ☯ - ♫", record["utf8_string"]);

            var array = (List<object>)record["array"];
            Assert.Equal(3, array.Count);
            Assert.Equal(1L, array[0]);
            Assert.Equal(2L, array[1]);
            Assert.Equal(3L, array[2]);

            var map = (Dictionary<string, object>)record["map"];
            Assert.Single(map);

            var mapX = (Dictionary<string, object>)map["mapX"];
            Assert.Equal(2, mapX.Count);
            Assert.Equal("hello", mapX["utf8_stringX"]);

            var arrayX = (List<object>)mapX["arrayX"];
            Assert.Equal(3, arrayX.Count);
            Assert.Equal(7L, arrayX[0]);
            Assert.Equal(8L, arrayX[1]);
            Assert.Equal(9L, arrayX[2]);

            Assert.Equal(42.123456, (double)record["double"], 9);
            Assert.Equal(1.1F, (float)record["float"], 5);
            Assert.Equal(-268435456, record["int32"]);
            Assert.Equal(100, record["uint16"]);
            Assert.Equal(268435456L, record["uint32"]);
            Assert.Equal(1152921504606846976UL, record["uint64"]);
            Assert.Equal(
                BigInteger.Parse("1329227995784915872903807060280344576"),
                record["uint128"]);
        }

        [Fact]
        public void TestDecodingTypesToObject()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            var record = reader.Find<TypeHolder>(IPAddress.Parse("1.1.1.1"), injectables);
            if (record == null)
            {
                throw new Xunit.Sdk.XunitException("unexpected null record value");
            }
            Assert.True(record.Boolean);
            Assert.Equal([0, 0, 0, 42], record.Bytes);
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);

            Assert.Equal(new List<long> { 1, 2, 3 }, record.Array);

            var mapX = record.Map.MapX;
            Assert.Equal("hello", mapX.Utf8StringX);
            Assert.Equal(new List<long> { 7, 8, 9 }, mapX.ArrayX);
            Assert.Equal("1.1.1.0/24", mapX.Network.ToString());

            Assert.Equal(42.123456, record.Double, 9);
            Assert.Equal(1.1F, record.Float, 5);
            Assert.Equal(-268435456, record.Int32);
            Assert.Equal(100, record.Uint16);
            Assert.Equal(268435456, record.Uint32);
            Assert.Equal(1152921504606846976UL, record.Uint64);
            Assert.Equal(BigInteger.Parse("1329227995784915872903807060280344576"), record.Uint128);

            Assert.Equal("injected string", record.Nonexistant.Injected);
            Assert.Equal("1.1.1.0/24", record.Nonexistant.Network.ToString());
            Assert.Equal("1.1.1.0/24", record.Nonexistant.Network2.ToString());

            Assert.Equal("injected string", record.Nonexistant.InnerNonexistant.Injected);
            Assert.Equal("1.1.1.0/24", record.Nonexistant.InnerNonexistant.Network.ToString());
        }

        [Fact]
        public void TestDecodingTypesToPropertyBasedObject()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var injectables = new InjectableValues();
            injectables.AddValue("injected", "injected string");
            var record = reader.Find<PropTypeHolder>(IPAddress.Parse("1.1.1.1"), injectables);
            if (record == null)
            {
                throw new Xunit.Sdk.XunitException("unexpected null record value");
            }
            Assert.True(record.Boolean);
            Assert.Equal([0, 0, 0, 42], record.Bytes);
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);

            Assert.Equal(new List<long> { 1, 2, 3 }, record.Array);

            var mapX = record.Map.MapX;
            Assert.Equal("hello", mapX.Utf8StringX);
            Assert.Equal(new List<long> { 7, 8, 9 }, mapX.ArrayX);
            Assert.Equal("1.1.1.0/24", mapX.Network?.ToString());

            Assert.Equal(42.123456, record.Double, 9);
            Assert.Equal(1.1F, record.Float, 5);
            Assert.Equal(-268435456, record.Int32);
            Assert.Equal(100, record.Uint16);
            Assert.Equal(268435456, record.Uint32);
            Assert.Equal(1152921504606846976UL, record.Uint64);
            Assert.Equal(BigInteger.Parse("1329227995784915872903807060280344576"), record.Uint128);

            Assert.Equal("injected string", record.Nonexistant.Injected);
            Assert.Equal("1.1.1.0/24", record.Nonexistant.Network?.ToString());
            Assert.Equal("1.1.1.0/24", record.Nonexistant.Network2?.ToString());

            Assert.Equal("injected string", record.Nonexistant.InnerNonexistant.Injected);
            Assert.Equal("1.1.1.0/24", record.Nonexistant.InnerNonexistant.Network?.ToString());

            Assert.Equal("should stay default", record.UnannotatedDefault);
        }

        [Fact]
        public void TestDecodingTypesWithDeprecatedParameterAttribute()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var record = reader.Find<DeprecatedParameterTypeHolder>(IPAddress.Parse("1.1.1.1"));
            if (record == null)
            {
                throw new Xunit.Sdk.XunitException("unexpected null record value");
            }
            Assert.Equal("unicode! ☯ - ♫", record.Utf8String);
            Assert.Equal(42.123456, record.Double, 9);
        }

        [Fact]
        public void TestNoConstructorNoParameterlessCtorThrows()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var ex = Assert.Throws<DeserializationException>(
                () => reader.Find<NoCtorNoAttributeType>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("no parameterless constructor found", ex.Message);
            Assert.Contains("rebuild the assembly that declares the model", ex.Message);
        }

        [Fact]
        public void TestReadOnlyPropertyThrows()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var ex = Assert.Throws<DeserializationException>(
                () => reader.Find<ReadOnlyPropertyType>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("must have a setter or init accessor", ex.Message);
        }

        [Fact]
        public void TestNoAnnotatedPropertiesThrows()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var ex = Assert.Throws<DeserializationException>(
                () => reader.Find<NoAnnotatedPropertiesType>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("No properties found", ex.Message);
            Assert.Contains("rebuild the assembly that declares the model", ex.Message);
        }

        [Fact]
        public void TestEnumerateDecoderDatabasePropertyBased()
        {
            var count = 0;
            var injectables = new InjectableValues();
            injectables.AddValue("injectable", "injectable_value");
            injectables.AddValue("injected", "injected_value");
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb")))
            {
                foreach (var node in reader.FindAll<PropNoNetworkTypeHolder>(injectables))
                {
                    TestNode(reader, node, injectables);
                    count++;
                }
            }
            Assert.Equal(26, count);
        }

        [Fact]
        public void TestZeros()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb"));
            var record = reader.Find<Dictionary<string, object>>(IPAddress.Parse("::"));
            if (record == null)
            {
                throw new Xunit.Sdk.XunitException("unexpected null record value");
            }
            Assert.False((bool)record["boolean"]);

            Assert.Empty((byte[])record["bytes"]);

            Assert.Empty(record["utf8_string"] as string ?? "null");

            Assert.IsType<List<object>>(record["array"]);
            Assert.Empty((List<object>)record["array"]);

            Assert.IsType<Dictionary<string, object>>(record["map"]);
            Assert.Empty((Dictionary<string, object>)record["map"]);

            Assert.Equal(0, (double)record["double"], 9);
            Assert.Equal(0, (float)record["float"], 5);
            Assert.Equal(0, record["int32"]);
            Assert.Equal(0, record["uint16"]);
            Assert.Equal(0L, record["uint32"]);
            Assert.Equal(0UL, record["uint64"]);
            Assert.Equal(new BigInteger(0), record["uint128"]);
        }

        [Fact]
        public void TestBrokenDatabase()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "GeoIP2-City-Test-Broken-Double-Format.mmdb"));
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("2001:220::")));
            Assert.Contains("contains bad data", ex.Message);
        }

        [Fact]
        public void TestBrokenSearchTreePointer()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-broken-pointers-24.mmdb"));
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.32")));
            Assert.Contains("search tree is corrupt", ex.Message);
        }

        [Fact]
        public void TestBrokenDataPointer()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-broken-pointers-24.mmdb"));
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.16")));
            Assert.Contains("data section contains bad data", ex.Message);
        }

        private static void TestIPV6(Reader reader, string file)
        {
            TestAddresses(reader,
                file,
                ["::1:ffff:ffff", "::2:0:0", "::2:0:40", "::2:0:50", "::2:0:58"],
                new Dictionary<string, string>
                {
                    {"::2:0:1", "::2:0:0"},
                    {"::2:0:33", "::2:0:0"},
                    {"::2:0:39", "::2:0:0"},
                    {"::2:0:41", "::2:0:40"},
                    {"::2:0:49", "::2:0:40"},
                    {"::2:0:52", "::2:0:50"},
                    {"::2:0:57", "::2:0:50"},
                    {"::2:0:59", "::2:0:58"}
                },
                ["1.1.1.33", "255.254.253.123", "89fa::"],
                new Dictionary<string, int>
                {
                    {"::2:0:1", 122}
                });
        }

        private static void TestIPV4(Reader reader, string file)
        {
            TestAddresses(reader,
                file,
                Enumerable.Range(0, 5).Select(i => "1.1.1." + (int)Math.Pow(2, i)),
                new Dictionary<string, string>
                {
                    {"1.1.1.3", "1.1.1.2"},
                    {"1.1.1.5", "1.1.1.4"},
                    {"1.1.1.7", "1.1.1.4"},
                    {"1.1.1.9", "1.1.1.8"},
                    {"1.1.1.15", "1.1.1.8"},
                    {"1.1.1.17", "1.1.1.16"},
                    {"1.1.1.31", "1.1.1.16"}
                },
                ["1.1.1.33", "255.254.253.123"],
                new Dictionary<string, int>
                {
                    {"1.1.1.3", 31},
                    {"4.0.0.1", 6}
                });
        }

        private static void TestAddresses(Reader reader, string file, IEnumerable<string> singleAddresses,
            Dictionary<string, string> pairs, IEnumerable<string> nullAddresses, Dictionary<string, int> prefixes)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.

            foreach (var address in singleAddresses)
            {
                Assert.Equal(
                    new string([.. address]),
                    reader.Find<Dictionary<string, object>>(IPAddress.Parse(address))["ip"]);
            }

            foreach (var address in pairs.Keys)
            {
                Assert.Equal(
                    pairs[address],
                    reader.Find<Dictionary<string, object>>(IPAddress.Parse(address))["ip"]);
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            foreach (var address in nullAddresses)
            {
                Assert.Null(
                    reader.Find<object>(IPAddress.Parse(address)));
            }

            foreach (var address in prefixes.Keys)
            {
                reader.Find<Dictionary<string, object>>(IPAddress.Parse(address), out var routingPrefix);
                Assert.Equal(
                    prefixes[address],
                    routingPrefix);
            }

            foreach (var node in reader.FindAll<Dictionary<string, object>>())
            {
                TestNode(reader, node);
            }
        }

        // Repeated pointers to one large scalar must exhaust the payload
        // budget even when the value count is within its limit.
        [Theory]
        [InlineData("MaxMind-DB-test-payload-amplification-dos.mmdb", "maximum payload size")]
        [InlineData("MaxMind-DB-test-payload-amplification-dos-string.mmdb", "maximum payload size")]
        // The value count is exactly at its limit. Each target is 65,535
        // bytes, so the 33rd occurrence crosses the 2 MiB payload limit.
        [InlineData("MaxMind-DB-test-payload-amplification-dos-worst-case.mmdb", "maximum payload size")]
        public void TestPayloadAmplificationIsRejected(string fixture, string expected)
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, fixture));
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains(expected, ex.Message);
        }

        [Fact]
        public void TestPayloadAtLimitDecodes()
        {
            // Exactly 2 MiB of bytes payload must decode.
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder-payload-limit.mmdb"));
            var result = reader.Find<object>(IPAddress.Parse("1.1.1.1"));
            var list = Assert.IsType<List<object>>(result);
            Assert.Equal(33, list.Count);
        }

        [Fact]
        public void TestPayloadOverLimitIsRejected()
        {
            // One byte over 2 MiB must be rejected.
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder-payload-limit-over.mmdb"));
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public void TestMetadataPayloadLimitIsRejectedOnOpen()
        {
            // The metadata languages array alone exceeds the payload limit.
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => new Reader(
                    Path.Combine(_testDataRoot, "MaxMind-DB-test-metadata-payload-limit.mmdb")));
            Assert.Contains("maximum payload size", ex.Message);
        }

        // The shared array targets must exhaust the value budget through
        // the real tree walk and pointer base.
        [Theory]
        [InlineData("MaxMind-DB-test-pointer-decoder-dos.mmdb", "1.1.1.1")]
        [InlineData("MaxMind-DB-test-pointer-decoder-dos-ipv6.mmdb", "::1")]
        public void TestPointerFanOutFixtureIsRejected(string fixture, string address)
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, fixture));
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse(address)));
            Assert.Contains("maximum number of values", ex.Message);
        }

        [Fact]
        public void TestValueCountAtLimitDecodes()
        {
            // Exactly 65,536 decoded values, the boundary the limit allows.
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder-value-limit.mmdb"));
            for (var i = 0; i < 3; i++)
            {
                var values = Assert.IsType<List<object>>(reader.Find<object>(IPAddress.Parse("1.1.1.1")));
                Assert.Equal(65_535, values.Count);
            }
        }

        [Fact]
        public void TestValueCountOverLimitIsRejected()
        {
            // One value past 65,536. Catches an off-by-one in the comparison.
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder-value-limit-over.mmdb"));
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("maximum number of values", ex.Message);
        }

        [Fact]
        public void TestPointerHeavyValueCountDecodes()
        {
            // 65,535 values reached through a depth-15 pointer fan-out, one
            // under the limit. A reader that over-counts a followed pointer
            // would reject this conformant database.
            using var reader = new Reader(
                Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder-value-limit-pointer-heavy.mmdb"));
            Assert.NotNull(reader.Find<object>(IPAddress.Parse("1.1.1.1")));
        }

        // Exercise the memory-loading path with the same hostile fixture.
        [Fact]
        public void TestPayloadLimitAppliesToMemoryMode()
        {
            var path = Path.Combine(_testDataRoot, "MaxMind-DB-test-payload-amplification-dos.mmdb");
            using var reader = new Reader(path, FileAccessMode.Memory);
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public void TestPayloadLimitAppliesToStreamConstruction()
        {
            var path = Path.Combine(_testDataRoot, "MaxMind-DB-test-payload-amplification-dos.mmdb");
            using var stream = File.OpenRead(path);
            using var reader = new Reader(stream);
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public async Task TestPayloadLimitAppliesToAsyncConstruction()
        {
            var path = Path.Combine(_testDataRoot, "MaxMind-DB-test-payload-amplification-dos.mmdb");
            using var reader = await Reader.CreateAsync(path);
            var ex = Assert.Throws<InvalidDatabaseException>(
                () => reader.Find<object>(IPAddress.Parse("1.1.1.1")));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public void TestPayloadLimitAppliesToFindAll()
        {
            // Enumeration must reject the hostile record when it is decoded.
            var path = Path.Combine(_testDataRoot, "MaxMind-DB-test-payload-amplification-dos.mmdb");
            using var reader = new Reader(path);
            var ex = Assert.Throws<InvalidDatabaseException>(
                () =>
                {
                    foreach (var node in reader.FindAll<object>())
                    {
                        _ = node;
                    }
                });
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
        [InlineData(FileAccessMode.Memory)]
        public void TestTruncatedRecordThrowsDatabaseException(FileAccessMode mode)
        {
            // The string declares 4,096 bytes, extending past the metadata and
            // the end of this small database. Its header is inside the file.
            var bytes = DatabaseWithRootRecords([0x5E, 0x0E, 0xE3], [0xA0]);
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, bytes);
                using var reader = new Reader(path, mode);
                var error = Assert.Throws<InvalidDatabaseException>(() => reader.Find<object>(IPAddress.Parse("1.1.1.1")));
                Assert.Contains("beyond the end", error.Message);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
        [InlineData(FileAccessMode.Memory)]
        public void TestReaderRecoversAfterModelLimitFailure(FileAccessMode mode)
        {
            // Populate one constructor argument before the next field exceeds
            // the value limit. The successful record omits that first field.
            byte[] hostile = [0xE2, 0x44, (byte)'n', (byte)'a', (byte)'m', (byte)'e',
                0x43, (byte)'b', (byte)'a', (byte)'d',
                0x46, (byte)'v', (byte)'a', (byte)'l', (byte)'u', (byte)'e', (byte)'s',
                0x1E, 0x04, 0xFE, 0xE2];
            byte[] valid = [0xE1, 0x46, (byte)'v', (byte)'a', (byte)'l', (byte)'u', (byte)'e', (byte)'s',
                0x01, 0x04, 0x01, 0x07]; // one-element array containing true
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, DatabaseWithRootRecords(hostile, valid));
                using var reader = new Reader(path, mode);
                for (var i = 0; i < 3; i++)
                {
                    var error = Assert.Throws<InvalidDatabaseException>(() => reader.Find<RecoveryRecord>(IPAddress.Parse("1.1.1.1")));
                    Assert.Contains("maximum number of values", error.Message);
                    var record = reader.Find<RecoveryRecord>(IPAddress.Parse("129.1.1.1"));
                    Assert.NotNull(record);
                    Assert.Null(record.Name);
                    Assert.NotNull(record.Values);
                    Assert.True(Assert.IsType<bool>(Assert.Single(record.Values)));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private sealed class RecoveryRecord
        {
            [Constructor]
            public RecoveryRecord(string? name = null, List<object>? values = null)
            {
                Name = name;
                Values = values;
            }

            public string? Name { get; }
            public List<object>? Values { get; }
        }

        private byte[] DatabaseWithRootRecords(byte[] left, byte[] right)
        {
            var path = Path.Combine(_testDataRoot, "MaxMind-DB-test-ipv4-24.mmdb");
            using var source = new Reader(path);
            var original = File.ReadAllBytes(path);
            byte[] marker = [0xAB, 0xCD, 0xEF, (byte)'M', (byte)'a', (byte)'x', (byte)'M', (byte)'i', (byte)'n', (byte)'d', (byte)'.', (byte)'c', (byte)'o', (byte)'m'];
            var metadataOffset = Enumerable.Range(0, original.Length - marker.Length + 1)
                .Last(i => original.Skip(i).Take(marker.Length).SequenceEqual(marker));
            var bytes = new List<byte>(original.Take(metadataOffset));
            var leftPointer = bytes.Count - source.Metadata.SearchTreeSize + source.Metadata.NodeCount;
            bytes.AddRange(left);
            var rightPointer = bytes.Count - source.Metadata.SearchTreeSize + source.Metadata.NodeCount;
            bytes.AddRange(right);
            bytes.AddRange(original.Skip(metadataOffset));
            // Root children select the two records by the address's first bit.
            for (var i = 0; i < 3; i++)
            {
                bytes[i] = (byte)(leftPointer >> (16 - 8 * i));
                bytes[i + 3] = (byte)(rightPointer >> (16 - 8 * i));
            }
            return [.. bytes];
        }

        private static void TestMetadata(Reader reader, int ipVersion)
        {
            var metadata = reader.Metadata;

            Assert.Equal(2, metadata.BinaryFormatMajorVersion);
            Assert.Equal(0, metadata.BinaryFormatMinorVersion);
            Assert.Equal(ipVersion, metadata.IPVersion);
            Assert.Equal("Test", metadata.DatabaseType);
            Assert.Contains("en", metadata.Languages);
            Assert.Contains("zh", metadata.Languages);
            Assert.Equal("Test Database", metadata.Description["en"]);
            Assert.Equal("Test Database Chinese", metadata.Description["zh"]);
            Assert.DoesNotContain("gibberish", metadata.Description.Keys);
        }
    }
}
