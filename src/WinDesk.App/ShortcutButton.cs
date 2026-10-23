using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinDesk.Core;

namespace WinDesk.App;

/// <summary>A normal launch button until the pointer passes the drag threshold.</summary>
internal sealed class ShortcutButton : Button
{
    private readonly PanelModel _panel;
    private readonly ShortcutModel _shortcut;
    private readonly DesktopPanel _owner;
    private DesktopNative.Point _start;
    private bool _dragging, _suppressClick;
    private bool _clickPending, _hasLaunched;
    private long _lastLaunchCompleted;

    internal ShortcutButton(PanelModel panel, ShortcutModel shortcut, DesktopPanel owner)
    { _panel = panel; _shortcut = shortcut; _owner = owner; }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _suppressClick = false;
        _start = ScreenPoint(e);
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
        var point = ScreenPoint(e);
        double scale = VisualTreeHelperScale();
        if (!_dragging && (Math.Abs(point.X - _start.X) >= SystemParameters.MinimumHorizontalDragDistance * scale ||
            Math.Abs(point.Y - _start.Y) >= SystemParameters.MinimumVerticalDragDistance * scale))
        {
            _dragging = App.CurrentApp.BeginShortcutDrag(_owner, _panel, _shortcut);
            _suppressClick = true;
        }
        if (_dragging) App.CurrentApp.UpdateShortcutDrag(point);
    }

    private double VisualTreeHelperScale() => System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
    private DesktopNative.Point ScreenPoint(MouseEventArgs e)
    { var point = PointToScreen(e.GetPosition(this)); return new DesktopNative.Point((int)Math.Round(point.X), (int)Math.Round(point.Y)); }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            App.CurrentApp.EndShortcutDrag(ScreenPoint(e));
        }
        // ButtonBase releases capture and resets its pressed state. OnClick below
        // suppresses launching even when a reordered item lands on itself.
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnClick()
    {
        if (_suppressClick || _clickPending || (_hasLaunched &&
            Environment.TickCount64 - _lastLaunchCompleted < System.Windows.Forms.SystemInformation.DoubleClickTime)) return;

        // Shell execution can pump messages while an application starts. Finish
        // the input event first, and reject repeat clicks even during that pump.
        _clickPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try { base.OnClick(); }
            finally
            {
                _lastLaunchCompleted = Environment.TickCount64;
                _hasLaunched = true;
                _clickPending = false;
            }
        }));
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragging && ReferenceEquals(e.OriginalSource, this))
        { _dragging = false; App.CurrentApp.EndShortcutDrag(default, false); }
        base.OnLostMouseCapture(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_dragging && e.Key == Key.Escape)
        {
            _dragging = false; App.CurrentApp.EndShortcutDrag(default, false);
            ReleaseMouseCapture(); e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}
