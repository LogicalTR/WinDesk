using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDesk.App;

internal static class DesktopLaunch
{
    internal static void EnsureUnpackaged()
    {
        uint length = 0;
        int result = GetCurrentPackageFullName(ref length, null);
        if (result == 15700) return; // APPMODEL_ERROR_NO_PACKAGE
        if (result is not (0 or 122)) throw new Win32Exception(result);
        // Never interpret a redirected package store as the user's desktop.
        // The menu launches through the desktop Explorer automation object.
        throw new InvalidOperationException("WinDesk normal masaüstü uygulaması olarak başlatılamadı. WinDesk.exe dosyasından açın; mevcut düzen dosyanıza dokunulmadı.");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? name);
}
