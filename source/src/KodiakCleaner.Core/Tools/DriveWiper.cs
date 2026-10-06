using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace KodiakCleaner.Core.Tools;

public sealed record WipeableDrive(string Root, string Label, string Format, long Size, long Free, DriveType Type, bool CanWipeEntireDrive, string? Reason)
{
    public string Letter => Root[..1].ToUpperInvariant();
    public string Display => $"{Letter}:  {(string.IsNullOrEmpty(Label) ? "Local Disk" : Label)}";
}

[SupportedOSPlatform("windows")]
public static class DriveWiper
{
    static DriveWiper() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // format.com writes in the OEM code page

    public static List<WipeableDrive> ListDrives()
    {
        var system = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var app = Path.GetPathRoot(AppContext.BaseDirectory) ?? "";
        string? Root(Environment.SpecialFolder f) => Path.GetPathRoot(Environment.GetFolderPath(f));
        var profile = Root(Environment.SpecialFolder.UserProfile);
        var programs = new[] { Root(Environment.SpecialFolder.ProgramFiles), Root(Environment.SpecialFolder.ProgramFilesX86) };
        var list = new List<WipeableDrive>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
            string? reason = null;
            if (string.Equals(d.Name, system, StringComparison.OrdinalIgnoreCase)) reason = "Windows is installed on this drive.";
            else if (string.Equals(d.Name, app, StringComparison.OrdinalIgnoreCase)) reason = "Kodiak Cleaner is running from this drive.";
            else if (File.Exists(Path.Combine(d.Name, "pagefile.sys"))) reason = "Windows uses this drive for its page file.";
            else if (string.Equals(d.Name, profile, StringComparison.OrdinalIgnoreCase)) reason = "Your user folder is on this drive.";
            else if (programs.Any(p => string.Equals(d.Name, p, StringComparison.OrdinalIgnoreCase))) reason = "Programs are installed on this drive.";
            list.Add(new WipeableDrive(d.Name, d.VolumeLabel, d.DriveFormat, d.TotalSize, d.AvailableFreeSpace, d.DriveType, reason is null, reason));
        }
        return list;
    }

    /// <summary>
    /// Erases an entire (non-system) drive with Windows' own format command, which writes zeros over every
    /// sector and then <paramref name="passes"/>-1 passes of random data. Needs administrator rights.
    /// Returns the format exit code; output lines are reported as they arrive.
    /// </summary>
    public static async Task<int> WipeEntireDriveAsync(WipeableDrive drive, int passes, string newLabel, IProgress<string> output, CancellationToken ct)
    {
        if (!drive.CanWipeEntireDrive) throw new InvalidOperationException(drive.Reason);
        // Look again right before formatting: if the drive was swapped and another one took its letter, stop.
        var now = ListDrives().FirstOrDefault(x => string.Equals(x.Root, drive.Root, StringComparison.OrdinalIgnoreCase));
        if (now is null || !now.CanWipeEntireDrive || now.Size != drive.Size || now.Label != drive.Label || now.Format != drive.Format)
            throw new InvalidOperationException("The drive changed since it was listed. Refresh the list and choose it again.");
        var fs = drive.Size > 32L * 1024 * 1024 * 1024 || drive.Type == DriveType.Fixed ? "NTFS" : "exFAT";
        var label = new string(newLabel.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').Take(32).ToArray());
        var psi = new ProcessStartInfo("format.com", $"{drive.Letter}: /FS:{fs} /V:\"{label}\" /P:{Math.Max(0, passes - 1)} /X")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start format.");
        // format asks for the current label (if any) and then "Proceed with Format (Y/N)?".
        if (!string.IsNullOrEmpty(drive.Label)) await p.StandardInput.WriteLineAsync(drive.Label);
        await p.StandardInput.WriteLineAsync("Y");
        p.StandardInput.Close();

        var readOut = Pump(p.StandardOutput, output);
        var readErr = Pump(p.StandardError, output);
        using (ct.Register(() => { try { p.Kill(); } catch (InvalidOperationException) { } }))
        {
            await p.WaitForExitAsync(CancellationToken.None);
        }
        await Task.WhenAll(readOut, readErr);
        ct.ThrowIfCancellationRequested();
        return p.ExitCode;
    }

    private static async Task Pump(StreamReader reader, IProgress<string> output)
    {
        // format rewrites its progress line with '\r', so split on both.
        var sb = new StringBuilder();
        var buffer = new char[256];
        int n;
        while ((n = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (buffer[i] is '\r' or '\n')
                {
                    if (sb.Length > 0) output.Report(sb.ToString().Trim());
                    sb.Clear();
                }
                else sb.Append(buffer[i]);
            }
        }
        if (sb.Length > 0) output.Report(sb.ToString().Trim());
    }
}
