using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDesk.App;

internal static class DesktopNative
{
    internal const int ChildStyle = 0x40000000;
    internal const int ClipSiblingsStyle = 0x04000000;
    internal const int NoActivateStyle = 0x08000000;
    internal const uint NoActivate = 0x10;
    internal const uint NoZOrder = 0x04;

    internal static IntPtr FindDesktopHost()
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (!IsDesktopHost(window)) return true;
            result = window; return false;
        }, IntPtr.Zero);
        return result;
    }

    internal static bool IsDesktopHost(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindow(window) || !IsWindowVisible(window)) return false;
        string name = ClassName(window);
        if (name is not ("Progman" or "WorkerW")) return false;
        IntPtr shell = GetShellWindow();
        if (shell == IntPtr.Zero) return false;
        GetWindowThreadProcessId(shell, out uint shellProcess);
        GetWindowThreadProcessId(window, out uint hostProcess);
        return shellProcess != 0 && shellProcess == hostProcess &&
            FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero;
    }

    internal static string ClassName(IntPtr window)
    {
        var name = new StringBuilder(128);
        GetClassName(window, name, name.Capacity); return name.ToString();
    }

    internal static string DescribeDesktop()
    {
        var output = new StringBuilder();
        var shell = GetShellWindow(); GetWindowThreadProcessId(shell, out uint shellProcess);
        output.AppendLine($"Shell: {shell:X}, process: {shellProcess}, host: {FindDesktopHost():X}");
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint process);
            if (process != shellProcess) return true;
            if (ClassName(window) is not ("Progman" or "WorkerW")) return true;
            GetWindowRect(window, out var rootRect);
            output.AppendLine($"Root {window:X} {ClassName(window)} visible={IsWindowVisible(window)} dpi={GetDpiForWindow(window)} rect={rootRect.Left},{rootRect.Top},{rootRect.Right},{rootRect.Bottom}");
            EnumChildWindows(window, (child, _) =>
            {
                GetWindowRect(child, out var rect);
                DwmGetWindowAttribute(child, 14, out int cloaked, sizeof(int));
                output.AppendLine($"  {child:X} {ClassName(child)} parent={GetParent(child):X} visible={IsWindowVisible(child)} cloaked={cloaked} style={GetWindowLongPtr(child, -16).ToInt64():X} ex={GetWindowLongPtr(child, -20).ToInt64():X} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
                return true;
            }, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
        return output.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }
    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr window, EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetLayeredWindowAttributes(IntPtr window, out uint color, out byte alpha, out uint flags);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("user32.dll")] internal static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr window, ref Point point);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool SetWindowText(IntPtr window, string text);
}
