namespace KodiakCleaner.Core.RegistryTools;

public sealed class RegistryIssue
{
    public required string Category { get; init; }
    public required string Description { get; init; }
    public required RegistryLocation Location { get; init; }
    public string Data { get; init; } = "";
}
