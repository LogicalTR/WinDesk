using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using WinDesk.App;
using WinDesk.Core;

internal static class CloseDelayTests
{
    // Run real WPF timers without attaching desktop windows, taking the app's
    // mutex, or reading/writing the user's workspace.
    private sealed class TestApp : App
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }

    internal static void Run(Action<bool, string> check)
    {
        var app = new TestApp();
        var xaml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppStyles.xaml"));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = new XElement(ns + "ResourceDictionary", xaml.Root!.Attributes().Where(a => a.IsNamespaceDeclaration),
            xaml.Root.Element(ns + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(resources.ToString());
        var workspace = Workspace.CreateDefault();
        typeof(App).GetProperty(nameof(App.Workspace))!.SetValue(app, workspace);
        Exception? failure = null;
        Dispatcher.CurrentDispatcher.InvokeAsync(async () => {
            try { await Run(app, check); }
            catch (Exception ex) { failure = ex; }
            finally {
                foreach (var panel in app.DesktopPanels) panel.Dispose();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        Dispatcher.Run();
        if (failure is not null) throw failure;
    }

    internal static async Task Run(App app, Action<bool, string> check)
    {
        app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
        app.Workspace.Panels.AddRange(Enumerable.Range(1, 3).Select(n => new PanelModel { Name = "Timing " + n }));
        app.Workspace.Combine(app.Workspace.Panels.Select(p => p.Id), "Close timing");
        app.RebuildPanels();
        var panel = app.DesktopPanels.Single();
        panel.Hide(); // Prevent the user's actual pointer from affecting these timer tests.
        var first = app.Workspace.Panels[0];
        var second = app.Workspace.Panels[1];
        void MouseEvent(RoutedEvent routedEvent) => panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = routedEvent });

        foreach (int delay in new[] { 1500, 200, 2000 })
        {
            app.SetCloseDelay(delay); panel.Expand(first.Id);
            var clock = Stopwatch.StartNew(); panel.HoverSection(second.Id);
            await Task.Delay(delay == 200 ? 70 : 350);
            check(panel.IsSectionExpanded(first.Id) && !panel.IsSectionExpanded(second.Id),
                $"Hovering another header keeps the current group open before the selected {delay} ms elapses");
            await Until(() => panel.ActiveSectionId == second.Id);
            long elapsed = clock.ElapsedMilliseconds;
            check(elapsed >= delay - 40 && elapsed < delay + 900 && !panel.IsSectionExpanded(first.Id) && panel.IsSectionExpanded(second.Id),
                $"Header switch uses the selected {delay} ms close delay (measured {elapsed} ms) and keeps one unpinned body");
        }

        app.SetCloseDelay(1500); panel.Expand(first.Id); panel.HoverSection(second.Id);
        await Task.Delay(250); panel.HoverSection(first.Id); await Task.Delay(1650);
        check(panel.ActiveSectionId == first.Id && panel.IsSectionExpanded(first.Id) && !panel.IsSectionExpanded(second.Id),
            "Returning to the original group cancels the pending header switch");

        panel.HoverSection(second.Id); await Task.Delay(200);
        var leaveClock = Stopwatch.StartNew(); MouseEvent(Mouse.MouseLeaveEvent);
        await Task.Delay(350);
        check(panel.IsSectionExpanded(first.Id) && !panel.IsSectionExpanded(second.Id),
            "Leaving the entire area cancels a pending header switch without closing the original group early");
        await Until(() => panel.ActiveSectionId is null);
        check(leaveClock.ElapsedMilliseconds >= 1460 && !panel.IsSectionExpanded(first.Id) && !panel.IsSectionExpanded(second.Id),
            "Leaving the area still uses the selected delay and never opens the abandoned target");

        panel.Expand(first.Id); panel.HoverSection(second.Id); await Task.Delay(200);
        var changeClock = Stopwatch.StartNew(); app.SetCloseDelay(200);
        await Until(() => panel.ActiveSectionId == second.Id);
        check(changeClock.ElapsedMilliseconds < 700 && panel.IsSectionExpanded(second.Id),
            "Shortening the setting updates a header switch already waiting");

        panel.Expand(first.Id); panel.HoverSection(second.Id); app.SetCloseDelay(1500);
        await Task.Delay(400);
        check(panel.ActiveSectionId == first.Id, "Lengthening the setting prevents an old short header timer from closing the group early");
        await Until(() => panel.ActiveSectionId == second.Id);

        app.SetCloseDelay(550); panel.Expand(first.Id); panel.HoverSection(second.Id);
        await Task.Delay(150); panel.HoverSection(app.Workspace.Panels[2].Id);
        await Task.Delay(250);
        check(panel.ActiveSectionId == first.Id, "Crossing another header does not let the abandoned target's timer close the group early");
        await Until(() => panel.ActiveSectionId == app.Workspace.Panels[2].Id);
        check(!panel.IsSectionExpanded(second.Id), "Rapid header movement opens only the last hovered group");

        app.SetCloseDelay(1500);

        panel.Collapse(); var openClock = Stopwatch.StartNew(); panel.HoverSection(first.Id);
        await Until(() => panel.ActiveSectionId == first.Id);
        check(openClock.ElapsedMilliseconds < 700, "Opening the first group still responds promptly when there is nothing to close");
        MouseEvent(Mouse.MouseEnterEvent);

        first.IsPinned = true; panel.PinChanged(first.Id);
        openClock.Restart(); panel.HoverSection(second.Id);
        await Until(() => panel.ActiveSectionId == second.Id);
        check(openClock.ElapsedMilliseconds < 700 && panel.IsSectionExpanded(first.Id) && panel.IsSectionExpanded(second.Id),
            "A pinned group remains open while another group opens promptly");
        first.IsPinned = false; panel.PinChanged(first.Id);
        panel.Collapse();

        app.SetCloseDelay(1500); panel.Expand(first.Id); panel.HoverSection(second.Id);
        panel.Collapse(); await Task.Delay(1650);
        check(panel.ActiveSectionId is null, "Explicit collapse cancels pending header switches");

        app.SetCloseDelay(200); panel.Expand(first.Id); panel.HoverSection(second.Id);
        var menu = new ContextMenu(); panel.TrackMenu(menu);
        menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
        await Task.Delay(400);
        check(panel.ActiveSectionId == first.Id, "Opening a context menu cancels a pending switch so the group stays in place");
        menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent)); panel.UntrackMenu(menu);

        panel.Expand(first.Id); panel.HoverSection(second.Id);
        panel.WithDialog(() => {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
            check(panel.ActiveSectionId == first.Id, "A modal dialog cannot trigger a pending header switch in its nested message loop");
        });
    }

    private static async Task Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < 3000) await Task.Delay(5);
        if (!condition()) throw new TimeoutException("Expected panel transition did not happen.");
    }
}
