using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinDesk.App;
using WinDesk.Core;

internal static class ShortcutImportTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "WinDeskImport-" + Guid.NewGuid().ToString("N"), "Türkçe çalışma-日本語");
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
        CheckIconFallback(root, importer, target, check);
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
        string freshExport = DesktopShortcutExporter.Export(new ShortcutModel {
            Name = "Türkçe uygulama", Path = target, Arguments = arguments, WorkingDirectory = root
        }, Path.Combine(root, "export"));
        File.Delete(output);
        using var exportedProcess = Process.Start(new ProcessStartInfo(freshExport) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
        for (int i = 0; i < 100 && !File.Exists(output); i++) await Task.Delay(50);
        using (var exportedResult = File.Exists(output) ? JsonDocument.Parse(File.ReadAllText(output)) : null)
            check(exportedResult is not null && exportedResult.RootElement.GetProperty("Arguments")[0].GetString() == "Türkçe değer",
                "A newly exported shortcut launches with Unicode arguments on a non-Turkish Windows installation");
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
        WindowsShellLink.Create(file, target, arguments, workingDirectory,
            iconPath: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll"), iconIndex: 3);
    }

    private static void CheckIconFallback(string root, ShortcutImporter importer, string target, Action<bool, string> check)
    {
        string iconFile = Path.Combine(root, "version-1.ico"), linkFile = Path.Combine(root, "Updated app.lnk");
        using (var stream = File.Create(iconFile)) System.Drawing.SystemIcons.Warning.Save(stream);
        WindowsShellLink.Create(linkFile, target, "--profile=work", root, iconPath: iconFile);
        var item = importer.Import(linkFile);
        byte[] original = File.ReadAllBytes(item.OwnedShortcutPath);
        byte[] customPixels = IconPixels(ShellIcons.Get(ShortcutImporter.IconPath(item)));
        byte[] targetPixels = IconPixels(ShellIcons.Get(target));
        check(ShortcutImporter.IconPath(item) == item.OwnedShortcutPath && !customPixels.SequenceEqual(targetPixels),
            "A valid custom icon is kept instead of being replaced by the application's icon");

        File.Delete(iconFile); // An updater removes the old version's icon after import.
        check(IconPixels(ShellIcons.Get(ShortcutImporter.IconPath(item))).SequenceEqual(targetPixels),
            "Deleting a versioned icon after import displays the actual application icon, not a generic shell icon");
        check(original.SequenceEqual(File.ReadAllBytes(item.OwnedShortcutPath)) && original.SequenceEqual(File.ReadAllBytes(linkFile)) &&
            ShortcutImporter.StartInfo(item).FileName == item.OwnedShortcutPath && item.Arguments == "--profile=work",
            "Icon fallback leaves both shortcut files and launch metadata unchanged");
        check(IconPixels(ShellIcons.Get(ShortcutImporter.IconPath(new ShortcutModel { Path = linkFile }))).SequenceEqual(targetPixels),
            "Legacy links with missing custom icons also fall back to their resolved application");

        using (var stream = File.Create(iconFile)) System.Drawing.SystemIcons.Warning.Save(stream);
        check(ShortcutImporter.IconPath(item) == item.OwnedShortcutPath,
            "Restoring a custom icon makes it eligible again without rewriting the saved shortcut");

        const string iconVariable = "WINDESK_ICON_FALLBACK_TEST";
        string? previous = Environment.GetEnvironmentVariable(iconVariable);
        try
        {
            Environment.SetEnvironmentVariable(iconVariable, root);
            string environmentLink = Path.Combine(root, "Environment icon.lnk");
            WindowsShellLink.Create(environmentLink, target, "", root, iconPath: "%" + iconVariable + "%\\version-1.ico");
            var environmentItem = importer.Import(environmentLink);
            check(ShortcutImporter.IconPath(environmentItem) == environmentItem.OwnedShortcutPath,
                "Environment variables in existing custom icon paths are expanded before checking availability");
            File.Delete(iconFile);
            check(ShortcutImporter.IconPath(environmentItem) == target,
                "A missing environment-based icon falls back to the application");
        }
        finally { Environment.SetEnvironmentVariable(iconVariable, previous); }

        string defaultLink = Path.Combine(root, "Default icon.lnk");
        WindowsShellLink.Create(defaultLink, target, "", root);
        var defaultItem = importer.Import(defaultLink);
        check(ShortcutImporter.IconPath(defaultItem) == defaultItem.OwnedShortcutPath,
            "Links without explicit custom icons continue to use Windows shell icon handling");
        string missingTargetLink = Path.Combine(root, "Missing target.lnk");
        WindowsShellLink.Create(missingTargetLink, Path.Combine(root, "missing.exe"), "", root, iconPath: iconFile);
        var missingTarget = importer.Import(missingTargetLink);
        check(ShortcutImporter.IconPath(missingTarget) == missingTarget.OwnedShortcutPath,
            "A missing application does not replace the shell icon with another broken path");
        string invalidLink = Path.Combine(root, "Unreadable.lnk"); File.WriteAllText(invalidLink, "invalid shell link");
        check(ShortcutImporter.IconPath(new ShortcutModel { Path = invalidLink }) == invalidLink,
            "An unreadable shell link does not prevent the rest of the panel from loading");
        check(ShortcutImporter.IconPath(new ShortcutModel { Path = target }) == target,
            "Direct application entries retain their own icon path");
    }

    private static byte[] IconPixels(ImageSource? source)
    {
        if (source is not BitmapSource bitmap) throw new InvalidOperationException("Expected a rendered shell icon.");
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        byte[] pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}
