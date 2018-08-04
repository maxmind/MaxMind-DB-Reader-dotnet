#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        [Constructor]
        public Country([Parameter("iso_code")] string isoCode)
        {
            IsoCode = isoCode;
        }
    }

    public class GeoIP2
    {
        public Country Country;

        [Constructor]
        public GeoIP2(Country country)
        {
            Country = country ?? new Country();
        }
    }

    public class Program
    {
        private static readonly int COUNT = 500000;

        private static void Main(string[] args)
        {
            // first we check if the command-line argument is provided
            string dbPath = args.Length > 0 ? args[0] : null;
            if (dbPath != null)
            {
                if (!File.Exists(dbPath))
                {
                    throw new Exception("Path provided by command-line argument does not exist!");
                }
            }
            else
            {
                // check if environment variable MAXMIND_BENCHMARK_DB is set
                dbPath = Environment.GetEnvironmentVariable("MAXMIND_BENCHMARK_DB");

                if (!string.IsNullOrEmpty(dbPath))
                {
                    if (!File.Exists(dbPath))
                    {
                        throw new Exception("Path set as environment variable MAXMIND_BENCHMARK_DB does not exist!");
                    }
                }
                else
                {
                    // check if GeoLite2-City.mmdb exists in CWD
                    dbPath = "GeoLite2-City.mmdb";

                    if (!File.Exists(dbPath))
                    {
                        throw new Exception($"{dbPath} does not exist in current directory!");
                    }
                }
            }

            using (var reader = new Reader(dbPath, FileAccessMode.Memory))
            {
                Bench("GeoIP2 class", ip => reader.Find<GeoIP2>(ip));
                Bench("dictionary", ip => reader.Find<IDictionary<string, object>>(ip));
            }
        }

        // TODO: Consider including this in the benchmark
        /*
        private static void TestEnumerateCitiesDatabaseSpeed()
        {
            Stopwatch timer = Stopwatch.StartNew();
            int count = 0;
            using (var reader = new Reader(Path.Combine(_testDataRoot, "../../GeoLite2-City.mmdb"), FileAccessMode.Memory))
                foreach (var node in reader.FindAll<Dictionary<string, object>>(int.MaxValue))
                {
                    count++;
                }
            count.Should().Be(12971146);

#if DEBUG

            timer.Elapsed.TotalMinutes.Should().BeLessThan(1.5);

#else

            timer.Elapsed.TotalMinutes.Should().BeLessThan(0.75);

#endif

        }

        private static void TestEnumerateCountriesDatabaseSpeed()
        {
            Stopwatch timer = Stopwatch.StartNew();
            int count = 0;
            using (var reader = new Reader(Path.Combine(_testDataRoot, "../../GeoLite2-Country.mmdb"), FileAccessMode.Memory))
            foreach (var node in reader.FindAll<Dictionary<string, object>>(int.MaxValue))
            {
                count++;
            }
            count.Should().Be(1282101);

#if DEBUG

            timer.Elapsed.TotalMinutes.Should().BeLessThan(0.5);

#else

            timer.Elapsed.TotalMinutes.Should().BeLessThan(0.25);

#endif

        }

        */

        private static void Bench(string name, Action<IPAddress> op)
        {
            var rand = new Random(1);
            var s = Stopwatch.StartNew();
            for (var i = 0; i < COUNT; i++)
            {
                var ip = new IPAddress(rand.Next(int.MaxValue));
                op(ip);
                if (i % 50000 == 0)
                    Console.WriteLine(i + " " + ip);
            }
            s.Stop();
            Console.WriteLine("{0}: {1:N0} queries per second", name, COUNT / s.Elapsed.TotalSeconds);
        }
    }
}
