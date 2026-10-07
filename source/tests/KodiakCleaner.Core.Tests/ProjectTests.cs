using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KodiakCleaner.Core.About;
using KodiakCleaner.Core.Supporter;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tests;

/// <summary>Finds files in the repository (the folder holding version.json).</summary>
internal static class Repo
{
    public static string Root { get; } = Find();

    public static string File(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());

    public static string Read(params string[] parts) => System.IO.File.ReadAllText(File(parts));

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (System.IO.File.Exists(Path.Combine(dir.FullName, "version.json"))) return dir.FullName;
        throw new InvalidOperationException("Repository root (version.json) not found.");
    }
}

public class ReleaseChannelTests
{
    [Fact]
    public void Version_names_follow_the_channel()
    {
        Assert.Equal("0.8.3 Dev", ReleaseChannels.VersionName(new Version(0, 8, 3), ReleaseChannel.Dev, new Version(1, 0, 0)));
        Assert.Equal("0.9.4 Beta", ReleaseChannels.VersionName(new Version(0, 9, 4), ReleaseChannel.Beta, null));
        Assert.Equal("1.0.2", ReleaseChannels.VersionName(new Version(1, 0, 2), ReleaseChannel.Stable, new Version(1, 2, 0)));
        // Once a stable release exists, dev and beta say which release they lead to.
        Assert.Equal("0.9.12 Beta (for 1.2.0)", ReleaseChannels.VersionName(new Version(0, 9, 12), ReleaseChannel.Beta, new Version(1, 2, 0)));
    }

    [Fact]
    public void Names_and_tags_include_the_channel()
    {
        Assert.Equal("Kodiak Cleaner Dev", ReleaseChannels.AppName("Kodiak Cleaner", ReleaseChannel.Dev));
        Assert.Equal("Kodiak Cleaner", ReleaseChannels.AppName("Kodiak Cleaner", ReleaseChannel.Stable));
        Assert.Equal("v0.8.4-dev", ReleaseChannels.Tag(new Version(0, 8, 4), ReleaseChannel.Dev));
        Assert.Equal("v1.0.2", ReleaseChannels.Tag(new Version(1, 0, 2), ReleaseChannel.Stable));
        Assert.Equal(ReleaseChannel.Beta, ReleaseChannels.FromTag("v0.9.0-beta"));
        Assert.Null(ReleaseChannels.FromTag("v1.0.0-rc1"));
    }

    [Fact]
    public void Version_json_is_in_its_channel_range_and_has_a_changelog_entry()
    {
        var json = Repo.Read("version.json");
        string Field(string name) => Regex.Match(json, $"\"{name}\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
        var version = Version.Parse(Field("version"));
        var channel = ReleaseChannels.Parse(Field("channel"));
        Assert.Equal(Field("channel"), channel.ToString().ToLowerInvariant());
        switch (channel)
        {
            case ReleaseChannel.Dev: Assert.Equal((0, 8), (version.Major, version.Minor)); break;
            case ReleaseChannel.Beta: Assert.Equal((0, 9), (version.Major, version.Minor)); break;
            default: Assert.True(version.Major >= 1); break;
        }
        var name = ReleaseChannels.VersionName(version, channel, Version.Parse(Field("targetStable")));
        Assert.Contains(Changelog.Parse(Repo.Read("CHANGELOG.md")), r => r.VersionName == name);
    }
}

public class SupporterCodeTests
{
    private static (string Private, string Public) NewKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key.ExportPkcs8PrivateKeyPem(), key.ExportSubjectPublicKeyInfoPem());
    }

    private static readonly SupporterLicense Jane = new("abc123", "Jane Doe", new DateOnly(2026, 10, 4));

    [Fact]
    public void Valid_code_round_trips()
    {
        var (priv, pub) = NewKey();
        var (status, license) = SupporterCode.Verify(SupporterCode.Create(Jane, priv), pub);
        Assert.Equal(CodeStatus.Valid, status);
        Assert.Equal(Jane, license);
    }

    [Fact]
    public void Codes_from_another_key_tampered_or_garbled_codes_are_rejected()
    {
        var (priv, pub) = NewKey();
        var (otherPriv, _) = NewKey();
        var code = SupporterCode.Create(Jane, priv);

        Assert.Equal(CodeStatus.BadSignature, SupporterCode.Verify(SupporterCode.Create(Jane, otherPriv), pub).Status);
        var forged = SupporterCode.Create(Jane with { Name = "Mallory" }, otherPriv).Split('.')[0] + "." + code.Split('.')[1];
        Assert.Equal(CodeStatus.BadSignature, SupporterCode.Verify(forged, pub).Status);
        Assert.Equal(CodeStatus.Malformed, SupporterCode.Verify("not a code", pub).Status);
        Assert.Equal(CodeStatus.Malformed, SupporterCode.Verify("a.b.c", pub).Status);
        Assert.Equal(CodeStatus.Empty, SupporterCode.Verify("  ", pub).Status);
        // Line breaks from email are ignored.
        Assert.Equal(CodeStatus.Valid, SupporterCode.Verify(code[..10] + "\r\n " + code[10..], pub).Status);
    }

    [Fact]
    public void Withdrawn_codes_stop_working()
    {
        var (priv, pub) = NewKey();
        var withdrawn = SupporterCode.ParseWithdrawnList("# comment\n\nabc123\n");
        Assert.Equal(CodeStatus.Withdrawn, SupporterCode.Verify(SupporterCode.Create(Jane, priv), pub, withdrawn).Status);
    }

    [Fact]
    public void Test_key_pair_in_the_repo_matches_and_the_private_key_is_ignored_by_git()
    {
        var code = SupporterCode.Create(Jane, Repo.Read("tools", "SupporterCodes", "supporter-test-private.pem"));
        Assert.Equal(CodeStatus.Valid, SupporterCode.Verify(code, Repo.Read("keys", "supporter-test-public.pem")).Status);
        Assert.Contains("keys/supporter-private.pem", Repo.Read(".gitignore"));
    }
}

/// <summary>Page names, card titles, section headings and field labels use Title Case; buttons and help text don't.</summary>
public class TitleCaseTests
{
    private static readonly HashSet<string> Minor = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "as", "at", "but", "by", "for", "from", "if", "in", "nor", "of", "on", "or", "so", "the", "to", "up", "vs", "via", "with", "yet",
    };

    public static bool IsTitleCase(string text)
    {
        var words = Regex.Split(text.Trim(), @"\s+").Where(w => w.Length > 0).ToArray();
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i].Trim('"', '(', ')', ':', ',', '.', '…', '&');
            if (w.Length == 0 || !char.IsLetter(w[0])) continue; // numbers, symbols, ".reg"
            bool edge = i == 0 || i == words.Length - 1;
            if (!edge && Minor.Contains(w)) continue;
            if (!char.IsUpper(w[0])) return false;
        }
        return true;
    }

    [Theory]
    [InlineData("Legal First Name", true)]
    [InlineData("Place of Birth", true)]
    [InlineData("Driver's License Number", true)]
    [InlineData("Keep Temp Files From the Last 24 Hours", true)]
    [InlineData("Clean when Windows starts", false)]
    [InlineData("Search in", false)]
    public void Checker_works(string text, bool expected) => Assert.Equal(expected, IsTitleCase(text));

    [Fact]
    public void Headings_and_labels_in_every_view_are_title_case()
    {
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var styles = new[] { "PageTitle", "SectionTitle", "SettingTitle" };
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.xaml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(file);
            foreach (var e in doc.Descendants())
            {
                var text = (string?)e.Attribute("Text");
                var style = (string?)e.Attribute("Style") ?? "";
                if (e.Name == ns + "TextBlock" && text is not null && !text.StartsWith('{') && styles.Any(s => style.Contains("{StaticResource " + s + "}")))
                    Check(file, text);
                // Table column headers and window titles are headings too.
                if (e.Attribute("Header") is { } header && !header.Value.StartsWith('{')) Check(file, header.Value);
                if (e.Name == ns + "Window" && e.Attribute("Title") is { } title && !title.Value.StartsWith('{')) Check(file, title.Value);
                // Ted wants Title Case on every button, switch and option label too (not on sentences or hints).
                if (e.Name.LocalName is "Button" or "CheckBox" or "RadioButton" or "ToggleButton")
                {
                    if (e.Attribute("Content") is { } content && !content.Value.StartsWith('{')) Check(file, content.Value);
                    foreach (var label in e.Descendants(ns + "TextBlock"))
                        if ((string?)label.Attribute("Text") is { } t && !t.StartsWith('{') && !t.Contains('.') &&
                            !((string?)label.Attribute("Style") ?? "").Contains("Icon") && t.Any(char.IsLetter))
                            Check(file, t);
                }
            }
        }

        // Settings page names and nav titles are set in code.
        foreach (var file in new[] { Repo.File("src", "KodiakCleaner.App", "ViewModels", "SettingsViewModel.cs"), Repo.File("src", "KodiakCleaner.App", "ViewModels", "MainViewModel.cs") })
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"(?:Add\(""[a-z]+"", |new NavPage\()""([^""]+)"""))
                Check(file, m.Groups[1].Value);

        // Labels and window titles built in code (0.8.24): prompts, folder pickers, Sort By, table values that read as labels.
        var codeLabels = new[]
        {
            @"TextPrompt\.Ask\(""([^""]+)""", @"FolderDialog \{ Title = ""([^""]+)""", @"new SortOption\(this, [^,]+, ""([^""]+)""\)",
            @"PerUser \? ""([^""]+)"" : ""[^""]+""", @"PerUser \? ""[^""]+"" : ""([^""]+)""", @"StartupSource\.\w+Folder => ""([^""]+)""",
        };
        foreach (var file in Directory.EnumerateFiles(Repo.File("src"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            var code = File.ReadAllText(file);
            foreach (var pattern in codeLabels)
                foreach (Match m in Regex.Matches(code, pattern))
                    Check(file, m.Groups[1].Value);
        }

        Assert.True(problems.Count == 0, "Not Title Case:\n" + string.Join("\n", problems));

        void Check(string file, string text)
        {
            if (!IsTitleCase(text)) problems.Add($"{Path.GetFileName(file)}: \"{text}\"");
        }
    }
}

public class DocumentTests
{
    [Fact]
    public void Hosted_privacy_policy_matches_the_built_in_one()
    {
        var md = SimpleMarkdown.Parse(Repo.Read("PRIVACY.md"));
        var html = Repo.Read("store-assets", "privacy-policy.html");
        foreach (var heading in md.Where(b => b.Kind is MarkdownBlockKind.Heading1 or MarkdownBlockKind.Heading2))
            Assert.Contains(System.Net.WebUtility.HtmlEncode(heading.Text), html);
        Assert.Contains("www.kodiakcleaner.app/updates", html);
        Assert.Contains("www.kodiakcleaner.app/contact", html);
        Assert.DoesNotMatch(@"<(script|link)\b|https?://fonts\.", html); // nothing loaded from elsewhere
    }

    [Fact]
    public void Terms_of_use_name_the_developer_contact_and_license()
    {
        var terms = Repo.Read("terms-of-use.md");
        Assert.Contains("Ted Tabaka", terms);
        Assert.Contains("www.kodiakcleaner.app/contact", terms);
        Assert.Contains("MIT License", terms);
        Assert.Matches(@"\*\*Effective date:\*\* \w+ \d{1,2}, \d{4}", terms);
        Assert.Contains("Ted Tabaka", Repo.Read("LICENSE"));
    }

    [Fact]
    public void Simple_markdown_reads_headings_paragraphs_and_bullets()
    {
        var blocks = SimpleMarkdown.Parse("# Title\n\nSome **bold**\ntext.\n\n- One\n  continued\n- [Two](https://x.y)\n## Next");
        Assert.Equal(new[] { MarkdownBlockKind.Heading1, MarkdownBlockKind.Paragraph, MarkdownBlockKind.Bullet, MarkdownBlockKind.Bullet, MarkdownBlockKind.Heading2 },
            blocks.Select(b => b.Kind));
        Assert.Equal("Some bold text.", blocks[1].Text);
        Assert.Equal("One continued", blocks[2].Text);
        Assert.Equal("Two (https://x.y)", blocks[3].Text);
    }

    [Fact]
    public void Packages_ship_the_docs_folder_and_nothing_private()
    {
        // The Portable ZIP carries KodiakCleaner.exe plus a Docs folder made by tools/MakeDocs (build.ps1, and package-linux.sh
        // on Linux); the MSI installs the same seven files to Program Files\Kodiak Cleaner\Docs (Ted's standard).
        var build = Repo.Read("build.ps1");
        var linux = Repo.Read("installer", "package-linux.sh");
        Assert.Contains(@"$files = @(""publish\KodiakCleaner.exe"", ""publish\Docs"")", build);
        Assert.Contains("dotnet run --project tools/MakeDocs -- publish\\Docs", build);
        Assert.Contains(@"dotnet run --project tools/MakeDocs -- ""$STAGE/portable/Docs""", linux);
        Assert.Contains(@"cp ""$PUBLISH/KodiakCleaner.exe"" ""$STAGE/portable/""", linux);
        var msi = XDocument.Load(Repo.File("installer", "Package.wxs")).Descendants()
            .Where(e => e.Name.LocalName == "File").Select(e => (string?)e.Attribute("Source") ?? "").ToList();
        Assert.Equal(DocsFolder.Files.Select(f => "$(var.DocsDir)" + f), msi.Where(f => f.StartsWith("$(var.DocsDir)")));
        foreach (var never in new[] { "tests", "tools", "keys", "store-assets", "CLAUDE.md", ".pem", ".md" })
            Assert.DoesNotContain(msi, f => f.Contains(never));
    }

    [Fact]
    public void The_docs_are_plain_text_and_link_to_the_right_places()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kc-docs-" + Guid.NewGuid().ToString("N"));
        try
        {
            DocsFolder.Write(Repo.Root, dir, ReleaseChannel.Dev);
            Assert.Equal(DocsFolder.Files, Directory.GetFiles(dir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            foreach (var file in DocsFolder.Files.Where(f => f.EndsWith(".txt")))
            {
                var text = File.ReadAllText(Path.Combine(dir, file));
                Assert.DoesNotMatch(@"(?m)^#|\*\*|\]\(|<!--", text);
                Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
            }
            Assert.Contains("URL=https://www.kodiakcleaner.app/\r\n", File.ReadAllText(Path.Combine(dir, "Homepage.url")));
            Assert.Contains("URL=https://github.com/ttabakagroup/kodiakcleaner/releases/download/latest-dev/KodiakCleaner-Dev-Setup.msi\r\n",
                File.ReadAllText(Path.Combine(dir, "Download.url")));
        }
        finally { Directory.Delete(dir, true); }
        Assert.Equal("https://github.com/ttabakagroup/kodiakcleaner/releases/latest/download/KodiakCleaner-Stable-Setup.msi",
            DocsFolder.DownloadUrl(ReleaseChannel.Stable));
    }

    [Fact]
    public void Markdown_becomes_clean_text()
    {
        var text = DocsFolder.FromMarkdown("# Changelog\n\nIntro **here**.\n\n## [Unreleased]\n\n## [0.8.31 Dev] - 2026-10-05\n\n### Settings\n- One\n- [Two](https://x.y)\n");
        Assert.Equal("Changelog\n=========\n\nIntro here.\n\n0.8.31 Dev - 2026-10-05\n-----------------------\n\nSettings\n\n- One\n- Two (https://x.y)\n", text);
    }

    [Fact]
    public void Releases_ship_the_bare_msi_and_portable_zip_with_fixed_names()
    {
        // Ted: GitHub releases carry the bare MSI (Download.url links it; 0.8.31) and the Portable ZIP, and no Setup ZIP
        // (0.8.37). Names stay the same every release (standard).
        var build = Repo.Read("build.ps1");
        foreach (var name in new[] { @"""dist\$product-$branch-Setup.msi""", @"""dist\$product-$branch-Portable.zip""" })
            Assert.Contains(name, build);
        Assert.DoesNotContain("Compress-Archive -Path \"$stage", build);
        var linux = Repo.Read("installer", "package-linux.sh");
        foreach (var name in new[] { @"""$OUTDIR/$PRODUCT-Dev-Setup.msi""", @"""$OUTDIR/$PRODUCT-Dev-Portable.zip""" })
            Assert.Contains(name, linux);
        Assert.DoesNotContain("Setup.zip", linux);
    }

    [Fact]
    public void Control_styles_never_share_a_key_with_a_theme_color()
    {
        // A DynamicResource finds the Controls.xaml entry first, so a style named like a brush (FocusRing in 0.8.31) breaks every use.
        static IEnumerable<string> Keys(string file) => Regex.Matches(Repo.Read("src", "KodiakCleaner.App", "Themes", file), @"x:Key=""([^""]+)""").Select(m => m.Groups[1].Value);
        var colors = Keys("HoneyLight.xaml").ToHashSet();
        Assert.Empty(Keys("Controls.xaml").Where(colors.Contains));
    }

    [Fact]
    public void Uninstall_cleanup_knows_the_installers_upgrade_code_and_the_update_reopens_to_the_front()
    {
        // UninstallCleanup skips everything while two products with this code are installed (an update in progress).
        var wix = Regex.Match(Repo.Read("installer", "KodiakCleaner.Installer.wixproj"), "<UpgradeCode>([^<]+)</UpgradeCode>").Groups[1].Value;
        Assert.Contains($"UpgradeCode = \"{{{wix}}}\"", Repo.Read("src", "KodiakCleaner.App", "UninstallCleanup.cs"));
        var update = Repo.Read("src", "KodiakCleaner.App", "UpdateService.cs");
        Assert.Contains("{UpdatedArg}", update);
        Assert.Contains("UpdateService.UpdatedArg", Repo.Read("src", "KodiakCleaner.App", "App.xaml.cs"));
    }

    [Fact]
    public void Theme_dictionaries_use_only_the_listed_colors()
    {
        // Every color in the theme files must come from the standard's theme table (tools/design/theme.css, which
        // ThemeStandardTests also checks the themes against) or the accent swatches. Glacier's FocusGlow is clear.
        var css = Repo.Read("tools", "design", "theme.css");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "#00000000" };
        foreach (Match m in Regex.Matches(css, @"#[0-9A-Fa-f]{6}\b")) allowed.Add(m.Value);
        foreach (Match m in Regex.Matches(css, @"rgba\((\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\)"))
        {
            double V(int i) => double.Parse(m.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture);
            allowed.Add($"#{(int)Math.Round(V(4) * 255, MidpointRounding.AwayFromZero):X2}{(int)V(1):X2}{(int)V(2):X2}{(int)V(3):X2}");
        }
        foreach (var (_, hex) in KodiakCleaner.Core.Appearance.AccentShades.Swatches) allowed.Add(hex);
        var themes = Repo.File("src", "KodiakCleaner.App", "Themes");
        foreach (var file in Directory.EnumerateFiles(themes, "*.xaml"))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"#[0-9A-Fa-f]{6,8}\b"))
                Assert.True(allowed.Contains(m.Value), $"{Path.GetFileName(file)} uses off-palette color {m.Value}");

        // Nothing outside the theme files holds a color.
        foreach (var file in Directory.EnumerateFiles(Repo.File("src", "KodiakCleaner.App"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".xaml") || f.EndsWith(".cs")) && !f.StartsWith(themes) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            Assert.DoesNotMatch(@"[""']#[0-9A-Fa-f]{6,8}[""']|Color\.FromRgb|\bColors\.[A-Z]", File.ReadAllText(file));
    }
}
