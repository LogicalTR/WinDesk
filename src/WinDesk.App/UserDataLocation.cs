using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WinDesk.Core;

namespace WinDesk.App;

internal static class UserDataLocation
{
    internal static void RecoverLegacyWorkspace(string workspaceFile, string[]? legacyFiles = null)
    {
        if (File.Exists(workspaceFile)) return;
        // Early development launches inherited Codex's filesystem redirection;
        // the first shell-menu build also used a private package store. Import
        // the original development layout first, only when no normal store
        // exists. Validate it before writing and leave the source untouched.
        var local = Path.GetDirectoryName(DirectoryPath)!;
        legacyFiles ??= new[] { "OpenAI.Codex_2p2nqsd0c76g0", "WinDesk.DesktopMenu_7v1s6b42e3yy6" }
            .Select(package => Path.Combine(local, "Packages", package, "LocalCache", "Local", "WinDesk", "workspace.json")).ToArray();
        foreach (var legacyFile in legacyFiles)
        {
            if (!File.Exists(legacyFile)) continue;
            var recovered = new WorkspaceStore(legacyFile).Load();
            new WorkspaceStore(workspaceFile).Save(recovered);
            return;
        }
    }

    internal static string DirectoryPath
    {
        get
        {
            // Package identity must not choose a second, empty workspace.
            // Environment.GetFolderPath(LocalApplicationData) is redirected in MSIX.
            Guid localAppData = new("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");
            const uint noPackageRedirection = 0x00010000;
            Marshal.ThrowExceptionForHR(SHGetKnownFolderPath(ref localAppData, noPackageRedirection, IntPtr.Zero, out var path));
            try { return Path.Combine(Marshal.PtrToStringUni(path)!, "WinDesk"); }
            finally { Marshal.FreeCoTaskMem(path); }
        }
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(ref Guid folder, uint flags, IntPtr token, out IntPtr path);
}
