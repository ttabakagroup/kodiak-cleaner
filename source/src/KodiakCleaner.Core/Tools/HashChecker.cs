using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace KodiakCleaner.Core.Tools;

public enum HashKind { Md5, Sha1, Sha256, Sha384, Sha512 }

/// <summary>One hash from a pasted or loaded list, with the file name it was listed for (null for a bare hash).</summary>
public sealed record ExpectedHash(HashKind Kind, string Hash, string? Name, int Line);

/// <summary>Every hash of one file, lowercase hex. A kind Windows refuses (MD5 under a FIPS policy) is left out.</summary>
public sealed record FileHashes(string Path, long Size, IReadOnlyDictionary<HashKind, string> Hashes);

public enum HashVerdict { NoList, Verified, Mismatch, NotInList }

/// <summary>How one file compares with the list: the entry that decided it, and which kind of hash matched.</summary>
public sealed record HashCheck(HashVerdict Verdict, HashKind? Kind, ExpectedHash? Entry);

/// <summary>
/// Hash Checker: works out MD5, SHA-1, SHA-256, SHA-384 and SHA-512 for files in one read, reads lists of expected hashes
/// in the formats people meet (sha256sum/md5sum, BSD "SHA256 (name) = hash", certutil, PowerShell Get-FileHash, bare hashes)
/// and checks files against them. Read-only and local: files are only read, nothing goes online.
/// </summary>
public static class HashChecker
{
    /// <summary>Every kind, in the order shown (oldest to strongest).</summary>
    public static readonly IReadOnlyList<HashKind> Kinds = [HashKind.Md5, HashKind.Sha1, HashKind.Sha256, HashKind.Sha384, HashKind.Sha512];

    public static string Name(HashKind kind) => kind switch
    {
        HashKind.Md5 => "MD5",
        HashKind.Sha1 => "SHA-1",
        HashKind.Sha256 => "SHA-256",
        HashKind.Sha384 => "SHA-384",
        _ => "SHA-512",
    };

    /// <summary>The usual extension of a checksum file of this kind (".sha256").</summary>
    public static string Extension(HashKind kind) => kind switch
    {
        HashKind.Md5 => ".md5",
        HashKind.Sha1 => ".sha1",
        HashKind.Sha256 => ".sha256",
        HashKind.Sha384 => ".sha384",
        _ => ".sha512",
    };

    /// <summary>The kind a hex hash of this many characters is (MD5 32, SHA-1 40, SHA-256 64, SHA-384 96, SHA-512 128).</summary>
    public static HashKind? KindForLength(int hexLength) => hexLength switch
    {
        32 => HashKind.Md5,
        40 => HashKind.Sha1,
        64 => HashKind.Sha256,
        96 => HashKind.Sha384,
        128 => HashKind.Sha512,
        _ => null,
    };

    /// <summary>The kind an algorithm label names ("SHA256", "sha-1", "SHA2-256", "MD5"), or null.</summary>
    public static HashKind? KindForLabel(string label) =>
        Regex.Replace(label.ToUpperInvariant(), "[^A-Z0-9]", "") switch
        {
            "MD5" => HashKind.Md5,
            "SHA1" => HashKind.Sha1,
            "SHA256" or "SHA2256" => HashKind.Sha256,
            "SHA384" or "SHA2384" => HashKind.Sha384,
            "SHA512" or "SHA2512" => HashKind.Sha512,
            _ => null,
        };

    private static readonly string[] ListExtensions =
        [".md5", ".sha1", ".sha256", ".sha384", ".sha512", ".md5sum", ".sha1sum", ".sha256sum", ".sha384sum", ".sha512sum"];

    private static readonly string[] ListNames = ["MD5SUMS", "SHA1SUMS", "SHA256SUMS", "SHA384SUMS", "SHA512SUMS", "CHECKSUMS"];

    /// <summary>True for a checksum list file (game.iso.sha256, SHA256SUMS, files.md5), which is read as a list, not hashed.</summary>
    public static bool IsListFile(string path)
    {
        var name = Path.GetFileName(path);
        return ListExtensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase)) ||
               ListNames.Any(n => Path.GetFileNameWithoutExtension(name).Equals(n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A hash as typed or pasted, without spaces, dashes or colons, in lowercase.</summary>
    public static string Normalize(string hash) => Regex.Replace(hash, @"[\s:-]", "").ToLowerInvariant();

    /// <summary>One line of a checksum file, as sha256sum writes it in binary mode ("hash *name"); sha256sum -c and most tools read it.</summary>
    public static string ChecksumLine(string hash, string name) => $"{hash} *{name}";

    /// <summary>Reads the stream once and returns every kind of hash. <paramref name="progress"/> gets the bytes read so far.</summary>
    public static Dictionary<HashKind, string> Compute(Stream stream, Action<long>? progress = null, CancellationToken ct = default)
    {
        var hashers = new List<(HashKind Kind, IncrementalHash Hash)>();
        foreach (var kind in Kinds)
        {
            try { hashers.Add((kind, IncrementalHash.CreateHash(Algorithm(kind)))); }
            catch (Exception e) when (e is CryptographicException or PlatformNotSupportedException or InvalidOperationException)
            {
                // A FIPS-only policy refuses MD5 (and SHA-1 on some systems); the other kinds still work.
            }
        }
        try
        {
            var buffer = new byte[4 * 1024 * 1024];
            long done = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                var span = new ReadOnlyMemory<byte>(buffer, 0, read);
                // Each kind runs on its own core, so all five take about as long as the slowest one.
                if (read >= 256 * 1024) Parallel.ForEach(hashers, h => h.Hash.AppendData(span.Span));
                else foreach (var h in hashers) h.Hash.AppendData(span.Span);
                done += read;
                progress?.Invoke(done);
            }
            return hashers.ToDictionary(h => h.Kind, h => Convert.ToHexString(h.Hash.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            foreach (var h in hashers) h.Hash.Dispose();
        }
    }

    /// <summary>Hashes a file without locking it for anyone else (it can stay open in another program).</summary>
    public static FileHashes ComputeFile(string path, Action<long>? progress = null, CancellationToken ct = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);
        var size = stream.Length;
        return new FileHashes(path, size, Compute(stream, progress, ct));
    }

    private static HashAlgorithmName Algorithm(HashKind kind) => kind switch
    {
        HashKind.Md5 => HashAlgorithmName.MD5,
        HashKind.Sha1 => HashAlgorithmName.SHA1,
        HashKind.Sha256 => HashAlgorithmName.SHA256,
        HashKind.Sha384 => HashAlgorithmName.SHA384,
        _ => HashAlgorithmName.SHA512,
    };

    private static readonly Regex Bsd = new(@"^(?<alg>[A-Za-z0-9-]+)\s*\((?<name>.*)\)\s*=\s*(?<hash>[0-9A-Fa-f]+)$");
    private static readonly Regex CertUtilHeading = new(@"^(?<alg>[A-Za-z0-9-]+) hash of (?:file )?(?<name>.+?):?$", RegexOptions.IgnoreCase);
    private static readonly Regex PowerShellPath = new(@"^Path\s*:\s*(?<name>.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex HexToken = new(@"(?<![0-9A-Fa-f])[0-9A-Fa-f]{32,128}(?![0-9A-Fa-f])");
    private static readonly Regex SpacedHex = new(@"^(?:[0-9A-Fa-f]{2}[ :-]?){16,64}$");

    /// <summary>
    /// Reads every hash in a list. Each line can be "hash  name" or "hash *name" (sha256sum, md5sum), "SHA256 (name) = hash"
    /// (BSD, macOS), certutil's "SHA256 hash of name:" followed by the hash, a PowerShell Get-FileHash table or list, "name hash",
    /// or just a hash. Lines starting with # or ; are comments. Hashes of an unknown length (CRC32, SHA-3 aside) are skipped.
    /// </summary>
    public static IReadOnlyList<ExpectedHash> ParseList(string text)
    {
        var list = new List<ExpectedHash>();
        string? pendingName = null;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;

            var bsd = Bsd.Match(line);
            if (bsd.Success && KindForLength(bsd.Groups["hash"].Length) is { } bsdKind)
            {
                list.Add(new ExpectedHash(KindForLabel(bsd.Groups["alg"].Value) ?? bsdKind, bsd.Groups["hash"].Value.ToLowerInvariant(),
                    CleanName(bsd.Groups["name"].Value), i + 1));
                continue;
            }

            // certutil -hashfile: "SHA256 hash of game.iso:" then the hash (older Windows puts spaces between the bytes).
            var heading = CertUtilHeading.Match(line);
            if (heading.Success && KindForLabel(heading.Groups["alg"].Value) is not null)
            {
                pendingName = CleanName(heading.Groups["name"].Value);
                continue;
            }
            if (SpacedHex.IsMatch(line) && KindForLength(Normalize(line).Length) is { } spacedKind)
            {
                list.Add(new ExpectedHash(spacedKind, Normalize(line), pendingName, i + 1));
                pendingName = null;
                continue;
            }

            // PowerShell's list view (Get-FileHash | Format-List) puts the path on the line after the hash.
            var psPath = PowerShellPath.Match(line);
            if (psPath.Success && list.Count > 0 && list[^1].Name is null && list[^1].Line == i)
            {
                list[^1] = list[^1] with { Name = CleanName(psPath.Groups["name"].Value) };
                continue;
            }

            var token = HexToken.Matches(line).FirstOrDefault(m => KindForLength(m.Length) is not null);
            if (token is null) continue;
            var found = KindForLength(token.Length)!.Value;
            // The rest of the line is the file name, minus a leading * (binary mode) and labels such as "SHA256" or "Hash :".
            var rest = (line[..token.Index] + " " + line[(token.Index + token.Length)..]).Trim();
            var words = rest.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            // The hash's length decides its kind; a label only has to be taken off the name.
            if (words.Length > 0 && KindForLabel(words[0]) is not null && (words.Length == 1 || rest.StartsWith(words[0] + " ")))
                rest = words.Length > 1 ? words[1] : "";
            rest = Regex.Replace(rest, @"^(?:Hash\s*:|Hash$|[:=,|])\s*", "", RegexOptions.IgnoreCase).Trim().TrimEnd(':', ',', '|').Trim();
            list.Add(new ExpectedHash(found, token.Value.ToLowerInvariant(), CleanName(rest), i + 1));
        }
        return list;
    }

    private static string? CleanName(string name)
    {
        name = name.Trim();
        if (name.StartsWith('*')) name = name[1..];
        name = name.Trim().Trim('"').Trim();
        return name.Length == 0 ? null : name;
    }

    /// <summary>True when a listed name is this file: the same full path (names are relative to the list's folder), else the same file name.</summary>
    public static bool NameMatches(ExpectedHash entry, string path, string? listFolder)
    {
        if (entry.Name is not { } name) return false;
        var listed = name.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        try
        {
            if (Path.IsPathFullyQualified(listed) || listFolder is not null)
            {
                var full = Path.GetFullPath(Path.IsPathFullyQualified(listed) ? listed : Path.Combine(listFolder!, listed));
                if (string.Equals(full, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { }
        return string.Equals(Path.GetFileName(listed), Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks a file against the list. When the list names the file, its hash must match one of those entries (else Mismatch);
    /// otherwise any entry with the same hash verifies it, such as a bare hash pasted from a download page or a renamed file.
    /// </summary>
    public static HashCheck Check(FileHashes file, IReadOnlyList<ExpectedHash> list, string? listFolder = null)
    {
        if (list.Count == 0) return new HashCheck(HashVerdict.NoList, null, null);
        bool Same(ExpectedHash e) => file.Hashes.TryGetValue(e.Kind, out var h) && h == e.Hash;
        var named = list.Where(e => NameMatches(e, file.Path, listFolder)).ToList();
        if (named.Count > 0)
        {
            var hit = named.FirstOrDefault(Same);
            return hit is not null ? new HashCheck(HashVerdict.Verified, hit.Kind, hit) : new HashCheck(HashVerdict.Mismatch, named[0].Kind, named[0]);
        }
        var any = list.FirstOrDefault(Same);
        return any is not null ? new HashCheck(HashVerdict.Verified, any.Kind, any) : new HashCheck(HashVerdict.NotInList, null, null);
    }
}
