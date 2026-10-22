using System;
using System.IO;
using System.Linq;
using WinDesk.App;
using WinDesk.Core;

internal static class PositionTests
{
    // Synchronous, hidden desktop children only: never start the application,
    // acquire its mutex, pump its queued startup, or use the user's workspace.
    internal static void Run(Action<bool, string> check, string output)
    {
        if (System.Windows.Application.Current is null)
        {
            var app = new App();
            app.InitializeComponent();
        }
        IntPtr desktop = DesktopNative.FindDesktopHost();
        check(desktop != IntPtr.Zero, "Position checks have a live Explorer desktop");
        double scale = DesktopNative.GetDpiForWindow(desktop) / 96.0;
        var store = new WorkspaceStore(Path.Combine(output, "workspace.json"));
        foreach (bool combined in new[] { false, true })
        {
            var workspace = Workspace.CreateDefault();
            var model = workspace.Panels[0];
            var stack = combined ? workspace.Combine(workspace.Panels.Select(p => p.Id), "Position test") : null;
            string kind = combined ? "Combined area" : "Standalone panel";
            void SetPosition(double x, double y)
            {
                if (stack is not null) { stack.X = x; stack.Y = y; }
                else { model.X = x; model.Y = y; }
            }
            (double X, double Y) Saved() => stack is not null ? (stack.X, stack.Y) : (model.X, model.Y);
            DesktopPanel Create() => new(stack is not null ? workspace.Panels : new[] { model }, stack);

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var work = screen.WorkingArea;
                double x = work.Right / scale - 336, y = work.Top / scale;
                SetPosition(x, y);
                store.Save(workspace);
                workspace = store.Load(); model = workspace.Panels[0]; stack = workspace.Stacks.SingleOrDefault();
                using var panel = Create();
                panel.Hide(); panel.ConnectToDesktop(desktop);
                check(Saved() == (x, y) && panel.ScreenX == x && panel.ScreenY == y,
                    $"{kind} reloads at the saved right edge of {screen.DeviceName}");
                check(DesktopNative.GetWindowRect(panel.Handle, out var rect) &&
                    Math.Abs(rect.Right - work.Right) <= 1 && Math.Abs(rect.Top - work.Top) <= 1,
                    $"{kind} native window is on the saved monitor");
                panel.DisconnectFromDesktop(); panel.ConnectToDesktop(desktop);
                check(Saved() == (x, y) && panel.ScreenX == x && panel.ScreenY == y,
                    $"{kind} keeps its position after desktop reconnection");
            }

            var secondary = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => !s.Primary);
            if (secondary is not null)
            {
                double x = secondary.WorkingArea.Right / scale - 336, y = secondary.WorkingArea.Top / scale;
                SetPosition(x, y);
                using var panel = Create();
                var actualWorkingArea = panel.WorkingAreaAt;
                panel.WorkingAreaAt = _ => System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
                panel.Hide(); panel.ConnectToDesktop(desktop);
                check(panel.ScreenX != x && Saved() == (x, y),
                    $"{kind} uses a temporary primary-screen fallback without changing the saved second-screen position");
                store.Save(workspace);
                panel.WorkingAreaAt = actualWorkingArea;
                panel.ConnectToDesktop(desktop);
                check(panel.ScreenX == x && panel.ScreenY == y && Saved() == (x, y),
                    $"{kind} returns to the second-screen right edge as soon as that screen is detected");
            }

            // The saved monitor is unavailable. Showing a reachable fallback
            // must not permanently replace the preferred position on next save.
            double missingX = System.Windows.Forms.Screen.AllScreens.Min(s => s.Bounds.Left) / scale - 8000;
            SetPosition(missingX, 90);
            using (var panel = Create())
            {
                panel.Hide(); panel.ConnectToDesktop(desktop);
                check(panel.ScreenX != missingX && DesktopNative.GetWindowRect(panel.Handle, out var rect) &&
                    System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.Contains(rect.Left, rect.Top)),
                    $"{kind} has a reachable fallback when its saved monitor is unavailable");
                panel.CapturePosition(); store.Save(workspace);
                var restored = store.Load();
                check((combined ? restored.Stacks[0].X : restored.Panels[0].X) == missingX && Saved() == (missingX, 90),
                    $"{kind} preserves its preferred monitor position through fallback, save and restart");
                panel.DisconnectFromDesktop(); panel.ConnectToDesktop(desktop);
                check(Saved() == (missingX, 90), $"{kind} reconnection cannot overwrite the preferred position");

                var work = System.Windows.Forms.Screen.AllScreens.Last().WorkingArea;
                double x = work.Right / scale - 336, y = work.Top / scale;
                panel.MoveTo(x - 40, y + 40);
                check(Saved() == (x - 40, y + 40), $"{kind} explicit dragging still updates the saved position");
            }
        }
    }
}
