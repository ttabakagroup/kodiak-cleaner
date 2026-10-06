using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace KodiakCleaner.Core.Startup;

/// <summary>Makes Windows shortcuts (.lnk) with the shell's own ShellLink object.</summary>
[SupportedOSPlatform("windows")]
public static class ShellLink
{
    public static void Create(string shortcut, string target, string arguments = "", string? workingFolder = null)
    {
        var link = (IShellLinkW)new ShellLinkObject();
        try
        {
            link.SetPath(target);
            if (arguments.Length > 0) link.SetArguments(arguments);
            link.SetWorkingDirectory(workingFolder ?? Path.GetDirectoryName(target) ?? "");
            ((IPersistFile)link).Save(shortcut, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    /// <summary>The icon a shortcut shows: its own icon setting, else the program it opens. ("", 0) when it can't be read.</summary>
    public static (string File, int Index) IconOf(string shortcut)
    {
        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)new ShellLinkObject();
            ((IPersistFile)link).Load(shortcut, 0);
            var text = new StringBuilder(1024);
            link.GetIconLocation(text, text.Capacity, out var index);
            var icon = Environment.ExpandEnvironmentVariables(text.ToString());
            if (icon.Length > 0 && File.Exists(icon)) return (icon, index);
            text.Clear();
            link.GetPath(text, text.Capacity, IntPtr.Zero, 0);
            var target = Environment.ExpandEnvironmentVariables(text.ToString());
            return target.Length > 0 && File.Exists(target) ? (target, 0) : ("", 0);
        }
        catch (Exception e) when (e is COMException or InvalidCastException or IOException or UnauthorizedAccessException or ArgumentException) { return ("", 0); }
        finally { if (link is not null) Marshal.FinalReleaseComObject(link); }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkObject { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int max, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int max);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int max);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int max);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
