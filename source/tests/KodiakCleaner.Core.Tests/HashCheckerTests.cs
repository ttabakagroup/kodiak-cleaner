using System.Text;
using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

public sealed class HashCheckerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kc-hash-" + Guid.NewGuid().ToString("N"));

    public HashCheckerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    // Known answers for "abc" (FIPS 180-2 and RFC 1321 test vectors).
    private const string AbcMd5 = "900150983cd24fb0d6963f7d28e17f72";
    private const string AbcSha1 = "a9993e364706816aba3e25717850c26c9cd0d89d";
    private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string AbcSha384 = "cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7";
    private const string AbcSha512 = "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void Every_kind_is_worked_out_in_one_read()
    {
        long seen = 0;
        var hashes = HashChecker.Compute(new MemoryStream(Encoding.ASCII.GetBytes("abc")), n => seen = n);
        Assert.Equal(AbcMd5, hashes[HashKind.Md5]);
        Assert.Equal(AbcSha1, hashes[HashKind.Sha1]);
        Assert.Equal(AbcSha256, hashes[HashKind.Sha256]);
        Assert.Equal(AbcSha384, hashes[HashKind.Sha384]);
        Assert.Equal(AbcSha512, hashes[HashKind.Sha512]);
        Assert.Equal(3, seen);
    }

    [Fact]
    public void Big_files_hash_the_same_as_in_one_piece()
    {
        var data = new byte[9 * 1024 * 1024 + 17];
        new Random(4).NextBytes(data);
        var path = Path.Combine(_dir, "big.bin");
        File.WriteAllBytes(path, data);
        var file = HashChecker.ComputeFile(path);
        Assert.Equal(data.Length, file.Size);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant(), file.Hashes[HashKind.Sha256]);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(data)).ToLowerInvariant(), file.Hashes[HashKind.Sha512]);
    }

    [Fact]
    public void A_file_open_in_another_program_can_still_be_hashed()
    {
        var path = Path.Combine(_dir, "open.txt");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        writer.Write(Encoding.ASCII.GetBytes("abc"));
        writer.Flush();
        Assert.Equal(AbcSha256, HashChecker.ComputeFile(path).Hashes[HashKind.Sha256]);
    }

    [Fact]
    public void Cancel_stops_part_way()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => HashChecker.Compute(new MemoryStream(new byte[1024]), null, cts.Token));
    }

    [Fact]
    public void Lists_in_every_common_format_are_read()
    {
        var text = string.Join("\n",
            "# made by sha256sum",
            $"{AbcSha256}  abc.txt",
            $"{AbcSha1} *folder/abc.txt",
            $"MD5 (abc.txt) = {AbcMd5}",
            $"SHA512 (two words.txt) = {AbcSha512}",
            "SHA384 hash of abc.txt:",
            string.Join(" ", Enumerable.Range(0, 48).Select(i => AbcSha384.Substring(i * 2, 2))),
            "CertUtil: -hashfile command completed successfully.",
            $"SHA256          {AbcSha256.ToUpperInvariant()}       C:\\Downloads\\abc.txt",
            $"Hash      : {AbcSha1.ToUpperInvariant()}",
            "Path      : C:\\Downloads\\abc.txt",
            $"abc.txt: {AbcMd5}",
            "; a comment",
            AbcSha256,
            "deadbeef  crc32-is-not-a-kind.txt",
            "");
        var list = HashChecker.ParseList(text);
        Assert.Equal(new[]
        {
            (HashKind.Sha256, AbcSha256, (string?)"abc.txt"),
            (HashKind.Sha1, AbcSha1, "folder/abc.txt"),
            (HashKind.Md5, AbcMd5, "abc.txt"),
            (HashKind.Sha512, AbcSha512, "two words.txt"),
            (HashKind.Sha384, AbcSha384, "abc.txt"),
            (HashKind.Sha256, AbcSha256, "C:\\Downloads\\abc.txt"),
            (HashKind.Sha1, AbcSha1, "C:\\Downloads\\abc.txt"),
            (HashKind.Md5, AbcMd5, "abc.txt"),
            (HashKind.Sha256, AbcSha256, null),
        }, list.Select(e => (e.Kind, e.Hash, e.Name)));
    }

    private static FileHashes Abc(string path) => new(path, 3, HashChecker.Compute(new MemoryStream(Encoding.ASCII.GetBytes("abc"))));

    [Fact]
    public void A_listed_file_must_match_its_own_entry()
    {
        var file = Abc(Path.Combine("C:", "Downloads", "abc.txt"));
        Assert.Equal(HashVerdict.NoList, HashChecker.Check(file, []).Verdict);

        var good = HashChecker.Check(file, HashChecker.ParseList($"{AbcSha256}  abc.txt"));
        Assert.Equal((HashVerdict.Verified, HashKind.Sha256), (good.Verdict, good.Kind!.Value));

        // Listed by name with another hash: a damaged or changed file, even if a bare hash elsewhere would match.
        var bad = HashChecker.Check(file, HashChecker.ParseList($"{new string('0', 64)}  ABC.TXT\n{AbcSha256}"));
        Assert.Equal(HashVerdict.Mismatch, bad.Verdict);

        Assert.Equal(HashVerdict.NotInList, HashChecker.Check(file, HashChecker.ParseList($"{new string('1', 64)}  other.txt")).Verdict);
    }

    [Fact]
    public void A_bare_or_differently_named_hash_still_verifies()
    {
        var file = Abc("abc.txt");
        Assert.Equal(HashVerdict.Verified, HashChecker.Check(file, HashChecker.ParseList(AbcMd5.ToUpperInvariant())).Verdict);
        var renamed = HashChecker.Check(file, HashChecker.ParseList($"{AbcSha512}  setup-1.0.exe"));
        Assert.Equal(HashVerdict.Verified, renamed.Verdict);
        Assert.Equal("setup-1.0.exe", renamed.Entry!.Name);
    }

    [Fact]
    public void Names_in_a_list_are_relative_to_its_folder()
    {
        var sub = Path.Combine(_dir, "sub");
        var entry = new ExpectedHash(HashKind.Sha256, AbcSha256, "sub/abc.txt", 1);
        Assert.True(HashChecker.NameMatches(entry, Path.Combine(sub, "abc.txt"), _dir));
        Assert.True(HashChecker.NameMatches(entry, Path.Combine(_dir, "elsewhere", "abc.txt"), _dir)); // same file name still counts
        Assert.False(HashChecker.NameMatches(entry, Path.Combine(sub, "abd.txt"), _dir));
        Assert.False(HashChecker.NameMatches(entry with { Name = null }, Path.Combine(sub, "abc.txt"), _dir));
    }

    [Theory]
    [InlineData("game.iso.sha256", true)]
    [InlineData("SHA256SUMS", true)]
    [InlineData("sha512sums.txt", true)]
    [InlineData("files.MD5", true)]
    [InlineData("setup.exe", false)]
    [InlineData("checksums-are-here.iso", false)]
    public void Checksum_files_are_read_as_lists(string name, bool isList) => Assert.Equal(isList, HashChecker.IsListFile(name));

    [Fact]
    public void Hashes_are_tidied_and_saved_as_sha256sum_reads_them()
    {
        Assert.Equal("ab12cd", HashChecker.Normalize(" AB:12-cd \t"));
        Assert.Equal($"{AbcSha256} *abc.txt", HashChecker.ChecksumLine(AbcSha256, "abc.txt"));
        Assert.Equal(new[] { "MD5", "SHA-1", "SHA-256", "SHA-384", "SHA-512" }, HashChecker.Kinds.Select(HashChecker.Name));
        Assert.All(HashChecker.Kinds, k => Assert.Equal(k, HashChecker.KindForLabel(HashChecker.Name(k))));
    }
}
