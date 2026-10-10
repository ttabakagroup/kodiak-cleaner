namespace KodiakCleaner.Core.Util;

/// <summary>Builds the "Label: value" lines shown when a row is hovered in Registry, Startup and Programs.</summary>
public static class DetailText
{
    /// <summary>One line per part, in the order given; parts with no value are left out.</summary>
    public static string Lines(params (string Label, string? Value)[] parts) =>
        string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => $"{p.Label}: {p.Value!.Trim()}"));
}
