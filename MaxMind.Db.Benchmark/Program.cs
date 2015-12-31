#region

using System;
using System.Diagnostics;
using System.Net;

#endregion

namespace MaxMind.Db.Benchmark
{
    public class Country
    {
        public string IsoCode;

        public Country()
        {
        }

        [MaxMindDbConstructor]
        public Country([MaxMindDbProperty("iso_code")] string isoCode)
        {
            IsoCode = isoCode;
        }
    }

    public class GeoIP2
    {
        public Country Country;

        [MaxMindDbConstructor]
        public GeoIP2(Country country)
        {
            Country = country ?? new Country();
        }
    }

    internal class Program
    {
        private static readonly int COUNT = 500000;

        private static void Main(string[] args)
        {
            using (var reader = new Reader("GeoLite2-City.mmdb", FileAccessMode.Memory))
            {
                Bench("GeoIP2 class", ip => reader.Find<GeoIP2>(ip));
                //                Bench("dictionary", ip => reader.Find<IDictionary<string, object>>(ip));
            }
        }

        private static void Bench(string name, Action<IPAddress> op) // Action<IPAddress> op)
        {
            var rand = new Random(1);
            var s = Stopwatch.StartNew();
            for (var i = 0; i < COUNT; i++)
            {
                var ip = new IPAddress(rand.Next(int.MaxValue));
                op(ip);
                if (i % 50000 == 0)
                    Console.WriteLine(i + " " + ip); // + " " + JsonConvert.SerializeObject(resp));
            }
            s.Stop();
            Console.WriteLine("{0}: {1:N0} queries per second", name, COUNT / s.Elapsed.TotalSeconds);
        }
    }
}