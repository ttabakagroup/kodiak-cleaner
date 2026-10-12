using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tools;

/// <summary>Letter case for the new name, A to Z (As Is first by the alphabet, and the default).</summary>
public enum RenameCase { AsIs, Lower, Sentence, Title, Upper }

/// <summary>What happens to each file's extension, A to Z.</summary>
public enum RenameExtension { AsIs, ChangeTo, Lower, Upper }

/// <summary>The order files are numbered in, A to Z.</summary>
public enum RenameOrder { DateCreated, DateModified, Name, Size }

/// <summary>One file to rename. Taken is a picture's Date Taken, read by the app when the pattern uses it.</summary>
public sealed record RenameFile(string Path, long Size, DateTime Modified, DateTime Created, DateTime? Taken = null)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
}

/// <summary>
/// The rules Mass Rename applies to every file, in this order: the pattern builds the new name, Find and Replace edits it,
/// then letter case, tidy spaces and the extension.
/// </summary>
public sealed class RenameRules
{
    public const string DefaultPattern = "{name}";

    public string Pattern { get; set; } = DefaultPattern;
    public string Find { get; set; } = "";
    public string Replace { get; set; } = "";
    public bool MatchCase { get; set; }
    public bool UseRegex { get; set; }
    public RenameCase Case { get; set; } = RenameCase.AsIs;
    public RenameExtension Extension { get; set; } = RenameExtension.AsIs;
    public string NewExtension { get; set; } = "";
    public bool TidySpaces { get; set; } = true;
    public int Start { get; set; } = 1;
    public int Step { get; set; } = 1;
    public int Digits { get; set; } = 1;
    public RenameOrder Order { get; set; } = RenameOrder.Name;
}

public enum RenameState { Ready, Unchanged, Problem, Skipped }

/// <summary>One row of the preview: the file, the name it would get, and whether it can.</summary>
public sealed record RenamePlanItem(RenameFile File, string NewName, RenameState State, string Note)
{
    public string NewPath => Path.Combine(File.Folder, NewName);
}

/// <summary>A rename that happened, kept so it can be undone.</summary>
public sealed record RenameMove(string From, string To);

public sealed record RenameOutcome(IReadOnlyList<RenameMove> Done, IReadOnlyList<(string Path, string Reason)> Failed);

/// <summary>
/// Tools › Mass Rename (Ted, 2026-10-12): renames a set of files in a folder by a pattern. Nothing is renamed until the
/// preview is accepted; every rename is written to a journal first so Undo Last Rename can put the names back.
/// </summary>
public static class MassRenamer
{
    /// <summary>The tokens a pattern can use, A to Z, with what each one becomes.</summary>
    public static readonly IReadOnlyList<(string Token, string Meaning)> Tokens = new[]
    {
        ("{created}", "date created, as 2026-10-12 (or {created:yyyy-MM-dd HH.mm})"),
        ("{ext}", "the current extension, without the dot"),
        ("{folder}", "the name of the folder the file is in"),
        ("{modified}", "date modified (or {modified:yyyy-MM-dd HH.mm})"),
        ("{name}", "the current name without the extension (or {name:1-5} for characters 1 to 5)"),
        ("{num}", "a number that counts up, padded to the Digits setting (or {num:3} for 3 digits)"),
        ("{taken}", "a picture's date taken, or date modified when it has none"),
    };

    private static readonly Regex TokenPattern = new(@"\{(name|ext|num|folder|modified|created|taken)(?::([^{}]*))?\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly HashSet<string> MinorWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "as", "at", "but", "by", "for", "from", "if", "in", "nor", "of", "on", "or", "so", "the", "to", "up", "vs", "via", "with", "yet",
    };

    /// <summary>True when the pattern needs a picture's Date Taken, which is slower to read.</summary>
    public static bool UsesTaken(string pattern) => pattern.Contains("{taken", StringComparison.OrdinalIgnoreCase);

    /// <summary>Puts the files in numbering order.</summary>
    public static IEnumerable<RenameFile> Ordered(IEnumerable<RenameFile> files, RenameOrder order) => order switch
    {
        RenameOrder.DateCreated => files.OrderBy(f => f.Created).ThenBy(f => f.Path, NaturalComparer.Instance),
        RenameOrder.DateModified => files.OrderBy(f => f.Modified).ThenBy(f => f.Path, NaturalComparer.Instance),
        RenameOrder.Size => files.OrderBy(f => f.Size).ThenBy(f => f.Path, NaturalComparer.Instance),
        _ => files.OrderBy(f => f.Folder, NaturalComparer.Instance).ThenBy(f => f.Name, NaturalComparer.Instance),
    };

    /// <summary>
    /// The new name for one file. Index counts from 0 in numbering order. Throws ArgumentException when Find is not a valid
    /// regular expression.
    /// </summary>
    public static string NewName(RenameFile file, RenameRules rules, int index)
    {
        var name = file.Name;
        var dot = name.LastIndexOf('.');
        // ".gitignore" is a name with no extension, the way File Explorer shows it.
        var (stem, ext) = dot > 0 ? (name[..dot], name[(dot + 1)..]) : (name, "");

        var result = TokenPattern.Replace(rules.Pattern ?? "", m => Expand(m.Groups[1].Value.ToLowerInvariant(),
            m.Groups[2].Success ? m.Groups[2].Value : null, file, stem, ext, rules, index));

        if (!string.IsNullOrEmpty(rules.Find))
        {
            if (rules.UseRegex)
            {
                var options = RegexOptions.CultureInvariant | (rules.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                result = Regex.Replace(result, rules.Find, rules.Replace ?? "", options, TimeSpan.FromSeconds(1));
            }
            else
                result = result.Replace(rules.Find, rules.Replace ?? "", rules.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        result = ApplyCase(result, rules.Case);
        if (rules.TidySpaces) result = Regex.Replace(result, @"\s+", " ").Trim();

        var newExt = rules.Extension switch
        {
            RenameExtension.ChangeTo => (rules.NewExtension ?? "").Trim().TrimStart('.'),
            RenameExtension.Lower => ext.ToLowerInvariant(),
            RenameExtension.Upper => ext.ToUpperInvariant(),
            _ => ext,
        };
        return newExt.Length == 0 ? result : result + "." + newExt;
    }

    private static string Expand(string token, string? arg, RenameFile file, string stem, string ext, RenameRules rules, int index)
    {
        switch (token)
        {
            case "name":
                return arg is null ? stem : Slice(stem, arg);
            case "ext":
                return ext;
            case "folder":
                return Path.GetFileName(Path.TrimEndingDirectorySeparator(file.Folder));
            case "num":
                var digits = int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? d : rules.Digits;
                var value = (long)rules.Start + (long)index * rules.Step;
                var text = Math.Abs(value).ToString(CultureInfo.InvariantCulture).PadLeft(Math.Clamp(digits, 1, 12), '0');
                return value < 0 ? "-" + text : text;
            default:
                var when = token switch
                {
                    "created" => file.Created,
                    "taken" => file.Taken ?? file.Modified,
                    _ => file.Modified,
                };
                var format = string.IsNullOrWhiteSpace(arg) ? "yyyy-MM-dd" : arg;
                try { return when.ToString(format, CultureInfo.CurrentCulture); }
                catch (FormatException) { return when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        }
    }

    /// <summary>"3" is the first 3 characters, "2-5" characters 2 to 5, "-4" the last 4 (counted from 1, like a person would).</summary>
    private static string Slice(string text, string arg)
    {
        var m = Regex.Match(arg.Trim(), @"^(\d*)(-)?(\d*)$");
        if (!m.Success) return text;
        int.TryParse(m.Groups[1].Value, out var a);
        int.TryParse(m.Groups[3].Value, out var b);
        if (!m.Groups[2].Success) return a <= 0 ? text : text[..Math.Min(a, text.Length)];          // "3"
        if (m.Groups[1].Value.Length == 0) return b <= 0 ? text : text[Math.Max(0, text.Length - b)..]; // "-4"
        var from = Math.Max(1, a) - 1;
        if (from >= text.Length) return "";
        var to = m.Groups[3].Value.Length == 0 ? text.Length : Math.Min(text.Length, Math.Max(b, from + 1));     // "2-" or "2-5"
        return text[from..to];
    }

    public static string ApplyCase(string text, RenameCase mode)
    {
        switch (mode)
        {
            case RenameCase.Lower: return text.ToLower(CultureInfo.CurrentCulture);
            case RenameCase.Upper: return text.ToUpper(CultureInfo.CurrentCulture);
            case RenameCase.Title:
                // Ted's Title Case: every word starts with a capital except short joining words (of, the, and...) inside the
                // name; the rest of each word is made lower case, and apostrophes stay inside words.
                var words = Regex.Matches(text, @"[\p{L}\p{N}]+(?:['’][\p{L}\p{N}]+)*").ToList();
                var sb = new StringBuilder(text.ToLower(CultureInfo.CurrentCulture));
                for (int w = 0; w < words.Count; w++)
                {
                    var word = words[w].Value.ToLower(CultureInfo.CurrentCulture);
                    if (w > 0 && w < words.Count - 1 && MinorWords.Contains(word)) continue;
                    sb[words[w].Index] = char.ToUpper(sb[words[w].Index], CultureInfo.CurrentCulture);
                }
                return sb.ToString();
            case RenameCase.Sentence:
                var lower = text.ToLower(CultureInfo.CurrentCulture);
                var i = 0;
                while (i < lower.Length && !char.IsLetter(lower[i])) i++;
                return i < lower.Length ? lower[..i] + char.ToUpper(lower[i], CultureInfo.CurrentCulture) + lower[(i + 1)..] : lower;
            default: return text;
        }
    }

    /// <summary>Why Windows wouldn't accept this file name, or null when it's fine.</summary>
    public static string? WhyInvalid(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.') && name.Trim('.').Length == 0) return "The new name is empty.";
        var bad = name.FirstOrDefault(c => c < 32 || "\\/:*?\"<>|".Contains(c));
        if (bad != default) return bad < 32 ? "The new name has a hidden control character." : $"Windows doesn't allow {bad} in a name.";
        if (name.EndsWith(' ') || name.EndsWith('.')) return "A name can't end with a space or a dot.";
        if (name.Length > 255) return "The new name is longer than Windows allows (255 characters).";
        var first = name.Split('.')[0].TrimEnd();
        if (Reserved.Contains(first)) return $"{first.ToUpperInvariant()} is a name Windows keeps for itself.";
        return null;
    }

    /// <summary>
    /// The preview. Files whose path is in <paramref name="skipped"/> keep their name and aren't counted. <paramref name="existing"/>
    /// lists every name already in each folder (any case), so nothing is read from disk here.
    /// </summary>
    public static IReadOnlyList<RenamePlanItem> Plan(IEnumerable<RenameFile> files, RenameRules rules,
        IReadOnlyDictionary<string, HashSet<string>> existing, ISet<string>? skipped = null)
    {
        var ordered = Ordered(files, rules.Order).ToList();
        var names = new List<(RenameFile File, string? Name, string? Error)>();
        int index = 0;
        string? patternError = null;
        foreach (var f in ordered)
        {
            if (skipped?.Contains(f.Path) == true)
            {
                names.Add((f, null, null));
                continue;
            }
            try { names.Add((f, NewName(f, rules, index++), null)); }
            catch (ArgumentException)
            {
                patternError ??= "Find isn't a valid regular expression.";
                names.Add((f, f.Name, patternError));
            }
            catch (RegexMatchTimeoutException)
            {
                patternError ??= "Find took too long to match. Try a simpler expression.";
                names.Add((f, f.Name, patternError));
            }
        }

        // Every final path, to catch two files getting the same name, and every file that will move out of its name.
        var leaving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (f, n, _) in names)
        {
            var final = n is null ? f.Path : Path.Combine(f.Folder, n);
            targets[final] = targets.TryGetValue(final, out var c) ? c + 1 : 1;
            if (n is not null && !string.Equals(n, f.Name, StringComparison.OrdinalIgnoreCase)) leaving.Add(f.Path);
        }

        var plan = new List<RenamePlanItem>(names.Count);
        foreach (var (f, n, error) in names)
        {
            if (n is null) { plan.Add(new(f, f.Name, RenameState.Skipped, "Not ticked")); continue; }
            if (error is not null) { plan.Add(new(f, n, RenameState.Problem, error)); continue; }
            if (string.Equals(n, f.Name, StringComparison.Ordinal)) { plan.Add(new(f, n, RenameState.Unchanged, "No change")); continue; }
            if (WhyInvalid(n) is { } why) { plan.Add(new(f, n, RenameState.Problem, why)); continue; }
            var final = Path.Combine(f.Folder, n);
            if (final.Length >= 260) { plan.Add(new(f, n, RenameState.Problem, "The full path would be longer than Windows allows.")); continue; }
            if (targets[final] > 1) { plan.Add(new(f, n, RenameState.Problem, "Another file would get the same name.")); continue; }
            // A name already in the folder is only a problem when that file isn't being renamed too (a case-only change is fine).
            var caseOnly = string.Equals(n, f.Name, StringComparison.OrdinalIgnoreCase);
            if (!caseOnly && existing.TryGetValue(f.Folder, out var there) && there.Contains(n) && !leaving.Contains(final))
            {
                plan.Add(new(f, n, RenameState.Problem, "A file with this name is already in the folder."));
                continue;
            }
            plan.Add(new(f, n, RenameState.Ready, caseOnly ? "Letter case only" : ""));
        }
        return plan;
    }

    /// <summary>
    /// Renames in two steps (each file to a temporary name, then to its new name) so files can swap names and a change of
    /// letter case alone works. A file that fails keeps its old name. <paramref name="journal"/> is written before anything
    /// changes and rewritten at the end with what was actually done.
    /// </summary>
    public static RenameOutcome Rename(IReadOnlyList<RenameMove> moves, string? journal, CancellationToken token = default)
    {
        var failed = new List<(string, string)>();
        var staged = new List<(RenameMove Move, string Temp)>();
        if (journal is not null) RenameJournal.Save(journal, new RenameJournal(DateTime.Now, moves.ToList(), Done: false));

        foreach (var m in moves)
        {
            if (token.IsCancellationRequested) break;
            var temp = Path.Combine(Path.GetDirectoryName(m.From) ?? "", $"~kodiak-rename-{Guid.NewGuid():N}.tmp");
            try
            {
                File.Move(m.From, temp);
                staged.Add((m, temp));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { failed.Add((m.From, Reason(e))); }
        }

        var done = new List<RenameMove>();
        foreach (var (m, temp) in staged)
        {
            try
            {
                File.Move(temp, m.To);
                done.Add(m);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed.Add((m.From, Reason(e)));
                try { File.Move(temp, m.From); }
                catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException)
                {
                    // Can't happen in practice (its old name was just freed), but never lose track of a file.
                    done.Add(new RenameMove(m.From, temp));
                }
            }
        }

        if (journal is not null)
        {
            if (done.Count > 0) RenameJournal.Save(journal, new RenameJournal(DateTime.Now, done, Done: true));
            else RenameJournal.Delete(journal);
        }
        return new RenameOutcome(done, failed);
    }

    /// <summary>Puts back the names in a journal. Files renamed again or removed since then are left alone.</summary>
    public static RenameOutcome Undo(RenameJournal journal, string? path)
    {
        var back = new List<RenameMove>();
        var failed = new List<(string, string)>();
        // An old name held by another file of the same rename is fine (files that swapped names swap back).
        var freed = new HashSet<string>(journal.Moves.Where(m => File.Exists(m.To)).Select(m => m.To), StringComparer.OrdinalIgnoreCase);
        foreach (var m in journal.Moves)
        {
            if (!File.Exists(m.To)) failed.Add((m.To, "It was moved, renamed or removed since."));
            else if (File.Exists(m.From) && !freed.Contains(m.From) && !string.Equals(m.From, m.To, StringComparison.OrdinalIgnoreCase))
                failed.Add((m.To, "Another file now has its old name."));
            else back.Add(new RenameMove(m.To, m.From));
        }
        var outcome = Rename(back, journal: null);
        if (path is not null) RenameJournal.Delete(path);
        return new RenameOutcome(outcome.Done, failed.Concat(outcome.Failed).ToList());
    }

    private static string Reason(Exception e) => e switch
    {
        UnauthorizedAccessException => "Access was denied.",
        FileNotFoundException => "It's no longer there.",
        _ when e.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021) => "It's open in another program.",
        _ => "Windows couldn't rename it.",
    };
}

/// <summary>The last rename, saved as JSON so it can be undone after Kodiak Cleaner is closed and opened again.</summary>
public sealed record RenameJournal(DateTime When, List<RenameMove> Moves, bool Done)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static RenameJournal? Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<RenameJournal>(File.ReadAllText(path), Json) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public static void Save(string path, RenameJournal journal)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(journal, Json), Encoding.UTF8);
        File.Move(temp, path, overwrite: true);
    }

    public static void Delete(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>Orders "file 2" before "file 10", the way File Explorer does.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null) return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var a = x[si..i].TrimStart('0');
                var b = y[sj..j].TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                var c = string.CompareOrdinal(a, b);
                if (c != 0) return c;
            }
            else
            {
                var c = string.Compare(x[i].ToString(), y[j].ToString(), StringComparison.CurrentCultureIgnoreCase);
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
