namespace KodiakCleaner.Core.Tools;

public sealed record SystemFact(string Group, string Name, string Value);

/// <summary>Turns System Information's facts into plain text for Copy to Clipboard. Nothing is sent anywhere.</summary>
public static class SystemReport
{
    public static string ToText(IEnumerable<SystemFact> facts, string title)
    {
        var lines = new List<string> { title };
        foreach (var group in facts.GroupBy(f => f.Group))
        {
            lines.Add("");
            lines.Add(group.Key);
            lines.AddRange(group.Select(f => $"  {f.Name}: {f.Value}"));
        }
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>"3 days, 4 hours" style uptime.</summary>
    public static string Uptime(TimeSpan span)
    {
        string Part(int n, string unit) => $"{n} {unit}{(n == 1 ? "" : "s")}";
        if (span.TotalDays >= 1) return $"{Part((int)span.TotalDays, "day")}, {Part(span.Hours, "hour")}";
        if (span.TotalHours >= 1) return $"{Part(span.Hours, "hour")}, {Part(span.Minutes, "minute")}";
        return Part(Math.Max(0, span.Minutes), "minute");
    }
}
