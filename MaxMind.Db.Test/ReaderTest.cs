#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection;
using MaxMind.Db.Test.Helper;
using NUnit.Framework;

#endregion

namespace MaxMind.Db.Test
{
    [TestFixture]
    public class ReaderTest
    {
        private readonly string _testDataRoot =
            Path.Combine(Program.CurrentDirectory, "TestData", "MaxMind-DB", "test-data");

        [Test]
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

        [Test]
        public void TestStream()
        {
            foreach (var recordSize in new long[] { 24, 28, 32 })
            {
                foreach (var ipVersion in new[] { 4, 6 })
                {
                    var file = Path.Combine(_testDataRoot,
                        "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");
                    using (var streamReader = File.OpenText(file))
                    {
                        using (var reader = new Reader(streamReader.BaseStream))
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
        }

        [Test]
        public void NullStreamThrowsArgumentNullException()
        {
            Assert.Throws(Is.TypeOf<ArgumentNullException>()
                .And.Message.Contains("The database stream must not be null."),
                () => new Reader((Stream)null));
        }

        [Test]
        public void TestEmptyStream()
        {
            using (var stream = new MemoryStream())
            {
                Assert.Throws(Is.TypeOf<InvalidDatabaseException>()
                    .And.Message.Contains("zero bytes left in the stream"),
                    () => new Reader(stream));
            }
        }

        [Test]
        public void NoIPV4SearchTree()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-no-ipv4-search-tree.mmdb")))
            {
                Assert.That(reader.Find<string>(IPAddress.Parse("1.1.1.1")), Is.EqualTo("::0/64"));
                Assert.That(reader.Find<string>(IPAddress.Parse("192.1.1.1")), Is.EqualTo("::0/64"));
            }
        }

        [Test]
        public void TestDecodingToDictionary()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb")))
            {
                var record = reader.Find<Dictionary<string, object>>(IPAddress.Parse("::1.1.1.0"));
                TestDecodingTypes(record);
            }
        }

        [Test]
        public void TestDecodingToGenericIDictionary()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb")))
            {
                var record = reader.Find<IDictionary<string, object>>(IPAddress.Parse("::1.1.1.0"));
                TestDecodingTypes(record);
            }
        }

        [Test]
        public void TestDecodingToConcurrentDictionary()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb")))
            {
                var record = reader.Find<ConcurrentDictionary<string, object>>(IPAddress.Parse("::1.1.1.0"));
                TestDecodingTypes(record);
            }
        }

        public void TestDecodingTypes(IDictionary<string, object> record)
        {
            Assert.That(record["boolean"], Is.True);

            Assert.That(record["bytes"], Is.EquivalentTo(new byte[] { 0, 0, 0, 42 }));

            Assert.That(record["utf8_string"], Is.EqualTo("unicode! ☯ - ♫"));

            var array = (List<object>)record["array"];
            Assert.That(array.Count(), Is.EqualTo(3));
            Assert.That(array[0], Is.EqualTo(1));
            Assert.That(array[1], Is.EqualTo(2));
            Assert.That(array[2], Is.EqualTo(3));

            var map = (Dictionary<string, object>)record["map"];
            Assert.That(map.Count(), Is.EqualTo(1));

            var mapX = (Dictionary<string, object>)map["mapX"];
            Assert.That(mapX.Count(), Is.EqualTo(2));
            Assert.That(mapX["utf8_stringX"], Is.EqualTo("hello"));

            var arrayX = (List<object>)mapX["arrayX"];
            Assert.That(arrayX.Count(), Is.EqualTo(3));
            Assert.That(arrayX[0], Is.EqualTo(7));
            Assert.That(arrayX[1], Is.EqualTo(8));
            Assert.That(arrayX[2], Is.EqualTo(9));

            Assert.AreEqual(42.123456, (double)record["double"], 0.000000001);
            Assert.AreEqual(1.1, (float)record["float"], 0.000001);
            Assert.That(record["int32"], Is.EqualTo(-268435456));
            Assert.That(record["uint16"], Is.EqualTo(100));
            Assert.That(record["uint32"], Is.EqualTo(268435456));
            Assert.That(record["uint64"], Is.EqualTo(1152921504606846976));
            Assert.That(record["uint128"],
                Is.EqualTo(BigInteger.Parse("1329227995784915872903807060280344576")));
        }

        [Test]
        public void TestDecodingTypesToObject()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb")))
            {
                var injectables = new InjectableValues();
                injectables.AddValue("injected", "injected string");
                var record = reader.Find<TypeHolder>(IPAddress.Parse("::1.1.1.0"), injectables);

                Assert.That(record.Boolean, Is.True);
                Assert.That(record.Bytes, Is.EquivalentTo(new byte[] { 0, 0, 0, 42 }));
                Assert.That(record.Utf8String, Is.EqualTo("unicode! ☯ - ♫"));

                Assert.That(record.Array, Is.EqualTo(new List<long> { 1, 2, 3 }));

                var mapX = record.Map.MapX;
                Assert.That(mapX.Utf8StringX, Is.EqualTo("hello"));

                Assert.That(mapX.ArrayX, Is.EqualTo(new List<long> { 7, 8, 9 }));

                Assert.AreEqual(42.123456, record.Double, 0.000000001);
                Assert.AreEqual(1.1, record.Float, 0.000001);
                Assert.That(record.Int32, Is.EqualTo(-268435456));
                Assert.That(record.Uint16, Is.EqualTo(100));
                Assert.That(record.Uint32, Is.EqualTo(268435456));
                Assert.That(record.Uint64, Is.EqualTo(1152921504606846976));
                Assert.That(record.Uint128,
                    Is.EqualTo(BigInteger.Parse("1329227995784915872903807060280344576")));

                Assert.That(record.Nonexistant.Injected, Is.EqualTo("injected string"));
                Assert.That(record.Nonexistant.InnerNonexistant.Injected, Is.EqualTo("injected string"));
            }
        }

        [Test]
        public void TestZeros()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-decoder.mmdb")))
            {
                var record = reader.Find<Dictionary<string, object>>(IPAddress.Parse("::"));

                Assert.That(record["boolean"], Is.False);

                Assert.That(record["bytes"], Is.EquivalentTo(new byte[0]));

                Assert.That(record["utf8_string"], Is.EqualTo(""));

                Assert.That(record["array"], Is.InstanceOf<List<object>>());
                Assert.That(((List<object>)record["array"]).Count(), Is.EqualTo(0));

                Assert.That(record["map"], Is.InstanceOf<Dictionary<string, object>>());
                Assert.That(((Dictionary<string, object>)record["map"]).Count(), Is.EqualTo(0));

                Assert.AreEqual(0, (double)record["double"], 0.000000001);
                Assert.AreEqual(0, (float)record["float"], 0.000001);
                Assert.That(record["int32"], Is.EqualTo(0));
                Assert.That(record["uint16"], Is.EqualTo(0));
                Assert.That(record["uint32"], Is.EqualTo(0));
                Assert.That(record["uint64"], Is.EqualTo(0));
                Assert.That(record["uint128"], Is.EqualTo(new BigInteger(0)));
            }
        }

        [Test]
        public void TestBrokenDatabase()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "GeoIP2-City-Test-Broken-Double-Format.mmdb")))
            {
                Assert.Throws(Is.TypeOf<InvalidDatabaseException>()
                    .And.Message.Contains("contains bad data"),
                    () => reader.Find<object>(IPAddress.Parse("2001:220::")));
            }
        }

        [Test]
        public void TestBrokenSearchTreePointer()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-broken-pointers-24.mmdb")))
            {
                Assert.Throws(Is.TypeOf<InvalidDatabaseException>()
                    .And.Message.Contains("search tree is corrupt"),
                    () => reader.Find<object>(IPAddress.Parse("1.1.1.32")));
            }
        }

        [Test]
        public void TestBrokenDataPointer()
        {
            using (var reader = new Reader(Path.Combine(_testDataRoot, "MaxMind-DB-test-broken-pointers-24.mmdb")))
            {
                Assert.Throws(Is.TypeOf<InvalidDatabaseException>()
                    .And.Message.Contains("data section contains bad data"),
                    () => reader.Find<object>(IPAddress.Parse("1.1.1.16")));
            }
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
            foreach (var address in singleAddresses)
            {
                Assert.That((reader.Find<Dictionary<string, object>>(IPAddress.Parse(address)))["ip"],
                    Is.EqualTo(address),
                    $"Did not find expected data record for {address} in {file}");
            }

            foreach (var address in pairs.Keys)
            {
                Assert.That((reader.Find<Dictionary<string, object>>(IPAddress.Parse(address)))["ip"],
                    Is.EqualTo(pairs[address]),
                    $"Did not find expected data record for {address} in {file}");
            }

            foreach (var address in nullAddresses)
            {
                Assert.That(reader.Find<object>(IPAddress.Parse(address)), Is.Null,
                    $"Did not find expected data record for {address} in {file}");
            }

            foreach (var address in prefixes.Keys)
            {
                int routingPrefix;
                reader.Find<Dictionary<string, object>>(IPAddress.Parse(address), out routingPrefix);
                Assert.That(routingPrefix, Is.EqualTo(prefixes[address]),
                    $"Invalid prefix for {address} in {file}");
            }
        }

        private void TestMetadata(Reader reader, int ipVersion)
        {
            var metadata = reader.Metadata;

            Assert.That(metadata.BinaryFormatMajorVersion, Is.EqualTo(2));
            Assert.That(metadata.BinaryFormatMinorVersion, Is.EqualTo(0));
            Assert.That(metadata.IPVersion, Is.EqualTo(ipVersion));
            Assert.That(metadata.DatabaseType, Is.EqualTo("Test"));
            Assert.That(metadata.Languages[0], Is.EqualTo("en"));
            Assert.That(metadata.Languages[1], Is.EqualTo("zh"));
            Assert.That(metadata.Description["en"], Is.EqualTo("Test Database"));
            Assert.That(metadata.Description["zh"], Is.EqualTo("Test Database Chinese"));
        }
    }
}