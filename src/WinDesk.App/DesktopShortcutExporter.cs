using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WinDesk.Core;

namespace WinDesk.App;

internal static class DesktopShortcutExporter
{
    // The destination is injectable so tests never write to the user's desktop.
    internal static string Export(ShortcutModel item, string desktopDirectory)
    {
        string source = Path.GetFullPath(File.Exists(item.OwnedShortcutPath) ? item.OwnedShortcutPath : item.Path);
        if (!File.Exists(source) && !Directory.Exists(source)) throw new FileNotFoundException("Kısayolun hedefi bulunamadı.", source);
        string desktop = Path.GetFullPath(desktopDirectory);
        string? sourceDirectory = Path.GetDirectoryName(source);
        string extension = Path.GetExtension(source).ToLowerInvariant();
        bool existingLink = extension is ".lnk" or ".url";
        if (existingLink && (string.Equals(sourceDirectory, desktop, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceDirectory, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), StringComparison.OrdinalIgnoreCase))) return source;
        Directory.CreateDirectory(desktop);
        string name = new string(item.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name)) name = "Kısayol";
        if (name.Length > 120) name = name[..120];
        string suffix = existingLink ? extension : ".lnk";
        // Reserve a unique filename atomically. Never overwrite an existing icon.
        string destination;
        for (int i = 0; ; i++)
        {
            destination = Path.Combine(desktop, name + (i == 0 ? "" : $" ({i + 1})") + suffix);
            try { using var reserved = new FileStream(destination, FileMode.CreateNew, FileAccess.Write); break; }
            catch (IOException) when (File.Exists(destination) || Directory.Exists(destination)) { }
        }
        try
        {
            if (existingLink) File.Copy(source, destination, overwrite: true);
            else
            {
                object? shell = null, link = null;
                try
                {
                    shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!);
                    link = ((dynamic)shell!).CreateShortcut(destination);
                    ((dynamic)link).TargetPath = source;
                    ((dynamic)link).Arguments = item.Arguments;
                    ((dynamic)link).WorkingDirectory = item.WorkingDirectory.Length > 0 ? item.WorkingDirectory : Directory.Exists(source) ? source : Path.GetDirectoryName(source);
                    ((dynamic)link).WindowStyle = item.WindowStyle;
                    ((dynamic)link).Description = item.Name;
                    ((dynamic)link).Save();
                }
                finally
                {
                    if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
                    if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
                }
            }
            if (new FileInfo(destination).Length == 0) throw new IOException("Masaüstü kısayolu oluşturulamadı.");
            return destination;
        }
        catch { File.Delete(destination); throw; } // Only the file reserved by this operation.
    }
}
