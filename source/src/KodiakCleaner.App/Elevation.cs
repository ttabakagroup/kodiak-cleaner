using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace KodiakCleaner.App;

public static class Elevation
{
    public static bool IsAdministrator { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>Restarts Kodiak Cleaner as administrator (Windows asks first), reopening on <paramref name="page"/> (a --page= key).</summary>
    public static void RestartElevated(string? page = null)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;
        // Restarting stops anything still running, so ask first (Ted, 2026-10-23).
        if (!MainWindow.ConfirmExit("Restart")) return;
        try
        {
            var args = RestartedArg + (page is { Length: > 0 } ? $" {Integration.PageArgPrefix}{page}" : "");
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The user declined the UAC prompt; keep running as-is (and ask again before a later close).
            MainWindow.ExitCancelled();
        }
    }

    /// <summary>The page open now, so a restart comes back to it; MainViewModel keeps it current.</summary>
    public static string? CurrentPage { get; set; }

    /// <summary>
    /// Says why a step needs administrator rights and offers to restart as administrator right away (Ted, 2026-10-23: a tool
    /// that needs them prompts for the restart). Every "needs administrator rights" message goes through here, never Dialogs.Error.
    /// </summary>
    public static void OfferRestart(string why)
    {
        if (IsAdministrator) { Dialogs.Error(why); return; }
        var choice = Dialogs.Choose(MessageKind.Question, "Administrator Rights Needed",
            why.TrimEnd() + "\n\nRestart Kodiak Cleaner as administrator now? Windows asks first, then Kodiak Cleaner opens again on the page you're on.", null,
            ["Not Now", "Restart as Administrator"], 1, 0);
        if (choice == 1) Application.Current.Dispatcher.Invoke(() => RestartElevated(CurrentPage));
    }

    /// <summary>Tells the new copy to wait for this one to close before it takes over the window.</summary>
    public const string RestartedArg = KodiakCleaner.Core.Util.LaunchCounter.RestartedArg;

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

public static partial class Shell
{
    /// <summary>
    /// Opens a web link in the default browser. Run as administrator (Always Admin, Restart as Administrator), a plain
    /// ShellExecute would start the browser with Kodiak Cleaner's elevated rights (pre-Beta review, 2026-10-25), so the link is
    /// handed to explorer.exe instead: with Windows' own shell running it passes the link to that shell, which opens the browser
    /// as the signed-in user, unelevated. Only http and https links go that way; without a running shell (or for anything else) it
    /// opens as before. As a standard user nothing changes.
    /// </summary>
    public static void OpenUrl(string url)
    {
        if (Elevation.IsAdministrator && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && GetShellWindow() != IntPtr.Zero)
        {
            try
            {
                // AbsoluteUri escapes quotes and spaces, so the link stays one argument.
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"\"{uri.AbsoluteUri}\"")
                {
                    UseShellExecute = false,
                })?.Dispose();
                return;
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>The desktop window of Windows' shell (Explorer); zero when no shell is running.</summary>
    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    private static partial IntPtr GetShellWindow();
}
