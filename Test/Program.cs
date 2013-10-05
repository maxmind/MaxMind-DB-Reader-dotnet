using System;
using System.Diagnostics;
using System.Net;

namespace MaxMind.MaxMindDb.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime start = DateTime.UtcNow;

            MaxMindDbReader db = new MaxMindDbReader("GeoLite2-City.mmdb", FileAccessMode.MemoryMapped);

            var tmp = db.Find("202.196.224.1");

            DateTime stop = DateTime.UtcNow;
            TimeSpan span = stop.Subtract(start);
            Console.WriteLine(String.Format("test #x - {0}", span.Duration()));
            Console.WriteLine(tmp.ToString());

#if false

            #region test1

            DateTime start = DateTime.UtcNow;

            MaxMindDbReader db = new MaxMindDbReader("GeoLite2-Country.mmdb", FileAccessMode.Memory);

            IPAddress address = IPAddress.None;

            for (int i = 0; i < 10000; i++)
            {
                int ip = GetRandom();
                address =  new IPAddress(BitConverter.GetBytes(ip));
                var tmp = db.Find(address);
            }

            DateTime stop = DateTime.UtcNow;
            TimeSpan span = stop.Subtract(start);
            Debug.WriteLine(String.Format("test #1 - {0}", span.Duration()));

            #endregion

            #region test2

            start = DateTime.UtcNow;

            db = new MaxMindDbReader("GeoLite2-City.mmdb", FileAccessMode.Memory);

            address = IPAddress.None;

            for (int i = 0; i < 10000; i++)
            {
                int ip = GetRandom();
                address = new IPAddress(BitConverter.GetBytes(ip));
                var tmp = db.Find(address);
            }

            stop = DateTime.UtcNow;
            span = stop.Subtract(start);
            Debug.WriteLine(String.Format("test #2 - {0}", span.Duration()));

            #endregion

            #region test3

            start = DateTime.UtcNow;

            db = new MaxMindDbReader("GeoLite2-Country.mmdb", FileAccessMode.MemoryMapped);

            address = IPAddress.None;

            for (int i = 0; i < 10000; i++)
            {
                int ip = GetRandom();
                address = new IPAddress(BitConverter.GetBytes(ip));
                var tmp = db.Find(address);
            }

            stop = DateTime.UtcNow;
            span = stop.Subtract(start);
            Debug.WriteLine(String.Format("test #3 - {0}", span.Duration()));

            #endregion

            #region test4

            start = DateTime.UtcNow;

            db = new MaxMindDbReader("GeoLite2-City.mmdb", FileAccessMode.MemoryMapped);

            address = IPAddress.None;

            for (int i = 0; i < 10000; i++)
            {
                int ip = GetRandom();
                address = new IPAddress(BitConverter.GetBytes(ip));
                var tmp = db.Find(address);
            }

            stop = DateTime.UtcNow;
            span = stop.Subtract(start);
            Debug.WriteLine(String.Format("test #4 - {0}", span.Duration()));

            #endregion

            /*
            test #1 - 00:00:07.9770951
            test #2 - 00:00:15.0705135
            test #3 - 00:00:01.5088017
            test #4 - 00:00:02.2709541
            */

            #region test5

            List<Thread> threads = new List<Thread>();

            for(int i = 0; i < 5; i++)
                threads.Add(new Thread(TestThread));

            threads.ForEach(o => o.Start());
            threads.ForEach(o => o.Join());

            #endregion

#endif
        }

        static void TestThread()
        {
            DateTime start = DateTime.UtcNow;

            MaxMindDbReader db = new MaxMindDbReader("GeoLite2-City.mmdb", FileAccessMode.MemoryMapped);

            IPAddress address = IPAddress.None;

            for (int i = 0; i < 10000; i++)
            {
                int ip = GetRandom();
                address = new IPAddress(BitConverter.GetBytes(ip));
                var tmp = db.Find(address);
            }

            DateTime stop = DateTime.UtcNow;
            TimeSpan span = stop.Subtract(start);
            Debug.WriteLine(String.Format("test #x - {0}", span.Duration()));
        }

        static int GetRandom()
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            return rand.Next(0, Int32.MaxValue);
        }
    }
}
