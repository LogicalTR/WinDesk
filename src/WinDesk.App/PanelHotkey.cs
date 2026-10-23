using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinDesk.App;

/// <summary>Observes a bare Ctrl+Shift chord without consuming input from other applications.</summary>
internal sealed class PanelHotkey : IDisposable
{
    private readonly Thread _thread;
    private uint _threadId;
    private bool _disposed;

    internal PanelHotkey(Action toggle)
    {
        using var ready = new ManualResetEventSlim();
        int error = 0;
        _thread = new Thread(() =>
        {
            var chord = new ControlShiftChord();
            // Seed keys already held when WinDesk starts. Do not treat them as a new chord.
            for (int key = 8; key < 256; key++)
                // Hook events identify left/right modifiers; do not also seed their generic aliases.
                if (key is not (0x10 or 0x11 or 0x12) && (GetAsyncKeyState(key) & 0x8000) != 0) chord.SeedKey(key);
            HookProcedure callback = (code, message, data) =>
            {
                if (code == 0 && message.ToInt64() is 0x100 or 0x101 or 0x104 or 0x105)
                {
                    int key = Marshal.ReadInt32(data);
                    bool down = message.ToInt64() is 0x100 or 0x104;
                    // Queue the UI work; this thread must promptly return to the hook chain.
                    if (chord.Process(key, down)) toggle();
                }
                return CallNextHookEx(IntPtr.Zero, code, message, data);
            };
            _threadId = GetCurrentThreadId();
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // Create the queue before Dispose can post WM_QUIT.
            IntPtr hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0); // WH_KEYBOARD_LL
            if (hook == IntPtr.Zero) error = Marshal.GetLastWin32Error();
            ready.Set();
            if (hook == IntPtr.Zero) return;
            try
            {
                while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
            }
            finally { UnhookWindowsHookEx(hook); GC.KeepAlive(callback); }
        }) { IsBackground = true, Name = "WinDesk panel hotkey" };
        _thread.Start();
        ready.Wait();
        if (error != 0) throw new Win32Exception(error);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PostThreadMessage(_threadId, 0x12, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        _thread.Join(TimeSpan.FromSeconds(1));
    }

    private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam, LParam;
        public uint Time;
        public DesktopNative.Point Point;
        public uint Private;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int kind, HookProcedure callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetMessage(out NativeMessage message, IntPtr window, uint minimum, uint maximum);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref NativeMessage message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}

internal sealed class ControlShiftChord
{
    private readonly bool[] _down = new bool[256];
    private bool _armed, _blocked, _used;

    internal void SeedKey(int key) { _down[key] = true; _blocked = true; }

    internal bool Process(int key, bool down)
    {
        if (key <= 0 || key >= _down.Length || _down[key] == down) return false;
        _down[key] = down;
        bool control = _down[0x11] || _down[0xA2] || _down[0xA3];
        bool shift = _down[0x10] || _down[0xA0] || _down[0xA1];
        for (int other = 1; other < _down.Length; other++)
            if (_down[other] && other is not (0x10 or 0x11 or 0xA0 or 0xA1 or 0xA2 or 0xA3)) _blocked = true;
        bool toggle = _armed && !down && !(control && shift) && !_blocked && !_used;
        if (toggle) _used = true;
        if (control && shift && !_blocked && !_used) _armed = true;
        // Wait for release so Ctrl+Shift+T, Ctrl+Shift+arrows, etc. keep their normal behavior.
        if (!control && !shift) _armed = _blocked = _used = false;
        return toggle;
    }
}
