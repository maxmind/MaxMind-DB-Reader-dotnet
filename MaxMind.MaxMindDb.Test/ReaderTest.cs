using System;
using System.Collections.Generic;

namespace MaxMind.MaxMindDb.Test
{
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
                        TestMetadata(reader, ipVersion, recordSize);

                        if (ipVersion == 4)
                        {
                            TestIPV4(reader, file);
                        }
                    }
                }
            }
        }

        private void TestIPV4(MaxMindDbReader reader, string file)
        {
            for (int i = 0; i <= 5; i++)
            {
                var address = "1.1.1." + (int)Math.Pow(2, i);
                Assert.That(reader.Find(address).Value<string>("ip"), Is.EqualTo(address), string.Format("Did not find expected data record for {0} in {1}", address, file));
            }

            var pairs = new Dictionary<string, string>()
                        {
                            {"1.1.1.3", "1.1.1.2"},
                            {"1.1.1.5", "1.1.1.4"},
                            {"1.1.1.7", "1.1.1.4"},
                            {"1.1.1.9", "1.1.1.8"},
                            {"1.1.1.15", "1.1.1.8"},
                            {"1.1.1.17", "1.1.1.16"},
                            {"1.1.1.31", "1.1.1.16"}
                        };

            foreach (var address in pairs.Keys)
            {
                Assert.That(reader.Find(address).Value<string>("ip"), Is.EqualTo(pairs[address]), string.Format("Did not find expected data record for {0} in {1}", address, file));
            }

            foreach (string ip in new [] { "1.1.1.33", "255.254.253.123" })
            {
                Assert.That(reader.Find(ip), Is.Null, string.Format("Did not find expected data record for {0} in {1}", ip, file));
            }
        }

        private void TestMetadata(MaxMindDbReader reader, int ipVersion, long recordSize)
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
