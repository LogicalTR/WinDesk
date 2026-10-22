using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WinDesk.Core;

namespace WinDesk.App;

internal sealed class ShortcutImporter(string directory)
{
    internal ShortcutModel Import(string source)
    {
        source = Path.GetFullPath(source);
        if (!File.Exists(source) && !Directory.Exists(source)) throw new FileNotFoundException("Kısayol bulunamadı.", source);
        string extension = Path.GetExtension(source).ToLowerInvariant();
        var item = new ShortcutModel { Path = source, Name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)) };
        if (extension is ".lnk" or ".url" or ".exe") item.Name = Path.GetFileNameWithoutExtension(source);
        if (string.IsNullOrEmpty(item.Name)) item.Name = source;
        if (extension is not (".lnk" or ".url") || Directory.Exists(source)) return item;

        if (extension == ".lnk")
        {
            try
            {
                var shortcut = WindowsShellLink.Read(source);
                string target = Environment.ExpandEnvironmentVariables(shortcut.Target);
                item.Path = Path.IsPathFullyQualified(target) ? Path.GetFullPath(target) : "";
                item.Arguments = shortcut.Arguments;
                item.WorkingDirectory = Environment.ExpandEnvironmentVariables(shortcut.WorkingDirectory);
                item.WindowStyle = shortcut.WindowStyle;
            }
            catch (COMException ex) { throw new IOException("Windows kısayolu okunamadı.", ex); }
        }
        // Preserve the full shell link too: custom icon, elevation, MSI and
        // virtual-shell targets cannot all be represented by an EXE path.
        byte[] bytes = File.ReadAllBytes(source);
        if (bytes.Length == 0) throw new InvalidDataException("Kısayol dosyası boş.");
        Directory.CreateDirectory(directory);
        string owned = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(bytes)) + extension);
        try { using var stream = new FileStream(owned, FileMode.CreateNew, FileAccess.Write); stream.Write(bytes); }
        catch (IOException) when (File.Exists(owned) && File.ReadAllBytes(owned).SequenceEqual(bytes)) { }
        item.OwnedShortcutPath = owned;
        if (extension == ".url" || item.Path.Length == 0) item.Path = owned;
        return item;
    }

    internal static string IconPath(ShortcutModel item) => File.Exists(item.OwnedShortcutPath) ? item.OwnedShortcutPath : item.Path;

    internal static ProcessStartInfo StartInfo(ShortcutModel item)
    {
        if (!File.Exists(item.Path) && !Directory.Exists(item.Path))
            throw new FileNotFoundException("Kısayolun açtığı uygulama veya dosya bulunamadı.", item.Path);
        // The private copy points at the real target and retains shell-specific
        // launch flags. It never depends on the former desktop .lnk file.
        if (File.Exists(item.OwnedShortcutPath)) return new(item.OwnedShortcutPath) { UseShellExecute = true };
        return new(item.Path) { UseShellExecute = true, Arguments = item.Arguments,
            WorkingDirectory = item.WorkingDirectory,
            WindowStyle = item.WindowStyle == 3 ? ProcessWindowStyle.Maximized : item.WindowStyle == 7 ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal };
    }

    internal int UpgradeExisting(Workspace workspace, IEnumerable<string>? searchFolders = null)
    {
        searchFolders ??= new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) };
        string[]? candidates = null;
        int upgraded = 0;
        foreach (var item in workspace.Panels.SelectMany(panel => panel.Items))
        {
            if (item.OwnedShortcutPath.Length > 0 || Path.GetExtension(item.Path).ToLowerInvariant() is not (".lnk" or ".url")) continue;
            try
            {
                ShortcutModel? imported;
                if (File.Exists(item.Path)) imported = Import(item.Path);
                else
                {
                    candidates ??= searchFolders.SelectMany(FindLinks).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    var matches = new List<ShortcutModel>();
                    foreach (string candidate in candidates.Where(path => string.Equals(Path.GetFileName(path), Path.GetFileName(item.Path), StringComparison.OrdinalIgnoreCase)))
                    {
                        try {
                            var match = Import(candidate);
                            if ((File.Exists(match.Path) || Directory.Exists(match.Path)) && !matches.Any(old => old.SameLaunchTarget(match))) matches.Add(match);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Trace.WriteLine(ex.Message); }
                    }
                    // A matching name is insufficient if it points at different
                    // apps or profiles. Leave ambiguous entries for the user.
                    imported = matches.Count == 1 ? matches[0] : null;
                }
                if (imported is null) continue;
                item.Path = imported.Path; item.Arguments = imported.Arguments; item.WorkingDirectory = imported.WorkingDirectory;
                item.OwnedShortcutPath = imported.OwnedShortcutPath; item.WindowStyle = imported.WindowStyle;
                upgraded++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Trace.WriteLine(ex.Message); }
        }
        return upgraded;
    }

    private static IEnumerable<string> FindLinks(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        try { return Directory.GetFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".lnk" or ".url"); }
        catch (IOException) { return Array.Empty<string>(); }
    }
}
