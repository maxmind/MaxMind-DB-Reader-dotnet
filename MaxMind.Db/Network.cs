using System.Net;

namespace MaxMind.Db
{
    /// <summary>
    ///   <c>Network</c> represents an IP network.
    /// </summary>
    public sealed class Network
    {
        private IPAddress ip;

        /// <summary>
        ///     The prefix length is the number of leading 1 bits in the 
        ///     subnet mask. Sometimes also known as netmask length.
        /// </summary>
        public int PrefixLength { get; }

        /// <summary>
        ///     The first address in the network.
        /// </summary>
        public IPAddress NetworkAddress
        {
            get
            {
                var ipBytes = ip.GetAddressBytes();
                var networkBytes = new byte[ipBytes.Length];
                var curPrefix = PrefixLength;
                for (var i = 0; i < ipBytes.Length && curPrefix > 0; i++)
                {
                    var b = ipBytes[i];
                    if (curPrefix < 8)
                    {
                        var shiftN = 8 - curPrefix;
                        b = (byte)(0xFF & (b >> shiftN) << shiftN);
                    }
                    networkBytes[i] = b;
                    curPrefix -= 8;
                }

                return new IPAddress(networkBytes);
            }
        }

        /// <summary>
        ///     Constructs a <c>Network</c>.
        /// </summary>
        /// <param name="ip">
        ///     An IP address in the network. This does not have to be the
        ///     first address in the network.
        /// </param>
        /// <param name="prefixLength">The prefix length for the network.</param>
        public Network(IPAddress ip, int prefixLength)
        {
            this.ip = ip;
            PrefixLength = prefixLength;
        }

        /// <returns>
        ///     A string representation of the network in CIDR notation, e.g.,
        ///     1.2.3.0/24 or 2001::/8.
        /// </returns>
        public override string ToString()
        {
            return $"{NetworkAddress}/{PrefixLength}";
        }
    }
}
