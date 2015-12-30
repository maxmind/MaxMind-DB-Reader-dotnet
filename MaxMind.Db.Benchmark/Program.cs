#region

using System;
using System.Collections.Generic;
using System.Net;

#endregion

namespace MaxMind.Db.Benchmark
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (var reader = new Reader("GeoLite2-City.mmdb", FileAccessMode.Memory))
            {
                var count = 400000;
                var rand = new Random();
                var start = DateTime.Now;
                for (var i = 0; i < count; i++)
                {
                    var ip = new IPAddress(rand.Next(int.MaxValue));
                    var resp = reader.Find<IDictionary<string, object>>(ip);

                    if (i % 50000 == 0)
                        Console.WriteLine(i + " " + ip); // + " " + JsonConvert.SerializeObject(resp));
                }

                var stop = DateTime.Now;
                Console.WriteLine("Requests per second: " + count / (stop - start).TotalSeconds);
            }
        }
    }
}