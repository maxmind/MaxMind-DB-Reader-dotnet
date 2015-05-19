#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;

#endregion

namespace MaxMind.Db.Test
{
    [TestFixture]
    public class ThreadingTest
    {
        [Test]
        [TestCase(FileAccessMode.MemoryMapped)]
        [TestCase(FileAccessMode.Memory)]
        public void TestParallelFor(FileAccessMode mode)
        {
            var count = 0;
            var ipsAndResults = new Dictionary<IPAddress, string>();
            var rand = new Random();
            using (var reader = new Reader(Path.Combine("..", "..", "TestData", "GeoLite2-City.mmdb"), mode))
            {
                while (count < 10000)
                {
                    var ip = new IPAddress(rand.Next(int.MaxValue));
                    var resp = reader.Find(ip);
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
                    var result = reader.Find(ipAddress);
                    var resultString = result.ToString();
                    var expectedString = ipsAndResults[ipAddress];
                    if (resultString != expectedString)
                        throw new Exception(string.Format("Non-matching result. Expected {0}, found {1}", expectedString,
                            resultString));
                });
            }
        }

        [Test]
        [TestCase(FileAccessMode.MemoryMapped)]
        [TestCase(FileAccessMode.Memory)]
        [Category("BreaksMono")]
        public void TestManyOpens(FileAccessMode mode)
        {
            Parallel.For(0, 50, i =>
            {
                using (var reader = new Reader(Path.Combine("..", "..", "TestData", "GeoLite2-City.mmdb"), mode))
                {
                    reader.Find("1.1.1.1");
                }
            });
        }
    }
}