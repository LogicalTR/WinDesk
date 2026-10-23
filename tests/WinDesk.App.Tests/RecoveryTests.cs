using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Text.Json;
using WinDesk.App;
using WinDesk.Core;

internal static class RecoveryTests
{
    internal static void DesktopContext(Action<bool, string> check, string directory)
    {
        const string requested = @"C:\Users\test\AppData\Local\WinDesk\workspace.json";
        check(DesktopLaunch.IsRedirectedWorkspace(requested,
            @"C:\Users\test\AppData\Local\Packages\Example_123\LocalCache\Local\WinDesk\workspace.json"),
            "A redirected workspace is detected independently of package identity");
        check(!DesktopLaunch.IsRedirectedWorkspace(requested, requested.ToUpperInvariant()) &&
            !DesktopLaunch.IsRedirectedWorkspace(requested, @"D:\UserData\WinDesk\workspace.json"),
            "Ordinary workspace paths do not trigger a relaunch");
        Directory.CreateDirectory(directory);
        string report = Path.Combine(directory, "desktop-context-" + Guid.NewGuid().ToString("N") + ".json");
        string executable = Path.Combine(AppContext.BaseDirectory, "WinDesk.App.Tests.exe");
        DesktopLaunch.StartOnDesktop(executable, $"\"{executable}\" --desktop-context-probe \"{report}\"");
        var watch = Stopwatch.StartNew();
        while (!File.Exists(report) && watch.ElapsedMilliseconds < 15000) Thread.Sleep(50);
        check(File.Exists(report), "Explorer-context child starts without an interactive console");
        using var data = JsonDocument.Parse(File.ReadAllText(report));
        string wanted = data.RootElement.GetProperty("Requested").GetString()!;
        string actual = data.RootElement.GetProperty("Actual").GetString()!;
        check(string.Equals(wanted, actual, StringComparison.OrdinalIgnoreCase),
            "The launched process reads the real user workspace instead of the launcher's package cache");
    }

    internal static void Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "WinDeskRecovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "legacy.json"), target = Path.Combine(directory, "normal", "workspace.json");
        var original = Workspace.CreateDefault();
        original.Panels[0].Name = "Kurtarılan grup"; original.Panels[0].IsPinned = true; original.CloseDelayMs = 1500;
        original.Panels[0].Items.Add(new ShortcutModel { Name = "Örnek", Path = Path.Combine(directory, "shortcut.lnk") });
        new WorkspaceStore(source).Save(original);
        string sourceText = File.ReadAllText(source);
        UserDataLocation.RecoverLegacyWorkspace(target, new[] { source });
        var recovered = new WorkspaceStore(target).Load();
        check(recovered.Panels[0].Id == original.Panels[0].Id && recovered.Panels[0].Items.Count == 1 && recovered.Panels[0].IsPinned && recovered.CloseDelayMs == 1500,
            "Legacy recovery preserves group identity, shortcuts, pin state and close delay");
        check(File.ReadAllText(source) == sourceText, "Recovery leaves the original package data untouched");
        recovered.Panels[0].Name = "Sonradan düzenlenen"; new WorkspaceStore(target).Save(recovered);
        UserDataLocation.RecoverLegacyWorkspace(target, new[] { source });
        check(new WorkspaceStore(target).Load().Panels[0].Name == "Sonradan düzenlenen", "Restart never overwrites an existing normal workspace with an older package copy");
        string corrupt = Path.Combine(directory, "corrupt.json"), invalidTarget = Path.Combine(directory, "invalid", "workspace.json");
        File.WriteAllText(corrupt, "{broken");
        bool rejected = false;
        try { UserDataLocation.RecoverLegacyWorkspace(invalidTarget, new[] { corrupt }); }
        catch (System.Text.Json.JsonException) { rejected = true; }
        check(rejected && !File.Exists(invalidTarget), "Invalid legacy data cannot create an empty replacement workspace");
    }
}
