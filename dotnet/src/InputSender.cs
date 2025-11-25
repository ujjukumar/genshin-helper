using System.Runtime.InteropServices;

namespace AutoSkipper;

// --- 2. Input Helper ---
internal static class InputSender
{
    // Pool input array to avoid allocations on every key press
    private static readonly Native.Input[] _inputPool = new Native.Input[2];
    private static readonly object _inputLock = new();
    
    public static void PressKey(ushort vkCode)
    {
        lock (_inputLock)
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
