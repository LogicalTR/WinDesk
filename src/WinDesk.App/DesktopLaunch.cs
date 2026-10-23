using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WinDesk.App;

internal static class DesktopLaunch
{
    internal static bool RelaunchIfRedirected(string[] args)
    {
        // A desktop process can inherit filesystem virtualization even when it
        // has no package identity. Resolve the actual file before loading it.
        if (Array.IndexOf(args, "--data-dir") >= 0) return false;
        string workspace = Path.Combine(UserDataLocation.DirectoryPath, "workspace.json");
        if (!File.Exists(workspace)) return false;
        string actual = ResolveFilePath(workspace);
        if (!IsRedirectedWorkspace(workspace, actual)) return false;
        if (Array.IndexOf(args, "--desktop-context") >= 0)
            throw new InvalidOperationException("Windows düzen dosyasını başka bir uygulamanın klasörüne yönlendirdi. WinDesk.exe dosyasını Dosya Gezgini'nden açın; düzeninize dokunulmadı.");
        StartOnDesktop(Environment.ProcessPath!, Environment.CommandLine + " --desktop-context");
        return true;
    }

    internal static bool IsRedirectedWorkspace(string requested, string actual) =>
        !string.Equals(requested, actual, StringComparison.OrdinalIgnoreCase) &&
        actual.Contains("\\Packages\\", StringComparison.OrdinalIgnoreCase) &&
        actual.Contains("\\LocalCache\\Local\\WinDesk\\", StringComparison.OrdinalIgnoreCase);

    internal static string ResolveFilePath(string path)
    {
        using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var text = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandle(file, text, (uint)text.Capacity, 0);
        if (length == 0 || length >= text.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        string result = text.ToString();
        return result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result[4..] : result;
    }

    internal static void StartOnDesktop(string executable, string commandLine)
    {
        // Inherit Explorer's ordinary desktop context instead of a packaged
        // launcher's virtualized data store. No elevation or shell restart.
        DesktopNative.GetWindowThreadProcessId(DesktopNative.GetShellWindow(), out uint shellProcess);
        using var parent = OpenProcess(0x0080, false, shellProcess); // PROCESS_CREATE_PROCESS
        if (parent.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        nuint bytes = 0;
        InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref bytes);
        IntPtr attributes = Marshal.AllocHGlobal(checked((int)bytes));
        IntPtr parentValue = Marshal.AllocHGlobal(IntPtr.Size), policyValue = Marshal.AllocHGlobal(sizeof(int));
        bool initialized = false;
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref bytes)) throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            Marshal.WriteIntPtr(parentValue, parent.DangerousGetHandle());
            Marshal.WriteInt32(policyValue, 1); // DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE
            if (!UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20000), parentValue, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero) ||
                !UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20012), policyValue, sizeof(int), IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var startup = new StartupInfoEx { Info = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 1, ShowWindow = 0 }, Attributes = attributes };
            if (!CreateProcess(executable, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, false,
                0x00080000 | 0x08000000, IntPtr.Zero, Path.GetDirectoryName(executable), ref startup, out var process)) // EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW
                throw new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(process.Thread); CloseHandle(process.Process);
        }
        finally
        {
            if (initialized) DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(attributes); Marshal.FreeHGlobal(parentValue); Marshal.FreeHGlobal(policyValue);
        }
    }

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCount, YCount, FillAttribute, Flags;
        public short ShowWindow, ReservedSize;
        public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint size, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr attributes, int count, int flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr attributes, uint flags, IntPtr attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returnedSize);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr attributes);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes,
        bool inherit, uint flags, IntPtr environment, string? directory, ref StartupInfoEx startup, out ProcessInformation process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
