using KodiakCleaner.Core.Tools;

namespace KodiakCleaner.Core.Tests;

public sealed class DriverCleanerTests
{
    private const string NvidiaDisplay = """
        ; NVIDIA display driver
        [Version]
        Signature   = "$Windows NT$"
        Provider    = %NVIDIA%
        Class       = Display
        ClassGUID   = {4D36E968-E325-11CE-BFC1-08002BE10318}
        DriverVer   = 09/18/2026,32.0.15.6614

        [Manufacturer]
        %NVIDIA_A% = NVIDIA_Devices,NTamd64.10.0

        [NVIDIA_Devices.NTamd64.10.0]
        %NVIDIA_DEV.2684% = Section001, PCI\VEN_10DE&DEV_2684
        %NVIDIA_DEV.2704% = Section001, PCI\VEN_10DE&DEV_2704

        [Strings]
        NVIDIA = "NVIDIA"
        NVIDIA_A = "NVIDIA"
        NVIDIA_DEV.2684 = "NVIDIA GeForce RTX 4090"
        NVIDIA_DEV.2704 = "NVIDIA GeForce RTX 4080" ; a comment
        """;

    private static DriverPackage Pkg(string provider, string classGuid, params string[] devices) =>
        new("oem1.inf", provider, "", classGuid, "1.0", "01/01/2026", devices.FirstOrDefault() ?? "", devices);

    [Fact]
    public void Inf_reader_resolves_strings_version_and_device_names()
    {
        var p = InfReader.Parse("oem42.inf", NvidiaDisplay);

        Assert.Equal("oem42.inf", p.PublishedName);
        Assert.Equal("NVIDIA", p.Provider);
        Assert.Equal("Display", p.ClassName);
        Assert.Equal(DriverCleaner.DisplayClass, p.ClassGuid);
        Assert.Equal("32.0.15.6614", p.Version);
        Assert.Equal("09/18/2026", p.Date);
        Assert.Equal(new[] { "NVIDIA GeForce RTX 4080", "NVIDIA GeForce RTX 4090" }, p.Devices);
        Assert.Equal("NVIDIA GeForce RTX 4080", p.Description);
    }

    [Fact]
    public void Inf_reader_uses_language_strings_when_the_plain_section_lacks_a_name()
    {
        var p = InfReader.Parse("oem3.inf", "[Version]\nProvider=%Mfg%\nClassGuid={4d36e96c-e325-11ce-bfc1-08002be10318}\n[Strings.0409]\nMfg=\"Realtek Semiconductor Corp.\"\n");

        Assert.Equal("Realtek Semiconductor Corp.", p.Provider);
        Assert.Equal(DriverVendor.Realtek, DriverCleaner.VendorFromProvider(p.Provider));
    }

    [Theory]
    [InlineData(@"PCI\VEN_10DE&DEV_2684&SUBSYS_1", DriverVendor.Nvidia)]
    [InlineData(@"PCI\VEN_1002&DEV_744C", DriverVendor.Amd)]
    [InlineData(@"pci\ven_8086&dev_a7a0", DriverVendor.Intel)]
    public void Vendors_come_from_pci_ids(string id, DriverVendor vendor) => Assert.Equal(vendor, DriverCleaner.VendorFromHardwareId(id));

    [Theory]
    [InlineData("Advanced Micro Devices, Inc.", DriverVendor.Amd)]
    [InlineData("ATI Technologies Inc.", DriverVendor.Amd)]
    [InlineData("Intel Corporation", DriverVendor.Intel)]
    [InlineData("NVIDIA", DriverVendor.Nvidia)]
    public void Vendors_come_from_providers(string provider, DriverVendor vendor) => Assert.Equal(vendor, DriverCleaner.VendorFromProvider(provider));

    [Fact]
    public void Unknown_makers_and_lookalike_words_are_not_matched()
    {
        Assert.Null(DriverCleaner.VendorFromProvider("Microsoft"));
        Assert.Null(DriverCleaner.VendorFromProvider("Amdocs Software")); // AMD only as a whole word
        Assert.Null(DriverCleaner.VendorFromHardwareId(@"PCI\VEN_1234&DEV_1111"));
    }

    [Fact]
    public void Display_drivers_are_ticked_and_other_parts_only_when_they_belong_to_the_graphics_driver()
    {
        Assert.True(DriverCleaner.TickedByDefault(Pkg("Intel Corporation", DriverCleaner.DisplayClass, "Intel(R) UHD Graphics 770"), DriverVendor.Intel));
        Assert.True(DriverCleaner.TickedByDefault(Pkg("Intel Corporation", DriverCleaner.MediaClass, "Intel(R) Display Audio"), DriverVendor.Intel));
        Assert.False(DriverCleaner.TickedByDefault(Pkg("Intel Corporation", DriverCleaner.MediaClass, "Intel(R) Smart Sound Technology for USB Audio"), DriverVendor.Intel));
        Assert.True(DriverCleaner.TickedByDefault(Pkg("Advanced Micro Devices, Inc.", DriverCleaner.MediaClass, "AMD High Definition Audio Device"), DriverVendor.Amd));
        Assert.False(DriverCleaner.TickedByDefault(Pkg("Advanced Micro Devices, Inc.", DriverCleaner.SoftwareComponentClass, "AMD PSP 11.0 Device"), DriverVendor.Amd));
        Assert.True(DriverCleaner.TickedByDefault(Pkg("NVIDIA", DriverCleaner.SoftwareComponentClass, "NVIDIA Display Container LS"), DriverVendor.Nvidia));
        Assert.True(DriverCleaner.TickedByDefault(Pkg("Realtek Semiconductor Corp.", DriverCleaner.MediaClass, "Realtek High Definition Audio"), DriverVendor.Realtek));
    }

    [Fact]
    public void Chipset_network_and_storage_classes_are_never_listed()
    {
        const string system = "{4d36e97d-e325-11ce-bfc1-08002be10318}", net = "{4d36e972-e325-11ce-bfc1-08002be10318}";
        Assert.False(DriverCleaner.IsCandidate(Pkg("Advanced Micro Devices, Inc.", system, "AMD GPIO Controller"), DriverVendor.Amd));
        Assert.False(DriverCleaner.IsCandidate(Pkg("Realtek", net, "Realtek PCIe GbE Family Controller"), DriverVendor.Realtek));
        Assert.False(DriverCleaner.IsCandidate(Pkg("NVIDIA", DriverCleaner.DisplayClass, "NVIDIA GeForce"), DriverVendor.Amd)); // another maker's
        Assert.False(DriverCleaner.IsCandidate(Pkg("Realtek", DriverCleaner.DisplayClass, "Realtek display"), DriverVendor.Realtek)); // audio only
    }

    [Fact]
    public void Download_pages_are_the_makers_own_https_sites()
    {
        foreach (var v in Enum.GetValues<DriverVendor>())
        {
            var uri = new Uri(DriverCleaner.DownloadUrl(v));
            Assert.Equal("https", uri.Scheme);
            Assert.EndsWith(DriverCleaner.Name(v).ToLowerInvariant() + ".com", uri.Host);
        }
    }

    [Fact]
    public void Leftovers_are_listed_a_to_z_and_stay_inside_the_makers_own_folders_and_keys()
    {
        foreach (var v in Enum.GetValues<DriverVendor>())
        {
            var folders = DriverCleaner.Folders(v).ToList();
            Assert.Equal(folders.OrderBy(f => f.Root, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Relative, StringComparer.OrdinalIgnoreCase), folders);
            var keys = DriverCleaner.RegistryKeys(v).Select(k => k.Root + "\\" + k.Relative).ToList();
            Assert.Equal(keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase), keys);
            Assert.All(DriverCleaner.RegistryKeys(v), k => Assert.StartsWith(@"SOFTWARE\", k.Relative));
            // Never a whole maker key (SOFTWARE\Intel, SOFTWARE\AMD, ...), except NVIDIA's, which only its graphics driver uses.
            Assert.All(DriverCleaner.RegistryKeys(v), k => Assert.True(v == DriverVendor.Nvidia || k.Relative.Count(c => c == '\\') >= 2 || k.Relative.Contains("ATI")));
        }
    }
}
