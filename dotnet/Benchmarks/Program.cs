using BenchmarkDotNet.Running;

namespace AutoSkipper.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Use BenchmarkRunner to run the benchmarks
        BenchmarkRunner.Run<CoreBenchmarks>();
    }
}
