using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using WinDesk.App;
using WinDesk.Core;

internal static class ShortcutClickTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Exercise the real button through UI Automation without starting WinDesk,
        // taking its mutex, attaching panels, or opening an external application.
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Exception? failure = null;
        Dispatcher.CurrentDispatcher.InvokeAsync(async () => {
            try { await TestAsync(check); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
        if (failure is not null) throw failure;
    }

    internal static async Task TestAsync(Action<bool, string> check)
    {
        // The desktop owner is used only for dragging, outside this click fixture.
        static ShortcutButton Button() => new(new PanelModel(), new ShortcutModel(), null!);
        static IInvokeProvider Invoker(ShortcutButton button) =>
            (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);
        static async Task Drain() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

        var first = Button();
        var firstInvoke = Invoker(first);
        int launches = 0;
        first.Click += (_, _) => launches++;
        firstInvoke.Invoke(); firstInvoke.Invoke();
        check(launches == 0, "Launching is deferred until the input event has finished");
        await Drain();
        check(launches == 1, "Two queued activations of one shortcut launch it only once");
        firstInvoke.Invoke(); await Drain();
        check(launches == 1, "An immediate repeat after launch completion is ignored");

        var second = Button();
        int otherLaunches = 0;
        second.Click += (_, _) => otherLaunches++;
        Invoker(second).Invoke(); await Drain();
        check(otherLaunches == 1, "Launching one shortcut does not block a different shortcut");

        await Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 50);
        firstInvoke.Invoke(); await Drain();
        check(launches == 2, "A later deliberate activation can open another instance");

        var slow = Button();
        var slowInvoke = Invoker(slow);
        int slowLaunches = 0;
        slow.Click += (_, _) => {
            slowLaunches++;
            if (slowLaunches != 1) return;
            // Model a slow ShellExecute call pumping a nested dispatcher loop.
            // The repeat arrives after the double-click interval has elapsed.
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime + 50) };
            timer.Tick += (_, _) => {
                timer.Stop(); slowInvoke.Invoke();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
        };
        slowInvoke.Invoke(); await Drain();
        check(slowLaunches == 1, "A slow launch pumping messages cannot reenter the same shortcut launch");
        slowInvoke.Invoke(); await Drain();
        check(slowLaunches == 1, "The repeat-click interval starts after the slow launch completes");
    }
}
