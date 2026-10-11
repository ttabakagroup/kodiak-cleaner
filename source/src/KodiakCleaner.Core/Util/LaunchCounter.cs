namespace KodiakCleaner.Core.Util;

/// <summary>
/// Which starts count toward the donation reminder (Ted, 0.8.64): every start a person makes, from 1 on the very first one,
/// but not the copies Kodiak Cleaner reopens by itself (after Restart as Administrator, or after installing an update), which
/// used to make one opening count twice.
/// </summary>
public static class LaunchCounter
{
    /// <summary>Given to the copy that Restart as Administrator opens.</summary>
    public const string RestartedArg = "--restarted";
    /// <summary>Given to the copy that reopens after an update installs.</summary>
    public const string UpdatedArg = "--updated";

    public static bool CountsAsStart(IEnumerable<string> args) =>
        !args.Any(a => a.Equals(RestartedArg, StringComparison.OrdinalIgnoreCase) || a.Equals(UpdatedArg, StringComparison.OrdinalIgnoreCase));

    public static int Next(int count) => count < int.MaxValue ? Math.Max(0, count) + 1 : count;
}
