using MaxMind.Db;
using System;
using System.Net;
using System.Diagnostics;
using System.Collections.Generic;

var reader = new Reader("/var/lib/GeoIP/GeoLite2-City.mmdb");
var ip = IPAddress.Parse("8.8.8.8");

var sw = Stopwatch.StartNew();
var result = reader.Find<Dictionary<string, object>>(ip);
sw.Stop();

Console.WriteLine($"Single lookup took: {sw.Elapsed.TotalMicroseconds:F1} μs");
if (result != null)
    Console.WriteLine($"Result has {result.Count} keys");
else
    Console.WriteLine("No result found");
