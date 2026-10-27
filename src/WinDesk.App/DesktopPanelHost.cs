using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WinDesk.App;

/// <summary>Hosts a panel on the desktop, or above applications when explicitly requested.</summary>
internal sealed class DesktopPanelHost : IDisposable
{
    private HwndSource? _source;
    private DesktopContainer? _container;
    private int _regionWidth, _regionHeight;
    internal IntPtr Parent { get; }
    internal bool IsTopmost { get; private set; }
    internal IntPtr Handle => _container?.Handle ?? IntPtr.Zero;
    internal bool IsAlive => Handle != IntPtr.Zero && DesktopNative.IsWindow(Handle) && DesktopNative.GetParent(Handle) == (IsTopmost ? IntPtr.Zero : Parent) &&
        _source is { IsDisposed: false } && DesktopNative.IsWindow(_source.Handle) && DesktopNative.GetParent(_source.Handle) == Handle;
    internal double Scale => DesktopNative.GetDpiForWindow(Parent) is var dpi && dpi != 0 ? dpi / 96.0 : 1;

    internal DesktopPanelHost(IntPtr parent, FrameworkElement content, string name, bool topmost = false)
    {
        if (!DesktopNative.IsDesktopHost(parent)) throw new InvalidOperationException("Masaüstü bağlantısı bulunamadı.");
        Parent = parent; IsTopmost = topmost;
        // Cross-process parent/child windows must have compatible DPI contexts.
        // Switch only while constructing the HWND, then restore the UI thread.
        IntPtr previous = DesktopNative.SetThreadDpiAwarenessContext(DesktopNative.GetWindowDpiAwarenessContext(parent));
        if (previous == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            // Windows 11's desktop uses a layered compositor. Give it an ordinary,
            // opaque layered container; WPF paints a regular child inside it.
            // Per-pixel WPF sources can be logically visible yet absent from this
            // cross-process desktop composition tree.
            _container = new DesktopContainer();
            _container.CreateHandle(new System.Windows.Forms.CreateParams
            {
                Caption = name, Parent = topmost ? IntPtr.Zero : parent,
                Style = (topmost ? unchecked((int)0x80000000) : DesktopNative.ChildStyle) | DesktopNative.ClipSiblingsStyle | 0x02000000,
                ExStyle = DesktopNative.NoActivateStyle | 0x00080000 | (topmost ? 0x00000088 : 0), // TOOLWINDOW | TOPMOST
                Width = (int)Math.Ceiling(content.Width * Scale),
                Height = (int)Math.Ceiling(content.Height * Scale)
            });
            if (!DesktopNative.SetLayeredWindowAttributes(Handle, 0, 255, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var parameters = new HwndSourceParameters(name)
            {
                ParentWindow = Handle,
                WindowStyle = DesktopNative.ChildStyle | DesktopNative.ClipSiblingsStyle | 0x10000000,
                ExtendedWindowStyle = DesktopNative.NoActivateStyle,
                Width = (int)Math.Ceiling(content.Width * Scale),
                Height = (int)Math.Ceiling(content.Height * Scale),
                HwndSourceHook = WindowProcedure
            };
            _source = new HwndSource(parameters) { SizeToContent = SizeToContent.Manual };
            _source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            _source.CompositionTarget.BackgroundColor = Color.FromRgb(26, 34, 48);
            _source.RootVisual = content;
            // Only the panel is raised; Explorer is never activated or reordered.
            if (!DesktopNative.SetWindowPos(Handle, topmost ? new IntPtr(-1) : IntPtr.Zero, 0, 0, 0, 0, 0x03 | DesktopNative.NoActivate))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch { Dispose(); throw; }
        finally { DesktopNative.SetThreadDpiAwarenessContext(previous); }
    }

    private IntPtr WindowProcedure(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x21) // WM_MOUSEACTIVATE: clicks and hovers must not raise Explorer or steal focus.
        { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        return IntPtr.Zero;
    }

    internal void SetTopmost(bool topmost)
    {
        if (IsTopmost == topmost || !IsAlive) return;
        if (!DesktopNative.IsDesktopHost(Parent)) throw new InvalidOperationException("Masaüstü bağlantısı bulunamadı.");
        if (!DesktopNative.GetWindowRect(Handle, out var bounds)) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr previous = DesktopNative.SetThreadDpiAwarenessContext(DesktopNative.GetWindowDpiAwarenessContext(Parent));
        if (previous == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            // Keep both HWNDs and the WPF visual alive so the first click,
            // button capture and an in-progress drag survive the layer change.
            long style = DesktopNative.GetWindowLongPtr(Handle, -16).ToInt64();
            long extended = DesktopNative.GetWindowLongPtr(Handle, -20).ToInt64();
            var position = new DesktopNative.Point(bounds.Left, bounds.Top);
            if (topmost)
            {
                Reparent(IntPtr.Zero);
                SetStyle(-16, (style & ~DesktopNative.ChildStyle) | 0x80000000L);
                SetStyle(-20, extended | 0x80); // WS_EX_TOOLWINDOW
            }
            else
            {
                if (!DesktopNative.ScreenToClient(Parent, ref position)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!DesktopNative.SetWindowPos(Handle, new IntPtr(-2), 0, 0, 0, 0, 0x03 | DesktopNative.NoActivate))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                SetStyle(-16, (style & ~0x80000000L) | DesktopNative.ChildStyle);
                SetStyle(-20, extended & ~0x88L); // TOOLWINDOW | TOPMOST
                Reparent(Parent);
            }
            IsTopmost = topmost;
            if (!DesktopNative.SetWindowPos(Handle, topmost ? new IntPtr(-1) : IntPtr.Zero,
                position.X, position.Y, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top,
                DesktopNative.NoActivate | 0x20)) // SWP_FRAMECHANGED
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { DesktopNative.SetThreadDpiAwarenessContext(previous); }
    }

    private void Reparent(IntPtr parent)
    {
        Marshal.SetLastPInvokeError(0);
        if (DesktopNative.SetParent(Handle, parent) == IntPtr.Zero && Marshal.GetLastWin32Error() is int error && error != 0)
            throw new Win32Exception(error);
    }

    private void SetStyle(int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        if (DesktopNative.SetWindowLongPtr(Handle, index, new IntPtr(value)) == IntPtr.Zero && Marshal.GetLastWin32Error() is int error && error != 0)
            throw new Win32Exception(error);
    }

    internal void SetBounds(double screenX, double screenY, double width, double height)
    {
        if (!IsAlive) return;
        var point = new DesktopNative.Point((int)Math.Round(screenX * Scale), (int)Math.Round(screenY * Scale));
        if (!IsTopmost && !DesktopNative.ScreenToClient(Parent, ref point)) return;
        DesktopNative.SetWindowPos(Handle, IntPtr.Zero, point.X, point.Y,
            Math.Max(1, (int)Math.Ceiling(width * Scale)), Math.Max(1, (int)Math.Ceiling(height * Scale)),
            DesktopNative.NoActivate | DesktopNative.NoZOrder);
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(width * Scale));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(height * Scale));
        if (_source is { IsDisposed: false })
            DesktopNative.SetWindowPos(_source.Handle, IntPtr.Zero, 0, 0, pixelWidth, pixelHeight,
                DesktopNative.NoActivate | DesktopNative.NoZOrder);
        if (_regionWidth != pixelWidth || _regionHeight != pixelHeight)
        {
            var region = DesktopNative.CreateRoundRectRgn(0, 0, pixelWidth + 1, pixelHeight + 1, (int)(20 * Scale), (int)(20 * Scale));
            if (region != IntPtr.Zero && DesktopNative.SetWindowRgn(Handle, region, true) == 0)
                DesktopNative.DeleteObject(region); // On success, ownership belongs to Windows.
            _regionWidth = pixelWidth; _regionHeight = pixelHeight;
        }
    }

    internal void SetVisible(bool visible)
    {
        if (IsAlive) DesktopNative.ShowWindow(Handle, visible ? 8 : 0); // SW_SHOWNA / SW_HIDE, never activation
    }

    public void Dispose()
    {
        var source = _source; _source = null;
        if (source is { IsDisposed: false }) { source.RootVisual = null; source.Dispose(); }
        _container?.DestroyHandle(); _container = null;
    }

    private sealed class DesktopContainer : System.Windows.Forms.NativeWindow
    {
        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            if (message.Msg == 0x21) { message.Result = new IntPtr(3); return; }
            base.WndProc(ref message);
        }
    }
}
