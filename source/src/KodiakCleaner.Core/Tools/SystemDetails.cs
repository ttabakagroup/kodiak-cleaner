using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using KodiakCleaner.Core.Util;
using Microsoft.Win32;

namespace KodiakCleaner.Core.Tools;

/// <summary>
/// System Information's Advanced view (Ted, 0.8.46): the details support people ask for (drive and memory serial
/// numbers, network addresses, memory speed, processor cache, BIOS, graphics driver, a few Windows settings, and
/// temperatures where Windows shares them). Read-only and local: WMI, the registry and .NET's network classes, nothing
/// online (so no public IP address). Each part is read on its own, so one that fails or is blocked just leaves its rows out.
/// Groups are A to Z. Left out on purpose: the Windows product key, nearby Wi-Fi networks, per-core and SMART tables.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class SystemDetails
{
    public static List<SystemFact> Collect() => Collect(null, CancellationToken.None);

    /// <summary>
    /// Reads every part in turn, telling <paramref name="progress"/> which part is next (its name, how many are done, how many
    /// there are), and stops between parts once <paramref name="ct"/> is cancelled (0.8.51: System Information shows progress and
    /// can be cancelled). A part already being read finishes in the background; its rows are dropped.
    /// </summary>
    public static List<SystemFact> Collect(Action<string, int, int>? progress, CancellationToken ct)
    {
        var parts = new (string Group, Func<IEnumerable<SystemFact>> Read)[]
        {
            ("Audio", Audio), ("Graphics", Graphics), ("Memory", Memory), ("Motherboard and BIOS", Board), ("Network", Network),
            ("Processor", Processor), ("Storage", Storage), ("Temperatures", Temperatures), ("Windows Settings", WindowsSettings),
        };
        var facts = new List<SystemFact>();
        for (int i = 0; i < parts.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (group, read) = parts[i];
            progress?.Invoke(group, i, parts.Length);
            var started = Environment.TickCount64;
            try { facts.AddRange(read()); }
            catch (Exception e) when (Expected(e) || e is InvalidOperationException or IOException or NetworkInformationException)
            {
                // Leave the rest of that part out rather than fail the whole page, and note why in the activity log.
                ActivityLog.Write("System Information", $"{group} couldn't be read: {e.GetType().Name}: {e.Message}");
            }
            // A slow part is what makes the page feel stuck, so note which one it was.
            var seconds = (Environment.TickCount64 - started) / 1000.0;
            if (seconds >= 5) ActivityLog.Write("System Information", $"Reading {group} took {seconds:0.#} seconds");
        }
        ct.ThrowIfCancellationRequested();
        progress?.Invoke("", parts.Length, parts.Length);
        return facts;
    }

    // ------------------------------------------------------------------ parts

    private static IEnumerable<SystemFact> Audio() =>
        Query("SELECT Name, Manufacturer FROM Win32_SoundDevice")
            .Select(d => (Name: Text(d, "Name"), Maker: Text(d, "Manufacturer")))
            .Where(d => d.Name is not null).Distinct().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new SystemFact("Audio", d.Name!, d.Maker ?? "Unknown maker"));

    private static IEnumerable<SystemFact> Graphics()
    {
        foreach (var g in Query("SELECT Name, AdapterCompatibility, AdapterRAM, DriverVersion, DriverDate, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController")
                     .OrderBy(g => Text(g, "Name"), StringComparer.OrdinalIgnoreCase))
        {
            var name = Text(g, "Name") ?? "Graphics Adapter";
            if (Text(g, "AdapterCompatibility") is { } maker) yield return new("Graphics", "Maker", maker, name);
            long vram = VideoMemory(name) ?? Number(g, "AdapterRAM") ?? 0;
            if (vram > 0) yield return new("Graphics", "Video Memory", ByteSize.Format(vram), name);
            if (Text(g, "DriverVersion") is { } driver) yield return new("Graphics", "Driver Version", driver, name);
            if (Date(g, "DriverDate") is { } date) yield return new("Graphics", "Driver Date", date, name);
            if (Number(g, "CurrentHorizontalResolution") is { } w && Number(g, "CurrentVerticalResolution") is { } h)
                yield return new("Graphics", "Resolution", $"{w} x {h}" + (Number(g, "CurrentRefreshRate") is { } hz and > 1 ? $", {hz} Hz" : ""), name);
        }
    }

    private static IEnumerable<SystemFact> Memory()
    {
        var modules = Query("SELECT BankLabel, DeviceLocator, Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, MemoryType, FormFactor, Manufacturer, PartNumber, SerialNumber FROM Win32_PhysicalMemory")
            .OrderBy(m => Text(m, "DeviceLocator"), StringComparer.OrdinalIgnoreCase).ToList();
        long total = modules.Sum(m => Number(m, "Capacity") ?? 0);
        if (total > 0) yield return new("Memory", "Installed", ByteSize.Format(total));
        var first = modules.FirstOrDefault();
        if (first is not null && MemoryType(Number(first, "SMBIOSMemoryType"), Number(first, "MemoryType")) is { } type)
            yield return new("Memory", "Type", type);
        if (first is not null && MemorySpeed(first) is { } speed) yield return new("Memory", "Speed", speed);
        long slots = Query("SELECT MemoryDevices, MaxCapacityEx FROM Win32_PhysicalMemoryArray").Sum(a => Number(a, "MemoryDevices") ?? 0);
        if (slots > 0 && modules.Count > 0) yield return new("Memory", "Slots Used", $"{modules.Count} of {slots}");
        long max = Query("SELECT MaxCapacityEx FROM Win32_PhysicalMemoryArray").Sum(a => Number(a, "MaxCapacityEx") ?? 0);
        if (max > 0) yield return new("Memory", "Most Supported", ByteSize.Format(max * 1024));

        foreach (var m in modules)
        {
            var slot = Text(m, "DeviceLocator") ?? Text(m, "BankLabel") ?? "Module";
            var item = $"Slot {slot}";
            if (Number(m, "Capacity") is { } size) yield return new("Memory", "Size", ByteSize.Format(size), item);
            var kind = string.Join(" ", new[] { MemoryType(Number(m, "SMBIOSMemoryType"), Number(m, "MemoryType")), FormFactor(Number(m, "FormFactor")) }.Where(s => s is not null));
            if (kind.Length > 0) yield return new("Memory", "Type", kind, item);
            if (MemorySpeed(m) is { } s) yield return new("Memory", "Speed", s, item);
            if (Clean(Text(m, "Manufacturer")) is { } maker) yield return new("Memory", "Maker", maker, item);
            if (Clean(Text(m, "PartNumber")) is { } part) yield return new("Memory", "Part Number", part, item);
            if (Clean(Text(m, "SerialNumber")) is { } serial) yield return new("Memory", "Serial Number", serial, item);
        }
    }

    private static string? MemorySpeed(Dictionary<string, object?> m)
    {
        long rated = Number(m, "Speed") ?? 0, running = Number(m, "ConfiguredClockSpeed") ?? 0;
        if (rated <= 0 && running <= 0) return null;
        if (rated <= 0 || running <= 0 || rated == running) return $"{Math.Max(rated, running)} MT/s";
        return $"{running} MT/s (rated {rated} MT/s)";
    }

    private static IEnumerable<SystemFact> Board()
    {
        const string g = "Motherboard and BIOS";
        var cs = Query("SELECT Manufacturer, Model FROM Win32_ComputerSystem").FirstOrDefault();
        if (cs is not null && Clean(Text(cs, "Manufacturer")) is { } maker) yield return new(g, "PC Maker", maker);
        if (cs is not null && Clean(Text(cs, "Model")) is { } model) yield return new(g, "PC Model", model);
        var bios = Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber FROM Win32_BIOS").FirstOrDefault();
        if (bios is not null && Clean(Text(bios, "SerialNumber")) is { } serial) yield return new(g, "PC Serial Number", serial);
        var board = Query("SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard").FirstOrDefault();
        if (board is not null)
        {
            var name = string.Join(" ", new[] { Clean(Text(board, "Manufacturer")), Clean(Text(board, "Product")) }.Where(s => s is not null));
            if (name.Length > 0) yield return new(g, "Motherboard", name);
            if (Clean(Text(board, "Version")) is { } version) yield return new(g, "Motherboard Version", version);
            if (Clean(Text(board, "SerialNumber")) is { } boardSerial) yield return new(g, "Motherboard Serial Number", boardSerial);
        }
        if (bios is not null)
        {
            if (Clean(Text(bios, "Manufacturer")) is { } vendor) yield return new(g, "BIOS Maker", vendor);
            if (Clean(Text(bios, "SMBIOSBIOSVersion")) is { } version) yield return new(g, "BIOS Version", version);
            if (Date(bios, "ReleaseDate") is { } date) yield return new(g, "BIOS Date", date);
        }
        if (GetFirmwareType(out var firmware) && firmware is 1 or 2) yield return new(g, "Firmware", firmware == 2 ? "UEFI" : "Legacy BIOS");
        if (RegistryNumber(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled") is { } secure)
            yield return new(g, "Secure Boot", secure == 1 ? "On" : "Off");
        if (Tpm() is { } tpm) yield return new(g, "Security Chip (TPM)", tpm);
    }

    private static string? Tpm()
    {
        try
        {
            var tpm = RawQuery("SELECT IsEnabled_InitialValue, SpecVersion FROM Win32_Tpm", @"root\CIMV2\Security\MicrosoftTpm").FirstOrDefault();
            if (tpm is null) return "Not found";
            var version = Text(tpm, "SpecVersion")?.Split(',')[0].Trim();
            var on = tpm.GetValueOrDefault("IsEnabled_InitialValue") is true;
            return (version is { Length: > 0 } ? $"Version {version}, " : "") + (on ? "on" : "off");
        }
        catch (Exception e) when (e is UnauthorizedAccessException || e is ManagementException { ErrorCode: ManagementStatus.AccessDenied })
        {
            return "Run as administrator to check";
        }
        catch (Exception e) when (Expected(e)) { return null; }
    }

    private static IEnumerable<SystemFact> Network()
    {
        // Each detail is read on its own: a driver that refuses one (NetworkInformationException) only loses that line.
        static T? Safe<T>(Func<T> read) where T : class
        {
            try { return read(); }
            catch (Exception e) when (e is NetworkInformationException or PlatformNotSupportedException or InvalidOperationException) { return null; }
        }

        var facts = new List<SystemFact>();
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                     .Where(n => n.OperationalStatus == OperationalStatus.Up ||
                                 n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                     .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
        {
            var item = n.Name;
            void Add(string name, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value)) facts.Add(new("Network", name, value, item));
            }
            bool up = n.OperationalStatus == OperationalStatus.Up;
            Add("Adapter", n.Description);
            Add("Status", up ? "Connected" : "Not connected");
            Add("Connection", n.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT => "Ethernet",
                NetworkInterfaceType.Ppp => "Dial-up or VPN",
                var t => t.ToString(),
            });
            Add("MAC Address", Safe(() => Mac(n.GetPhysicalAddress().GetAddressBytes())));
            if (!up) continue;
            Add("Link Speed", Safe(() => n.Speed > 0 ? LinkSpeed(n.Speed) : null));
            var ip = Safe(n.GetIPProperties);
            if (ip is null) continue;
            Add("IPv4 Address", Safe(() => string.Join(", ", ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => $"{a.Address}/{a.PrefixLength}"))));
            Add("IPv6 Address", Safe(() => string.Join(", ", ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6)
                .OrderBy(a => a.Address.IsIPv6LinkLocal).Select(a => a.Address.ToString()))));
            Add("Gateway", Safe(() => string.Join(", ", ip.GatewayAddresses.Select(a => a.Address.ToString()).Where(a => a is not ("0.0.0.0" or "::")))));
            Add("DNS Servers", Safe(() => string.Join(", ", ip.DnsAddresses.Select(a => a.ToString()))));
            Add("DHCP", Safe(() => ip.GetIPv4Properties() is { } p4
                ? p4.IsDhcpEnabled ? "On" + (ip.DhcpServerAddresses.Count > 0 ? $", from {ip.DhcpServerAddresses[0]}" : "") : "Off (set by hand)"
                : null));
            Add("DNS Suffix", Safe(() => ip.DnsSuffix));
        }
        return facts;
    }

    private static IEnumerable<SystemFact> Processor()
    {
        const string g = "Processor";
        var cpus = Query("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, CurrentClockSpeed, SocketDesignation, L2CacheSize, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor").ToList();
        var caches = Query("SELECT Level, MaxCacheSize FROM Win32_CacheMemory")
            .GroupBy(c => Number(c, "Level") ?? 0).ToDictionary(c => c.Key, c => c.Sum(x => Number(x, "MaxCacheSize") ?? 0));
        bool hypervisor = Query("SELECT HypervisorPresent FROM Win32_ComputerSystem").FirstOrDefault()?.GetValueOrDefault("HypervisorPresent") is true;
        for (int i = 0; i < cpus.Count; i++)
        {
            var c = cpus[i];
            var item = cpus.Count > 1 ? $"Processor {i + 1}" : "";
            if (Text(c, "Name") is { } name) yield return new(g, "Name", name.Trim(), item);
            if (Text(c, "Manufacturer") is { } maker) yield return new(g, "Maker", maker, item);
            if (Number(c, "NumberOfCores") is { } cores) yield return new(g, "Cores", cores.ToString(CultureInfo.InvariantCulture), item);
            if (Number(c, "NumberOfLogicalProcessors") is { } threads) yield return new(g, "Threads", threads.ToString(CultureInfo.InvariantCulture), item);
            if (Number(c, "MaxClockSpeed") is { } max and > 0) yield return new(g, "Base Speed", Ghz(max), item);
            if (Number(c, "CurrentClockSpeed") is { } now and > 0) yield return new(g, "Current Speed", Ghz(now), item);
            if (Clean(Text(c, "SocketDesignation")) is { } socket) yield return new(g, "Socket", socket, item);
            // Win32_CacheMemory levels: 3 = L1, 4 = L2, 5 = L3 (in KB). It covers the whole PC, so it is only used for one processor.
            long l1 = cpus.Count == 1 ? caches.GetValueOrDefault(3) : 0;
            long l2 = Number(c, "L2CacheSize") ?? (cpus.Count == 1 ? caches.GetValueOrDefault(4) : 0);
            long l3 = Number(c, "L3CacheSize") ?? (cpus.Count == 1 ? caches.GetValueOrDefault(5) : 0);
            if (l1 > 0) yield return new(g, "L1 Cache", ByteSize.Format(l1 * 1024), item);
            if (l2 > 0) yield return new(g, "L2 Cache", ByteSize.Format(l2 * 1024), item);
            if (l3 > 0) yield return new(g, "L3 Cache", ByteSize.Format(l3 * 1024), item);
            // Windows reports firmware virtualization as off while a hypervisor (Hyper-V, WSL 2, Memory Integrity) is using it.
            var virt = hypervisor ? "On (in use by Windows)" : c.GetValueOrDefault("VirtualizationFirmwareEnabled") is true ? "On" : "Off";
            yield return new(g, "Virtualization", virt, item);
        }
    }

    private static IEnumerable<SystemFact> Storage()
    {
        var disks = Query("SELECT DeviceId, FriendlyName, SerialNumber, Size, MediaType, BusType, FirmwareVersion, HealthStatus FROM MSFT_PhysicalDisk",
            @"root\Microsoft\Windows\Storage");
        if (disks.Count == 0)
        {
            // Older or trimmed-down Windows: the classic disk list has the same basics.
            foreach (var d in Query("SELECT Index, Model, SerialNumber, Size, InterfaceType, FirmwareRevision, MediaType FROM Win32_DiskDrive").OrderBy(d => Number(d, "Index")))
            {
                var item = $"Disk {Number(d, "Index")}: {Text(d, "Model")?.Trim()}";
                if (Clean(Text(d, "SerialNumber")) is { } serial) yield return new("Storage", "Serial Number", serial, item);
                if (Number(d, "Size") is { } size) yield return new("Storage", "Size", ByteSize.Format(size), item);
                if (Text(d, "InterfaceType") is { } bus) yield return new("Storage", "Connection", bus, item);
                if (Clean(Text(d, "FirmwareRevision")) is { } fw) yield return new("Storage", "Firmware", fw, item);
            }
            yield break;
        }

        var temps = new Dictionary<string, long>(); // administrators only
        foreach (var r in Query("SELECT DeviceId, Temperature FROM MSFT_StorageReliabilityCounter", @"root\Microsoft\Windows\Storage"))
            if (Text(r, "DeviceId") is { } id && Number(r, "Temperature") is { } t and > 0) temps[id] = t;

        foreach (var d in disks.OrderBy(d => int.TryParse(Text(d, "DeviceId"), out var n) ? n : int.MaxValue))
        {
            var id = Text(d, "DeviceId") ?? "?";
            var item = $"Disk {id}: {Text(d, "FriendlyName")?.Trim()}";
            if (Clean(Text(d, "SerialNumber")) is { } serial) yield return new("Storage", "Serial Number", serial, item);
            if (Number(d, "Size") is { } size) yield return new("Storage", "Size", ByteSize.Format(size), item);
            if (MediaType(Number(d, "MediaType")) is { } media) yield return new("Storage", "Type", media, item);
            if (BusType(Number(d, "BusType")) is { } bus) yield return new("Storage", "Connection", bus, item);
            if (Clean(Text(d, "FirmwareVersion")) is { } fw) yield return new("Storage", "Firmware", fw, item);
            if (Health(Number(d, "HealthStatus")) is { } health) yield return new("Storage", "Health", health, item);
            if (temps.TryGetValue(id, out var temp)) yield return new("Storage", "Temperature", $"{temp} °C", item);
        }
    }

    private static IEnumerable<SystemFact> Temperatures()
    {
        var zones = Query("SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature", @"root\WMI");
        var readings = zones.Select(z => Number(z, "CurrentTemperature")).Where(t => t is > 2732).Select(t => Celsius(t!.Value)).ToList();
        if (readings.Count == 0)
        {
            yield return new("Temperatures", "Sensors", "Not shared by this PC. Windows reports temperatures only on some PCs, and only to administrators. Drive temperatures are under Storage when available.");
            yield break;
        }
        for (int i = 0; i < readings.Count; i++)
            yield return new("Temperatures", readings.Count == 1 ? "Motherboard" : $"Thermal Zone {i + 1}", readings[i]);
    }

    private static IEnumerable<SystemFact> WindowsSettings()
    {
        const string g = "Windows Settings";
        var av = Query("SELECT displayName FROM AntiVirusProduct", @"root\SecurityCenter2")
            .Select(a => Text(a, "displayName")).OfType<string>().Distinct().OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
        if (av.Count > 0) yield return new(g, "Antivirus", string.Join(", ", av));
        if (RegistryNumber(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled") is { } fast)
            yield return new(g, "Fast Startup", fast == 1 ? "On" : "Off");
        if (Firewall() is { } firewall) yield return new(g, "Firewall", firewall);
        if (RegistryNumber(@"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") is { } hib)
            yield return new(g, "Hibernation", hib == 1 ? "On" : "Off");
        var os = Query("SELECT InstallDate, LastBootUpTime FROM Win32_OperatingSystem").FirstOrDefault();
        if (os is not null && Date(os, "InstallDate") is { } installed) yield return new(g, "Installed On", installed);
        yield return new(g, "Language and Region", CultureInfo.CurrentCulture.DisplayName);
        if (os is not null && DateTimeText(os, "LastBootUpTime") is { } boot) yield return new(g, "Last Started", boot);
        if (PageFile() is { } pageFile) yield return new(g, "Page File", pageFile);
        var plan = Query("SELECT ElementName FROM Win32_PowerPlan WHERE IsActive = True", @"root\cimv2\power").FirstOrDefault();
        if (plan is not null && Text(plan, "ElementName") is { } planName) yield return new(g, "Power Plan", planName);
        if (RegistryNumber(@"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections") is { } deny)
            yield return new(g, "Remote Desktop", deny == 0 ? "On (others can connect)" : "Off");
        yield return new(g, "Time Zone", TimeZoneInfo.Local.DisplayName);
        if (RegistryNumber(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA") is { } uac)
            yield return new(g, "User Account Control", uac == 1 ? "On" : "Off");
    }

    private static string? Firewall()
    {
        const string root = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
        var profiles = new[] { ("Domain", "DomainProfile"), ("Private", "StandardProfile"), ("Public", "PublicProfile") }
            .Select(p => (Name: p.Item1, On: RegistryNumber(root + p.Item2, "EnableFirewall"))).ToList();
        if (profiles.All(p => p.On is null)) return null; // not the Windows firewall's settings at all
        var on = profiles.Where(p => p.On == 1).Select(p => p.Name).ToList();
        return on.Count switch
        {
            0 => "Off",
            3 => "On for all networks",
            _ => "On for " + string.Join(" and ", on) + " networks",
        };
    }

    private static string? PageFile()
    {
        var cs = Query("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem").FirstOrDefault();
        if (cs is null) return null;
        if (cs.GetValueOrDefault("AutomaticManagedPagefile") is true)
        {
            long mb = Query("SELECT AllocatedBaseSize FROM Win32_PageFileUsage").Sum(p => Number(p, "AllocatedBaseSize") ?? 0);
            return "Managed by Windows" + (mb > 0 ? $", {ByteSize.Format(mb * 1024 * 1024)} now" : "");
        }
        var files = Query("SELECT Name, AllocatedBaseSize FROM Win32_PageFileUsage")
            .Select(p => $"{Text(p, "Name")} ({ByteSize.Format((Number(p, "AllocatedBaseSize") ?? 0) * 1024 * 1024)})").ToList();
        return files.Count > 0 ? "Set by hand: " + string.Join(", ", files) : "None";
    }

    // ------------------------------------------------------------------ names and formats (tested)

    internal static string? MemoryType(long? smbios, long? legacy) => (smbios is > 0 ? smbios : legacy) switch
    {
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => null,
    };

    internal static string? FormFactor(long? value) => value switch
    {
        8 => "DIMM",
        12 => "SO-DIMM",
        _ => null,
    };

    internal static string? MediaType(long? value) => value switch
    {
        3 => "Hard drive (HDD)",
        4 => "Solid-state drive (SSD)",
        5 => "Storage-class memory",
        _ => null,
    };

    internal static string? BusType(long? value) => value switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "FireWire",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD card",
        13 => "MMC",
        15 => "Virtual disk (file)",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "Storage Class Memory",
        _ => null,
    };

    internal static string? Health(long? value) => value switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        _ => null,
    };

    /// <summary>"AA-BB-CC-DD-EE-FF", the way Windows shows it; null for adapters without one.</summary>
    internal static string? Mac(byte[] bytes) =>
        bytes.Length == 0 || bytes.All(b => b == 0) ? null : string.Join("-", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

    internal static string LinkSpeed(long bitsPerSecond) => bitsPerSecond >= 1_000_000_000
        ? string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond / 1e9:0.#} Gbps")
        : string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond / 1e6:0.#} Mbps");

    internal static string Ghz(long mhz) => string.Create(CultureInfo.InvariantCulture, $"{mhz / 1000.0:0.00} GHz");

    /// <summary>ACPI thermal zones report tenths of a kelvin.</summary>
    internal static string Celsius(long tenthsKelvin) => string.Create(CultureInfo.InvariantCulture, $"{(tenthsKelvin - 2731.5) / 10:0} °C");

    /// <summary>Drops the placeholder text PC makers leave in serial numbers and names.</summary>
    internal static string? Clean(string? value)
    {
        var v = value?.Trim().TrimEnd('.');
        if (string.IsNullOrEmpty(v)) return null;
        string[] junk = { "To be filled by O.E.M", "Default string", "System Serial Number", "System Product Name", "System manufacturer",
                          "Not Specified", "None", "Unknown", "0", "00000000", "Undefined", "Base Board Serial Number", "SerNum0", "PartNum0" };
        return junk.Any(j => string.Equals(j, v, StringComparison.OrdinalIgnoreCase)) ? null : v;
    }

    // ------------------------------------------------------------------ WMI and registry helpers

    /// <summary>
    /// What a blocked or missing WMI class throws. PlatformNotSupportedException comes from System.Management itself when
    /// Windows' .NET Framework 4 files are missing (Windows always has them; Wine doesn't).
    /// </summary>
    private static bool Expected(Exception e) =>
        e is ManagementException or COMException or UnauthorizedAccessException or System.Security.SecurityException
            or PlatformNotSupportedException or TypeInitializationException;

    /// <summary>The rows a query returns, or none when the class is missing, blocked or needs administrator rights.</summary>
    internal static List<Dictionary<string, object?>> Query(string wql, string scope = @"root\cimv2")
    {
        try { return RawQuery(wql, scope); }
        catch (Exception e) when (Expected(e)) { return new(); }
    }

    private static List<Dictionary<string, object?>> RawQuery(string wql, string scope = @"root\cimv2")
    {
        var options = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(15), ReturnImmediately = true, Rewindable = false };
        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql), options);
        var rows = new List<Dictionary<string, object?>>();
        foreach (ManagementBaseObject mo in searcher.Get())
            using (mo)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in mo.Properties) row[p.Name] = p.Value;
                rows.Add(row);
            }
        return rows;
    }

    internal static string? Text(Dictionary<string, object?> row, string name) =>
        row.GetValueOrDefault(name) is { } v && v.ToString() is { Length: > 0 } s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    internal static long? Number(Dictionary<string, object?> row, string name)
    {
        try { return row.GetValueOrDefault(name) is { } v ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : null; }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException) { return null; }
    }

    private static string? Date(Dictionary<string, object?> row, string name) =>
        Cim(row, name)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? DateTimeText(Dictionary<string, object?> row, string name) =>
        Cim(row, name)?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static DateTime? Cim(Dictionary<string, object?> row, string name)
    {
        try { return Text(row, name) is { } cim ? ManagementDateTimeConverter.ToDateTime(cim) : null; }
        catch (Exception e) when (e is ArgumentOutOfRangeException or FormatException) { return null; }
    }

    private static long? RegistryNumber(string key, string value)
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(key);
            return k?.GetValue(value) is int i ? i : k?.GetValue(value) is long l ? l : null;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return null; }
    }

    /// <summary>
    /// Win32_VideoController's AdapterRAM stops at 4 GB, so the real size comes from the display driver's registry entry
    /// (HardwareInformation.qwMemorySize, or MemorySize on older drivers).
    /// </summary>
    private static long? VideoMemory(string adapter)
    {
        const string cls = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(cls);
            if (root is null) return null;
            foreach (var name in root.GetSubKeyNames().Where(n => n.All(char.IsDigit)))
            {
                using var k = root.OpenSubKey(name);
                if (k?.GetValue("DriverDesc") is not string desc || !string.Equals(desc.Trim(), adapter, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var v in new[] { "HardwareInformation.qwMemorySize", "HardwareInformation.MemorySize" })
                    switch (k.GetValue(v))
                    {
                        case long l when l > 0: return l;
                        case int i when i != 0: return (uint)i;
                        case byte[] b when b.Length >= 8: return BitConverter.ToInt64(b, 0);
                        case byte[] b when b.Length >= 4: return BitConverter.ToUInt32(b, 0);
                    }
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return null;
    }

    private static bool GetFirmwareType(out int type)
    {
        try { return GetFirmwareTypeNative(out type); }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException) { type = 0; return false; }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetFirmwareType")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFirmwareTypeNative(out int type);
}
