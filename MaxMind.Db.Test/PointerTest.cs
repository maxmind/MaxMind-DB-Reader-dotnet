#region

using NUnit.Framework;
using System.Collections.ObjectModel;
using System.IO;

#endregion

namespace MaxMind.Db.Test
{
    [TestFixture]
    public class PointerTest
    {
        [Test]
        public void TestWithPointers()
        {
            var path = Path.Combine("..", "..", "TestData", "MaxMind-DB", "test-data", "maps-with-pointers.raw");
            using (var database = new ArrayReader(path))
            {
                var decoder = new Decoder(database, 0);

                int offset;
                var node = decoder.Decode<ReadOnlyDictionary<string, object>>(0, out offset);
                Assert.That(node["long_key"], Is.EqualTo("long_value1"));

                node = decoder.Decode<ReadOnlyDictionary<string, object>>(22, out offset);
                Assert.That(node["long_key"], Is.EqualTo("long_value2"));

                node = decoder.Decode<ReadOnlyDictionary<string, object>>(37, out offset);
                Assert.That(node["long_key2"], Is.EqualTo("long_value1"));

                node = decoder.Decode<ReadOnlyDictionary<string, object>>(50, out offset);
                Assert.That(node["long_key2"], Is.EqualTo("long_value2"));

                node = decoder.Decode<ReadOnlyDictionary<string, object>>(55, out offset);
                Assert.That(node["long_key"], Is.EqualTo("long_value1"));

                node = decoder.Decode<ReadOnlyDictionary<string, object>>(57, out offset);
                Assert.That(node["long_key2"], Is.EqualTo("long_value2"));
            }
        }
    }
}