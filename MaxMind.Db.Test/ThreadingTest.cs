#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using MaxMind.Db.Test.Helper;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    public class ThreadingTest
    {
        private readonly string _testDatabase =
            Path.Combine(TestUtils.TestDirectory, "TestData", "MaxMind-DB", "test-data", "GeoIP2-City-Test.mmdb");

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
        [InlineData(FileAccessMode.MemoryMappedGlobal)]
        [InlineData(FileAccessMode.Memory)]
        public void TestParallelFor(FileAccessMode mode)
        {
            var count = 0;
            var ipsAndResults = new Dictionary<IPAddress, string>();
            var rand = new Random();
            using (var reader = new Reader(_testDatabase, mode))
            {
                while (count < 10000)
                {
                    var ip = new IPAddress(rand.Next(int.MaxValue));
                    var resp = reader.Find<object>(ip);
                    if (resp != null && !ipsAndResults.ContainsKey(ip))
                    {
                        ipsAndResults.Add(ip, resp.ToString());
                        count++;
                    }
                }

                var ips = ipsAndResults.Keys.ToArray();
                Parallel.For(0, ips.Length, i =>
                {
                    var ipAddress = ips[i];
                    var result = reader.Find<object>(ipAddress);
                    var resultString = result.ToString();
                    var expectedString = ipsAndResults[ipAddress];
                    if (resultString != expectedString)
                        throw new Exception($"Non-matching result. Expected {expectedString}, found {resultString}");
                });
            }
        }

        [Theory]
        [InlineData(FileAccessMode.MemoryMapped)]
        [InlineData(FileAccessMode.MemoryMappedGlobal)]
        [InlineData(FileAccessMode.Memory)]
        [Trait("Category", "BreaksMono")]
        public void TestManyOpens(FileAccessMode mode)
        {
            Parallel.For(0, 50, i =>
            {
                using (var reader = new Reader(_testDatabase, mode))
                {
                    reader.Find<object>(IPAddress.Parse("1.1.1.1"));
                }
            });
        }
    }
}
