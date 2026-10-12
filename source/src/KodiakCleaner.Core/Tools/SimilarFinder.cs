using System.Numerics;
using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tools;

/// <summary>A file in a similar set, with how alike it is to its closest match in the set and why.</summary>
public sealed record SimilarFile(DuplicateFile File, double Score, string Why);

/// <summary>Files that are alike without being identical. <see cref="Score"/> is the closest pair's likeness (0 to 1).</summary>
public sealed record SimilarGroup(double Score, IReadOnlyList<SimilarFile> Files);

/// <summary>How alike two files are, part by part (each 0 to 1; Picture is null when either isn't a readable picture).</summary>
public sealed record Likeness(double Name, double Size, double Content, double? Picture)
{
    /// <summary>
    /// One number for the pair. Contents count most; names and sizes only lift a pair whose contents are partly the same,
    /// so two different files that happen to share a name stay low.
    /// </summary>
    public double Score
    {
        get
        {
            var content = Math.Max(Content, Picture ?? 0);
            var byContent = content * 0.85 + Name * 0.10 + Size * 0.05;
            var byName = content * 0.60 + Name * 0.30 + Size * 0.10;
            // A re-saved or resized picture differs in every byte and size; how alike it looks is the measure.
            var byPicture = Picture ?? 0;
            // Identical files are listed as identical, so a similar pair tops out at 99% rather than rounding up to 100%.
            return Math.Clamp(Math.Max(Math.Max(byContent, byName), byPicture), 0, 0.99);
        }
    }

    /// <summary>A whole percent the same in every language ("85%").</summary>
    public static string Pct(double share) => Math.Round(share * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>A short sentence-case reason, for example "Same name, contents 85% the same".</summary>
    public string Why
    {
        get
        {
            var parts = new List<string>();
            if (Picture is >= SimilarFinder.PictureAlike) parts.Add($"pictures look {Pct(Picture.Value)} alike");
            if (Content >= 0.05) parts.Add($"contents {Pct(Math.Min(Content, 0.99))} the same");
            if (Name >= 0.999) parts.Add("same name");
            else if (Name >= 0.5) parts.Add($"names {Pct(Name)} alike");
            if (Size >= 0.999) parts.Add("same size");
            else if (Size >= 0.5) parts.Add($"sizes {Pct(Size)} alike");
            if (parts.Count == 0) return "";
            var text = string.Join(", ", parts);
            return char.ToUpperInvariant(text[0]) + text[1..];
        }
    }
}

/// <summary>
/// In-depth search for files that might be the same file: an edited copy, a re-saved picture, "Report (1).docx" next to
/// "Report.docx". Likely pairs are picked cheaply (similar names, nearby sizes, same-size files, pictures with a close
/// difference hash), then their contents are compared by content-defined chunks of the first 8 MB, so an insert near the
/// start doesn't make the rest look different. Nothing here decides what to remove; the person does.
/// </summary>
public static class SimilarFinder
{
    /// <summary>Picture likeness from which two pictures count as looking alike (at most 10 of 64 hash bits differ).</summary>
    public const double PictureAlike = 54.0 / 64;
    private const int ContentBytes = 8 * 1024 * 1024;
    private const int MaxPairs = 200_000;
    private const int MaxGroups = 2_000;

    private static readonly HashSet<string> PictureTypes = new(StringComparer.OrdinalIgnoreCase)
        { ".bmp", ".gif", ".heic", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp" };

    public static List<SimilarGroup> Find(IReadOnlyList<DuplicateFile> files, IReadOnlyList<DuplicateGroup> identical,
        DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress = null, CancellationToken ct = default)
    {
        // Identical copies are already listed as such; only the first of each set takes part, so a pair isn't reported twice.
        var copies = new HashSet<string>(identical.SelectMany(g => g.Files.Skip(1)).Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        var pool = files.Where(f => !copies.Contains(f.Path)).ToList();
        var index = pool.Select((f, i) => (f.Path, i)).ToDictionary(x => x.Path, x => x.i, StringComparer.OrdinalIgnoreCase);
        var pairs = new HashSet<(int, int)>();
        void AddPair(int a, int b)
        {
            if (a == b || pairs.Count >= MaxPairs) return;
            pairs.Add(a < b ? (a, b) : (b, a));
        }

        var keys = pool.Select(f => NameKey(Path.GetFileName(f.Path))).ToArray();
        var kinds = pool.Select(f => Kind(Path.GetExtension(f.Path))).ToArray();

        // 1. Same kind of file and the same name once copy marks are taken off ("Report (1)" and "Copy of Report").
        foreach (var bucket in Enumerable.Range(0, pool.Count).Where(i => keys[i].Length >= 3).GroupBy(i => (kinds[i], keys[i])))
        {
            var members = bucket.OrderBy(i => pool[i].Size).ToList();
            for (int a = 0; a < members.Count; a++)
                for (int b = a + 1; b < members.Count && b <= a + (members.Count <= 40 ? members.Count : 5); b++)
                    AddPair(members[a], members[b]);
        }

        // 2. Same kind, nearby sizes and alike names; 3. same kind and exactly the same size (an edited tag or a changed byte).
        foreach (var kind in Enumerable.Range(0, pool.Count).GroupBy(i => kinds[i]))
        {
            ct.ThrowIfCancellationRequested();
            var bySize = kind.OrderBy(i => pool[i].Size).ToList();
            for (int a = 0; a < bySize.Count; a++)
            {
                var size = pool[bySize[a]].Size;
                for (int b = a + 1; b < bySize.Count && b <= a + 8 && pool[bySize[b]].Size <= size + size / 7 + 1; b++)
                    if (pool[bySize[b]].Size == size || NameLikeness(keys[bySize[a]], keys[bySize[b]]) >= 0.6)
                        AddPair(bySize[a], bySize[b]);
            }
        }

        // 4. Pictures that look alike, whatever their names (needs the app's picture decoder).
        var pictureHashes = new Dictionary<int, ulong>();
        if (options.PictureHash is { } hashPicture)
        {
            var pictures = Enumerable.Range(0, pool.Count)
                .Where(i => PictureTypes.Contains(Path.GetExtension(pool[i].Path)) && pool[i].Size <= 100L * 1024 * 1024).ToList();
            for (int n = 0; n < pictures.Count; n++)
            {
                ct.ThrowIfCancellationRequested();
                if (hashPicture(pool[pictures[n]].Path) is { } h && IsUsefulPictureHash(h)) pictureHashes[pictures[n]] = h;
                if (n % 25 == 0) progress?.Report(new DuplicateProgress("Pictures", files.Count, n, pictures.Count));
            }
            var hashed = pictureHashes.ToList();
            for (int a = 0; a < hashed.Count; a++)
            {
                if (a % 1000 == 0) ct.ThrowIfCancellationRequested();
                for (int b = a + 1; b < hashed.Count; b++)
                    if (BitOperations.PopCount(hashed[a].Value ^ hashed[b].Value) <= 10) AddPair(hashed[a].Key, hashed[b].Key);
            }
        }

        // Compare every likely pair. Fingerprints are kept per file and chunk size, so each file is read at most once or twice.
        var prints = new Dictionary<(int, int), HashSet<ulong>?>();
        HashSet<ulong>? Print(int i, int bits)
        {
            if (!prints.TryGetValue((i, bits), out var p)) prints[(i, bits)] = p = Fingerprint(pool[i].Path, bits, ct);
            return p;
        }

        var accepted = new List<(int A, int B, Likeness L)>();
        int done = 0;
        foreach (var (a, b) in pairs.OrderBy(p => p.Item1).ThenBy(p => p.Item2))
        {
            ct.ThrowIfCancellationRequested();
            if (++done % 50 == 0) progress?.Report(new DuplicateProgress("Similar", files.Count, done, pairs.Count));
            var fa = pool[a];
            var fb = pool[b];
            var name = NameLikeness(keys[a], keys[b]);
            var size = Math.Min(fa.Size, fb.Size) / (double)Math.Max(1, Math.Max(fa.Size, fb.Size));
            double? picture = pictureHashes.TryGetValue(a, out var ha) && pictureHashes.TryGetValue(b, out var hb)
                ? 1 - BitOperations.PopCount(ha ^ hb) / 64.0 : null;
            double content = 0;
            if (kinds[a] == kinds[b])
            {
                var bits = ChunkBits(Math.Max(fa.Size, fb.Size));
                if (Print(a, bits) is { } pa && Print(b, bits) is { } pb) content = Jaccard(pa, pb);
            }
            var likeness = new Likeness(name, size, content, picture);
            // A pair needs some real overlap (shared contents or a look-alike picture); a shared name alone isn't enough.
            if (likeness.Score >= options.MinSimilarity && (content >= 0.15 || picture >= PictureAlike))
                accepted.Add((a, b, likeness));
        }
        progress?.Report(new DuplicateProgress("Similar", files.Count, pairs.Count, pairs.Count));

        return Group(pool, accepted);
    }

    /// <summary>Joins accepted pairs into sets; each file shows its closest match in its set. Most alike sets first.</summary>
    private static List<SimilarGroup> Group(List<DuplicateFile> pool, List<(int A, int B, Likeness L)> accepted)
    {
        var parent = new Dictionary<int, int>();
        int Root(int x)
        {
            while (parent[x] != x) x = parent[x] = parent[parent[x]];
            return x;
        }
        var best = new Dictionary<int, Likeness>();
        foreach (var (a, b, l) in accepted)
        {
            parent.TryAdd(a, a);
            parent.TryAdd(b, b);
            var ra = Root(a);
            var rb = Root(b);
            if (ra != rb) parent[rb] = ra;
            foreach (var x in new[] { a, b })
                if (!best.TryGetValue(x, out var have) || l.Score > have.Score) best[x] = l;
        }
        return best.Keys.GroupBy(Root)
            .Select(g => new SimilarGroup(g.Max(i => best[i].Score),
                g.OrderByDescending(i => best[i].Score).ThenBy(i => pool[i].Path, StringComparer.OrdinalIgnoreCase)
                 .Select(i => new SimilarFile(pool[i], best[i].Score, best[i].Why)).ToList()))
            .OrderByDescending(g => g.Score).ThenByDescending(g => g.Files.Sum(f => f.File.Size))
            .Take(MaxGroups).ToList();
    }

    private static readonly Regex[] CopyMarks =
    {
        new(@"^copy( \(\d+\))? of\s+", RegexOptions.CultureInvariant),
        new(@"\s*-\s*copy(\s*\(\d+\))?$", RegexOptions.CultureInvariant),
        new(@"\s*[\(\[]\d{1,3}[\)\]]$", RegexOptions.CultureInvariant),
        new(@"[\s_.-]+(copy|final|old|backup|bak|new|edited|edit|orig|original|v\d+(\.\d+)*)$", RegexOptions.CultureInvariant),
    };

    /// <summary>A file name without its extension, case or copy marks, punctuation turned into single spaces.</summary>
    public static string NameKey(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant().Trim();
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (var mark in CopyMarks)
            {
                var next = mark.Replace(stem, "").Trim();
                if (next.Length > 0 && next != stem) { stem = next; changed = true; }
            }
        }
        return Regex.Replace(stem, @"[^\p{L}\p{N}]+", " ").Trim();
    }

    /// <summary>
    /// How alike two name keys are (0 to 1), by edit distance. Names that differ only in their numbers ("IMG 0041" and
    /// "IMG 0042") are numbered series, usually different files, so they count for little.
    /// </summary>
    public static double NameLikeness(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;
        if (Regex.Replace(a, @"\d", "") == Regex.Replace(b, @"\d", "")) return 0.3;
        a = a.Length > 120 ? a[..120] : a;
        b = b.Length > 120 ? b[..120] : b;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return 1 - prev[b.Length] / (double)Math.Max(a.Length, b.Length);
    }

    /// <summary>Kind of file for comparing: the extension, with spellings of the same type joined and every picture type as one.</summary>
    private static string Kind(string extension)
    {
        var e = extension.ToLowerInvariant();
        if (PictureTypes.Contains(e)) return ".picture";
        return e switch { ".htm" => ".html", ".mpeg" => ".mpg", ".yml" => ".yaml", _ => e };
    }

    /// <summary>A blank or single-color picture hashes to almost all zeros or ones and would match every other one.</summary>
    private static bool IsUsefulPictureHash(ulong h) => BitOperations.PopCount(h) is >= 4 and <= 60;

    /// <summary>Average chunk size as a power of two: about 1/64 of the larger file, from 64 bytes to 4 KB.</summary>
    internal static int ChunkBits(long size)
    {
        var bits = 63 - BitOperations.LeadingZeroCount((ulong)Math.Max(1, Math.Min(size, ContentBytes) / 64));
        return Math.Clamp(bits, 6, 12);
    }

    public static double Jaccard(HashSet<ulong> a, HashSet<ulong> b)
    {
        if (a.Count == 0 && b.Count == 0) return 1;
        var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
        int shared = small.Count(large.Contains);
        return shared / (double)(a.Count + b.Count - shared);
    }

    private static readonly ulong[] Gear = MakeGear();
    private static ulong[] MakeGear()
    {
        // Fixed table, so fingerprints are the same from run to run.
        var g = new ulong[256];
        ulong x = 0x9E3779B97F4A7C15;
        for (int i = 0; i < 256; i++)
        {
            x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            g[i] = x;
        }
        return g;
    }

    /// <summary>
    /// Content-defined chunks of the file's first 8 MB, each as a 64-bit hash. A chunk ends where a rolling hash of the last
    /// bytes hits a pattern, so the same bytes give the same chunks even when something was inserted before them.
    /// Null when the file can't be read.
    /// </summary>
    internal static HashSet<ulong>? Fingerprint(string path, int bits, CancellationToken ct)
    {
        var mask = (1UL << bits) - 1;
        int min = 1 << (bits - 2), max = 1 << (bits + 3);
        var chunks = new HashSet<ulong>();
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);
            var buffer = new byte[1 << 16];
            ulong rolling = 0, chunk = 14695981039346656037UL;
            int length = 0, total = 0, n;
            while (total < ContentBytes && (n = stream.Read(buffer, 0, Math.Min(buffer.Length, ContentBytes - total))) > 0)
            {
                ct.ThrowIfCancellationRequested();
                total += n;
                for (int i = 0; i < n; i++)
                {
                    var b = buffer[i];
                    rolling = (rolling << 1) + Gear[b];
                    chunk = (chunk ^ b) * 1099511628211UL;
                    length++;
                    if ((length >= min && (rolling & mask) == 0) || length >= max)
                    {
                        chunks.Add(chunk ^ (ulong)length);
                        chunk = 14695981039346656037UL;
                        length = 0;
                    }
                }
            }
            if (length > 0) chunks.Add(chunk ^ (ulong)length);
            return chunks;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
