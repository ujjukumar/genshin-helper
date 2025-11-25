using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
            return 0.05 + Random.Shared.NextDouble() * 0.04;
        }
        if (Random.Shared.NextDouble() < 1.0/50.0)
        {
            _burstPool = Random.Shared.Next(2, 6);
            return 0.05 + Random.Shared.NextDouble() * 0.04;
        }
        if (Random.Shared.NextDouble() < 1.0/8.0)
        {
            return 0.09 + Random.Shared.NextDouble() * 0.16;
        }
        return 0.11 + Random.Shared.NextDouble() * 0.10;
    }

    private BreakType MaybeBreak()
    {
        double r = Random.Shared.NextDouble();
        if (r < 1.0 / 100.0) return BreakType.Long;
        if (r < 1.0 / 100.0 + 1.0 / 25.0) return BreakType.Short;
        return BreakType.None;
    }

    private double BreakDuration(BreakType kind) => kind == BreakType.Long
        ? 4.0 + Random.Shared.NextDouble() * 6.0
        : 2.0 + Random.Shared.NextDouble() * 4.0;

    public void Run()
    {
        Logger.Log("Instructions: F7=Log, F8=Start, F9=Pause, F12=Exit. Mouse4=T, Mouse5=Burst.");
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
                    _wakeEvent.Wait(500);
                    _wakeEvent.Reset();
                    lastPressTime = GetTime();
                    continue;
                }

                bool isActive = IsGameActive();
                if (isActive != _windowActive)
                {
                    _windowActive = isActive;
                    Logger.Log(isActive ? "Window State: ACTIVE" : "Window State: INACTIVE");
                }

                if (!isActive)
                {
                    SleepUntil(now + 0.4);
                    continue;
                }

                // Active break
                if (now < _breakUntil)
                {
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
                        Logger.Log($"Break: {br} {dur:F1}s");
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
                        Logger.Log(isDialogue ? "Dialogue State: DETECTED" : "Dialogue State: ENDED");
                    }

                    nextStateCheck = now + 0.15;
                    if (!isDialogue)
                    {
                        SleepUntil(now + 0.25);
                        continue;
                    }
                }

                // Post-burst pause
                if (now < _postBurstPauseUntil)
                {
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
                        Logger.Log($"Burst mode: {_burstRemaining}");
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
        bool useSpace = Random.Shared.NextDouble() < (_burstMode ? 0.1 : 0.1);
        string key = useSpace ? "Space" : "F";
        if (useSpace) InputSender.TapSpace(); else InputSender.TapF();
        Logger.LogDebug(() => $"Press: {key}");
        
        if (!useSpace && _doubleNext)
        {
            _doubleNext = false;
            InputSender.TapF();
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
        Logger.Log(on ? "RUN" : "PAUSE"); 
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
                Logger.Log("Burst Start");
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
                while (!linkedCts.IsCancellationRequested)
                {
                    if (IsGameActive()) InputSender.TapF();
                    try
                    {
                        await Task.Delay(100 + Random.Shared.Next(0, 80), linkedCts.Token);
                    }
                    catch (TaskCanceledException) { break; }
                }
                Logger.Log("Burst End");
            }, token);
        }
    }
}
