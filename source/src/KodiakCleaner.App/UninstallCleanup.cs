using System.IO;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Run by the MSI while Kodiak Cleaner is being removed (never on an upgrade), so it leaves nothing behind (Ted, 0.8.36).
/// <list type="bullet">
/// <item><c>--uninstall-user UILevel [DELETEUSERDATA]</c>, as the person removing it: removes the per-user registrations
/// (clean at Windows startup, the Recycle Bin menu), the downloaded-update folder, and asks whether to delete the backups
/// and logs in Documents\Kodiak Cleaner. A quiet uninstall (msiexec /qn) doesn't ask: it keeps them unless
/// DELETEUSERDATA=1 is given.</item>
/// <item><c>--uninstall-system</c>, elevated: deletes Kodiak Cleaner's Task Scheduler folder (Skip UAC and every
/// scheduled cleaning).</item>
/// </list>
/// </summary>
public static class UninstallCleanup
{
    public const string UserArg = "--uninstall-user", SystemArg = "--uninstall-system";

    public static bool IsCleanupArg(string[] args) =>
        args.Length > 0 && (args[0].Equals(UserArg, StringComparison.OrdinalIgnoreCase) || args[0].Equals(SystemArg, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Called before WPF starts: runs the cleanup that never shows a window (the elevated part, and a quiet uninstall
    /// that doesn't ask). False when there is nothing of that kind to do, so Kodiak Cleaner starts as usual.
    /// </summary>
    public static bool TryRunWithoutWindow(string[] args)
    {
        if (!IsCleanupArg(args)) return false;
        if (args[0].Equals(SystemArg, StringComparison.OrdinalIgnoreCase))
        {
            Try(DeleteScheduledTasks);
            return true;
        }
        var (uiLevel, delete) = Read(args);
        if (delete is null && uiLevel > 2) return false; // it will ask, which needs WPF: App.Start calls TryRun
        Try(() =>
        {
            Settings.Load();
            RunForUser(uiLevel, delete);
        });
        return true;
    }

    /// <summary>The cleanup that asks, from App.Start (WPF and the theme are ready). False for any other start.</summary>
    public static bool TryRun(string[] args)
    {
        if (!IsCleanupArg(args)) return false;
        var (uiLevel, delete) = Read(args);
        Try(() => RunForUser(uiLevel, delete));
        return true;
    }

    // msiexec UILevel: 2 = no UI (/qn); 3 and up show a window, so a question can be asked. DELETEUSERDATA: 1 deletes, 0 keeps.
    private static (int UiLevel, bool? Delete) Read(string[] args) =>
        (args.Length > 1 && int.TryParse(args[1], out var l) ? l : 5,
         args.Length > 2 && args[2].Length > 0 ? args[2] == "1" : null);

    private static void RunForUser(int uiLevel, bool? delete)
    {
        Try(() => Integration.SetCleanOnStartup(false));
        Try(() => Integration.SetRecycleBinMenu(false));
        Try(() =>
        {
            var updates = Path.Combine(Path.GetTempPath(), AppInfo.Id + "-Update");
            if (Directory.Exists(updates)) Directory.Delete(updates, recursive: true);
        });

        Settings.PrepareDataFolders(); // also moves backups out of Program Files first, so a Keep keeps them
        var data = Settings.Data;
        if (!Directory.Exists(data.Root)) return;
        if (!Directory.EnumerateFiles(data.Root, "*", SearchOption.AllDirectories).Any())
        {
            Try(data.DeleteAll); // only empty folders: nothing to ask about
            return;
        }
        bool remove = delete ?? (uiLevel > 2 && Ask(data));
        ActivityLog.Write("Uninstall", $"{AppInfo.Name} {AppInfo.VersionName} was uninstalled; backups, logs and reports " + (remove ? "deleted." : $"kept in {data.Root}."));
        if (remove) Try(data.DeleteAll);
    }

    private static bool Ask(DataFolders data)
    {
        ThemeManager.Apply();
        return Dialogs.Choose(MessageKind.Question, "Remove Backups, Logs and Reports Too?",
            $"{AppInfo.Name} is being removed. Its backups, activity logs and reports are in:\n\n{data.Root}\n\n" +
            "Delete them as well, so nothing is left behind?",
            "Registry backups let you undo registry fixes. Keep them if you might still need them.",
            new[] { "Delete Them", "Keep Them" }, defaultButton: 1, cancelButton: 1) == 0;
    }

    /// <summary>Deletes every task in Task Scheduler's Kodiak Cleaner folder, then the folder.</summary>
    private static void DeleteScheduledTasks()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service");
        if (type is null) return;
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        dynamic root = service.GetFolder("\\");
        dynamic folder;
        try { folder = service.GetFolder("\\" + AppInfo.Id); }
        catch (System.Runtime.InteropServices.COMException) { return; } // no folder: nothing was scheduled
        foreach (dynamic task in folder.GetTasks(1)) // 1 = include hidden tasks
            folder.DeleteTask(task.Name, 0);
        root.DeleteFolder(AppInfo.Id, 0);
    }

    /// <summary>
    /// Cleanup is best effort: whatever goes wrong (a missing Task Scheduler, a locked file), it must never show an error
    /// or stop Windows from removing Kodiak Cleaner, so every exception is swallowed here.
    /// </summary>
    private static void Try(Action a)
    {
        try { a(); }
        catch (Exception) { }
    }
}
