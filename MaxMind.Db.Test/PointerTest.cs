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
                var node = (ReadOnlyDictionary<string, object>)decoder.Decode(0, out offset);
                Assert.That(node["long_key"], Is.EqualTo("long_value1"));

                node = (ReadOnlyDictionary<string, object>)decoder.Decode(22, out offset);
                Assert.That(node["long_key"], Is.EqualTo("long_value2"));

                node = (ReadOnlyDictionary<string, object>)decoder.Decode(37, out offset);
                Assert.That(node["long_key2"], Is.EqualTo("long_value1"));

                node = (ReadOnlyDictionary<string, object>)decoder.Decode(50, out offset);
                Assert.That(node["long_key2"], Is.EqualTo("long_value2"));

                node = (ReadOnlyDictionary<string, object>)decoder.Decode(55, out offset);
                Assert.That(node["long_key"], Is.EqualTo("long_value1"));

                node = (ReadOnlyDictionary<string, object>)decoder.Decode(57, out offset);
                Assert.That(node["long_key2"], Is.EqualTo("long_value2"));
            }
        }
    }
}