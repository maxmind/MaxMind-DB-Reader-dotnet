using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace MaxMind.MaxMindDb.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var reader = new Reader("GeoLite2-City.mmdb", FileAccessMode.MemoryMapped))
            {
                var count = 100000;
                var rand = new Random();
                var start = DateTime.Now;
                for (int i = 0; i < count; i++)
                {
                    var ip = new IPAddress(rand.Next(int.MaxValue));
                    if (i%50000 == 0)
                        Console.WriteLine(i + " " + ip);

                    var resp = reader.Find(ip);
                }

                var stop = DateTime.Now;
                Console.WriteLine("Requests per second: " + count/(stop - start).TotalSeconds);
            }
        }
    }
}
