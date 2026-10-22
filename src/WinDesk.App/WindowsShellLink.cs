using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace WinDesk.App;

internal static class WindowsShellLink
{
    // WScript.Shell can round-trip arguments through the system ANSI code page.
    // Use the Unicode shell interface even on a non-Turkish Windows installation.
    private static readonly Guid ClassId = new("00021401-0000-0000-C000-000000000046");
    private const int BufferLength = 32768; // Windows' maximum process command line.

    internal static (string Target, string Arguments, string WorkingDirectory, int WindowStyle) Read(string file)
    {
        object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(ClassId, true)!)!;
        try
        {
            ((IPersistFile)instance).Load(file, 0);
            var link = (IShellLinkW)instance;
            var target = new StringBuilder(BufferLength);
            var arguments = new StringBuilder(BufferLength);
            var workingDirectory = new StringBuilder(BufferLength);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 4); // SLGP_RAWPATH
            link.GetArguments(arguments, arguments.Capacity);
            link.GetWorkingDirectory(workingDirectory, workingDirectory.Capacity);
            link.GetShowCmd(out int show);
            return (target.ToString(), arguments.ToString(), workingDirectory.ToString(), show);
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }

    internal static void Create(string file, string target, string arguments, string workingDirectory,
        int windowStyle = 1, string description = "", string? iconPath = null, int iconIndex = 0)
    {
        object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(ClassId, true)!)!;
        try
        {
            var link = (IShellLinkW)instance;
            link.SetPath(target);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            link.SetShowCmd(windowStyle);
            link.SetDescription(description);
            if (iconPath is not null) link.SetIconLocation(iconPath, iconIndex);
            ((IPersistFile)instance).Save(file, true);
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }

    // Vtable order follows IShellLinkW in shobjidl_core.h. IPersistFile provides
    // serialization; keeping the original .lnk bytes preserves other metadata.
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr findData, uint flags);
        void GetIDList(out IntPtr pidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int show);
        void SetShowCmd(int show);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
