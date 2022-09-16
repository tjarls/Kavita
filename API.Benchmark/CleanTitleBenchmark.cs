using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace API.Benchmark;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class CleanTitleBenchmarks
{
    private readonly IList<string> _names;

    public CleanTitleBenchmarks()
    {
        // Read all comics file names from Comics.txt
        _names = File.ReadAllLines("Data/Comics.txt");
        Console.WriteLine($"Performing benchmark on {_names.Count} file names");
    }

    [Benchmark]
    public void TestOldCleanTitle()
    {
        foreach (var name in _names)
        {
            OldParser.Parser.CleanTitle(name, true);
        }
    }

    [Benchmark]
    public void TestNewCleanTitle()
    {
        foreach (var name in _names)
        {
            Services.Tasks.Scanner.Parser.Parser.CleanTitle(name, true);
        }
    }
}
