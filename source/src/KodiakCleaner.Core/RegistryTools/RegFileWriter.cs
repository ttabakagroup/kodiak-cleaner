// Only registry enum values are used here (no registry access), which is safe on every OS.
#pragma warning disable CA1416
using System.Text;
using Microsoft.Win32;

namespace KodiakCleaner.Core.RegistryTools;

/// <summary>Writes regedit-compatible .reg (version 5.00) text. Pure formatting; no registry access.</summary>
public sealed class RegFileWriter
{
    private readonly StringBuilder _sb = new();

    public RegFileWriter()
    {
        _sb.Append("Windows Registry Editor Version 5.00\r\n");
    }

    public void BeginKey(string fullKeyPath)
    {
        _sb.Append("\r\n[").Append(fullKeyPath).Append("]\r\n");
    }

    public void WriteValue(string name, RegistryValueKind kind, object? data)
    {
        _sb.Append(string.IsNullOrEmpty(name) ? "@" : Quote(name)).Append('=');
        _sb.Append(FormatData(kind, data)).Append("\r\n");
    }

    /// <summary>
    /// Writes a value of any registry type by its type number (hex(N), as regedit does), for the rare types .NET
    /// can't name, such as REG_RESOURCE_LIST or REG_LINK, so restoring the file brings back the same type.
    /// </summary>
    public void WriteRawValue(string name, uint type, byte[] data)
    {
        _sb.Append(string.IsNullOrEmpty(name) ? "@" : Quote(name)).Append('=');
        _sb.Append(FormatRaw(type, data)).Append("\r\n");
    }

    internal static string FormatRaw(uint type, byte[] data) => $"hex({type:x}):" + Hex(data);

    public override string ToString() => _sb.ToString();

    internal static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    internal static string FormatData(RegistryValueKind kind, object? data) => kind switch
    {
        // Line breaks or NULs can't go in a quoted .reg string, so they're written as hex(1) like regedit does.
        RegistryValueKind.String when data is string s && s.IndexOfAny(LineBreaks) >= 0 => "hex(1):" + Hex(Utf16Z(s)),
        RegistryValueKind.String => Quote(data?.ToString() ?? ""),
        RegistryValueKind.DWord => "dword:" + unchecked((uint)Convert.ToInt32(data)).ToString("x8"),
        RegistryValueKind.QWord => "hex(b):" + Hex(BitConverter.GetBytes(Convert.ToInt64(data))),
        RegistryValueKind.ExpandString => "hex(2):" + Hex(Utf16Z(data?.ToString() ?? "")),
        RegistryValueKind.MultiString => "hex(7):" + Hex(MultiSz((string[]?)data ?? Array.Empty<string>())),
        RegistryValueKind.Binary => "hex:" + Hex((byte[]?)data ?? Array.Empty<byte>()),
        RegistryValueKind.None => "hex(0):" + Hex((byte[]?)data ?? Array.Empty<byte>()),
        _ => "hex(0):" + Hex(data as byte[] ?? Array.Empty<byte>()),
    };

    private static readonly char[] LineBreaks = { '\r', '\n', '\0' };

    private static byte[] Utf16Z(string s) => Encoding.Unicode.GetBytes(s + "\0");

    private static byte[] MultiSz(string[] items) => Encoding.Unicode.GetBytes(string.Concat(items.Select(i => i + "\0")) + "\0");

    private static string Hex(byte[] bytes) => string.Join(",", bytes.Select(b => b.ToString("x2")));
}
