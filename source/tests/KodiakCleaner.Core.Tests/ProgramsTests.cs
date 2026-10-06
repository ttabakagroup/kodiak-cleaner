#pragma warning disable CA1416 // RegistryLocation only holds enum values here
using KodiakCleaner.Core.Programs;
using KodiakCleaner.Core.RegistryTools;
using Microsoft.Win32;

namespace KodiakCleaner.Core.Tests;

public class ProgramsTests
{
    private static InstalledProgram Program(string key, bool msi = false, bool noRepair = false, bool noModify = false, string modify = "") => new()
    {
        Name = "Sample",
        KeyName = key,
        IsWindowsInstaller = msi,
        NoRepair = noRepair,
        NoModify = noModify,
        ModifyPath = modify,
        Location = new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + key),
    };

    [Fact]
    public void Windows_installer_programs_repair_with_msiexec()
    {
        var code = "{6F3C2B8E-4D1A-4E7B-9C25-8A0B7D3E51F4}";
        Assert.Equal(("msiexec.exe", $"/fa {code}"), Program(code, msi: true).RepairCommand());
    }

    [Fact]
    public void Other_programs_open_their_own_change_screen_or_offer_no_repair()
    {
        Assert.Equal((@"C:\App\setup.exe", "/modify"), Program("App", modify: @"C:\App\setup.exe /modify").RepairCommand());
        Assert.Null(Program("App").RepairCommand());
        Assert.Null(Program("App", noModify: true, modify: "setup.exe").RepairCommand());
        Assert.False(Program("{6F3C2B8E-4D1A-4E7B-9C25-8A0B7D3E51F4}", msi: true, noRepair: true).CanRepair);
        Assert.False(Program("NotAGuid", msi: true).CanRepair);
    }

    [Fact]
    public void Packs_product_codes_like_windows_installer() =>
        Assert.Equal("68AB67CA7DA73301B7449A0000000010",
            KodiakCleaner.Core.Programs.ProgramInventory.PackProductCode("{AC76BA86-7AD7-1033-7B44-A90000000001}"));
}
