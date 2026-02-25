using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using MaxMind.Db;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;

BenchmarkRunner.Run<CityBenchmark>(new DebugInProcessConfig());

[MemoryDiagnoser]
public class CityBenchmark
{
    // A random IP that has city info.
    private Reader _reader = null!;
    private Reader _stringInternedReader = null!;
    private Reader _ArrayBufferReader = null!;

    private IPAddress[] _ipAddresses = [];

    [GlobalSetup]
    public void GlobalSetup()
    {
        const string dbPathVarName = "MAXMIND_BENCHMARK_DB";
        string dbPath = Environment.GetEnvironmentVariable(dbPathVarName) ??
                        throw new InvalidOperationException($"{dbPathVarName} was not set");
        _reader = new Reader(dbPath);
        _ArrayBufferReader = new Reader(dbPath, FileAccessMode.Memory);
        _stringInternedReader = new Reader(dbPath, FileAccessMode.Memory);

        const string ipAddressesVarName = "MAXMIND_BENCHMARK_IP_ADDRESSES";
        string ipAddressesStr = Environment.GetEnvironmentVariable(ipAddressesVarName) ?? "";
        _ipAddresses = ipAddressesStr
            .Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(IPAddress.Parse)
            .ToArray();
        if (_ipAddresses.Length == 0)
        {
            Random random = new(Seed: 0);
            List<IPAddress> list = [];
            for (int i = 0; i < 1_000; i += 1)
            {
                list.Add(new IPAddress(random.Next()));
            }

            _ipAddresses = list.ToArray();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _reader.Dispose();
    }

    [Benchmark]
    public int CityMemoryMappedLookup()
    {
        int x = 0;
        foreach (var ipAddress in _ipAddresses)
        {
            if (_reader.Find<CityResponse>(ipAddress) != null)
            {
                x += 1;
            }
        }

        return x;
    }

    [Benchmark]
    public int CityMemoryLookup()
    {
        int x = 0;
        foreach (var ipAddress in _ipAddresses)
        {
            if (_ArrayBufferReader.Find<CityResponse>(ipAddress) != null)
            {
                x += 1;
            }
        }

        return x;
    }

    [Benchmark]
    public int CityInternedStringsLookup()
    {
        int x = 0;
        foreach (var ipAddress in _ipAddresses)
        {
            if (_stringInternedReader.Find<CityResponse>(ipAddress) != null)
            {
                x += 1;
            }
        }

        return x;
    }
}

public abstract class AbstractCountryResponse
{
    protected AbstractCountryResponse(
        Continent? continent = null,
        Country? country = null,
        Country? registeredCountry = null)
    {
        Continent = continent ?? new Continent();
        Country = country ?? new Country();
        RegisteredCountry = registeredCountry ?? new Country();
    }

    public Continent Continent { get; internal set; }
    public Country Country { get; internal set; }
    public Country RegisteredCountry { get; internal set; }
}

public abstract class AbstractCityResponse : AbstractCountryResponse
{
    protected AbstractCityResponse(
        City? city = null,
        Continent? continent = null,
        Country? country = null,
        Location? location = null,
        Country? registeredCountry = null,
        IReadOnlyList<Subdivision>? subdivisions = null)
        : base(continent, country, registeredCountry)
    {
        City = city ?? new City();
        Location = location ?? new Location();
        Subdivisions = subdivisions ?? new List<Subdivision>().AsReadOnly();
    }

    public City City { get; internal set; }
    public Location Location { get; internal set; }
    public IReadOnlyList<Subdivision> Subdivisions { get; internal set; }
}

public class CityResponse : AbstractCityResponse
{
    [Constructor]
    public CityResponse(
        City? city = null,
        Continent? continent = null,
        Country? country = null,
        Location? location = null,
        [Parameter("registered_country")] Country? registeredCountry = null)
        : base(city, continent, country, location, registeredCountry)
    {
    }
}

public class City : NamedEntity
{
    [Constructor]
    public City(int? confidence = null,
        [Parameter("geoname_id")] long? geoNameId = null,
        IReadOnlyDictionary<string, string>? names = null,
        IReadOnlyList<string>? locales = null)
        : base(geoNameId, names, locales)
    {
        Confidence = confidence;
    }

    public int? Confidence { get; internal set; }
}

public abstract class NamedEntity
{
    [Constructor]
    protected NamedEntity(long? geoNameId = null, IReadOnlyDictionary<string, string>? names = null,
        IReadOnlyList<string>? locales = null)
    {
        Names = names ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        GeoNameId = geoNameId;
        Locales = locales ?? new List<string> { "en" }.AsReadOnly();
    }

    public IReadOnlyDictionary<string, string> Names { get; internal set; }
    public long? GeoNameId { get; internal set; }
    protected internal IReadOnlyList<string> Locales { get; set; }
    public string? Name
    {
        get
        {
            var locale = Locales.FirstOrDefault(l => Names.ContainsKey(l));
            return locale == null ? null : Names[locale];
        }
    }
}

public class Continent : NamedEntity
{
    [Constructor]
    public Continent(
        string? code = null,
        [Parameter("geoname_id")] long? geoNameId = null,
        IReadOnlyDictionary<string, string>? names = null,
        IReadOnlyList<string>? locales = null)
        : base(geoNameId, names, locales)
    {
        Code = code;
    }

    public string? Code { get; internal set; }
}

public class Country : NamedEntity
{
    [Constructor]
    public Country(
        int? confidence = null,
        [Parameter("geoname_id")] long? geoNameId = null,
        [Parameter("is_in_european_union")] bool isInEuropeanUnion = false,
        [Parameter("iso_code")] string? isoCode = null,
        IReadOnlyDictionary<string, string>? names = null,
        IReadOnlyList<string>? locales = null)
        : base(geoNameId, names, locales)
    {
        Confidence = confidence;
        IsoCode = isoCode;
        IsInEuropeanUnion = isInEuropeanUnion;
    }

    public int? Confidence { get; internal set; }
    public bool IsInEuropeanUnion { get; internal set; }
    public string? IsoCode { get; internal set; }
}

public class Location
{
    [Constructor]
    public Location(
        [Parameter("accuracy_radius")] int? accuracyRadius = null,
        double? latitude = null,
        double? longitude = null,
        [Parameter("time_zone")] string? timeZone = null)
    {
        AccuracyRadius = accuracyRadius;
        Latitude = latitude;
        Longitude = longitude;
        TimeZone = timeZone;
    }

    public int? AccuracyRadius { get; internal set; }
    public int? AverageIncome { get; internal set; }
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
    public double? Latitude { get; internal set; }
    public double? Longitude { get; internal set; }
    public int? PopulationDensity { get; internal set; }
    public string? TimeZone { get; internal set; }
}

public class Subdivision : NamedEntity
{
    [Constructor]
    public Subdivision(
        int? confidence = null,
        [Parameter("geoname_id")] long? geoNameId = null,
        [Parameter("iso_code")] string? isoCode = null,
        IReadOnlyDictionary<string, string>? names = null,
        IReadOnlyList<string>? locales = null)
        : base(geoNameId, names, locales)
    {
        Confidence = confidence;
        IsoCode = isoCode;
    }

    public int? Confidence { get; internal set; }
    public string? IsoCode { get; internal set; }
}

public sealed class ReadOnlyMemoryByteComparer : IEqualityComparer<ReadOnlyMemory<byte>>

{
    public static ReadOnlyMemoryByteComparer Default { get; } = new ReadOnlyMemoryByteComparer();

    public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
    {
        return x.Span.SequenceEqual(y.Span);
    }

    public int GetHashCode(ReadOnlyMemory<byte> obj)
    {
        unchecked
        {
            int hash = 17;
            ReadOnlySpan<byte> span = obj.Span;
            
            // Simple hashing of the sequence
            foreach (byte b in span)
            {
                hash = hash * 31 + b;
            }
            return hash;
        }
    }
}

public static class InternedStrings
{
    internal static Dictionary<ReadOnlyMemory<byte>, string> s_Dictionary = new (ReadOnlyMemoryByteComparer.Default);
    
    public static string GetString(ReadOnlyMemory<byte> bytes)
    {
        bool found = s_Dictionary.TryGetValue(bytes, out string? returnValue);

        if (!found)
        {
#if NETCOREAPP2_1_OR_GREATER
            returnValue = Encoding.UTF8.GetString(bytes.Span);
#else
            returnValue = Encoding.UTF8.GetString(bytes.Span.ToArray());
#endif
            s_Dictionary.TryAdd(bytes, returnValue);
        }

        Debug.Assert(returnValue is not null);
        return returnValue;
    }
}
