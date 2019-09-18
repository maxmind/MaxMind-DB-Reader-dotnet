using System.Net;
using Xunit;

namespace MaxMind.Db.Test
{
    public class NetworkTest
    {
        [Fact]
        public void TestIPv6()
        {
            var network = new Network(
                IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
                28
                );

            Assert.Equal("2001:db0::", network.NetworkAddress.ToString());
            Assert.Equal(28, network.PrefixLength);
            Assert.Equal("2001:db0::/28", network.ToString());
        }

        [Fact]
        public void TestIPv4()
        {
            var network = new Network(
                IPAddress.Parse("192.168.213.111"),
                31
                );

            Assert.Equal("192.168.213.110", network.NetworkAddress.ToString());
            Assert.Equal(31, network.PrefixLength);
            Assert.Equal("192.168.213.110/31", network.ToString());
        }
    }
}
