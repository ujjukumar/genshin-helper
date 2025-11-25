using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoSkipper;

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
            if (vkCode == 118) Logger.ToggleFileLogging();
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
