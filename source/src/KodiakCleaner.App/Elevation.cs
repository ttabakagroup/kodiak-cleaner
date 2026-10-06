using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace KodiakCleaner.App;

public static class Elevation
{
    public static bool IsAdministrator { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public static void RestartElevated()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(exe, RestartedArg) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The user declined the UAC prompt; keep running as-is.
        }
    }

    /// <summary>Tells the new copy to wait for this one to close before it takes over the window.</summary>
    public const string RestartedArg = "--restarted";

    /// <summary>Added to the elevated copy's arguments so a declined or failed elevation never loops.</summary>
    public const string ElevatedArg = "--elevated";

    /// <summary>
    /// Starts an elevated copy with the same arguments (Windows shows its permission prompt).
    /// Returns false when the person declines the prompt or Windows refuses.
    /// </summary>
    public static bool TryStartElevated(IEnumerable<string> args)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;
        var psi = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add(ElevatedArg);
        try
        {
            // UseShellExecute ignores ArgumentList, so pass a quoted string.
            psi.Arguments = string.Join(" ", psi.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            psi.ArgumentList.Clear();
            return Process.Start(psi) is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public static void ShowInExplorer(string file) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
}

public static class Shell
{
    /// <summary>Opens a web link in the default browser.</summary>
    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
