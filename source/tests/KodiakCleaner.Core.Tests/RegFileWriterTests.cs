using Microsoft.Win32;
using KodiakCleaner.Core.Programs;
using KodiakCleaner.Core.RegistryTools;

namespace KodiakCleaner.Core.Tests;

public class RegFileWriterTests
{
    [Fact]
    public void Writes_header_key_and_values_in_regedit_format()
    {
        var w = new RegFileWriter();
        w.BeginKey(@"HKEY_CURRENT_USER\Software\Test");
        w.WriteValue("", RegistryValueKind.String, "default");
        w.WriteValue("Path", RegistryValueKind.String, @"C:\a ""b""");
        w.WriteValue("Count", RegistryValueKind.DWord, 10);
        w.WriteValue("Neg", RegistryValueKind.DWord, -1);
        w.WriteValue("Bin", RegistryValueKind.Binary, new byte[] { 0x02, 0xff });

        var expected =
            "Windows Registry Editor Version 5.00\r\n" +
            "\r\n[HKEY_CURRENT_USER\\Software\\Test]\r\n" +
            "@=\"default\"\r\n" +
            "\"Path\"=\"C:\\\\a \\\"b\\\"\"\r\n" +
            "\"Count\"=dword:0000000a\r\n" +
            "\"Neg\"=dword:ffffffff\r\n" +
            "\"Bin\"=hex:02,ff\r\n";
        Assert.Equal(expected, w.ToString());
    }

    [Fact]
    public void Formats_expand_multi_and_qword_as_hex()
    {
        Assert.Equal("hex(2):25,00,00,00", RegFileWriter.FormatData(RegistryValueKind.ExpandString, "%"));
        Assert.Equal("hex(7):61,00,00,00,62,00,00,00,00,00", RegFileWriter.FormatData(RegistryValueKind.MultiString, new[] { "a", "b" }));
        Assert.Equal("hex(b):01,00,00,00,00,00,00,00", RegFileWriter.FormatData(RegistryValueKind.QWord, 1L));
    }

    [Fact]
    public void Strings_with_line_breaks_are_written_as_hex_so_the_file_stays_valid()
    {
        Assert.Equal("hex(1):61,00,0a,00,62,00,00,00", RegFileWriter.FormatData(RegistryValueKind.String, "a\nb"));
        Assert.Equal("\"plain\"", RegFileWriter.FormatData(RegistryValueKind.String, "plain"));
    }

    [Fact]
    public void Rare_value_types_keep_their_type_number()
    {
        Assert.Equal("hex(8):01,02", RegFileWriter.FormatRaw(8, new byte[] { 1, 2 }));
        Assert.Equal("hex(0):", RegFileWriter.FormatRaw(0, Array.Empty<byte>()));
        var w = new RegFileWriter();
        w.WriteRawValue("Custom", 0x20, new byte[] { 0xab });
        Assert.EndsWith("\"Custom\"=hex(20):ab\r\n", w.ToString());
    }

    [Fact]
    public void Wow64_paths_are_shown_under_WOW6432Node()
    {
        var loc = new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "X");
        Assert.Equal(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", loc.DisplayKeyPath);
    }

    [Theory]
    [InlineData("20240131", 2024, 1, 31)]
    [InlineData("garbage", 0, 0, 0)]
    public void Parses_install_dates(string raw, int y, int m, int d)
    {
        var parsed = ProgramInventory.ParseDate(raw);
        if (y == 0) Assert.Null(parsed);
        else Assert.Equal(new DateTime(y, m, d), parsed);
    }
}
