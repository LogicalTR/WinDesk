using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WinDesk.App;

// Live DWM composition preview for diagnosing actual desktop rendering. Unlike
// RenderTargetBitmap, this displays the pixels composed by Windows, not WPF's
// detached visual tree. It never activates, reparents or modifies Explorer.
internal static class DesktopPreview
{
    internal static int Run()
    {
        var app = new Application();
        var window = new Window
        {
            Title = "WinDesk · Canlı masaüstü kontrolü", Width = 1170, Height = 690,
            Background = Brushes.Black, WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        IntPtr thumbnail = IntPtr.Zero;
        window.SourceInitialized += (_, _) =>
        {
            var target = new WindowInteropHelper(window).Handle;
            int result = DwmRegisterThumbnail(target, DesktopNative.FindDesktopHost(), out thumbnail);
            if (result != 0) throw new InvalidOperationException($"DWM preview failed: {result:X8}");
        };
        void Update()
        {
            if (thumbnail == IntPtr.Zero) return;
            var properties = new ThumbnailProperties
            {
                Flags = 1 | 4 | 8 | 16, Opacity = 255, Visible = true, ClientOnly = false,
                Destination = new NativeRect { Right = (int)window.ActualWidth - 16, Bottom = (int)window.ActualHeight - 40 }
            };
            DwmUpdateThumbnailProperties(thumbnail, ref properties);
        }
        window.Loaded += (_, _) => Update(); window.SizeChanged += (_, _) => Update();
        window.Closed += (_, _) => { if (thumbnail != IntPtr.Zero) DwmUnregisterThumbnail(thumbnail); };
        return app.Run(window);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct ThumbnailProperties
    {
        public uint Flags; public NativeRect Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
}
