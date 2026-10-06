using System.Diagnostics;
using KodiakCleaner.Core.Cleaning;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.App;

/// <summary>Analyze/clean for one cleaner item. Shared by the Cleaner page and the /autoclean startup run.</summary>
public sealed class TargetRun
{
    public TargetRun(CleanTarget target) => Target = target;
    public CleanTarget Target { get; }
    public AnalysisResult? Files { get; private set; }
    public RecycleBinAnalysis? Bin { get; private set; }
    public string? SkippedBecause { get; private set; }
    /// <summary>For a Windows task: what it would do.</summary>
    public SystemTasks.Plan? Plan { get; private set; }

    public long Bytes => Files?.TotalBytes ?? Bin?.Bytes ?? Plan?.Bytes ?? 0;
    public long Count => Files?.Files.Count ?? Bin?.Items ?? 0;
    public bool Analyzed => Files is not null || Bin is not null || Plan is not null;
    /// <summary>True when the last <see cref="Clean"/> really ran (it wasn't skipped), so it counts as cleaned.</summary>
    public bool Ran { get; private set; }

    /// <summary>The Result column after an analysis.</summary>
    public string AnalyzedStatus => Plan is { } plan ? plan.Note
        : Plural(Count, Target.Kind == CleanTargetKind.RecycleBin ? "item" : "file");

    public static string? RunningBlocker(CleanTarget t)
    {
        foreach (var name in t.BlockingProcesses)
        {
            var procs = Process.GetProcessesByName(name);
            try { if (procs.Length > 0) return name; }
            finally { foreach (var p in procs) p.Dispose(); }
        }
        return null;
    }

    public void Analyze(CancellationToken ct)
    {
        Files = null;
        Bin = null;
        Plan = null;
        if (Target.RequiresAdmin && Target.Kind == CleanTargetKind.Task && !Elevation.IsAdministrator)
        {
            SkippedBecause = "Skipped: needs administrator";
            return;
        }
        SkippedBecause = RunningBlocker(Target) is { } proc ? $"Skipped: close {Target.Group} first ({proc}.exe is running)" : null;
        if (SkippedBecause is not null) return;
        if (Target.Kind == CleanTargetKind.RecycleBin) Bin = RecycleBinCleaner.Analyze(Settings.Current.RecycleBinMinAge);
        else if (Target.Kind == CleanTargetKind.Task) Plan = SystemTasks.Analyze(Target.TaskId);
        else Files = FileCleaner.Analyze(Target.Rules, ct);
    }

    /// <returns>Bytes freed, items that could not be removed, and a status line.</returns>
    public (long Freed, int Failed, string Status) Clean(CancellationToken ct)
    {
        Ran = false;
        if (!Analyzed) Analyze(ct);
        if (SkippedBecause is not null) return (0, 0, SkippedBecause);
        if (RunningBlocker(Target) is { } proc) return (0, 0, $"Skipped: close {Target.Group} first ({proc}.exe is running)");
        Ran = true;

        if (Plan is not null)
        {
            Plan = null;
            var (bytes, status) = SystemTasks.Run(Target.TaskId, ct);
            return (bytes, 0, status);
        }
        if (Bin is { } bin)
        {
            var (freed, failed) = RecycleBinCleaner.Clean(bin);
            Bin = null;
            return (freed, failed, failed > 0 ? $"Emptied; {failed} item(s) could not be removed" : "Emptied");
        }
        var r = FileCleaner.Clean(Files!, ct);
        Files = null;
        return (r.BytesFreed, r.FilesFailed, r.FilesFailed > 0
            ? $"Removed {Plural(r.FilesDeleted, "file")}; {r.FilesFailed:N0} in use or protected"
            : $"Removed {Plural(r.FilesDeleted, "file")}");
    }

    /// <summary>"1 file", "2 files".</summary>
    public static string Plural(long n, string noun) => $"{n:N0} {noun}{(n == 1 ? "" : "s")}";

    public static IReadOnlyList<CleanTarget> Catalog() =>
        CleanCatalog.Build(Settings.Current.TempMinAge, Settings.Current.RecycleBinMinAge);
}

public static class AutoClean
{
    /// <summary>Runs the cleaner with the saved selections and no window (used at Windows startup and from the jump list).</summary>
    public static void Run()
    {
        var selected = Settings.Current.CheckedTargets?.ToHashSet();
        var (freed, cleaned) = RunItems(TargetRun.Catalog().Where(t => selected?.Contains(t.Id) ?? t.EnabledByDefault));
        ActivityLog.Write("Cleaner", $"Cleaned {cleaned} item(s) without a window (Windows startup or jump list), {ByteSize.Format(freed)} freed.");
    }

    /// <summary>
    /// Cleans <paramref name="targets"/> unattended and records each one in the Cleaner history. Items with a warning only run
    /// when you click Run Cleaner yourself, never unattended. Returns the bytes freed and how many items ran.
    /// </summary>
    public static (long Freed, int Cleaned) RunItems(IEnumerable<CleanTarget> targets)
    {
        long freed = 0;
        int cleaned = 0;
        foreach (var t in targets.Where(t => t.Warning.Length == 0))
        {
            try
            {
                var run = new TargetRun(t);
                var (f, _, _) = run.Clean(CancellationToken.None);
                if (!run.Ran) continue;
                Settings.Current.History.Record(t.Id, DateTime.UtcNow, f);
                freed += f;
                cleaned++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        Settings.Current.Save();
        return (freed, cleaned);
    }
}
