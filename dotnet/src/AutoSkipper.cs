using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace AutoSkipper;

internal enum BreakType
{
    None,
    Short,
    Long
}

internal class AutoSkipper : IDisposable
{
    private readonly ScreenConfig _cfg;
    private bool _running = false;
    public bool IsRunning => _running;
    public bool ShouldExit = false;
    private readonly IntPtr _hdc;
    private CancellationTokenSource _spamCancel = new();
    private readonly object _spamLock = new();
    private readonly ManualResetEventSlim _wakeEvent = new(false);

    // State
    private int _burstPool = 0;
    private bool _skipNext = false;
    private bool _doubleNext = false;
    private bool _burstMode = false;
    private int _burstRemaining = 0;
    private double _postBurstPauseUntil = 0.0;
    private double _breakUntil = 0.0;
    private double _lastBreakCheck = 0.0;
    private const double BreakInterval = 30.0;
    private bool _inDialogue = false;
    private bool _windowActive = false;
    private static ReadOnlySpan<char> GenshinWindowTitle => "Genshin Impact".AsSpan();

    // Counter State
    private int _pressCount = 0;
    private double _sessionStartTime = 0.0;
    private double _inactiveSince = 0.0;
    private StatusContext? _statusContext;

    public AutoSkipper(ScreenConfig cfg)
    {
        _cfg = cfg;
        _hdc = Native.GetDC(IntPtr.Zero);
    }

    ~AutoSkipper()
    {
        Native.ReleaseDC(IntPtr.Zero, _hdc);
    }

    public void Dispose()
    {
        Native.ReleaseDC(IntPtr.Zero, _hdc);
        GC.SuppressFinalize(this);
    }

    public bool IsGameActive()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        Span<char> buffer = stackalloc char[256];
        int length = Native.GetWindowText(hwnd, buffer, buffer.Length);
        var windowTitleSpan = buffer[..length];

        bool isActive = windowTitleSpan.Equals(GenshinWindowTitle, StringComparison.OrdinalIgnoreCase);

        if (!isActive)
        {
            // The expensive Process.GetProcessesByName call has been removed.
            // We now only rely on the window title check.
            // If the process not running, the window won't be in the foreground anyway.
        }
        
        return isActive;
    }

    public static bool ColorsMatch(uint pixel, int r, int g, int b)
    {
        int pr = (int)(pixel & 0xFF);
        int pg = (int)((pixel >> 8) & 0xFF);
        int pb = (int)((pixel >> 16) & 0xFF);
        return Math.Abs(pr - r) <= 10 && Math.Abs(pg - g) <= 10 && Math.Abs(pb - b) <= 10;
    }

    private static double GetTime() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private double NextKeyInterval()
    {
        if (_burstPool > 0)
        {
            _burstPool--;
            // Fast burst: 50% of standard speed
            double min = _cfg.Config.StandardDelayMin * 0.5;
            double max = _cfg.Config.StandardDelayMax * 0.5;
            return min + Random.Shared.NextDouble() * (max - min);
        }
        
        // Chance to enter fast burst mode
        if (Random.Shared.NextDouble() < _cfg.Config.FastBurstChance)
        {
            _burstPool = Random.Shared.Next(2, 6);
            double min = _cfg.Config.StandardDelayMin * 0.5;
            double max = _cfg.Config.StandardDelayMax * 0.5;
            return min + Random.Shared.NextDouble() * (max - min);
        }

        // Standard speed
        return _cfg.Config.StandardDelayMin + Random.Shared.NextDouble() * (_cfg.Config.StandardDelayMax - _cfg.Config.StandardDelayMin);
    }

    private BreakType MaybeBreak()
    {
        // Simplified break logic: just one type of break, but we keep the enum for now
        if (Random.Shared.NextDouble() < _cfg.Config.BreakChance) return BreakType.Short;
        return BreakType.None;
    }

    private double BreakDuration(BreakType kind) => 
        _cfg.Config.BreakDurationMin + Random.Shared.NextDouble() * (_cfg.Config.BreakDurationMax - _cfg.Config.BreakDurationMin);

    public void Run()
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.SimpleDots) // Fallback, but we will override style
            .SpinnerStyle(Style.Parse("black")) // Hide spinner by making it black (or match bg)
            .Start("Waiting for input...", ctx =>
            {
                ctx.Spinner(new NoSpinner()); // Use custom no-op spinner
                _statusContext = ctx;
                RunInternal();
            });
    }

    // Custom spinner that does nothing
    private class NoSpinner : Spinner
    {
        public override TimeSpan Interval => TimeSpan.FromMilliseconds(1000);
        public override bool IsUnicode => false;
        public override IReadOnlyList<string> Frames => new[] { " " };
    }

    private void RunInternal()
    {
        Logger.Log("Ready.");
        UpdateStatus("Ready");
        
        double lastPressTime = GetTime();
        double nextInterval = NextKeyInterval();
        double nextStateCheck = 0.0;
        _lastBreakCheck = GetTime();

        try
        {
            while (!ShouldExit)
            {
                double now = GetTime();

                if (!_running)
                {
                    UpdateStatus("Paused");
                    // Do not call ResetCounter here repeatedly
                    _wakeEvent.Wait(500);
                    _wakeEvent.Reset();
                    lastPressTime = GetTime();
                    continue;
                }

                bool isActive = IsGameActive();

                if (isActive)
                {
                    _inactiveSince = 0.0;
                }
                else
                {
                    if (_inactiveSince == 0.0) _inactiveSince = now;
                    if (now - _inactiveSince > _cfg.Config.InactivePauseSeconds)
                    {
                        Logger.Log($"[yellow]Paused due to inactivity ({_cfg.Config.InactivePauseSeconds}s)[/]");
                        ToggleRun(false);
                        _inactiveSince = 0.0;
                    }
                }

                if (isActive != _windowActive)
                {
                    _windowActive = isActive;
                    string countLog = GetCountLog();
                    ResetCounter();
                    Logger.Log(isActive ? $"Window State: [green]ACTIVE[/]{countLog}" : $"Window State: [red]INACTIVE[/]{countLog}");
                }

                if (!isActive)
                {
                    UpdateStatus("Waiting for Window...");
                    SleepUntil(now + 0.4);
                    continue;
                }

                // Active break
                if (now < _breakUntil)
                {
                    UpdateStatus($"Taking a break... ({_breakUntil - now:F1}s)");
                    SleepUntil(_breakUntil);
                    continue;
                }

                // Periodic break check
                if (now - _lastBreakCheck > BreakInterval)
                {
                    _lastBreakCheck = now;
                    BreakType br = MaybeBreak();
                    if (br != BreakType.None)
                    {
                        double dur = BreakDuration(br);
                        string countLog = GetCountLog();
                        ResetCounter();
                        Logger.Log($"Break: [yellow]{br}[/] {dur:F1}s{countLog}");
                        _breakUntil = now + dur;
                        nextInterval = NextKeyInterval();
                        continue;
                    }
                }

                // Dialogue detection
                if (now >= nextStateCheck)
                {
                    uint pPlaying = Native.GetPixel(_hdc, _cfg.PLAYING_ICON.x, _cfg.PLAYING_ICON.y);
                    bool isPlaying = ColorsMatch(pPlaying, 236, 229, 216);

                    Logger.LogDebug(() => $"Playing pixel: RGB({(pPlaying & 0xFF)},{((pPlaying >> 8) & 0xFF)},{((pPlaying >> 16) & 0xFF)}) @ ({_cfg.PLAYING_ICON.x},{_cfg.PLAYING_ICON.y}) -> {isPlaying}");
                    
                    bool isChoice = false;
                    if (!isPlaying)
                    {
                        uint pLoad = Native.GetPixel(_hdc, _cfg.LOADING_PIXEL.x, _cfg.LOADING_PIXEL.y);
                        if (!ColorsMatch(pLoad, 255, 255, 255))
                        {
                            uint pLow = Native.GetPixel(_hdc, _cfg.DIALOGUE_ICON.x, _cfg.DIALOGUE_ICON.ly);
                            uint pHigh = Native.GetPixel(_hdc, _cfg.DIALOGUE_ICON.x, _cfg.DIALOGUE_ICON.hy);
                            isChoice = ColorsMatch(pLow, 255, 255, 255) || ColorsMatch(pHigh, 255, 255, 255);

                            Logger.LogDebug(() => $"Choice pixels: Low=RGB({(pLow & 0xFF)},{((pLow >> 8) & 0xFF)},{((pLow >> 16) & 0xFF)}), High=RGB({(pHigh & 0xFF)},{((pHigh >> 8) & 0xFF)},{((pHigh >> 16) & 0xFF)}) -> {isChoice}");
                        }
                    }

                    bool isDialogue = isPlaying || isChoice;
                    if (isDialogue != _inDialogue)
                    {
                        _inDialogue = isDialogue;
                        string countLog = GetCountLog();
                        ResetCounter();
                        Logger.Log(isDialogue ? $"Dialogue State: [green]DETECTED[/]{countLog}" : $"Dialogue State: [yellow]ENDED[/]{countLog}");
                    }

                    nextStateCheck = now + 0.15;
                    if (!isDialogue)
                    {
                        UpdateStatus("Idle (No Dialogue)");
                        SleepUntil(now + 0.25);
                        continue;
                    }
                }

                // Post-burst pause
                if (now < _postBurstPauseUntil)
                {
                    UpdateStatus("Pausing after burst...");
                    SleepUntil(_postBurstPauseUntil);
                    continue;
                }

                // Action
                bool actionDue = (now - lastPressTime) >= nextInterval || _burstMode;
                if (actionDue)
                {
                    double r1 = Random.Shared.NextDouble();
                    double r2 = Random.Shared.NextDouble();
                    double r3 = Random.Shared.NextDouble();

                    if (!_skipNext && r1 < 1.0/40.0) _skipNext = true;
                    if (!_doubleNext && r2 < 1.0/35.0) _doubleNext = true;
                    if (!_burstMode && r3 < 1.0/60.0)
                    {
                        _burstMode = true;
                        _burstRemaining = Random.Shared.Next(3, 6);
                        string countLog = GetCountLog();
                        ResetCounter();
                        Logger.Log($"Burst mode: [red]{_burstRemaining}[/]{countLog}");
                    }

                    if (_skipNext)
                    {
                        _skipNext = false;
                        lastPressTime = now;
                        nextInterval = NextKeyInterval();
                    }
                    else
                    {
                        PerformPress(now);
                        lastPressTime = now;
                        nextInterval = NextKeyInterval();
                    }
                }
                else
                {
                    UpdateStatus($"Skipping... [green]{_pressCount}[/] presses");
                }

                double nextActionTime = lastPressTime + nextInterval;
                double wakeTarget = nextActionTime;
                if (_breakUntil > now && _breakUntil < wakeTarget) wakeTarget = _breakUntil;
                if (nextStateCheck < wakeTarget) wakeTarget = nextStateCheck;
                if (_postBurstPauseUntil > now && _postBurstPauseUntil < wakeTarget) wakeTarget = _postBurstPauseUntil;

                SleepUntil(Math.Min(wakeTarget, now + 0.35));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"An unexpected error occurred in the main loop: {ex.Message}");
            Logger.Log(ex.ToString());
        }
        finally
        {
            Logger.Log("Closing Logic Loop");
        }
    }
    
    private void PerformPress(double now)
    {
        bool useSpace = Random.Shared.NextDouble() < (_burstMode ? 0.1 : _cfg.Config.SpaceKeyChance);
        string key = useSpace ? "Space" : "F";
        if (useSpace) InputSender.TapSpace(); else InputSender.TapF();
        Logger.LogDebug(() => $"Press: {key}");
        
        if (!useSpace)
        {
            _pressCount++;
            UpdateStatus($"Skipping... [green]{_pressCount}[/] presses");
        }
        
        if (!useSpace && _doubleNext)
        {
            _doubleNext = false;
            InputSender.TapF();
            _pressCount++;
            UpdateStatus($"Skipping... [green]{_pressCount}[/] presses");
            _postBurstPauseUntil = now + 0.4 + Random.Shared.NextDouble() * 0.6;

            Logger.LogDebug(() => "Press: F (double)");
        }

        if (_burstMode)
        {
            _burstRemaining--;
            if (_burstRemaining <= 0)
            {
                _burstMode = false;
                _postBurstPauseUntil = now + 0.4 + Random.Shared.NextDouble() * 0.6;
            }
        }
    }
    
    private void SleepUntil(double targetTime)
    {
        if (ShouldExit) return;
        double now = GetTime();
        double timeout = Math.Max(0.0, targetTime - now);
        int ms = (int)(timeout * 1000);
        if (ms > 0)
        {
            _wakeEvent.Wait(ms);
            _wakeEvent.Reset();
        }
    }

    public void Wake() => _wakeEvent.Set();

    public void ToggleRun(bool on) 
    { 
        _running = on; 
        string countLog = "";
        if (!on) 
        {
            countLog = GetCountLog();
            ResetCounter();
            UpdateStatus("Paused");
        }
        Logger.Log(on ? "[bold green]RUN[/]" : $"[bold yellow]PAUSE[/]{countLog}"); 
        Wake(); 
    }
    
    public void HandleMouse4()
    {
        if (IsGameActive()) { InputSender.TapT(); Logger.Log("Remap: T"); }
    }

    public void HandleMouse5()
    {
        if (!IsGameActive()) return;
        lock (_spamLock)
        {
            _spamCancel.Cancel();
            _spamCancel = new();
            var token = _spamCancel.Token;
            
            Task.Run(async () =>
            {
                Logger.LogWarning("Burst Start");
                ResetCounter();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
                while (!linkedCts.IsCancellationRequested)
                {
                    if (IsGameActive()) 
                    {
                        InputSender.TapF();
                        _pressCount++;
                        UpdateStatus($"Bursting... [red]{_pressCount}[/] presses");
                    }
                    try
                    {
                        await Task.Delay(_cfg.Config.BurstModeDelayMs, linkedCts.Token);
                    }
                    catch (TaskCanceledException) { break; }
                }
                string countLog = GetCountLog();
                Logger.LogWarning($"Burst End{countLog}");
                ResetCounter();
            }, token);
        }
    }

    private string GetCountLog()
    {
        if (_pressCount > 0)
        {
            double duration = GetTime() - _sessionStartTime;
            return $". Total F presses: [cyan]{_pressCount}[/] in [cyan]{duration:F1}s[/]";
        }
        return "";
    }

    private void ResetCounter()
    {
        _pressCount = 0;
        _sessionStartTime = GetTime();
        UpdateStatus("Ready");
    }

    private void UpdateStatus(string status)
    {
        if (_statusContext != null)
        {
            _statusContext.Status(status);
        }
    }
}
