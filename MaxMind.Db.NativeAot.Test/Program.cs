using MaxMind.Db;
using System.Net;

var databasePath = Path.Combine(AppContext.BaseDirectory, "MaxMind-DB-test-decoder.mmdb");
using var reader = new Reader(databasePath);
var record = reader.Find<Dictionary<string, object>>(IPAddress.Parse("::1.1.1.0"));

if (record is null || !Equals(record["utf8_string"], "unicode! ☯ - ♫"))
{
    throw new InvalidOperationException("Native AOT lookup returned an unexpected record.");
}

Console.WriteLine("Native AOT lookup passed.");
