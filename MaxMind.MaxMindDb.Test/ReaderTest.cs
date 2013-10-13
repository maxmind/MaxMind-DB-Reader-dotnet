using System;
using System.Collections.Generic;

namespace MaxMind.MaxMindDb.Test
{
    using System.Linq;

    using Newtonsoft.Json.Linq;

    using NUnit.Framework;

    [TestFixture]
    public class ReaderTest
    {
        private const string TEST_DATA_ROOT = "..\\..\\TestData\\MaxMind-DB\\test-data\\";

        [Test]
        public void Test()
        {
            foreach (var recordSize in new long[]{24, 28, 32})
            {
                foreach (var ipVersion in new int[]{4, 6})
                {
                    var file = TEST_DATA_ROOT + "MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb";
                    var reader = new MaxMindDbReader(file);
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
        public void NoIPV4SearchTree()
        {
            var reader = new MaxMindDbReader(TEST_DATA_ROOT + "MaxMind-DB-no-ipv4-search-tree.mmdb");

            Assert.That(reader.Find("1.1.1.1").ToObject<string>(), Is.EqualTo("::/64"));
            Assert.That(reader.Find("192.1.1.1").ToObject<string>(), Is.EqualTo("::/64"));
        }

        [Test]
        public void TestDecodingTypes()
        {
            var reader = new MaxMindDbReader(TEST_DATA_ROOT + "MaxMind-DB-test-decoder.mmdb");

            var record = reader.Find("::1.1.1.0");

            Assert.That(record.Value<bool>("boolean"), Is.True);

            Assert.That(record["bytes"].Value<byte[]>(0), Is.EquivalentTo(new byte[]{0,0,0, (byte)42}));

            Assert.That(record.Value<string>("utf8_string"), Is.EqualTo("unicode! ☯ - ♫"));

            var array = record["array"];
            Assert.That(array, Is.InstanceOf<JArray>());
            Assert.That(array.Count(), Is.EqualTo(3));
            Assert.That(array[0].Value<int>(), Is.EqualTo(1));
            Assert.That(array[1].Value<int>(), Is.EqualTo(2));
            Assert.That(array[2].Value<int>(), Is.EqualTo(3));

            var map = record["map"];
            Assert.That(map, Is.InstanceOf<JObject>());
            Assert.That(map.Count(), Is.EqualTo(1));

            var mapX = map["mapX"];
            Assert.That(mapX.Count(), Is.EqualTo(2));

            var arrayX = mapX["arrayX"];
            Assert.That(arrayX.Count(), Is.EqualTo(3));
            Assert.That(arrayX[0].Value<int>(), Is.EqualTo(7));
            Assert.That(arrayX[1].Value<int>(), Is.EqualTo(8));
            Assert.That(arrayX[2].Value<int>(), Is.EqualTo(9));

            Assert.That(mapX.Value<string>("utf8_stringX"), Is.EqualTo("hello"));
        }

        private void TestIPV6(MaxMindDbReader reader, string file)
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
                new [] {"1.1.1.33", "255.254.253.123", "89fa::"}
                );

        }

        private void TestIPV4(MaxMindDbReader reader, string file)
        {
            TestAddresses(reader, 
                file, 
                Enumerable.Range(0,5).Select(i => "1.1.1." + (int)Math.Pow(2, 1)), 
                new Dictionary<string, string>(){
                            {"1.1.1.3", "1.1.1.2"},
                            {"1.1.1.5", "1.1.1.4"},
                            {"1.1.1.7", "1.1.1.4"},
                            {"1.1.1.9", "1.1.1.8"},
                            {"1.1.1.15", "1.1.1.8"},
                            {"1.1.1.17", "1.1.1.16"},
                            {"1.1.1.31", "1.1.1.16"}
                        }, 
                new [] { "1.1.1.33", "255.254.253.123" });
        }

        public void TestAddresses(MaxMindDbReader reader, string file, IEnumerable<string> singleAddresses, Dictionary<string, string> pairs, IEnumerable<string> nullAddresses)
        {
            foreach (var address in singleAddresses)
            {
                Assert.That(reader.Find(address).Value<string>("ip"), Is.EqualTo(address), string.Format("Did not find expected data record for {0} in {1}", address, file));
            }

            foreach (var address in pairs.Keys)
            {
                Assert.That(reader.Find(address).Value<string>("ip"), Is.EqualTo(pairs[address]), string.Format("Did not find expected data record for {0} in {1}", address, file));
            }

            foreach (var address in nullAddresses)
            {
                Assert.That(reader.Find(address), Is.Null, string.Format("Did not find expected data record for {0} in {1}", address, file));
            }
        }

        private void TestMetadata(MaxMindDbReader reader, int ipVersion)
        {
            var metadata = reader.Metadata;

            Assert.That(metadata.BinaryFormatMajorVersion, Is.EqualTo(2));
            Assert.That(metadata.binaryFormatMinorVersion, Is.EqualTo(0));
            Assert.That(metadata.IpVersion, Is.EqualTo(ipVersion));
            Assert.That(metadata.DatabaseType, Is.EqualTo("Test"));
            Assert.That(metadata.Languages[0], Is.EqualTo("en"));
            Assert.That(metadata.Languages[1], Is.EqualTo("zh"));
            Assert.That(metadata.Description["en"], Is.EqualTo("Test Database"));
            Assert.That(metadata.Description["zh"], Is.EqualTo("Test Database Chinese"));

        }
    }
}
