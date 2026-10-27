using System;
using System.Runtime.InteropServices;
using System.Threading;
using WinDesk.App;

internal static class PanelHotkeyRecoveryTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int lost in new[] { 0x54, 0xA2, 0xA0, 0x11, 0x10 })
        {
            var chord = new ControlShiftChord();
            chord.Process(lost, true); // The matching release was never delivered.
            chord.Synchronize(_ => false);
            check(chord.IsIdle, $"Recovery clears a missed key-up for {lost:X}");
            chord.Process(0xA2, true); chord.Process(0xA0, true);
            check(chord.Process(0xA0, false) && !chord.Process(0xA2, false),
                "The next bare Ctrl+Shift works after a lost key-up");
        }
        var held = new ControlShiftChord();
        held.Synchronize(key => key is 0xA2 or 0xA0);
        check(!held.Process(0xA0, false) && !held.Process(0xA2, false),
            "Keys discovered during recovery never create an unsolicited toggle");
        var uninterrupted = new ControlShiftChord();
        uninterrupted.Process(0xA2, true); uninterrupted.Process(0xA0, true);
        uninterrupted.Synchronize(key => key is 0xA2 or 0xA0);
        check(uninterrupted.Process(0xA0, false) && !uninterrupted.Process(0xA2, false),
            "Maintenance preserves an uninterrupted held Ctrl+Shift chord");
        var extra = new ControlShiftChord();
        extra.Process(0xA2, true); extra.Process(0xA0, true);
        extra.Process(0x54, true); extra.Process(0x54, false);
        extra.Synchronize(key => key is 0xA2 or 0xA0);
        check(!extra.Process(0xA0, false) && !extra.Process(0xA2, false),
            "Maintenance does not turn Ctrl+Shift+T into a bare modifier chord");
        var missedPress = new ControlShiftChord();
        missedPress.Synchronize(key => key == 0x54);
        missedPress.Process(0xA2, true); missedPress.Process(0xA0, true);
        check(!missedPress.Process(0xA0, false) && !missedPress.Process(0xA2, false),
            "A physically held extra key still cancels the shortcut after recovery");

        int toggles = 0;
        var hotkey = new PanelHotkey(() => Interlocked.Increment(ref toggles));
        try
        {
            int registrations = hotkey.Registrations;
            check(UnhookWindowsHookEx(hotkey.HookHandle), "Remove the real native hook to simulate Windows silently dropping it");
            check(SpinWait.SpinUntil(() => hotkey.Registrations > registrations, TimeSpan.FromSeconds(8)),
                "The dedicated listener automatically registers a replacement hook");
            check(toggles == 0, "Re-registering the hook never toggles panels by itself");
        }
        finally { hotkey.Dispose(); }
        check(hotkey.HookHandle == IntPtr.Zero, "Disposal releases the replacement hook and timer");
    }

    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
}
