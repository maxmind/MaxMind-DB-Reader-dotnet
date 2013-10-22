using System.Threading;

namespace MaxMind.DB.Test
{
    using System.IO;
    using NUnit.Framework;

    [TestFixture]
    public class PointerTest
    {
        [Test]
        public void TestWithPointers()
        {
			var path = Path.Combine(new []{"..", "..", "TestData", "MaxMind-DB", "test-data", "maps-with-pointers.raw"});
            var stream = new ThreadLocal<Stream>(() => new MemoryStream(File.ReadAllBytes(path)));
            using (stream)
            {
                var decoder = new Decoder(stream, 0);

                var node = decoder.Decode(0).Node;
                Assert.That(node.Value<string>("long_key"), Is.EqualTo("long_value1"));

                node = decoder.Decode(22).Node;
                Assert.That(node.Value<string>("long_key"), Is.EqualTo("long_value2"));

                node = decoder.Decode(37).Node;
                Assert.That(node.Value<string>("long_key2"), Is.EqualTo("long_value1"));

                node = decoder.Decode(50).Node;
                Assert.That(node.Value<string>("long_key2"), Is.EqualTo("long_value2"));

                node = decoder.Decode(55).Node;
                Assert.That(node.Value<string>("long_key"), Is.EqualTo("long_value1"));

                node = decoder.Decode(57).Node;
                Assert.That(node.Value<string>("long_key2"), Is.EqualTo("long_value2"));
            }
        }
    }
}
