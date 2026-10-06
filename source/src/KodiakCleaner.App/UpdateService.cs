using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using KodiakCleaner.Core.About;
using KodiakCleaner.Core.Updates;

namespace KodiakCleaner.App;

public sealed record UpdateCheckResult(bool Available, UpdateInfo? Info, string Message);

/// <summary>
/// Checks GitHub Releases for a newer Kodiak Cleaner and installs it. Only runs when the user clicks
/// "Check now" or turns on automatic updates; nothing else ever contacts the network.
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();
    private static string? _pendingMsi;
    private static string? _pendingSha;

    public static string? PendingVersion { get; private set; }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"KodiakCleaner/{AppInfo.Version.ToString(3)}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync($"https://api.github.com/repos/{AppInfo.Repository}/releases?per_page=30", ct);
            // Only a check that reached GitHub counts toward the once-a-day limit, so an offline start tries again next time.
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                MarkChecked();
                return new(false, null, "No releases have been published yet.");
            }
            resp.EnsureSuccessStatusCode();
            MarkChecked();
            // Dev, Beta and Stable each update only to their own channel's releases.
            var info = UpdateParser.PickLatest(await resp.Content.ReadAsStringAsync(ct), AppInfo.Channel);
            if (info is null) return new(false, null, $"No {AppInfo.Name} releases have been published yet.");
            var newName = ReleaseChannels.VersionName(info.Version, AppInfo.Channel, AppInfo.TargetStable);
            return UpdateParser.IsNewer(info, AppInfo.Version)
                ? new(true, info, $"{AppInfo.Name} {newName} is available. You have {AppInfo.VersionName}.")
                : new(false, info, $"You have the latest version ({AppInfo.VersionName}).");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return new(false, null, "Could not reach GitHub to check for updates. " + e.Message);
        }
    }

    private static void MarkChecked()
    {
        Settings.Current.LastUpdateCheckUtc = DateTime.UtcNow;
        Settings.Current.Save();
    }

    /// <summary>
    /// Downloads the release MSI and checks it against the SHA-256 GitHub publishes for it.
    /// The installer is then run when Kodiak Cleaner closes (or right away via <see cref="InstallPendingNow"/>).
    /// </summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, CancellationToken ct = default)
    {
        if (!Integration.IsInstalled)
            throw new InvalidOperationException("This is the portable version. Download the new ZIP from the releases page.");
        var msi = info.Msi ?? throw new InvalidOperationException("The release has no MSI installer.");
        if (msi.Sha256 is null)
            throw new InvalidOperationException("GitHub did not publish a checksum for the installer, so it can't be verified. Install it from the releases page instead.");

        var dir = Path.Combine(Path.GetTempPath(), AppInfo.Id + "-Update");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, Path.GetFileName(new Uri(msi.DownloadUrl).LocalPath));
        await using (var src = await Http.GetStreamAsync(msi.DownloadUrl, ct))
        await using (var dst = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
            await src.CopyToAsync(dst, ct);

        if (!HashMatches(file, msi.Sha256))
        {
            File.Delete(file);
            throw new InvalidOperationException("The downloaded installer did not match its published checksum and was deleted.");
        }
        // Releases before 0.8.37 could ship the MSI inside a ZIP: take the one .msi out of the verified ZIP, by file name only.
        if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            string extracted;
            using (var zip = System.IO.Compression.ZipFile.OpenRead(file))
            {
                var entries = zip.Entries.Where(e => e.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)).ToList();
                if (entries.Count != 1) throw new InvalidOperationException("The downloaded ZIP doesn't hold exactly one installer.");
                extracted = Path.Combine(dir, Path.GetFileName(entries[0].Name));
                using var from = entries[0].Open();
                using var to = new FileStream(extracted, FileMode.Create, FileAccess.Write, FileShare.None);
                from.CopyTo(to);
            }
            File.Delete(file);
            file = extracted;
        }
        _pendingMsi = file;
        // The installer's own hash, checked again right before it runs.
        using (var fs = File.OpenRead(file)) _pendingSha = Convert.ToHexString(SHA256.HashData(fs));
        PendingVersion = info.Version.ToString(3);
        return file;
    }

    /// <summary>
    /// Called when Kodiak Cleaner exits: starts a downloaded, verified update and returns without waiting,
    /// so this process can exit and Windows Installer can replace its files.
    /// </summary>
    public static void InstallPendingOnExit()
    {
        if (_pendingMsi is null || _pendingSha is null || !File.Exists(_pendingMsi)) return;
        var msi = _pendingMsi;
        _pendingMsi = null;
        // Copy to a fresh, unpredictable name and verify that copy, so nothing can swap the file after the check.
        var run = Path.Combine(Path.GetDirectoryName(msi)!, Path.GetRandomFileName() + ".msi");
        try
        {
            File.Copy(msi, run, overwrite: false);
            if (!HashMatches(run, _pendingSha)) { File.Delete(run); return; }
            KodiakCleaner.Core.Util.ActivityLog.Write("Updates", $"Installing the update {Path.GetFileName(msi)}");
            Process.Start(new ProcessStartInfo("msiexec.exe", $"/i \"{run}\" /passive /norestart") { UseShellExecute = true });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
    }

    public static void InstallPendingNow() => System.Windows.Application.Current.Shutdown();

    /// <summary>Automatic updates: at most one check a day, at startup, in the background.</summary>
    public static async Task AutoCheckAsync(Action<string> notify)
    {
        var s = Settings.Current;
        if (!s.AutoUpdate || !Integration.IsInstalled) return;
        if (s.LastUpdateCheckUtc is { } last && DateTime.UtcNow - last < TimeSpan.FromDays(1)) return;
        var result = await CheckAsync();
        if (!result.Available || result.Info is null) return;
        try
        {
            await DownloadAsync(result.Info);
            notify($"{AppInfo.Name} {PendingVersion} has been downloaded and will install when you close {AppInfo.ShortName}.");
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or IOException or TaskCanceledException or InvalidDataException or UnauthorizedAccessException)
        {
            notify($"{AppInfo.Name} {result.Info.Version.ToString(3)} is available, but it could not be downloaded: {e.Message}");
        }
    }

    private static bool HashMatches(string file, string expectedHex)
    {
        using var fs = File.OpenRead(file);
        return HashMatches(fs, expectedHex);
    }

    private static bool HashMatches(Stream stream, string expectedHex)
    {
        stream.Position = 0;
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase);
    }
}
