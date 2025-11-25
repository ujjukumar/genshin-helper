using System;
using System.Diagnostics;
using System.Threading;

namespace AutoSkipper;

// --- 6. Entry Point ---
internal class Program
{
    static void Main(string[] args)
    {
        // Parse command line arguments
        bool verbose = false;
        foreach (var arg in args)
        {
            if (arg == "--benchmark")
            {
                RunBenchmark();
                return;
            }
            if (arg == "-v" || arg == "--verbose")
            {
                verbose = true;
            }
        }
        // Console.Title = "Genshin AutoSkip";
        var config = ScreenConfig.Load();
        Logger.SetVerbose(verbose);
        using var skipper = new AutoSkipper(config);
        using var hooks = new GlobalHooks(skipper);

        Thread logicThread = new(skipper.Run)
        {
            IsBackground = true
        };
        logicThread.Start();

        // Standard Win32 Message Loop for Hooks
        while (Native.GetMessage(out Native.MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            Native.TranslateMessage(msg);
            Native.DispatchMessage(msg);
        }

        skipper.ShouldExit = true;
        skipper.Wake();
        logicThread.Join();

        Logger.Shutdown();
    }

    static void RunBenchmark()
    {
        IntPtr hdc = Native.GetDC(IntPtr.Zero);
        int count = 100;
        Console.WriteLine($"Benchmarking {count} GetPixel calls.");

        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++)
        {
            _ = Native.GetPixel(hdc, 100, 100);
        }
        long end = Stopwatch.GetTimestamp();

        _ = Native.ReleaseDC(IntPtr.Zero, hdc);

        double duration = (end - start) / (double)Stopwatch.Frequency;
        double ops = count / duration;

        Console.WriteLine($"Time: {duration:F4} seconds");
        Console.WriteLine($"Speed: {ops:N0} ops/sec");
    }
}
