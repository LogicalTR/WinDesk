using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinDesk.Core;

namespace WinDesk.App;

public partial class App
{
    private ShortcutDrag? _shortcutDrag;
    internal string DesktopShortcutDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    internal bool IsShortcutDragging => _shortcutDrag is not null;

    private sealed class ShortcutDrag : IDisposable
    {
        internal required DesktopPanel Source;
        internal required PanelModel Group;
        internal required ShortcutModel Item;
        internal DesktopPanelHost? Preview;
        internal DispatcherTimer Timer = new() { Interval = TimeSpan.FromMilliseconds(70) };
        internal TextBlock Hint = new() { FontSize = 11, Foreground = ManagerWindow.Brush("#93E7D5"), Margin = new Thickness(0, 4, 0, 0) };
        public void Dispose() { Timer.Stop(); Preview?.Dispose(); }
    }

    internal bool BeginShortcutDrag(DesktopPanel source, PanelModel group, ShortcutModel item)
    {
        if (!source.IsDropAvailable || !source.Contains(group.Id) || !group.Items.Contains(item)) return false;
        EndShortcutDrag(default, false);
        CancelPanelDrag();
        var drag = new ShortcutDrag { Source = source, Group = group, Item = item };
        _shortcutDrag = drag;
        source.HoldShortcutSource(group.Id);
        drag.Timer.Tick += (_, _) => { if (DesktopNative.GetCursorPos(out var point)) UpdateShortcutDrag(point); };
        drag.Timer.Start();
        foreach (var panel in _windows.Values) panel.SetDragState(true, false, false);
        try
        {
            var text = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
            text.Children.Add(new TextBlock { Text = item.Name, Foreground = Brushes.White, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(drag.Hint);
            drag.Preview = new DesktopPanelHost(source.DesktopParent, new Border
            {
                Width = 238, Height = 62, Child = text, Background = ManagerWindow.Brush("#1A3030"),
                BorderBrush = ManagerWindow.Brush("#73D9C3"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10)
            }, "WinDesk · Kısayolu taşı", source.IsTopmost);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { System.Diagnostics.Trace.WriteLine($"WinDesk shortcut preview: {ex}"); }
        return true;
    }

    private bool IsDesktopPoint(ShortcutDrag drag, DesktopNative.Point point) => drag.Source.IsDropAvailable &&
        IsPanelSurfacePoint(drag.Source, point);

    internal PanelSection? FindShortcutTarget(DesktopNative.Point point)
    {
        // Respect actual sibling stacking when two panels overlap.
        for (IntPtr hit = WindowAtPoint(point); hit != IntPtr.Zero; hit = DesktopNative.GetParent(hit))
        {
            var panel = _windows.Values.FirstOrDefault(p => p.Handle == hit && p.IsDropAvailable);
            if (panel is not null) return panel.Sections.FirstOrDefault(s => s.ContainsScreenPoint(point));
        }
        return _windows.Values.Where(p => p.IsDropAvailable).Reverse().SelectMany(p => p.Sections).FirstOrDefault(s => s.ContainsScreenPoint(point));
    }

    private bool IsOverPanel(DesktopNative.Point point) => _windows.Values.Any(p => p.IsDropAvailable &&
        DesktopNative.GetWindowRect(p.Handle, out var rect) && point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom);

    internal void UpdateShortcutDrag(DesktopNative.Point point)
    {
        if (_shortcutDrag is not { } drag) return;
        double scale = DesktopNative.GetDpiForWindow(drag.Source.Handle) / 96.0;
        drag.Preview?.SetBounds(point.X / scale + 18, point.Y / scale + 18, 238, 62);
        drag.Preview?.SetVisible(true);
        bool desktop = IsDesktopPoint(drag, point);
        var target = desktop ? FindShortcutTarget(point) : null;
        if (target is not null && !target.IsExpanded) target.ExpandForDrop();
        target?.ScrollForDrop(point);
        int? slot = target?.InsertionIndex(point);
        foreach (var section in _windows.Values.SelectMany(p => p.Sections)) section.ShowShortcutDrop(section == target ? slot : null);
        drag.Hint.Text = !desktop ? "Taşımak için masaüstüne dön" : target is null ? "Masaüstüne kısayol olarak bırak" :
            target.Model == drag.Group ? "Sırasını değiştirmek için bırak" : $"{target.Model.Name} grubuna taşı";
    }

    internal void EndShortcutDrag(DesktopNative.Point point, bool commit = true)
    {
        if (_shortcutDrag is not { } drag) return;
        _shortcutDrag = null;
        bool desktop = commit && IsDesktopPoint(drag, point);
        var target = desktop ? FindShortcutTarget(point) : null;
        if (target is null && IsOverPanel(point)) desktop = false;
        int index = target?.InsertionIndex(point) ?? 0;
        drag.Dispose();
        drag.Source.HoldShortcutSource(null);
        foreach (var panel in _windows.Values)
        {
            panel.SetDragState(false, false, false);
            foreach (var section in panel.Sections) section.ShowShortcutDrop(null);
        }
        if (!desktop) return;
        // Rebuild the item buttons after ButtonBase has released mouse capture.
        Dispatcher.BeginInvoke(() =>
        {
            if (!Workspace.Panels.Contains(drag.Group) || !drag.Group.Items.Contains(drag.Item)) return;
            try
            {
                if (target is not null)
                {
                    if (!Workspace.Panels.Contains(target.Model)) return;
                    Workspace.MoveShortcut(drag.Group.Id, drag.Item, target.Model.Id, index);
                    SurfaceFor(drag.Group).Refresh();
                    if (SurfaceFor(target.Model) != SurfaceFor(drag.Group)) SurfaceFor(target.Model).Refresh();
                }
                else
                {
                    DesktopShortcutExporter.Export(drag.Item, DesktopShortcutDirectory);
                    drag.Group.Items.Remove(drag.Item);
                    SurfaceFor(drag.Group).Refresh();
                }
                Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
            { ShowError("Kısayol taşınamadı", ex); }
        });
    }
}
