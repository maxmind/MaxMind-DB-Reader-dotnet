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

        [Fact]
        public void Test()
        {
            foreach (var recordSize in new long[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");
                    var reader = new Reader(file);
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
        public async Task TestAsync()
        {
            foreach (var recordSize in new long[] { 24, 28, 32 })
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
            foreach (var recordSize in new long[] { 24, 28, 32 })
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
            foreach (var recordSize in new long[] { 24, 28, 32 })
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
            foreach (var recordSize in new long[] { 24, 28, 32 })
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
            foreach (var recordSize in new long[] { 24, 28, 32 })
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
            Assert.Contains("zero bytes left in the stream", ex.Message);
        }

        [Fact]
        public async Task TestEmptyStreamAsync()
        {
            using var stream = new MemoryStream();
            var ex = await Assert.ThrowsAsync<InvalidDatabaseException>(
                async () => await Reader.CreateAsync(stream));
            Assert.Contains("zero bytes left in the stream", ex.Message);
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
            Assert.Equal("::0/64", reader.Find<string>(IPAddress.Parse("1.1.1.1")));
            Assert.Equal("::0/64", reader.Find<string>(IPAddress.Parse("192.1.1.1")));
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

            Assert.Equal(new byte[] { 0, 0, 0, 42 }, (byte[])record["bytes"]);

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
            Assert.Equal(new byte[] { 0, 0, 0, 42 }, record.Bytes);
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
