using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WinDesk.App;

/// <summary>
/// Owns the connection to Explorer's desktop. Panels are child HWNDs, never
/// top-level windows. If Explorer is absent, no panel HWND is created.
/// </summary>
internal sealed class DesktopWindows : IDisposable
{
    private readonly Func<IEnumerable<DesktopPanel>> _panels;
    private readonly Func<IntPtr> _findDesktop;
    private readonly Action _statusChanged;
    private readonly DispatcherTimer _watchdog = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _disposed;
    public bool IsConnected { get; private set; }
    public string Status { get; private set; } = "Masaüstüne bağlanılıyor…";

    public DesktopWindows(Func<IEnumerable<DesktopPanel>> panels, Action statusChanged, Func<IntPtr>? findDesktop = null)
    {
        _panels = panels; _statusChanged = statusChanged;
        _findDesktop = findDesktop ?? DesktopNative.FindDesktopHost;
        _watchdog.Tick += (_, _) => Synchronize();
        _watchdog.Start();
    }

    internal void Synchronize()
    {
        if (_disposed) return;
        IntPtr desktop = _findDesktop();
        bool connected = desktop != IntPtr.Zero && DesktopNative.IsDesktopHost(desktop);
        foreach (var panel in _panels())
        {
            if (!connected) { panel.DisconnectFromDesktop(); continue; }
            try { panel.ConnectToDesktop(desktop); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // No floating-window fallback: a connection error must never
                // place panel content over other applications.
                panel.DisconnectFromDesktop(); connected = false;
                System.Diagnostics.Trace.WriteLine($"WinDesk desktop connection: {ex}");
            }
        }
        string status = connected ? "Paneller yalnızca masaüstünde görünür." : "Masaüstü bekleniyor; paneller geçici olarak gizli.";
        if (IsConnected == connected && Status == status) return;
        IsConnected = connected; Status = status; _statusChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _watchdog.Stop();
        foreach (var panel in _panels()) panel.DisconnectFromDesktop();
    }
}

internal static class ShellIcons
{
    public static ImageSource? Get(string path)
    {
        var result = SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100); // SHGFI_ICON, large
        if (result == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze(); return image;
        }
        finally { DestroyIcon(info.Icon); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
}
