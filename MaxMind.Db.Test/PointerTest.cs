#region

using MaxMind.Db.Test.Helper;
using System.Collections.Generic;
using System.IO;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    public class PointerTest
    {
        [Fact]
        public void TestWithPointers()
        {
            var path = Path.Combine(TestUtils.TestDirectory, "TestData", "MaxMind-DB", "test-data", "maps-with-pointers.raw");

            using var database = new ArrayBuffer(File.ReadAllBytes(path));
            var decoder = new Decoder(database, 0);

            var node = decoder.Decode<Dictionary<string, object>>(0, out _);
            Assert.Equal("long_value1", node["long_key"]);

            node = decoder.Decode<Dictionary<string, object>>(22, out _);
            Assert.Equal("long_value2", node["long_key"]);

            node = decoder.Decode<Dictionary<string, object>>(37, out _);
            Assert.Equal("long_value1", node["long_key2"]);

            node = decoder.Decode<Dictionary<string, object>>(50, out _);
            Assert.Equal("long_value2", node["long_key2"]);

            node = decoder.Decode<Dictionary<string, object>>(55, out _);
            Assert.Equal("long_value1", node["long_key"]);

            node = decoder.Decode<Dictionary<string, object>>(57, out _);
            Assert.Equal("long_value2", node["long_key2"]);
        }
    }
}
