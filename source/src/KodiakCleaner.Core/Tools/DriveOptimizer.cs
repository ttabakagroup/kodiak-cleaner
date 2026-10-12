using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using KodiakCleaner.Core.Util;

namespace KodiakCleaner.Core.Tools;

/// <summary>What a drive needs to stay fast.</summary>
public enum OptimizeAction
{
    /// <summary>Hard drives: put each file's pieces back together.</summary>
    Defragment,
    /// <summary>SSDs: tell the drive which blocks are free so it can keep writing quickly.</summary>
    Trim,
    /// <summary>The type isn't known: Windows' optimizer picks the right step itself.</summary>
    WindowsDecides,
    /// <summary>Flash drives (USB sticks, memory cards) need neither.</summary>
    NotNeeded,
}

public sealed record OptimizeDrive(string Root, string Label, string Format, long Size, long Free, MediaKind Kind, bool? TrimEnabled)
{
    public string Letter => Root[..1].ToUpperInvariant();
    public string Display => $"{Letter}:  {(string.IsNullOrEmpty(Label) ? "Local Disk" : Label)}";
    public string KindName => MediaKindText.Name(Kind);
    public OptimizeAction Action => DriveOptimizer.Decide(Kind, Format);
    public string ActionName => DriveOptimizer.Name(Action);
}

/// <summary>
/// Defragments hard drives and trims SSDs with Windows' own optimizer (defrag.exe, the engine behind Optimize Drives),
/// choosing the step from the drive type. Needs administrator rights, like Windows' optimizer itself.
/// </summary>
public static partial class DriveOptimizer
{
    static DriveOptimizer() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // defrag.exe writes in the OEM code page

    private static readonly string[] Defraggable = ["NTFS", "FAT", "FAT32"];

    public static OptimizeAction Decide(MediaKind kind, string fileSystem) => kind switch
    {
        MediaKind.HardDrive => Defraggable.Contains(fileSystem, StringComparer.OrdinalIgnoreCase) ? OptimizeAction.Defragment : OptimizeAction.WindowsDecides,
        MediaKind.Ssd or MediaKind.NvmeSsd => OptimizeAction.Trim,
        MediaKind.FlashDrive => OptimizeAction.NotNeeded,
        _ => OptimizeAction.WindowsDecides,
    };

    public static string Name(OptimizeAction a) => a switch
    {
        OptimizeAction.Defragment => "Defragment",
        OptimizeAction.Trim => "TRIM",
        OptimizeAction.WindowsDecides => "Optimize",
        _ => "Not Needed",
    };

    /// <summary>defrag.exe switches: /D defragment, /L retrim, /O the right step for the media; /U progress, /V details, /H normal priority.</summary>
    public static string Arguments(string letter, OptimizeAction a) => a switch
    {
        OptimizeAction.Defragment => $"{letter}: /D /U /V /H",
        OptimizeAction.Trim => $"{letter}: /L /U /V /H",
        _ => $"{letter}: /O /U /V /H",
    };

    public static List<OptimizeDrive> ListDrives()
    {
        var list = new List<OptimizeDrive>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                DiskMedia? disk = null;
                try { disk = DriveMedia.DiskOf(d.Name); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or EntryPointNotFoundException) { }
                list.Add(new OptimizeDrive(d.Name, d.VolumeLabel, d.DriveFormat, d.TotalSize, d.AvailableFreeSpace,
                    disk?.Kind ?? (d.DriveType == DriveType.Removable ? MediaKind.FlashDrive : MediaKind.Unknown), disk?.TrimEnabled));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // a drive that went away while listing
        }
        return list;
    }

    /// <summary>Runs the drive's step and reports percent done. Returns defrag.exe's exit code (0 = done).</summary>
    public static async Task<int> OptimizeAsync(OptimizeDrive drive, IProgress<int> percent, IProgress<string> output, CancellationToken ct)
    {
        var code = await RunAsync(Arguments(drive.Letter, drive.Action), percent, output, ct);
        ActivityLog.Write("Defrag and TRIM", $"{drive.ActionName} {drive.Letter}: ({drive.KindName}) " + (code == 0 ? "finished" : $"stopped with exit code {code}"));
        return code;
    }

    /// <summary>Analyzes a drive and returns how fragmented it is in percent (null if Windows didn't say).</summary>
    public static async Task<int?> AnalyzeAsync(OptimizeDrive drive, IProgress<int> percent, CancellationToken ct)
    {
        int? fragmented = null;
        var collect = new List<string>();
        await RunAsync($"{drive.Letter}: /A /V", percent, new SyncProgress(line => { lock (collect) collect.Add(line); }), ct);
        lock (collect)
            foreach (var line in collect)
                if (FragmentedOf(line) is { } f) fragmented = f;
        return fragmented;
    }

    /// <summary>"Total fragmented space = 3%" (or the same line in another language: the last number before % on a "fragment" line).</summary>
    public static int? FragmentedOf(string line)
    {
        if (!line.Contains("fragment", StringComparison.OrdinalIgnoreCase) || !line.Contains('=')) return null;
        var m = Percent().Match(line[line.IndexOf('=')..]);
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>The percent in a progress line such as "Defragmentation:  45% complete...".</summary>
    public static int? PercentOf(string line)
    {
        var m = Percent().Match(line);
        return m.Success && int.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out var v) && v is >= 0 and <= 100 ? v : null;
    }

    [GeneratedRegex(@"(\d{1,3})\s*%")]
    private static partial Regex Percent();

    private static async Task<int> RunAsync(string arguments, IProgress<int> percent, IProgress<string> output, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "defrag.exe"), arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Windows' optimizer could not be started.");
        var both = new SyncProgress(line =>
        {
            output.Report(line);
            if (PercentOf(line) is { } v) percent.Report(v);
        });
        var readOut = Pump(p.StandardOutput, both);
        var readErr = Pump(p.StandardError, both);
        using (ct.Register(() => { try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }))
        {
            await p.WaitForExitAsync(CancellationToken.None);
        }
        await Task.WhenAll(readOut, readErr);
        ct.ThrowIfCancellationRequested();
        return p.ExitCode;
    }

    private static async Task Pump(StreamReader reader, IProgress<string> output)
    {
        // defrag rewrites its progress line with '\r', so split on both.
        var sb = new StringBuilder();
        var buffer = new char[256];
        int n;
        while ((n = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (buffer[i] is '\r' or '\n')
                {
                    if (sb.ToString().Trim() is { Length: > 0 } line) output.Report(line);
                    sb.Clear();
                }
                else sb.Append(buffer[i]);
            }
        }
        if (sb.ToString().Trim() is { Length: > 0 } last) output.Report(last);
    }

    /// <summary>Reports on the calling thread (Progress&lt;T&gt; would post each line later and out of order).</summary>
    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
