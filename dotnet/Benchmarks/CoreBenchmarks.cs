using BenchmarkDotNet.Attributes;
using System.Text;

namespace AutoSkipper.Benchmarks;

/// <summary>
/// A simple synchronous file logger for benchmark comparison.
/// Mimics the original logging implementation.
/// </summary>
public class SyncLogger
{
    private readonly StreamWriter _writer;
    public SyncLogger()
    {
        _writer = new("sync_log.txt", false, Encoding.UTF8) { AutoFlush = true };
    }

    public void Log(string message)
    {
        _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} | {message}");
    }
}

[MemoryDiagnoser]
[ShortRunJob]
public class CoreBenchmarks
{
    private readonly SyncLogger _syncLogger = new();
    private readonly IntPtr _hdc = Native.GetDC(IntPtr.Zero);
    
    [GlobalSetup]
    public void GlobalSetup()
    {
        // Pre-initialize the async logger so its first-run costs aren't part of the benchmark
        Logger.Log("Benchmark setup: Initializing async logger.");
    }
    
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Native.ReleaseDC(IntPtr.Zero, _hdc);
        Logger.Shutdown();
    }

    // --- Benchmark Group 1: Logging ---

    [Benchmark(Description = "Old: Synchronous Logger")]
    public void SyncLog()
    {
        _syncLogger.Log("This is a synchronous log message.");
    }

    [Benchmark(Description = "New: Asynchronous Logger")]
    public void AsyncLog()
    {
        Logger.Log("This is an asynchronous log message.");
    }

    // --- Benchmark Group 2: P/Invoke Calls ---
    
    [Benchmark(Description = "P/Invoke: GetPixel")]
    public uint GetPixel()
    {
        return Native.GetPixel(_hdc, 100, 100);
    }
    
    // --- Benchmark Group 3: Utility Methods ---

    [Benchmark(Description = "Utility: ColorsMatch (True)")]
    public bool ColorsMatch_True()
    {
        // Pixel color 0xFFEEF5 is RGB(245, 238, 255)
        return AutoSkipper.ColorsMatch(0xFFEEF5, 250, 240, 250);
    }
    
    [Benchmark(Description = "Utility: ColorsMatch (False)")]
    public bool ColorsMatch_False()
    {
        // Pixel color 0xFFEEF5 is RGB(245, 238, 255)
        return AutoSkipper.ColorsMatch(0xFFEEF5, 10, 20, 30);
    }
}
