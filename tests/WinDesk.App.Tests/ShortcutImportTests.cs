using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using WinDesk.App;
using WinDesk.Core;

internal static class ShortcutImportTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "WinDeskImport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string desktop = Path.Combine(root, "desktop"), owned = Path.Combine(root, "data", "Shortcuts"), moved = Path.Combine(root, "moved");
        Directory.CreateDirectory(desktop); Directory.CreateDirectory(moved);
        string source = Path.Combine(desktop, "Özel profil.lnk"), output = Path.Combine(root, "launched.json");
        string target = Environment.ProcessPath!;
        string arguments = $"--shortcut-probe \"{output}\" \"Türkçe değer\" --profile=work";
        CreateLink(source, target, arguments, root);
        byte[] original = File.ReadAllBytes(source);
        var importer = new ShortcutImporter(owned);
        var item = importer.Import(source);
        check(item.Path == target && item.Name == "Özel profil" && item.Arguments == arguments && item.WorkingDirectory == root,
            "Import resolves the actual application and retains arguments, working directory and display name");
        check(File.Exists(item.OwnedShortcutPath) && original.SequenceEqual(File.ReadAllBytes(item.OwnedShortcutPath)),
            "Private shell link preserves all native metadata without editing the desktop shortcut");
        check(importer.Import(source).OwnedShortcutPath == item.OwnedShortcutPath, "Repeated imports reuse the same verified private copy");
        string movedLink = Path.Combine(moved, "Renamed.lnk"); File.Move(source, movedLink);
        check(ShortcutImporter.StartInfo(item).FileName == item.OwnedShortcutPath && ShellIcons.Get(ShortcutImporter.IconPath(item)) is not null,
            "Moving and renaming the desktop shortcut preserves launch routing and the shell icon");
        File.Delete(movedLink);
        var start = ShortcutImporter.StartInfo(item); start.WindowStyle = ProcessWindowStyle.Hidden;
        using var process = Process.Start(start);
        for (int i = 0; i < 100 && !File.Exists(output); i++) await Task.Delay(50);
        check(File.Exists(output), "The private shortcut really launches its target after the original shortcut is deleted");
        using (var result = JsonDocument.Parse(File.ReadAllText(output)))
            check(result.RootElement.GetProperty("Directory").GetString() == root &&
                result.RootElement.GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()).SequenceEqual(new[] { "Türkçe değer", "--profile=work" }),
                "A launched process receives the original Unicode arguments and working directory");
        var workspace = Workspace.CreateDefault(); workspace.Panels[0].Items.Add(item);
        string stateFile = Path.Combine(root, "workspace.json"); new WorkspaceStore(stateFile).Save(workspace);
        var reloaded = new WorkspaceStore(stateFile).Load().Panels[0].Items.Single();
        check(reloaded.Path == target && reloaded.Arguments == arguments && reloaded.OwnedShortcutPath == item.OwnedShortcutPath,
            "Resolved targets and private shortcut ownership survive save and reload");
        string exported = DesktopShortcutExporter.Export(reloaded, Path.Combine(root, "export"));
        var exportedItem = importer.Import(exported);
        check(exportedItem.Path == target && exportedItem.Arguments == arguments && original.SequenceEqual(File.ReadAllBytes(exported)),
            "Dragging an imported item back out produces a shortcut to the actual target with its original metadata");
        var profileTwo = new ShortcutModel { Name = "Personal", Path = target, Arguments = "--profile=personal", WorkingDirectory = root };
        check(workspace.Panels[0].AddShortcut(profileTwo) && !workspace.Panels[0].AddShortcut(reloaded),
            "Different application profiles stay distinct while identical launches are deduplicated");
        workspace.Panels[1].Items.Add(profileTwo); workspace.Panels[0].Items.Remove(profileTwo);
        workspace.MoveShortcut(workspace.Panels[0].Id, item, workspace.Panels[1].Id, 0);
        check(workspace.Panels[1].Items.Count == 2, "Group transfers never consolidate shortcuts with different launch arguments");
        string url = Path.Combine(desktop, "Site.url"); File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.com\r\n");
        var website = importer.Import(url); File.Delete(url);
        check(File.ReadAllText(website.Path).Contains("https://example.com"), "Internet shortcuts remain independent of their source file too");

        string menu = Path.Combine(root, "menu"); Directory.CreateDirectory(menu);
        CreateLink(Path.Combine(menu, "Lost.lnk"), target, "--work", root);
        var legacy = Workspace.CreateDefault(); var legacyItem = new ShortcutModel { Name = "Benim adım", Path = Path.Combine(desktop, "Lost.lnk") };
        legacy.Panels[0].Items.Add(legacyItem); legacy.Panels[0].IsPinned = true;
        check(importer.UpgradeExisting(legacy, new[] { menu }) == 1 && legacyItem.Path == target && legacyItem.Name == "Benim adım" && legacy.Panels[0].IsPinned,
            "A missing old desktop link is recovered from a unique installed shortcut without changing its group or name");
        check(importer.UpgradeExisting(legacy, new[] { menu }) == 0, "Already imported items are not rewritten on each restart");
        string anotherMenu = Path.Combine(root, "other-menu"); Directory.CreateDirectory(anotherMenu);
        CreateLink(Path.Combine(anotherMenu, "Lost.lnk"), target, "--different-profile", root);
        var ambiguous = Workspace.CreateDefault(); string missing = Path.Combine(desktop, "Lost.lnk");
        ambiguous.Panels[0].Items.Add(new ShortcutModel { Name = "Lost", Path = missing });
        check(importer.UpgradeExisting(ambiguous, new[] { menu, anotherMenu }) == 0 && ambiguous.Panels[0].Items[0].Path == missing,
            "Ambiguous replacement shortcuts cannot silently point a saved item at another profile");
    }

    private static void CreateLink(string file, string target, string arguments, string workingDirectory)
    {
        object? shell = null, link = null;
        try {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true)!);
            link = ((dynamic)shell!).CreateShortcut(file);
            ((dynamic)link).TargetPath = target; ((dynamic)link).Arguments = arguments;
            ((dynamic)link).WorkingDirectory = workingDirectory;
            ((dynamic)link).IconLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll") + ",3";
            ((dynamic)link).Save();
        }
        finally {
            if (link is not null) Marshal.FinalReleaseComObject(link);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
