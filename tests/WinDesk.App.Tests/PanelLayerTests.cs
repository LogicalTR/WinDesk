using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    internal static async Task ClickPreview(App app, Action<bool, string> check)
    {
        app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
        var model = new PanelModel { Name = "Geçici önizleme", X = 140, Y = 140, IsPinned = true };
        app.Workspace.Panels.Add(model);
        app.Workspace.Panels.Add(new PanelModel { Name = "Diğer panel", X = 600, Y = 140 });
        app.SetCloseDelay(200); app.RebuildPanels();
        var panel = app.DesktopPanels.First();
        var cover = new Window { Title = "WinDesk click layer test", Width = 200, Height = 350,
            ShowActivated = false, ShowInTaskbar = false, Background = Brushes.DarkSlateGray };
        var menu = new ContextMenu(); menu.Items.Add(new MenuItem { Header = "Test menüsü" }); panel.TrackMenu(menu);
        var originalHitTest = app.WindowAtPoint;
        try
        {
            cover.Show();
            await Task.Delay(200);
            var coverHandle = new WindowInteropHelper(cover).Handle;
            DesktopNative.GetWindowRect(panel.Handle, out var bounds);
            DesktopNative.SetWindowPos(coverHandle, IntPtr.Zero, bounds.Left - 10, bounds.Top - 10, 200, 350, DesktopNative.NoActivate);
            await Task.Delay(200);
            var point = new DesktopNative.Point(bounds.Left + 80, bounds.Top + 22);
            var blockingRoot = DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2);
            check(blockingRoot != IntPtr.Zero && blockingRoot != panel.DesktopParent,
                "An application covers the panel before the click");
            IntPtr handle = panel.Handle;
            var presentation = PresentationSource.FromVisual(panel);
            var foreground = DesktopNative.GetForegroundWindow();
            // Drive the same routed preview event as a click on a visible child.
            // Only pointer hit testing is substituted; HWNDs and timers are real.
            bool pointerOver = true;
            app.WindowAtPoint = _ => pointerOver ? panel.Handle : coverHandle;
            void Click()
            {
                var input = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Mouse.PreviewMouseDownEvent };
                panel.Sections.First().RaiseEvent(input);
                check(!input.Handled, "The first click is still delivered to the original control");
            }
            Click();
            check(panel.IsTopmost && !app.PanelsOnTop && !app.DesktopPanels.Last().IsTopmost,
                "One click temporarily raises only the clicked panel");
            check(panel.Handle == handle && ReferenceEquals(presentation, PresentationSource.FromVisual(panel)),
                "Raising retains the native windows and WPF input source");
            check(DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2) == handle &&
                DesktopNative.GetForegroundWindow() == foreground,
                "The clicked panel covers the application without stealing keyboard focus");
            await Task.Delay(2300); app.RefreshDesktopConnection();
            check(panel.IsTopmost, "Pointer hover and the desktop watchdog preserve the temporary foreground layer");
            pointerOver = false; await Task.Delay(1300);
            check(panel.IsTopmost, "The panel stays forward before the full two seconds, independent of the close delay");
            pointerOver = true; await Task.Delay(1100);
            check(panel.IsTopmost, "Returning the pointer cancels the pending layer return");
            pointerOver = false; await Task.Delay(2300);
            handle = panel.Handle;
            check(!panel.IsTopmost && DesktopNative.GetParent(handle) == panel.DesktopParent &&
                DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2) == blockingRoot,
                "Two seconds after leaving, the panel returns behind the application");
            check(!ReferenceEquals(presentation, PresentationSource.FromVisual(panel)) &&
                ((HwndSource)presentation!).IsDisposed && DesktopNative.IsWindowVisible(handle) &&
                PresentationSource.FromVisual(panel) is HwndSource { IsDisposed: false } replacement && replacement.RootVisual == panel,
                "Returning creates a fresh visible desktop surface without waiting for another hover");
            check(DesktopNative.GetWindowRect(handle, out var restored) && SameBounds(bounds, restored) &&
                panel.IsSectionExpanded(model.Id) && DesktopNative.GetForegroundWindow() == foreground,
                "Returning retains pinned contents, exact bounds and foreground focus");

            Click(); menu.PlacementTarget = panel; menu.IsOpen = true;
            await Task.Delay(2300);
            check(panel.IsTopmost, "An open menu holds the temporary foreground layer");
            menu.IsOpen = false; await Task.Delay(1100);
            check(panel.IsTopmost, "Closing the menu starts a fresh two-second countdown");
            await Task.Delay(1200);
            check(!panel.IsTopmost, "The panel returns after the menu closes and the delay expires");

            Click(); panel.BeginDrag(new DesktopNative.Point(bounds.Left + 20, bounds.Top + 20));
            await Task.Delay(2300);
            check(panel.IsTopmost, "Dragging outside the panel holds the temporary layer");
            panel.EndDrag(default, false); await Task.Delay(2300);
            check(!panel.IsTopmost, "Cancelling a drag resumes the return countdown");

            Click(); panel.Hide(); panel.Show();
            check(!panel.IsTopmost && DesktopNative.IsWindowVisible(panel.Handle), "Hide/show clears temporary foreground state");
            Click(); app.TogglePanelLayer(); await Task.Delay(2300);
            check(app.PanelsOnTop && panel.IsTopmost, "Explicit Ctrl+Shift mode overrides the automatic return");
            app.TogglePanelLayer();
            check(!panel.IsTopmost, "The next explicit toggle returns the panel to the desktop");
            Click(); panel.DisconnectFromDesktop(); app.RefreshDesktopConnection();
            check(!panel.IsTopmost && panel.Handle != IntPtr.Zero, "Reconnection discards stale temporary foreground state");
        }
        finally
        {
            app.WindowAtPoint = originalHitTest;
            panel.UntrackMenu(menu);
            if (app.PanelsOnTop) app.TogglePanelLayer();
            cover.Close();
        }
    }
}
