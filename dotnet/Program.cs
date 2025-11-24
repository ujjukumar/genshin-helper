using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoSkipper;

// --- 1. Low Level Native Interop (Win32 API) ---
internal static unsafe partial class Native
{
    [LibraryImport("user32.dll")] public static partial int GetSystemMetrics(int nIndex);
    [LibraryImport("user32.dll")] public static partial IntPtr GetForegroundWindow();
    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GetWindowText(IntPtr hWnd, Span<char> lpString, int nMaxCount);
    [LibraryImport("user32.dll")] public static partial IntPtr GetDC(IntPtr hWnd);
    [LibraryImport("user32.dll")] public static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [LibraryImport("gdi32.dll")] public static partial uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
    [LibraryImport("user32.dll")] public static partial void PostQuitMessage(int nExitCode);
    
    // Hooks
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(IntPtr hhk);
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? lpModuleName);

    // Message Loop
    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")] public static partial int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in MSG lpMsg);
    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial IntPtr DispatchMessage(in MSG lpMsg);

    // Input
    [LibraryImport("user32.dll")] public static partial uint SendInput(uint nInputs, [In] Input[] pInputs, int cbSize);

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP = 0x020C;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion { [FieldOffset(0)] public MouseInput mi; [FieldOffset(0)] public KeyboardInput ki; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
}

// --- 2. Input Helper ---
internal static class InputSender
{
    // Pool input array to avoid allocations on every key press
    private static readonly Native.Input[] _inputPool = new Native.Input[2];
    private static readonly System.Threading.Lock _inputLock = new();
    
    public static void PressKey(ushort vkCode)
    {
        using (_inputLock.EnterScope())
        {
            _inputPool[0].type = Native.INPUT_KEYBOARD;
            _inputPool[0].u.ki.wVk = vkCode;
            _inputPool[0].u.ki.dwFlags = 0; // Down

            _inputPool[1].type = Native.INPUT_KEYBOARD;
            _inputPool[1].u.ki.wVk = vkCode;
            _inputPool[1].u.ki.dwFlags = Native.KEYEVENTF_KEYUP; // Up

            Native.SendInput(2, _inputPool, Marshal.SizeOf<Native.Input>());
        }
    }
    
    public static void TapT() => PressKey(0x54); // T
    public static void TapF() => PressKey(0x46); // F
    public static void TapSpace() => PressKey(0x20); // Space
}

// --- 3. Configuration ---
internal class ScreenConfig
{
    public int WIDTH { get; private set; }
    public int HEIGHT { get; private set; }
    public const int BASE_W = 1920;
    public const int BASE_H = 1080;
    public (int x, int y) PLAYING_ICON;
    public (int x, int ly, int hy) DIALOGUE_ICON;
    public (int x, int y) LOADING_PIXEL;
    public string WINDOW_TITLE = "Genshin Impact";

    public static ScreenConfig Load()
    {
        int w = Native.GetSystemMetrics(0); // SM_CXSCREEN
        int h = Native.GetSystemMetrics(1); // SM_CYSCREEN
        string? envW = null, envH = null;

        if (File.Exists(".env"))
        {
            foreach(var line in File.ReadAllLines(".env"))
            {
                if (line.StartsWith("WIDTH=")) envW = line[6..].Trim();
                if (line.StartsWith("HEIGHT=")) envH = line[7..].Trim();
            }
        }

        if (int.TryParse(envW, out int ew) && int.TryParse(envH, out int eh))
        {
            w = ew; h = eh;
        }
        else
        {
            Console.WriteLine($"Detected Resolution: {w}x{h}");
            Console.Write("Is this correct? (y/n): ");
            var k = Console.ReadLine();
            if (k?.StartsWith("n", StringComparison.OrdinalIgnoreCase) == true)
            {
                w = GetInteger("Enter Width: ", 1920);
                h = GetInteger("Enter Height: ", 1080);
                File.WriteAllText(".env", $"WIDTH={w}\nHEIGHT={h}\n");
            }
        }

        return new ScreenConfig(w, h);
    }

    private static int GetInteger(string prompt, int defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }
            if (int.TryParse(input, out int value))
            {
                return value;
            }
            Console.WriteLine("Invalid input, please enter a number.");
        }
    }

    public ScreenConfig(int w, int h)
    {
        WIDTH = w; HEIGHT = h;
        PLAYING_ICON = CalcPlayingIcon();
        DIALOGUE_ICON = CalcDialogueIcon();
        LOADING_PIXEL = (Wa(1200), Ha(700));
    }

    private int Wa(int x) => (int)(x / (float)BASE_W * WIDTH);
    private int Ha(int y) => (int)(y / (float)BASE_H * HEIGHT);

    private int ScalePos(int hd, int doubleHd, float extra = 0.0f)
    {
        if (WIDTH <= 3840) extra = 0.0f;
        int diff = doubleHd - hd;
        return (int)(hd + (WIDTH - 1920) * ((diff / 1920.0f) + extra));
    }

    private (int, int) CalcPlayingIcon()
    {
        bool ws = WIDTH > 1920 && Math.Abs((double)HEIGHT/WIDTH - 0.5625) > 0.001;
        int x = ws ? Math.Min(ScalePos(84, 230), 230) : Wa(84);
        return (x, Ha(46));
    }

    private (int, int, int) CalcDialogueIcon()
    {
        bool ws = WIDTH > 1920 && Math.Abs((double)HEIGHT/WIDTH - 0.5625) > 0.001;
        int x = ws ? ScalePos(1301, 2770, 0.02f) : Wa(1301);
        int ly = ws ? Ha(810) : Ha(808);
        int hy = ws ? Ha(792) : Ha(790);
        return (x, ly, hy);
    }
}

// --- 4. Logic & State Machine ---
internal class AutoSkipper(ScreenConfig cfg)
{
    private readonly ScreenConfig _cfg = cfg;
    private readonly Random _rnd = new();
    private bool _running = false;
    public bool ShouldExit = false;
    private readonly IntPtr _hdc = Native.GetDC(IntPtr.Zero);
    private CancellationTokenSource _spamCancel = new();
    private readonly System.Threading.Lock _spamLock = new();
    private readonly ManualResetEventSlim _wakeEvent = new(false);
    

    // Logging
    private static readonly System.Threading.Lock _logLock = new();
    private static bool _fileLogging = false;
    private static bool _verbose = false;
    private static StreamWriter? _logWriter;

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

    public static void Log(string msg)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} | {msg}";
        Console.WriteLine(line);
        
        if (_fileLogging)
        {
            lock (_logLock)
            {
                try
                {
                    _logWriter ??= new StreamWriter("autoskip_dialogue.log", true, Encoding.UTF8) { AutoFlush = true };
                    _logWriter.WriteLine(line);
                }
                catch { }
            }
        }
    }

    public static void LogDebug(Func<string> msgFactory)
    {
        if (_verbose) Log($"[DEBUG] {msgFactory()}");
    }

    public static void SetVerbose(bool verbose)
    {
        _verbose = verbose;
        if (verbose) Log("Verbose mode enabled");
    }

    public static void ToggleFileLogging()
    {
        lock (_logLock)
        {
            _fileLogging = !_fileLogging;
            if (!_fileLogging)
            {
                Log("File logging disabled");
                _logWriter?.Dispose();
                _logWriter = null;
            }
            else
            {
                Log("File logging enabled");
            }
        }
    }

    ~AutoSkipper() => _ = Native.ReleaseDC(IntPtr.Zero, _hdc);

    public bool IsGameActive()
    {
        IntPtr hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        Span<char> buffer = stackalloc char[256];
        int length = Native.GetWindowText(hwnd, buffer, buffer.Length);
        string windowTitle = new(buffer[..length]);
        bool isActive = windowTitle.Equals(_cfg.WINDOW_TITLE, StringComparison.OrdinalIgnoreCase);
        LogDebug(() => $"Window: '{windowTitle}' -> {isActive}");
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
            return 0.05 + _rnd.NextDouble() * 0.04;
        }
        if (_rnd.NextDouble() < 1.0/50.0)
        {
            _burstPool = _rnd.Next(2, 6);
            return 0.05 + _rnd.NextDouble() * 0.04;
        }
        if (_rnd.NextDouble() < 1.0/8.0)
        {
            return 0.09 + _rnd.NextDouble() * 0.16;
        }
        return 0.11 + _rnd.NextDouble() * 0.10;
    }

    private string? MaybeBreak()
    {
        double r = _rnd.NextDouble();
        if (r < 1.0/100.0) return "long";
        if (r < 1.0/100.0 + 1.0/25.0) return "short";
        return null;
    }

    private double BreakDuration(string kind) => kind == "long" 
        ? 4.0 + _rnd.NextDouble() * 6.0 
        : 2.0 + _rnd.NextDouble() * 4.0;

    public void Run()
    {
        Log("Instructions: F7=Log, F8=Start, F9=Pause, F12=Exit. Mouse4=T, Mouse5=Burst.");
        double lastPressTime = GetTime();
        double nextInterval = NextKeyInterval();
        double nextStateCheck = 0.0;
        _lastBreakCheck = GetTime();

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
                Log(isActive ? "Window State: ACTIVE" : "Window State: INACTIVE");
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
                string? br = MaybeBreak();
                if (br != null)
                {
                    double dur = BreakDuration(br);
                    Log($"Break: {br} {dur:F1}s");
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

                LogDebug(() => $"Playing pixel: RGB({(pPlaying & 0xFF)},{((pPlaying >> 8) & 0xFF)},{((pPlaying >> 16) & 0xFF)}) @ ({_cfg.PLAYING_ICON.x},{_cfg.PLAYING_ICON.y}) -> {isPlaying}");
                
                bool isChoice = false;
                if (!isPlaying)
                {
                    uint pLoad = Native.GetPixel(_hdc, _cfg.LOADING_PIXEL.x, _cfg.LOADING_PIXEL.y);
                    if (!ColorsMatch(pLoad, 255, 255, 255))
                    {
                        uint pLow = Native.GetPixel(_hdc, _cfg.DIALOGUE_ICON.x, _cfg.DIALOGUE_ICON.ly);
                        uint pHigh = Native.GetPixel(_hdc, _cfg.DIALOGUE_ICON.x, _cfg.DIALOGUE_ICON.hy);
                        isChoice = ColorsMatch(pLow, 255, 255, 255) || ColorsMatch(pHigh, 255, 255, 255);

                        LogDebug(() => $"Choice pixels: Low=RGB({(pLow & 0xFF)},{((pLow >> 8) & 0xFF)},{((pLow >> 16) & 0xFF)}), High=RGB({(pHigh & 0xFF)},{((pHigh >> 8) & 0xFF)},{((pHigh >> 16) & 0xFF)}) -> {isChoice}");
                    }
                }

                bool isDialogue = isPlaying || isChoice;
                if (isDialogue != _inDialogue)
                {
                    _inDialogue = isDialogue;
                    Log(isDialogue ? "Dialogue State: DETECTED" : "Dialogue State: ENDED");
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
                double r1 = _rnd.NextDouble();
                double r2 = _rnd.NextDouble();
                double r3 = _rnd.NextDouble();

                if (!_skipNext && r1 < 1.0/40.0) _skipNext = true;
                if (!_doubleNext && r2 < 1.0/35.0) _doubleNext = true;
                if (!_burstMode && r3 < 1.0/60.0)
                {
                    _burstMode = true;
                    _burstRemaining = _rnd.Next(3, 6);
                    Log($"Burst mode: {_burstRemaining}");
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
        Log("Closing Logic Loop");
    }

    private void PerformPress(double now)
    {
        bool useSpace = _rnd.NextDouble() < (_burstMode ? 0.1 : 0.1);
        string key = useSpace ? "Space" : "F";
        if (useSpace) InputSender.TapSpace(); else InputSender.TapF();
        LogDebug(() => $"Press: {key}");
        
        if (!useSpace && _doubleNext)
        {
            _doubleNext = false;
            InputSender.TapF();
            _postBurstPauseUntil = now + 0.4 + _rnd.NextDouble() * 0.6;

            LogDebug(() => "Press: F (double)");
        }

        if (_burstMode)
        {
            _burstRemaining--;
            if (_burstRemaining <= 0)
            {
                _burstMode = false;
                _postBurstPauseUntil = now + 0.4 + _rnd.NextDouble() * 0.6;
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
        Log(on ? "RUN" : "PAUSE"); 
        Wake(); 
    }
    
    public void HandleMouse4()
    {
        if (IsGameActive()) { InputSender.TapT(); Log("Remap: T"); }
    }

    public void HandleMouse5()
    {
        if (!IsGameActive()) return;
        lock (_spamLock)
        {
            _spamCancel.Cancel();
            _spamCancel = new();
            var token = _spamCancel.Token;
            
            Task.Run(() => 
            {
                Log("Burst Start");
                long end = Stopwatch.GetTimestamp() + (long)(4.0 * Stopwatch.Frequency);
                while (Stopwatch.GetTimestamp() < end && !token.IsCancellationRequested)
                {
                    if(IsGameActive()) InputSender.TapF();
                    Thread.Sleep(100 + _rnd.Next(0, 80));
                }
                Log("Burst End");
            });
        }
    }
}

// --- 5. Global Hooks ---
internal class GlobalHooks : IDisposable
{
    private readonly Native.HookProc _kbdProc;
    private readonly Native.HookProc _mouseProc;
    private readonly IntPtr _hKbdHook = IntPtr.Zero;
    private readonly IntPtr _hMouseHook = IntPtr.Zero;
    private readonly AutoSkipper _skipper;

    public GlobalHooks(AutoSkipper skipper)
    {
        _skipper = skipper;
        _kbdProc = HookCallbackKbd;
        _mouseProc = HookCallbackMouse;
        
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr modHandle = Native.GetModuleHandle(curModule?.ModuleName);

        _hKbdHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _kbdProc, modHandle, 0);
        _hMouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, modHandle, 0);
    }

    private IntPtr HookCallbackKbd(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)Native.WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            // F7=118, F8=119, F9=120, F12=123
            if (vkCode == 118) AutoSkipper.ToggleFileLogging();
            else if (vkCode == 119) _skipper.ToggleRun(true);
            else if (vkCode == 120) _skipper.ToggleRun(false);
            else if (vkCode == 123) { _skipper.ShouldExit = true; _skipper.Wake(); Native.PostQuitMessage(0); }
        }
        return Native.CallNextHookEx(_hKbdHook, nCode, wParam, lParam);
    }

    private IntPtr HookCallbackMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)Native.WM_XBUTTONDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
            int btn = (int)(hookStruct.mouseData >> 16);
            if (btn == 1) _skipper.HandleMouse4(); // XBUTTON1
            if (btn == 2) _skipper.HandleMouse5(); // XBUTTON2
        }
        return Native.CallNextHookEx(_hMouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hKbdHook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hKbdHook);
        if (_hMouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hMouseHook);
    }
}

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
        Console.Title = "Genshin AutoSkip (.NET 10 Native)";
        var config = ScreenConfig.Load();
        AutoSkipper.SetVerbose(verbose);
        var skipper = new AutoSkipper(config);
        
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
    }

    static void RunBenchmark()
    {
        IntPtr hdc = Native.GetDC(IntPtr.Zero);
        int count = 1000;
        Console.WriteLine($"Benchmarking {count} GetPixel calls in .NET 10 (P/Invoke)...");

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