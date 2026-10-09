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
/// Checks Kodiak Cleaner's website for a newer version and installs it (since 0.8.40; earlier versions asked GitHub, which
/// stopped answering once the repository went private). Only runs when the user clicks "Check now" or turns on automatic
/// updates; nothing else ever contacts the network.
/// </summary>
public static class UpdateService
{
    // No redirects: the update file and the installer are only accepted from the website itself, never from wherever
    // a redirect leads (a sign-in page, another host).
    private static readonly HttpClient Http = CreateClient();
    /// <summary>Given to the copy that reopens after an update, so it comes to the front (Windows keeps a program started in the background behind other windows).</summary>
    public const string UpdatedArg = "--updated";
    private static string? _pendingMsi;
    private static string? _pendingSha;

    public static string? PendingVersion { get; private set; }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"KodiakCleaner/{AppInfo.Version.ToString(3)}");
        return c;
    }

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        const string unreachable = "Could not reach the Kodiak Cleaner website to check for updates.";
        try
        {
            using var resp = await Http.GetAsync(UpdateManifest.Url(AppInfo.Channel), ct);
            // Only a check that reached the website counts as "Last checked".
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                MarkChecked();
                return new(false, null, $"No {AppInfo.Channel} updates have been published yet.");
            }
            if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentType?.MediaType != "application/json")
                return new(false, null, $"{unreachable} The website didn't answer with update information. Please try again later.");
            var info = UpdateManifest.Parse(await resp.Content.ReadAsStringAsync(ct), AppInfo.Channel);
            MarkChecked();
            if (info is null) return new(false, null, "The website's update information couldn't be used, so nothing was downloaded.");
            var newName = ReleaseChannels.VersionName(info.Version, AppInfo.Channel, AppInfo.TargetStable);
            return UpdateManifest.IsNewer(info, AppInfo.Version)
                ? new(true, info, $"{AppInfo.Name} {newName} is available. You have {AppInfo.VersionName}.")
                : new(false, info, $"You have the latest version ({AppInfo.VersionName}).");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return new(false, null, $"{unreachable} {e.Message}");
        }
    }

    private static void MarkChecked()
    {
        Settings.Current.LastUpdateCheckUtc = DateTime.UtcNow;
        Settings.Current.Save();
    }

    /// <summary>
    /// Downloads the installer and checks it against the SHA-256 in the website's update file.
    /// The installer is then run when Kodiak Cleaner closes (or right away via <see cref="InstallPendingNow"/>).
    /// </summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, CancellationToken ct = default)
    {
        if (!Integration.IsInstalled)
            throw new InvalidOperationException("This is the portable version. Download the new ZIP from the website.");

        var dir = Path.Combine(Path.GetTempPath(), AppInfo.Id + "-Update");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{AppInfo.Id}-{info.Version.ToString(3)}.msi");
        using (var resp = await Http.GetAsync(info.MsiUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"The website didn't send the installer ({(int)resp.StatusCode}). Please try again later.");
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
            await src.CopyToAsync(dst, ct);
        }

        if (!HashMatches(file, info.Sha256))
        {
            File.Delete(file);
            throw new InvalidOperationException("The downloaded installer did not match its published checksum and was deleted.");
        }
        KodiakCleaner.Core.Util.ActivityLog.Write("Updates", $"Downloaded and verified Kodiak Cleaner {info.Version.ToString(3)}");
        _pendingMsi = file;
        _pendingSha = info.Sha256;
        PendingVersion = info.Version.ToString(3);
        return file;
    }

    /// <summary>
    /// Called when Kodiak Cleaner exits: starts a downloaded, verified update and returns without waiting,
    /// so this process can exit and Windows Installer can replace its files. When the install succeeds, Kodiak Cleaner
    /// opens again on its own (Ted, 0.8.43): a hidden cmd.exe waits for msiexec and, only on exit code 0, starts the exe.
    /// It runs as the user, so the reopened copy isn't elevated by the installer's prompt.
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
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "KodiakCleaner.exe");
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            Process.Start(new ProcessStartInfo(cmd,
                $"/d /s /c \"start \"\" /wait msiexec.exe /i \"{run}\" /passive /norestart & if not errorlevel 1 start \"\" \"{exe}\" {UpdatedArg}\"")
            { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
    }

    public static void InstallPendingNow() => System.Windows.Application.Current.Shutdown();

    private static bool _autoChecking;

    /// <summary>
    /// Automatic updates: a background check at start (<paramref name="atStartup"/>) and, while Kodiak Cleaner is open, whenever
    /// Settings > Updates' interval says one is due (Ted, 0.8.44). Nothing more is checked once an update is waiting to install.
    /// A found update is only announced unless Download and Install Updates Automatically is also on (Ted, 0.8.51).
    /// </summary>
    public static async Task AutoCheckAsync(Action<string> notify, bool atStartup)
    {
        var s = Settings.Current;
        if (!s.AutoUpdate || !Integration.IsInstalled || PendingVersion is not null || _autoChecking) return;
        if (!UpdateSchedule.IsDue(s.UpdateInterval, s.LastUpdateCheckUtc, DateTime.UtcNow, atStartup)) return;
        _autoChecking = true;
        try { await AutoCheckNowAsync(notify); }
        finally { _autoChecking = false; }
    }

    private static async Task AutoCheckNowAsync(Action<string> notify)
    {
        var result = await CheckAsync();
        if (!result.Available || result.Info is null) return;
        if (!Settings.Current.AutoInstall)
        {
            notify($"{result.Message} Install it from Settings > Updates or About > Check for Updates.");
            return;
        }
        try
        {
            await DownloadAsync(result.Info);
            notify($"{AppInfo.Name} {PendingVersion} has been downloaded and will install when you close {AppInfo.ShortName}, which then opens again.");
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException)
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
