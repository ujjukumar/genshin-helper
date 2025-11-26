using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace AutoSkipper;

// --- 6. Entry Point ---
internal class Program
{
    static async Task Main(string[] args)
    {
        // Fancy Header
        AnsiConsole.Write(
            new FigletText("Genshin AutoSkip")
                .LeftJustified()
                .Color(Color.Cyan1));

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
        
        Logger.SetVerbose(verbose);
        
        var config = await ScreenConfig.CreateAsync();
        
        // Display Config Table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Setting[/]");
        table.AddColumn(new TableColumn("[yellow]Value[/]").Centered());

        table.AddRow("Resolution", $"{config.WIDTH}x{config.HEIGHT}");
        table.AddRow("Window Title", config.WINDOW_TITLE);
        table.AddRow("Typing Speed", $"{config.Config.StandardDelayMin:F2}s - {config.Config.StandardDelayMax:F2}s");
        table.AddRow("Break Chance", $"{config.Config.BreakChance:P1}");
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Instructions Panel
        var instructions = new Panel(
            new Markup(
                "[bold green]F8[/]: Start  [bold yellow]F9[/]: Pause  [bold red]F12[/]: Exit\n" +
                "[bold blue]F7[/]: Toggle Log File  [bold purple]Mouse5[/]: Burst Mode"))
            .Header("Controls")
            .BorderColor(Color.Green);
        AnsiConsole.Write(instructions);
        AnsiConsole.WriteLine();
        
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
        Logger.Log($"Benchmarking {count} GetPixel calls.");

        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++)
        {
            _ = Native.GetPixel(hdc, 100, 100);
        }
        long end = Stopwatch.GetTimestamp();

        _ = Native.ReleaseDC(IntPtr.Zero, hdc);

        double duration = (end - start) / (double)Stopwatch.Frequency;
        double ops = count / duration;

        Logger.Log($"Time: {duration:F4} seconds");
        Logger.Log($"Speed: {ops:N0} ops/sec");
    }
}
