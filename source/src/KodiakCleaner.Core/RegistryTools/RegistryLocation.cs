// Only registry enum values are used here (no registry access), which is safe on every OS.
#pragma warning disable CA1416
using Microsoft.Win32;

namespace KodiakCleaner.Core.RegistryTools;

/// <summary>A registry key, or one value in it when <see cref="ValueName"/> is set.</summary>
public sealed record RegistryLocation(RegistryHive Hive, RegistryView View, string SubKey, string? ValueName = null)
{
    public bool IsWholeKey => ValueName is null;

    public string HiveName => Hive switch
    {
        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
        RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
        RegistryHive.Users => "HKEY_USERS",
        RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
        _ => Hive.ToString(),
    };

    /// <summary>The path as regedit shows it; 32-bit HKLM keys live under WOW6432Node.</summary>
    public string DisplayKeyPath
    {
        get
        {
            var sub = SubKey;
            if (View == RegistryView.Registry32 && Hive == RegistryHive.LocalMachine &&
                sub.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase) &&
                !sub.StartsWith(@"SOFTWARE\WOW6432Node", StringComparison.OrdinalIgnoreCase))
                sub = @"SOFTWARE\WOW6432Node\" + sub[9..];
            return $@"{HiveName}\{sub}";
        }
    }

    public override string ToString() => ValueName is null ? DisplayKeyPath : $"{DisplayKeyPath} : {(ValueName.Length == 0 ? "(Default)" : ValueName)}";
}
