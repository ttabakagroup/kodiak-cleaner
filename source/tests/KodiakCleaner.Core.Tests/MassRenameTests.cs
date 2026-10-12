using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

public sealed class MassRenameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kc-rename-" + Guid.NewGuid().ToString("N"));

    public MassRenameTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static readonly DateTime When = new(2026, 10, 12, 9, 30, 0);

    private static readonly string Photos = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "Photos");

    private static RenameFile F(string name, long size = 1, DateTime? modified = null) =>
        new(Path.Combine(Photos, name), size, modified ?? When, When.AddDays(-1));

    private static string New(string name, RenameRules rules, int index = 0) => MassRenamer.NewName(F(name), rules, index);

    [Theory]
    [InlineData("{name}", "IMG_0001.JPG", "IMG_0001.JPG")]
    [InlineData("Holiday {num:3}", "IMG_0001.JPG", "Holiday 005.JPG")]
    [InlineData("{modified} {name}", "a.txt", "2026-10-12 a.txt")]
    [InlineData("{modified:yyyy.MM} {name}", "a.txt", "2026.10 a.txt")]
    [InlineData("{created}", "a.txt", "2026-10-11.txt")]
    [InlineData("{name:3}", "Report final.docx", "Rep.docx")]
    [InlineData("{name:2-4}", "Report.docx", "epo.docx")]
    [InlineData("{name:-5}", "Report final.docx", "final.docx")]
    [InlineData("{name:8-}", "Report final.docx", "final.docx")]
    [InlineData("{folder} - {name}", "a.txt", "Photos - a.txt")]
    [InlineData("{NAME}", "a.txt", "a.txt")]
    [InlineData("{name}.{ext}", "a.txt", "a.txt.txt")]
    [InlineData("{unknown} {name}", "a.txt", "{unknown} a.txt")]
    [InlineData("{name}", ".gitignore", ".gitignore")]
    public void Patterns_build_the_new_name(string pattern, string name, string expected) =>
        Assert.Equal(expected, New(name, new RenameRules { Pattern = pattern, Start = 1 }, index: 4));

    [Fact]
    public void Numbers_count_from_start_by_step_padded_to_digits()
    {
        var rules = new RenameRules { Pattern = "{num}", Start = 10, Step = 5, Digits = 4 };
        Assert.Equal("0010.jpg", New("x.jpg", rules, 0));
        Assert.Equal("0025.jpg", New("x.jpg", rules, 3));
        Assert.Equal("-005.jpg", New("x.jpg", new RenameRules { Pattern = "{num:3}", Start = 0, Step = -5 }, 1));
    }

    [Fact]
    public void Find_and_replace_plain_and_regular_expression()
    {
        Assert.Equal("My Photo.jpg", New("my_photo.jpg", new RenameRules { Find = "_", Replace = " ", Case = RenameCase.Title }));
        Assert.Equal("aXbX.txt", New("axbx.txt", new RenameRules { Find = "X", Replace = "X" }));
        Assert.Equal("axbX.txt", New("axbX.txt", new RenameRules { Find = "x", Replace = "x", MatchCase = true }));
        Assert.Equal("2024-Trip.jpg", New("Trip 2024.jpg", new RenameRules { Find = @"(\w+) (\d{4})", Replace = "$2-$1", UseRegex = true }));
        Assert.ThrowsAny<ArgumentException>(() => New("a.txt", new RenameRules { Find = "(", UseRegex = true }));
    }

    [Theory]
    [InlineData(RenameCase.AsIs, "the QUICK fox's den", "the QUICK fox's den")]
    [InlineData(RenameCase.Lower, "The QUICK Fox", "the quick fox")]
    [InlineData(RenameCase.Upper, "The quick fox", "THE QUICK FOX")]
    [InlineData(RenameCase.Title, "the QUICK fox's den-hole", "The Quick Fox's Den-Hole")]
    [InlineData(RenameCase.Title, "sunset at the pier of the bay", "Sunset at the Pier of the Bay")]
    [InlineData(RenameCase.Title, "2026 trip to the lake", "2026 Trip to the Lake")]
    [InlineData(RenameCase.Title, "where it's at", "Where It's At")]
    [InlineData(RenameCase.Sentence, "01 the QUICK Fox", "01 The quick fox")]
    public void Letter_case(RenameCase mode, string text, string expected) => Assert.Equal(expected, MassRenamer.ApplyCase(text, mode));

    [Fact]
    public void Extension_and_tidy_spaces()
    {
        Assert.Equal("a.jpg", New("a.JPG", new RenameRules { Extension = RenameExtension.Lower }));
        Assert.Equal("a.JPG", New("a.jpg", new RenameRules { Extension = RenameExtension.Upper }));
        Assert.Equal("a.jpeg", New("a.jpg", new RenameRules { Extension = RenameExtension.ChangeTo, NewExtension = ".jpeg" }));
        Assert.Equal("a", New("a.jpg", new RenameRules { Extension = RenameExtension.ChangeTo, NewExtension = "" }));
        Assert.Equal("a b.txt", New("  a   b  .txt", new RenameRules()));
        Assert.Equal("  a   b  .txt", New("  a   b  .txt", new RenameRules { TidySpaces = false }));
    }

    [Theory]
    [InlineData("good name.txt", null)]
    [InlineData("", "The new name is empty.")]
    [InlineData("a:b.txt", "Windows doesn't allow : in a name.")]
    [InlineData("a?.txt", "Windows doesn't allow ? in a name.")]
    [InlineData("name.", "A name can't end with a space or a dot.")]
    [InlineData("CON.txt", "CON is a name Windows keeps for itself.")]
    [InlineData("lpt1", "LPT1 is a name Windows keeps for itself.")]
    [InlineData("console.txt", null)]
    public void Names_Windows_wont_take_are_explained(string name, string? why) => Assert.Equal(why, MassRenamer.WhyInvalid(name));

    [Fact]
    public void Numbering_order_is_natural_and_follows_the_choice()
    {
        var files = new[] { F("file 10.txt", 3, When.AddHours(1)), F("file 2.txt", 1, When.AddHours(3)), F("File 1.txt", 2, When.AddHours(2)) };
        Assert.Equal(new[] { "File 1.txt", "file 2.txt", "file 10.txt" }, MassRenamer.Ordered(files, RenameOrder.Name).Select(f => f.Name));
        Assert.Equal(new[] { "file 10.txt", "File 1.txt", "file 2.txt" }, MassRenamer.Ordered(files, RenameOrder.DateModified).Select(f => f.Name));
        Assert.Equal(new[] { "file 2.txt", "File 1.txt", "file 10.txt" }, MassRenamer.Ordered(files, RenameOrder.Size).Select(f => f.Name));
    }

    private static Dictionary<string, HashSet<string>> Existing(params string[] names) =>
        new(StringComparer.OrdinalIgnoreCase) { [Photos] = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase) };

    [Fact]
    public void Preview_flags_clashes_and_skips_unticked_files_from_numbering()
    {
        var files = new[] { F("a.jpg"), F("b.jpg"), F("c.jpg"), F("d.jpg") };
        var existing = Existing("a.jpg", "b.jpg", "c.jpg", "d.jpg", "Trip 3.jpg");
        var skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { files[1].Path };
        var plan = MassRenamer.Plan(files, new RenameRules { Pattern = "Trip {num}" }, existing, skipped).ToDictionary(p => p.File.Name);

        Assert.Equal(("Trip 1.jpg", RenameState.Ready), (plan["a.jpg"].NewName, plan["a.jpg"].State));
        Assert.Equal(RenameState.Skipped, plan["b.jpg"].State);
        Assert.Equal(("Trip 2.jpg", RenameState.Ready), (plan["c.jpg"].NewName, plan["c.jpg"].State));
        // Trip 3.jpg is already in the folder and isn't being renamed away.
        Assert.Equal(RenameState.Problem, plan["d.jpg"].State);
        Assert.Contains("already", plan["d.jpg"].Note);
    }

    [Fact]
    public void Preview_allows_swaps_and_case_only_changes_but_not_two_files_with_one_name()
    {
        var existing = Existing("1.txt", "2.txt", "Photo.JPG");
        var swap = MassRenamer.Plan(new[] { F("1.txt"), F("2.txt") }, new RenameRules { Pattern = "{num}", Start = 2, Step = -1 }, existing);
        Assert.All(swap, p => Assert.Equal(RenameState.Ready, p.State));

        var caseOnly = MassRenamer.Plan(new[] { F("Photo.JPG") }, new RenameRules { Case = RenameCase.Lower, Extension = RenameExtension.Lower }, existing);
        Assert.Equal(("photo.jpg", RenameState.Ready), (caseOnly[0].NewName, caseOnly[0].State));

        var same = MassRenamer.Plan(new[] { F("1.txt"), F("2.txt") }, new RenameRules { Pattern = "same" }, existing);
        Assert.All(same, p => Assert.Equal(RenameState.Problem, p.State));

        var unchanged = MassRenamer.Plan(new[] { F("1.txt") }, new RenameRules(), existing);
        Assert.Equal(RenameState.Unchanged, unchanged[0].State);

        var badRegex = MassRenamer.Plan(new[] { F("1.txt") }, new RenameRules { Find = "[", UseRegex = true }, existing);
        Assert.Equal("Find isn't a valid regular expression.", badRegex[0].Note);
    }

    [Fact]
    public void Rename_swaps_names_keeps_a_journal_and_undo_puts_them_back()
    {
        var a = Path.Combine(_dir, "a.txt");
        var b = Path.Combine(_dir, "b.txt");
        var c = Path.Combine(_dir, "Case.txt");
        File.WriteAllText(a, "A");
        File.WriteAllText(b, "B");
        File.WriteAllText(c, "C");
        var journal = Path.Combine(_dir, "Backups", "Mass Rename", "last-rename.json");

        var outcome = MassRenamer.Rename(new[] { new RenameMove(a, b), new RenameMove(b, a), new RenameMove(c, Path.Combine(_dir, "case.txt")) }, journal);
        Assert.Equal(3, outcome.Done.Count);
        Assert.Empty(outcome.Failed);
        Assert.Equal("B", File.ReadAllText(a));
        Assert.Equal("A", File.ReadAllText(b));
        Assert.Contains("case.txt", Directory.GetFiles(_dir).Select(Path.GetFileName));
        Assert.DoesNotContain(Directory.GetFiles(_dir), f => f.EndsWith(".tmp"));

        var saved = RenameJournal.Load(journal);
        Assert.NotNull(saved);
        Assert.True(saved!.Done);
        Assert.Equal(3, saved.Moves.Count);

        var undo = MassRenamer.Undo(saved, journal);
        Assert.Equal(3, undo.Done.Count);
        Assert.Equal("A", File.ReadAllText(a));
        Assert.Equal("B", File.ReadAllText(b));
        Assert.Contains("Case.txt", Directory.GetFiles(_dir).Select(Path.GetFileName));
        Assert.False(File.Exists(journal));
    }

    [Fact]
    public void A_file_that_cant_be_renamed_keeps_its_name_and_undo_skips_files_changed_since()
    {
        var a = Path.Combine(_dir, "a.txt");
        File.WriteAllText(a, "A");
        var missing = Path.Combine(_dir, "gone.txt");
        var outcome = MassRenamer.Rename(new[] { new RenameMove(missing, Path.Combine(_dir, "x.txt")), new RenameMove(a, Path.Combine(_dir, "b.txt")) }, null);
        Assert.Single(outcome.Done);
        Assert.Single(outcome.Failed);

        File.Delete(Path.Combine(_dir, "b.txt"));
        var undo = MassRenamer.Undo(new RenameJournal(DateTime.Now, outcome.Done.ToList(), true), null);
        Assert.Empty(undo.Done);
        Assert.Single(undo.Failed);
    }

    [Fact]
    public void Every_token_is_listed_A_to_Z()
    {
        var tokens = MassRenamer.Tokens.Select(t => t.Token).ToList();
        Assert.Equal(tokens.OrderBy(t => t, StringComparer.Ordinal), tokens);
        Assert.Equal(7, tokens.Count);
    }
}
