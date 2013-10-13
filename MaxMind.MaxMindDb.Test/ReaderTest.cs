using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MaxMind.MaxMindDb.Test
{
    using System.Runtime.CompilerServices;

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
                    var reader = new MaxMindDbReader("..\\..\\TestData\\MaxMind-DB\\test-data\\MaxMind-DB-test-ipv" + ipVersion + "-" + recordSize + ".mmdb");
                    using (reader)
                    {
                        TestMetadata(reader, ipVersion, recordSize);
                    }
                }
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
