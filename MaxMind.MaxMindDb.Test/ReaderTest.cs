using System;
using System.Collections.Generic;

namespace MaxMind.MaxMindDb.Test
{
    using System.Linq;

    using NUnit.Framework;

    [TestFixture]
    public class ReaderTest
    {
        [Test]
        public void Test()
        {
            foreach (var recordSize in new long[]{24, 28, 32})
            {
                foreach (var ipVersion in new int[]{4, 6})
                {
                    var file = "..\\..\\TestData\\MaxMind-DB\\test-data\\MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb";
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
