using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace KodiakCleaner.Core.Tools;

/// <summary>What a drive is built from, which decides how it is erased and optimized.</summary>
public enum MediaKind
{
    Unknown,
    HardDrive,
    Ssd,
    NvmeSsd,
    FlashDrive,
}

public static class MediaKindText
{
    public static string Name(MediaKind kind) => kind switch
    {
        MediaKind.HardDrive => "Hard Drive",
        MediaKind.Ssd => "SSD",
        MediaKind.NvmeSsd => "NVMe SSD",
        MediaKind.FlashDrive => "Flash Drive",
        _ => "Unknown",
    };
}

/// <summary>A physical disk as Windows reports it: bus, seek penalty (spinning or not), TRIM support, model and serial.</summary>
public sealed record DiskMedia(int Number, string Model, string Serial, long Size, int BusType, bool? SeekPenalty, bool? TrimEnabled, bool Removable)
{
    public MediaKind Kind => DriveMedia.Classify(BusType, SeekPenalty, Removable);
    public string KindName => MediaKindText.Name(Kind);
    public bool IsUsb => BusType is DriveMedia.BusUsb or DriveMedia.BusSd or DriveMedia.BusMmc;
}

/// <summary>A mounted volume and the physical disk(s) it lives on.</summary>
public sealed record VolumeOnDisk(string VolumeName, IReadOnlyList<string> Paths, IReadOnlyList<int> Disks);

/// <summary>
/// Reads what kind of drive each disk is, straight from Windows' storage stack (no WMI): NVMe by its bus, a hard drive by its
/// seek penalty, a flash drive by its USB/SD bus or removable media. Read-only, works without administrator rights.
/// </summary>
public static class DriveMedia
{
    // STORAGE_BUS_TYPE values used here.
    public const int BusUsb = 7, BusSata = 11, BusSd = 12, BusMmc = 13, BusNvme = 17;

    /// <summary>NVMe first (always flash), then the seek penalty: spinning disks pay one, flash doesn't.</summary>
    public static MediaKind Classify(int busType, bool? seekPenalty, bool removable)
    {
        if (busType == BusNvme) return MediaKind.NvmeSsd;
        if (seekPenalty == true) return MediaKind.HardDrive;
        bool usbLike = busType is BusUsb or BusSd or BusMmc;
        if (seekPenalty == false) return usbLike && removable ? MediaKind.FlashDrive : MediaKind.Ssd;
        return removable && usbLike ? MediaKind.FlashDrive : MediaKind.Unknown;
    }

    /// <summary>Every physical disk Windows lists (PhysicalDrive0 to 63), skipping numbers that don't exist.</summary>
    public static List<DiskMedia> ListDisks()
    {
        var list = new List<DiskMedia>();
        for (int n = 0; n < 64; n++)
            if (ReadDisk(n) is { } d) list.Add(d);
        return list;
    }

    public static DiskMedia? ReadDisk(int number)
    {
        using var h = Native.Open($@"\\.\PhysicalDrive{number}", 0);
        if (h.IsInvalid) return null;
        string model = "", serial = "";
        int bus = 0;
        bool removable = false;
        if (Native.QueryProperty(h, 0 /* StorageDeviceProperty */, 1024) is { Length: >= 36 } desc)
        {
            removable = desc[10] != 0;
            bus = BinaryPrimitives.ReadInt32LittleEndian(desc.AsSpan(28));
            string Str(int at)
            {
                int off = BinaryPrimitives.ReadInt32LittleEndian(desc.AsSpan(at));
                if (off <= 0 || off >= desc.Length) return "";
                int end = Array.IndexOf(desc, (byte)0, off);
                return Encoding.ASCII.GetString(desc, off, (end < 0 ? desc.Length : end) - off).Trim();
            }
            var vendor = Str(12);
            var product = Str(16);
            model = string.Join(" ", new[] { vendor, product }.Where(s => s.Length > 0));
            serial = Str(24);
        }
        // NVMe drives: Windows' storage descriptor often gives a drive ID (EUI-64, like 0025_3852_...) instead of the serial
        // printed on the label, so ask the drive's own Identify Controller data for it (Ted, 0.8.117). Read-only query.
        if (bus == BusNvme && Native.NvmeSerial(h) is { Length: > 0 } nvme) serial = nvme;
        serial = SerialText.Clean(serial);
        bool? seek = Native.QueryProperty(h, 7 /* StorageDeviceSeekPenaltyProperty */, 12) is { Length: >= 9 } sp ? sp[8] != 0 : null;
        bool? trim = Native.QueryProperty(h, 8 /* StorageDeviceTrimProperty */, 12) is { Length: >= 9 } tp ? tp[8] != 0 : null;
        long size = Native.Size(h);
        return new DiskMedia(number, model.Length > 0 ? model : $"Disk {number}", serial, size, bus, seek, trim, removable);
    }

    /// <summary>The physical disk(s) a drive letter (C:\) or volume name lives on; empty if Windows doesn't say.</summary>
    public static IReadOnlyList<int> DisksOf(string root)
    {
        var path = root.TrimEnd('\\');
        if (path.Length == 2 && path[1] == ':') path = @"\\.\" + path;
        using var h = Native.Open(path, 0);
        return h.IsInvalid ? Array.Empty<int>() : Native.Extents(h);
    }

    /// <summary>Every volume Windows has mounted, with its drive letters/folders and the disks under it.</summary>
    public static List<VolumeOnDisk> ListVolumes()
    {
        var list = new List<VolumeOnDisk>();
        var name = new StringBuilder(260);
        using var find = Native.FindFirstVolumeW(name, name.Capacity);
        if (find.IsInvalid) return list;
        do
        {
            var vol = name.ToString();
            list.Add(new VolumeOnDisk(vol, Native.PathsOf(vol), DisksOf(vol)));
        } while (Native.FindNextVolumeW(find, name, name.Capacity));
        return list;
    }

    /// <summary>The disk Windows starts from: the one holding the system (EFI or boot) partition, from HKLM\SYSTEM\Setup.</summary>
    [SupportedOSPlatform("windows")]
    public static int? StartupDisk()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\Setup");
            if (key?.GetValue("SystemPartition") is not string device || !device.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase)) return null;
            var disks = DisksOf(@"\\?\GLOBALROOT" + device);
            return disks.Count > 0 ? disks[0] : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return null; }
    }

    /// <summary>TRIM support of the disk under a drive letter (null if Windows doesn't say).</summary>
    public static DiskMedia? DiskOf(string root) => DisksOf(root) is { Count: 1 } d ? ReadDisk(d[0]) : null;

    internal static partial class Native
    {
        public const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
        private const uint ShareReadWrite = 3, OpenExisting = 3;
        public const uint IoctlStorageQueryProperty = 0x2D1400;
        public const uint IoctlVolumeGetDiskExtents = 0x560000;
        public const uint IoctlDiskGetLengthInfo = 0x7405C;
        public const uint IoctlDiskGetDriveGeometryEx = 0x700A0;
        public const uint IoctlDiskUpdateProperties = 0x70140;
        public const uint IoctlStorageReinitializeMedia = 0x2D9640;
        public const uint FsctlLockVolume = 0x90018, FsctlDismountVolume = 0x90020, FsctlUnlockVolume = 0x9001C;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[]? inBuf, int inSize, byte[]? outBuf, int outSize, out int returned, IntPtr overlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern FindVolumeHandle FindFirstVolumeW(StringBuilder name, int length);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool FindNextVolumeW(FindVolumeHandle h, StringBuilder name, int length);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FindVolumeClose(IntPtr h);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumePathNamesForVolumeNameW(string volume, char[] names, int length, out int returned);

        public sealed class FindVolumeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public FindVolumeHandle() : base(true) { }
            protected override bool ReleaseHandle() => FindVolumeClose(handle);
        }

        /// <summary>Opens a disk or volume; access 0 only asks questions, read/write is for erasing.</summary>
        public static SafeFileHandle Open(string path, uint access, uint flags = 0) =>
            CreateFileW(path.TrimEnd('\\'), access, ShareReadWrite, IntPtr.Zero, OpenExisting, flags, IntPtr.Zero);

        public static bool Control(SafeFileHandle h, uint code, byte[]? input = null) =>
            DeviceIoControl(h, code, input, input?.Length ?? 0, null, 0, out _, IntPtr.Zero);

        public static byte[]? QueryProperty(SafeFileHandle h, int propertyId, int size)
        {
            var query = new byte[12]; // STORAGE_PROPERTY_QUERY: PropertyId, QueryType = PropertyStandardQuery, AdditionalParameters
            BinaryPrimitives.WriteInt32LittleEndian(query, propertyId);
            var output = new byte[size];
            return DeviceIoControl(h, IoctlStorageQueryProperty, query, query.Length, output, output.Length, out var got, IntPtr.Zero) && got > 0
                ? output[..got] : null;
        }

        /// <summary>
        /// The serial number from an NVMe drive's Identify Controller data (bytes 4-23), asked through
        /// STORAGE_PROPERTY_QUERY with StorageAdapterProtocolSpecificProperty (49): ProtocolTypeNvme (3), NVMeDataTypeIdentify (1),
        /// CNS 1 = the controller. Only asks; the drive's data isn't read or written. Null when Windows won't answer.
        /// </summary>
        public static string? NvmeSerial(SafeFileHandle h)
        {
            const int Header = 8, Specific = 40, Data = 4096;
            var buffer = new byte[Header + Specific + Data];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, 49);            // PropertyId
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), 0);   // QueryType = PropertyStandardQuery
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), 3);   // ProtocolType = NVMe
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(12), 1);  // DataType = Identify
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16), 1);  // ProtocolDataRequestValue = CNS controller
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(24), Specific); // ProtocolDataOffset
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(28), Data);     // ProtocolDataLength
            if (!DeviceIoControl(h, IoctlStorageQueryProperty, buffer, buffer.Length, buffer, buffer.Length, out var got, IntPtr.Zero)) return null;
            // STORAGE_PROTOCOL_DATA_DESCRIPTOR: Version, Size, then the protocol data whose offset counts from byte 8.
            int offset = Header + BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(24));
            return got >= offset + 24 && offset > Header ? SerialText.FromIdentify(buffer.AsSpan(offset + 4, 20)) : null;
        }

        /// <summary>DISK_GEOMETRY_EX.DiskSize (after the 24-byte DISK_GEOMETRY); needs no access rights, unlike the length IOCTL.</summary>
        public static long Size(SafeFileHandle h)
        {
            var output = new byte[256];
            return DeviceIoControl(h, IoctlDiskGetDriveGeometryEx, null, 0, output, output.Length, out var got, IntPtr.Zero) && got >= 32
                ? BinaryPrimitives.ReadInt64LittleEndian(output.AsSpan(24)) : 0;
        }

        public static long Length(SafeFileHandle h)
        {
            var output = new byte[8];
            return DeviceIoControl(h, IoctlDiskGetLengthInfo, null, 0, output, 8, out _, IntPtr.Zero)
                ? BinaryPrimitives.ReadInt64LittleEndian(output) : 0;
        }

        /// <summary>VOLUME_DISK_EXTENTS: count, padding, then 24-byte DISK_EXTENT entries starting with the disk number.</summary>
        public static IReadOnlyList<int> Extents(SafeFileHandle h)
        {
            var output = new byte[8 + 24 * 32];
            if (!DeviceIoControl(h, IoctlVolumeGetDiskExtents, null, 0, output, output.Length, out _, IntPtr.Zero)) return Array.Empty<int>();
            int count = Math.Min(BinaryPrimitives.ReadInt32LittleEndian(output), 32);
            return Enumerable.Range(0, count).Select(i => BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(8 + 24 * i))).Distinct().ToList();
        }

        public static IReadOnlyList<string> PathsOf(string volume)
        {
            var buffer = new char[1024];
            if (!GetVolumePathNamesForVolumeNameW(volume, buffer, buffer.Length, out var got)) return Array.Empty<string>();
            return new string(buffer, 0, Math.Max(0, got)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
