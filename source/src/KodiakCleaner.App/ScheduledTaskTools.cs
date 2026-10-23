using System.IO;
using System.Runtime.InteropServices;
using KodiakCleaner.App.ViewModels;
using KodiakCleaner.Core.Tools;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>
/// Deleting, backing up and restoring scheduled tasks (Ted, 2026-10-23). A task is always saved to
/// Backups\Scheduled Tasks before it is deleted or replaced, and a task whose backup fails is left alone.
/// </summary>
public static class ScheduledTaskTools
{
    /// <summary>What Back Up, Delete and Restore say about a task that signs in with a saved password (Ted, 2026-10-23).</summary>
    public const string PasswordCaution =
        "Windows never gives out a task's saved password, so backing up a password-protected task may fail, and it may not be possible " +
        "to restore it from the backup. Kodiak Cleaner can't put these back itself; Task Scheduler's Import Task can, if you type the account's password again.";

    public static ScheduledTaskBackups Backups => new(Path.Combine(Settings.Data.Backups, ScheduledTaskBackups.FolderName));

    /// <summary>Task Scheduler's COM service, connected as the current user.</summary>
    public static dynamic Connect()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new COMException("Task Scheduler isn't available.");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    /// <summary>Saves the task's XML. Returns null when saved, else what went wrong.</summary>
    public static string? BackUp(ScheduledTaskItem task, string reason)
    {
        try
        {
            var path = Backups.Save(task.TaskPath, task.ReadXml(), task.Program, reason, DateTime.Now);
            ActivityLog.Write("Scheduled Tasks", $"Backed up task {task.TaskPath} to {path}");
            return null;
        }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e) || e is IOException or InvalidDataException)
        {
            if (task.UsesPassword) return "Windows wouldn't give out this password-protected task.";
            return e is UnauthorizedAccessException or COMException ? "Windows wouldn't give out this task (try Restart as Administrator)." : e.Message;
        }
    }

    /// <summary>Backs the task up, then deletes it. Returns null when deleted, else why it wasn't.</summary>
    public static string? Delete(ScheduledTaskItem task)
    {
        if (BackUp(task, ScheduledTaskBackups.BeforeDelete) is { } problem) return "Not deleted, because it couldn't be backed up first: " + problem;
        try
        {
            dynamic folder = Connect().GetFolder(task.Folder);
            folder.DeleteTask(task.Name, 0);
            ActivityLog.Write("Scheduled Tasks", $"Deleted task {task.TaskPath} (backup kept)");
            return null;
        }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e))
        {
            return NeedsAdmin(e) ? "Deleting it needs administrator rights. Use Restart as Administrator at the top." : e.Message;
        }
    }

    /// <summary>
    /// Registers the task again from its backup, in its old folder (made again if it's gone). A task of the same name that is
    /// there now is backed up first and replaced. Returns null when restored, else why it wasn't.
    /// </summary>
    public static string? Restore(TaskBackup backup, ScheduledTaskItem? current)
    {
        string xml;
        try { xml = File.ReadAllText(Backups.PathOf(backup)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return "The backup file can't be read: " + e.Message; }
        if (current is not null && BackUp(current, ScheduledTaskBackups.BeforeRestore) is { } problem)
            return "Not restored, because the task there now couldn't be backed up first: " + problem;
        try
        {
            dynamic service = Connect();
            dynamic folder = FolderFor(service, backup.Folder);
            dynamic definition = service.NewTask(0);
            definition.XmlText = xml;
            int logon = definition.Principal.LogonType;
            if (logon == 1) // TASK_LOGON_PASSWORD: Windows needs the account's password again
                return "This task is password-protected, and a backup can't hold the password. Open Task Scheduler, choose Import Task, pick the backup file, then type the account's password.";
            string? user = Text(() => definition.Principal.UserId) ?? Text(() => definition.Principal.GroupId);
            folder.RegisterTaskDefinition(backup.Name, definition, 6, user, null, logon, null); // 6 = create or update
            ActivityLog.Write("Scheduled Tasks", $"Restored task {backup.TaskPath} from {backup.File}");
            return null;
        }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e))
        {
            return NeedsAdmin(e) ? "Restoring it needs administrator rights. Use Restart as Administrator at the top." : e.Message;
        }
    }

    private static dynamic FolderFor(dynamic service, string path)
    {
        dynamic folder = service.GetFolder(@"\");
        foreach (var part in path.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            try { folder = folder.GetFolder(part); }
            catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e)) { folder = folder.CreateFolder(part, null); }
        }
        return folder;
    }

    private static string? Text(Func<object?> read)
    {
        try { return read() is string s && s.Length > 0 ? s : null; }
        catch (Exception e) when (ScheduledTasksViewModel.IsSchedulerError(e)) { return null; }
    }

    private static bool NeedsAdmin(Exception e) => e is UnauthorizedAccessException || e.HResult == unchecked((int)0x80070005);
}
