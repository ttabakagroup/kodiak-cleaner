namespace KodiakCleaner.Core.Cleaning;

public enum CleanTargetKind
{
    Files,
    RecycleBin,
    /// <summary>A Windows task rather than files (flush the DNS cache, turn hibernation off, ...); see <see cref="CleanTarget.TaskId"/>.</summary>
    Task,
}

/// <summary>One checkbox in the cleaner (e.g. "Chrome - Internet cache").</summary>
public sealed class CleanTarget
{
    public required string Id { get; init; }
    public required string Group { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public bool EnabledByDefault { get; init; }
    public bool RequiresAdmin { get; init; }
    public CleanTargetKind Kind { get; init; } = CleanTargetKind.Files;

    /// <summary>Process names (without .exe) that must not be running while this target is cleaned.</summary>
    public IReadOnlyList<string> BlockingProcesses { get; init; } = Array.Empty<string>();

    public IReadOnlyList<CleanRule> Rules { get; init; } = Array.Empty<CleanRule>();

    /// <summary>Where the program this item belongs to is usually installed; its own icon is shown beside the group.</summary>
    public IReadOnlyList<string> AppFiles { get; init; } = Array.Empty<string>();

    /// <summary>Shown only under Advanced Cleaning, and never ticked by default.</summary>
    public bool Advanced { get; init; }

    /// <summary>What can go wrong. When set, ticking the item asks first, Select All skips it, and the automatic cleans never run it.</summary>
    public string Warning { get; init; } = "";

    /// <summary>For <see cref="CleanTargetKind.Task"/> items: which Windows task to run.</summary>
    public string TaskId { get; init; } = "";
}
