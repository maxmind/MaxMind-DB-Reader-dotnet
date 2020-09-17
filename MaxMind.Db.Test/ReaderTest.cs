#region

using FluentAssertions;
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
                    var reader = await Reader.CreateAsync(file).ConfigureAwait(false);
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
                    using var reader = await Reader.CreateAsync(streamReader.BaseStream).ConfigureAwait(false);
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
                    using var reader = await Reader.CreateAsync(stream).ConfigureAwait(false);
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

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        [Fact]
        public void NullStreamThrowsArgumentNullException()
        {
            ((Action)(() => new Reader((Stream)null)))
                .Should().Throw<ArgumentNullException>()
                .WithMessage("The database stream must not be null.*");
        }

        [Fact]
        public void NullStreamThrowsArgumentNullExceptionAsync()
        {
            ((Func<Task>)(async () => { await Reader.CreateAsync((Stream)null).ConfigureAwait(false); }))
                .Should().Throw<ArgumentNullException>()
                .WithMessage("The database stream must not be null.*");
        }
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

        [Fact]
        public void TestEmptyStream()
        {
            using var stream = new MemoryStream();
            ((Action)(() => new Reader(stream)))
                .Should().Throw<InvalidDatabaseException>()
                .WithMessage("*zero bytes left in the stream*");
        }

        [Fact]
        public void TestEmptyStreamAsync()
        {
            using var stream = new MemoryStream();
            ((Func<Task>)(async () => { await Reader.CreateAsync(stream).ConfigureAwait(false); }))
                .Should().Throw<InvalidDatabaseException>()
                .WithMessage("*zero bytes left in the stream*");
        }

        [Fact]
        public void NoIPV4SearchTree()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-no-ipv4-search-tree.mmdb"));
            reader.Find<string>(IPAddress.Parse("1.1.1.1")).Should().Be("::0/64");
            reader.Find<string>(IPAddress.Parse("192.1.1.1")).Should().Be("::0/64");
        }

        [Fact]
        public void TestFindPrefixLength()
        {
            var tests = new[]
            {
                new
                {
                    ip = "1.1.1.1",
                    dbFile = "MaxMind-DB-test-ipv6-32.mmdb",
                    expectedPrefixLength = 8,
                    expectedOK = false,
                },
                new {
                    ip = "::1:ffff:ffff",
                    dbFile = "MaxMind-DB-test-ipv6-24.mmdb",
                    expectedPrefixLength = 128,
                    expectedOK = true,
                },
                new {
                    ip = "::2:0:1",
                    dbFile = "MaxMind-DB-test-ipv6-24.mmdb",
                    expectedPrefixLength = 122,
                    expectedOK = true,
                },
                new {
                    ip = "1.1.1.1",
                    dbFile = "MaxMind-DB-test-ipv4-24.mmdb",
                    expectedPrefixLength = 32,
                    expectedOK = true,
                },
                new {
                    ip = "1.1.1.3",
                    dbFile = "MaxMind-DB-test-ipv4-24.mmdb",
                    expectedPrefixLength = 31,
                    expectedOK = true,
                },
                new {
                    ip = "1.1.1.3",
                    dbFile = "MaxMind-DB-test-decoder.mmdb",
                    expectedPrefixLength = 24,
                    expectedOK = true,
                },
                new {
                    ip = "::ffff:1.1.1.128",
                    dbFile = "MaxMind-DB-test-decoder.mmdb",
                    expectedPrefixLength = 120,
                    expectedOK = true,
                },
                new {
                    ip = "::1.1.1.128",
                    dbFile = "MaxMind-DB-test-decoder.mmdb",
                    expectedPrefixLength = 120,
                    expectedOK = true,
                },
                new {
                    ip = "200.0.2.1",
                    dbFile = "MaxMind-DB-no-ipv4-search-tree.mmdb",
                    expectedPrefixLength = 0,
                    expectedOK = true,
                },
                new {
                    ip = "::200.0.2.1",
                    dbFile = "MaxMind-DB-no-ipv4-search-tree.mmdb",
                    expectedPrefixLength = 64,
                    expectedOK = true,
                },
                new {
                    ip = "0:0:0:0:ffff:ffff:ffff:ffff",
                    dbFile = "MaxMind-DB-no-ipv4-search-tree.mmdb",
                    expectedPrefixLength = 64,
                    expectedOK = true,
                },
                new {
                    ip = "ef00::",
                    dbFile = "MaxMind-DB-no-ipv4-search-tree.mmdb",
                    expectedPrefixLength = 1,
                    expectedOK = false,
                }
            };

            foreach (var test in tests)
            {
                using var reader = new Reader(Path.Combine(_testDataRoot, test.dbFile));
                var ip = IPAddress.Parse(test.ip);
                var record = reader.Find<object>(ip, out var prefixLength);

                prefixLength.Should().Be(test.expectedPrefixLength,
                    $"{test.expectedPrefixLength} is the prefix length for {ip} in {test.dbFile}");

                if (test.expectedOK)
                {
                    record.Should().NotBeNull($"there is a record for {ip} in {test.dbFile}");
                }
                else
                {
                    record.Should().BeNull($"there is no record for {ip} in {test.dbFile}");
                }
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

        private void TestNode<T>(Reader reader, Reader.ReaderIteratorNode<T> node, InjectableValues? injectables = null) where T : class
        {
            var lengthBits = node.Start.GetAddressBytes().Length * 8;
            lengthBits.Should().BeGreaterOrEqualTo(node.PrefixLength);

            // ensure a lookup back into the db produces correct results
            var find = reader.Find<T>(node.Start, injectables);
            find.Should().NotBeNull();
            var find2 = reader.Find<T>(node.Start, injectables);
            find2.Should().NotBeNull();
            find.Should().BeEquivalentTo(find2);
            find.Should().BeEquivalentTo(node.Data);
        }

        [Fact]
        public void TestEnumerateCountryDatabase()
        {
            var count = 0;
            using (var reader = new Reader(Path.Combine(_testDataRoot, "GeoIP2-Country-Test.mmdb")))
                foreach (var node in reader.FindAll<Dictionary<string, object>>())
                {
                    TestNode(reader, node);
                    count++;
                }

            count.Should().Be(297);
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
            count.Should().Be(26);
        }

        private void TestDecodingTypes(IDictionary<string, object>? record)
        {
            if (record == null)
            {
                throw new Xunit.Sdk.XunitException("unexpected null record value");
            }
            ((bool)record["boolean"]).Should().BeTrue();

            ((byte[])record["bytes"]).Should().Equal(0, 0, 0, 42);

            record["utf8_string"].Should().Be("unicode! ☯ - ♫");

            var array = (List<object>)record["array"];
            array.Should().HaveCount(3);
            array[0].Should().BeEquivalentTo(1);
            array[1].Should().BeEquivalentTo(2);
            array[2].Should().BeEquivalentTo(3);

            var map = (Dictionary<string, object>)record["map"];
            map.Should().HaveCount(1);

            var mapX = (Dictionary<string, object>)map["mapX"];
            mapX.Should().HaveCount(2);
            mapX["utf8_stringX"].Should().Be("hello");

            var arrayX = (List<object>)mapX["arrayX"];
            arrayX.Should().HaveCount(3);
            arrayX[0].Should().BeEquivalentTo(7);
            arrayX[1].Should().BeEquivalentTo(8);
            arrayX[2].Should().BeEquivalentTo(9);

            ((double)record["double"]).Should().BeApproximately(42.123456, 0.000000001);
            ((float)record["float"]).Should().BeApproximately(1.1F, 0.000001F);
            record["int32"].Should().BeEquivalentTo(-268435456);
            record["uint16"].Should().BeEquivalentTo(100);
            record["uint32"].Should().BeEquivalentTo(268435456);
            record["uint64"].Should().BeEquivalentTo(1152921504606846976);
            record["uint128"].Should().Be(
                BigInteger.Parse("1329227995784915872903807060280344576"));
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
            record.Boolean.Should().BeTrue();
            record.Bytes.Should().Equal(0, 0, 0, 42);
            record.Utf8String.Should().Be("unicode! ☯ - ♫");

            record.Array.Should().Equal(new List<long> { 1, 2, 3 });

            var mapX = record.Map.MapX;
            mapX.Utf8StringX.Should().Be("hello");
            mapX.ArrayX.Should().Equal(new List<long> { 7, 8, 9 });
            mapX.Network.ToString().Should().Be("1.1.1.0/24");

            record.Double.Should().BeApproximately(42.123456, 0.000000001);
            record.Float.Should().BeApproximately(1.1F, 0.000001F);
            record.Int32.Should().Be(-268435456);
            record.Uint16.Should().Be(100);
            record.Uint32.Should().Be(268435456);
            record.Uint64.Should().Be(1152921504606846976);
            record.Uint128.Should().Be(BigInteger.Parse("1329227995784915872903807060280344576"));

            record.Nonexistant.Injected.Should().Be("injected string");
            record.Nonexistant.Network.ToString().Should().Be("1.1.1.0/24");
            record.Nonexistant.Network2.ToString().Should().Be("1.1.1.0/24");

            record.Nonexistant.InnerNonexistant.Injected.Should().Be("injected string");
            record.Nonexistant.InnerNonexistant.Network.ToString().Should().Be("1.1.1.0/24");
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
                ((bool)record["boolean"]).Should().BeFalse();

            ((byte[])record["bytes"]).Should().BeEmpty();

            record["utf8_string"].ToString().Should().BeEmpty();

            record["array"].Should().BeOfType<List<object>>();
            ((List<object>)record["array"]).Should().BeEmpty();

            record["map"].Should().BeOfType<Dictionary<string, object>>();
            ((Dictionary<string, object>)record["map"]).Should().BeEmpty();

            ((double)record["double"]).Should().BeApproximately(0, 0.000000001);
            ((float)record["float"]).Should().BeApproximately(0, 0.000001F);
            record["int32"].Should().Be(0);
            record["uint16"].Should().Be(0);
            record["uint32"].Should().BeEquivalentTo(0);
            record["uint64"].Should().BeEquivalentTo(0);
            record["uint128"].Should().Be(new BigInteger(0));
        }

        [Fact]
        public void TestBrokenDatabase()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "GeoIP2-City-Test-Broken-Double-Format.mmdb"));
            ((Action)(() => reader.Find<object>(IPAddress.Parse("2001:220::"))))
                .Should().Throw<InvalidDatabaseException>()
                .WithMessage("*contains bad data*");
        }

        [Fact]
        public void TestBrokenSearchTreePointer()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-broken-pointers-24.mmdb"));
            ((Action)(() => reader.Find<object>(IPAddress.Parse("1.1.1.32"))))
                .Should().Throw<InvalidDatabaseException>()
                .WithMessage("*search tree is corrupt*");
        }

        [Fact]
        public void TestBrokenDataPointer()
        {
            using var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-broken-pointers-24.mmdb"));
            ((Action)(() => reader.Find<object>(IPAddress.Parse("1.1.1.16"))))
                .Should().Throw<InvalidDatabaseException>()
                .WithMessage("*data section contains bad data*");
        }

        private void TestIPV6(Reader reader, string file)
        {
            TestAddresses(reader,
                file,
                new[] { "::1:ffff:ffff", "::2:0:0", "::2:0:40", "::2:0:50", "::2:0:58" },
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
                new[] { "1.1.1.33", "255.254.253.123", "89fa::" },
                new Dictionary<string, int>
                {
                    {"::2:0:1", 122}
                });
        }

        private void TestIPV4(Reader reader, string file)
        {
            TestAddresses(reader,
                file,
                Enumerable.Range(0, 5).Select(i => "1.1.1." + (int)Math.Pow(2, 1)),
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
                new[] { "1.1.1.33", "255.254.253.123" },
                new Dictionary<string, int>
                {
                    {"1.1.1.3", 31},
                    {"4.0.0.1", 6}
                });
        }

        private void TestAddresses(Reader reader, string file, IEnumerable<string> singleAddresses,
            Dictionary<string, string> pairs, IEnumerable<string> nullAddresses, Dictionary<string, int> prefixes)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.

            foreach (var address in singleAddresses)
            {
                reader.Find<Dictionary<string, object>>(IPAddress.Parse(address))["ip"].Should().Be(
                    new string(address.ToArray()),
                    $"Did not find expected data record for {address} in {file}");
            }

            foreach (var address in pairs.Keys)
            {
                reader.Find<Dictionary<string, object>>(IPAddress.Parse(address))["ip"].Should().Be(
                    pairs[address],
                    $"Did not find expected data record for {address} in {file}");
            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            foreach (var address in nullAddresses)
            {
                reader.Find<object>(IPAddress.Parse(address)).Should().BeNull(
                    $"Did not find expected data record for {address} in {file}");
            }

            foreach (var address in prefixes.Keys)
            {
                reader.Find<Dictionary<string, object>>(IPAddress.Parse(address), out var routingPrefix);
                routingPrefix.Should().Be(prefixes[address],
                    $"Invalid prefix for {address} in {file}");
            }

            foreach (var node in reader.FindAll<Dictionary<string, object>>())
            {
                TestNode(reader, node);
            }
        }

        private void TestMetadata(Reader reader, int ipVersion)
        {
            var metadata = reader.Metadata;

            metadata.BinaryFormatMajorVersion.Should().Be(2);
            metadata.BinaryFormatMinorVersion.Should().Be(0);
            metadata.IPVersion.Should().Be(ipVersion);
            metadata.DatabaseType.Should().Be("Test");
            metadata.Languages.Should().HaveElementSucceeding("en", "zh");
            metadata.Description.Should().Contain("en", "Test Database");
            metadata.Description.Should().Contain("zh", "Test Database Chinese");
            metadata.Description.Should().NotContain("gibberish", "Lorem ipsum dolor sit amet");
        }
    }
}
