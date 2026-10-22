using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinDesk.App;

internal static class ExplorerRestartTests
{
    // Deliberately opt-in: ordinary tests must never restart the user's shell.
    internal static async Task Run(App app, Action<bool, string> check)
    {
        if (Environment.GetEnvironmentVariable("WINDESK_ALLOW_EXPLORER_RESTART") != "1")
            throw new InvalidOperationException("Run this test only in the disposable VM with WINDESK_ALLOW_EXPLORER_RESTART=1.");

        var model = app.Workspace.Panels.First();
        model.IsPinned = true;
        app.RebuildPanels();
        var panel = app.DesktopPanels.Single(p => p.Contains(model.Id));
        panel.MoveTo(180, 140); panel.CapturePosition(); app.Save();
        double x = model.X, y = model.Y;

        foreach (bool hidden in new[] { false, true })
        {
            if (app.PanelsHidden != hidden) app.TogglePanels();
            IntPtr oldHandle = panel.Handle;
            DesktopNative.GetWindowThreadProcessId(DesktopNative.GetShellWindow(), out uint shellId);
            using var shell = Process.GetProcessById((int)shellId);
            if (shell.ProcessName != "explorer" || shell.SessionId != Process.GetCurrentProcess().SessionId)
                throw new InvalidOperationException("Refusing to restart anything except this session's Explorer shell.");
            shell.Kill();
            await shell.WaitForExitAsync();

            // Windows normally restarts its shell. Start it if auto-restart is
            // unavailable in the test VM; never kill a second shell process.
            await Task.Delay(3000);
            if (DesktopNative.GetShellWindow() == IntPtr.Zero)
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true });
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline)
            {
                app.RefreshDesktopConnection();
                if (panel.Handle != IntPtr.Zero && panel.Handle != oldHandle &&
                    DesktopNative.IsWindow(panel.Handle) && DesktopNative.IsDesktopHost(panel.DesktopParent)) break;
                await Task.Delay(250);
            }

            string state = hidden ? "hidden" : "visible";
            check(panel.Handle != IntPtr.Zero && panel.Handle != oldHandle && DesktopNative.IsWindow(panel.Handle),
                $"Real Explorer restart recreates the {state} panel HWND");
            check(DesktopNative.IsDesktopHost(panel.DesktopParent) && DesktopNative.GetParent(panel.Handle) == panel.DesktopParent,
                $"Recovered {state} panel is a child of the new Explorer desktop");
            check(DesktopNative.IsWindowVisible(panel.Handle) == !hidden && app.PanelsHidden == hidden,
                $"Real Explorer restart preserves the {state} visibility preference");
            check(model.X == x && model.Y == y && model.IsPinned && panel.IsSectionExpanded(model.Id),
                $"Real Explorer restart preserves {state} panel position and pinned state");
        }
    }
}
