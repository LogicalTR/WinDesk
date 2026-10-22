using System;
using System.IO;
using WinDesk.App;
using WinDesk.Core;

internal static class RecoveryTests
{
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
