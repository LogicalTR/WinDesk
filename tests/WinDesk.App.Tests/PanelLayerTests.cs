using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WinDesk.App;
using WinDesk.Core;

internal static class PanelLayerTests
{
    internal static void Chords(Action<bool, string> check)
    {
        foreach (int control in new[] { 0xA2, 0xA3 })
        foreach (int shift in new[] { 0xA0, 0xA1 })
        foreach (bool reverse in new[] { false, true })
        {
            var chord = new ControlShiftChord();
            int first = reverse ? shift : control, second = reverse ? control : shift;
            check(!chord.Process(first, true) && !chord.Process(second, true) && !chord.Process(second, true),
                $"Ctrl/Shift {control:X}/{shift:X}, reverse={reverse}: holding and repeating keys waits for release");
            check(chord.Process(second, false) && !chord.Process(first, false), "A bare chord toggles exactly once on release");
            chord.Process(second, true); chord.Process(first, true);
            check(chord.Process(first, false) && !chord.Process(second, false), "The next complete chord toggles again");
        }
        foreach (int other in new[] { 0x54, 0x25, 0x1B, 0xA4, 0xA5, 0x5B, 0x5C })
        foreach (bool otherFirst in new[] { false, true })
        {
            var chord = new ControlShiftChord();
            if (otherFirst) chord.Process(other, true);
            chord.Process(0xA2, true); chord.Process(0xA0, true);
            if (!otherFirst) chord.Process(other, true);
            chord.Process(other, false);
            check(!chord.Process(0xA0, false) && !chord.Process(0xA2, false),
                $"An additional key {other:X}, otherFirst={otherFirst}, cancels the panel shortcut");
            chord.Process(0xA2, true); chord.Process(0xA0, true);
            check(chord.Process(0xA2, false) && !chord.Process(0xA0, false), "A cancelled shortcut does not block the next bare chord");
        }
        var seeded = new ControlShiftChord(); seeded.SeedKey(0xA2); seeded.SeedKey(0xA0);
        check(!seeded.Process(0xA2, false) && !seeded.Process(0xA0, false), "Modifiers held before startup do not activate the shortcut");
        var single = new ControlShiftChord();
        check(!single.Process(0xA2, true) && !single.Process(0xA2, false) && !single.Process(0xA0, true) && !single.Process(0xA0, false),
            "Ctrl or Shift alone never toggles panels");
    }

    internal static async Task Run(App app, Action<bool, string> check)
    {
        Chords(check);
        app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
        var first = new PanelModel { Name = "Öne getir", X = 140, Y = 140, IsPinned = true };
        var second = new PanelModel { Name = "İkinci grup", X = 550, Y = 140, IsPinned = true };
        first.Items.Add(new ShortcutModel { Name = "Test", Path = Environment.ProcessPath! });
        app.Workspace.Panels.Add(first); app.Workspace.Panels.Add(second);
        app.RebuildPanels();
        var panel = app.DesktopPanels.First();
        DesktopNative.GetWindowRect(panel.Handle, out var original);
        var cover = new Window { Title = "WinDesk layer test", Width = 750, Height = 400, ShowActivated = false,
            ShowInTaskbar = false, Background = Brushes.DarkSlateGray };
        try
        {
            cover.Show();
            var coverHandle = new WindowInteropHelper(cover).Handle;
            DesktopNative.SetWindowPos(coverHandle, IntPtr.Zero, original.Left - 10, original.Top - 10, 750, 400, DesktopNative.NoActivate);
            await Task.Delay(120);
            var point = new DesktopNative.Point(original.Left + 80, original.Top + 22);
            var blockingRoot = DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2);
            var foreground = DesktopNative.GetForegroundWindow();
            check(blockingRoot != panel.DesktopParent, "An ordinary application covers the desktop mode");
            app.TogglePanelLayer(); await Task.Delay(150);
            check(app.PanelsOnTop && app.DesktopPanels.All(p => p.IsTopmost && DesktopNative.GetParent(p.Handle) == IntPtr.Zero &&
                (DesktopNative.GetWindowLongPtr(p.Handle, -20).ToInt64() & 0x88) == 0x88),
                "The first toggle creates topmost tool windows for all panels");
            check(DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2) == panel.Handle,
                "The foreground panel is actually above the covering application");
            check(DesktopNative.GetForegroundWindow() == foreground, "Bringing panels forward preserves keyboard focus");
            check(DesktopNative.GetWindowRect(panel.Handle, out var raised) && SameBounds(original, raised) &&
                panel.IsSectionExpanded(first.Id) && first.IsPinned && first.Items.Count == 1,
                "Layer changes retain exact bounds, expanded pinned groups and shortcuts");
            app.RefreshDesktopConnection(); await Task.Delay(1100);
            check(app.PanelsOnTop && panel.IsTopmost && DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2) == panel.Handle,
                "The desktop watchdog keeps the requested foreground mode");
            check(app.FindDropTarget(app.DesktopPanels.Last(), point) == panel, "Panels remain valid drag targets above applications");
            check(app.BeginShortcutDrag(panel, first, first.Items[0]), "Shortcut dragging still starts in foreground mode");
            app.UpdateShortcutDrag(point);
            check(app.FindShortcutTarget(point)?.Model == first, "Shortcut drops recognize the raised panel");
            app.EndShortcutDrag(default, false);
            app.TogglePanelLayer(); await Task.Delay(150);
            check(!app.PanelsOnTop && app.DesktopPanels.All(p => !p.IsTopmost && DesktopNative.GetParent(p.Handle) == p.DesktopParent &&
                (DesktopNative.GetWindowLongPtr(p.Handle, -20).ToInt64() & 8) == 0),
                "The second toggle returns every panel to an Explorer child window");
            check(DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2) == blockingRoot,
                "The same application covers the panel again after sending it back");
            check(DesktopNative.GetForegroundWindow() == foreground && DesktopNative.GetWindowRect(panel.Handle, out var lowered) && SameBounds(original, lowered),
                "Sending panels back preserves foreground focus and screen coordinates");

            app.TogglePanelLayer();
            app.Workspace.Combine(new[] { first.Id, second.Id }, "Birleşik test"); app.RebuildPanels();
            panel = app.DesktopPanels.Single();
            check(panel.IsTopmost && panel.IsSectionExpanded(first.Id) && panel.IsSectionExpanded(second.Id),
                "Rebuilding a combined area preserves foreground mode and both pins");
            panel.DisconnectFromDesktop(); app.RefreshDesktopConnection();
            check(panel.IsTopmost && panel.Handle != IntPtr.Zero, "Recovery recreates the requested foreground mode");
            app.TogglePanels(); app.RefreshDesktopConnection();
            check(!DesktopNative.IsWindowVisible(panel.Handle), "Recovery does not reveal hidden foreground panels");
            app.TogglePanelLayer();
            check(app.PanelsHidden && !DesktopNative.IsWindowVisible(panel.Handle), "Sending a hidden panel back keeps it hidden");
            app.TogglePanelLayer();
            check(!app.PanelsHidden && panel.IsTopmost && DesktopNative.IsWindowVisible(panel.Handle), "Bringing hidden panels forward reveals them");
            using (var unavailable = new DesktopWindows(() => app.DesktopPanels, () => { }, () => IntPtr.Zero) { PanelsOnTop = true })
            {
                unavailable.Synchronize();
                check(panel.Handle == IntPtr.Zero && !unavailable.IsConnected, "Missing Explorer also releases foreground panel resources");
            }
            app.RefreshDesktopConnection();
            check(panel.IsTopmost && panel.Handle != IntPtr.Zero, "Foreground panels recover after Explorer becomes available");
        }
        finally
        {
            app.EndShortcutDrag(default, false);
            if (app.PanelsOnTop) app.TogglePanelLayer();
            if (app.PanelsHidden) app.TogglePanels();
            cover.Close();
        }
    }

    private static bool SameBounds(DesktopNative.Rect a, DesktopNative.Rect b) =>
        a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
}
