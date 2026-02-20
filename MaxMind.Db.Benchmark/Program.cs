using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using MaxMind.Db;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;

using MaxMind.Db.Benchmark;

BenchmarkRunner.Run<CityBenchmark>(new DebugInProcessConfig());

[MemoryDiagnoser]
public class CityBenchmark
{
    // A random IP that has city info.
    private Reader _reader = null!;
    private IPAddress[] _ipAddresses = [];

    [GlobalSetup]
    public void GlobalSetup()
    {
        const string dbPathVarName = "MAXMIND_BENCHMARK_DB";
        string dbPath = Environment.GetEnvironmentVariable(dbPathVarName) ??
                        throw new InvalidOperationException($"{dbPathVarName} was not set");
        _reader = new Reader(dbPath);

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
    public int City()
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
    public int Isp()
    {
        int x = 0;
        foreach (var ipAddress in _ipAddresses)
        {
            if (_reader.Find<IspResponse>(ipAddress) != null)
            {
                x += 1;
            }
        }

        return x;
    }
}