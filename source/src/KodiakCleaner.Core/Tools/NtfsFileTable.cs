using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace KodiakCleaner.Core.Tools;

/// <summary>
/// The Disk Analyzer's fast scan (Ted, 2026-10-13): for a whole NTFS drive, read the drive's master file table (NTFS's own list of
/// every file and folder) in one long read instead of opening every folder. Windows only lets an administrator open a drive this way,
/// so without administrator rights, on other file systems and on network drives the folder walk is used instead. Read-only: the drive
/// is opened for reading and nothing is ever written. Any surprise in the table means "not usable" and the folder walk takes over.
/// Counts the same things as the folder walk (main file contents, hidden and system files, links and junctions skipped), except that a
/// file with several hard links is counted once, and folders Windows keeps even administrators out of (System Volume Information) are counted.
/// </summary>
internal static class NtfsFileTable
{
    private const int RootRecord = 5;
    private const int FirstUserRecord = 16; // 0 to 15 are NTFS's own files ($MFT, $LogFile, $Extend and so on), never listed by Windows
    private const uint ReparsePoint = 0x400;
    private const long RecordMask = 0xFFFF_FFFF_FFFF;
    private const int ChunkBytes = 4 << 20;

    /// <summary>Reads a drive root ("C:\") through its file table. False (nothing changed) when that can't be done; the caller then walks the folders.</summary>
    public static bool TryScanDrive(DiskNode top, int largestCount, ConcurrentBag<List<DiskFile>> largest, DiskScanProgress progress, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || !top.IsDrive || top.Path.Length != 3 || top.Path[1] != ':' || !char.IsAsciiLetter(top.Path[0])) return false;
        try
        {
            if (new DriveInfo(top.Path).DriveType is not (DriveType.Fixed or DriveType.Removable)) return false;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return false; }
        return TryScan(@"\\.\" + top.Path[..2], top, largestCount, largest, progress, ct);
    }

    /// <summary>Reads the NTFS volume at <paramref name="volume"/> (a drive like \\.\C: or, in tests, an image file) into <paramref name="top"/>.</summary>
    internal static bool TryScan(string volume, DiskNode top, int largestCount, ConcurrentBag<List<DiskFile>> largest, DiskScanProgress progress, CancellationToken ct)
    {
        Table? table;
        try
        {
            using var handle = File.OpenHandle(volume, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            progress.Current = "Reading the file table of " + top.Path;
            table = Read(handle, largestCount, progress, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            table = null;
        }
        if (table is null || !table.Dirs.TryGetValue(RootRecord, out var root) || !root.Seen) return false;
        largest.Add(table.Build(top));
        DiskAnalyzer.Total(top);
        top.FileTable = true;
        return true;
    }

    /// <summary>A folder found in the table: its name and parent, and the files right inside it.</summary>
    private sealed class Dir
    {
        public string Name = "";
        public long Parent = -1;
        public bool Seen, Reparse;
        public long OwnBytes;
        public int OwnFiles;
        public long[]? Kinds;
    }

    /// <summary>What one file record (with its extension records) says.</summary>
    private sealed class Entry
    {
        public bool InUse, IsDir, HasBase, HasList;
        public uint Attributes;
        public long Modified;
        public string? Name;
        public long Parent = -1;
        public long Size = -1;

        public void Take(Entry other)
        {
            Name ??= other.Name;
            if (Parent < 0) Parent = other.Parent;
            if (Size < 0) Size = other.Size;
        }
    }

    private readonly record struct Candidate(long Record, string Name, long Parent, long Modified);

    private sealed class Table
    {
        public readonly Dictionary<long, Dir> Dirs = new();
        private readonly PriorityQueue<Candidate, long> _largest = new();
        private readonly int _largestCount;

        public Table(int largestCount) => _largestCount = largestCount;

        private Dir DirOf(long record)
        {
            if (!Dirs.TryGetValue(record, out var d)) Dirs[record] = d = new Dir();
            return d;
        }

        public void Add(long record, Entry e, DiskScanProgress progress)
        {
            if (record < FirstUserRecord && record != RootRecord) return;
            if (e.IsDir)
            {
                var d = DirOf(record);
                d.Seen = true;
                d.Name = e.Name ?? "";
                d.Parent = record == RootRecord ? -1 : e.Parent;
                d.Reparse = (e.Attributes & ReparsePoint) != 0;
                return;
            }
            if (e.Name is null || e.Parent < 0 || (e.Attributes & ReparsePoint) != 0) return;
            long size = Math.Max(0, e.Size);
            var p = DirOf(e.Parent);
            p.OwnBytes += size;
            p.OwnFiles++;
            (p.Kinds ??= new long[FileKinds.All.Count])[(int)FileKinds.Of(e.Name)] += size;
            progress.Add(1, size);
            if (_largestCount <= 0) return;
            var c = new Candidate(record, e.Name, e.Parent, e.Modified);
            // A few extra, since a file whose folder turns out to be a link or gone is dropped at the end.
            if (_largest.Count < _largestCount + 64) _largest.Enqueue(c, size);
            else if (_largest.TryPeek(out _, out var smallest) && size > smallest) _largest.EnqueueDequeue(c, size);
        }

        /// <summary>Hangs every folder reachable from the root under <paramref name="top"/> and returns the largest files.</summary>
        public List<DiskFile> Build(DiskNode top)
        {
            var children = new Dictionary<long, List<long>>();
            foreach (var (record, d) in Dirs)
            {
                if (!d.Seen || d.Reparse || record == RootRecord || record < FirstUserRecord || d.Parent < 0) continue;
                if (!children.TryGetValue(d.Parent, out var list)) children[d.Parent] = list = new List<long>();
                list.Add(record);
            }

            var nodes = new Dictionary<long, DiskNode> { [RootRecord] = top };
            Fill(top, Dirs[RootRecord]);
            var queue = new Queue<long>();
            queue.Enqueue(RootRecord);
            while (queue.Count > 0)
            {
                long at = queue.Dequeue();
                if (!children.TryGetValue(at, out var list)) continue;
                var parent = nodes[at];
                foreach (var record in list)
                {
                    if (nodes.ContainsKey(record)) continue;
                    var d = Dirs[record];
                    var node = new DiskNode(d.Name, parent);
                    Fill(node, d);
                    parent.Children.Add(node);
                    nodes[record] = node;
                    queue.Enqueue(record);
                }
            }

            var files = new List<DiskFile>(_largest.Count);
            while (_largest.TryDequeue(out var c, out var size))
            {
                if (!nodes.TryGetValue(c.Parent, out var folder)) continue;
                DateTime modified;
                try { modified = DateTime.FromFileTime(c.Modified); }
                catch (ArgumentOutOfRangeException) { modified = DateTime.MinValue; }
                files.Add(new DiskFile(Path.Combine(folder.Path, c.Name), c.Name, size, modified));
            }
            files.Reverse();
            if (files.Count > _largestCount) files.RemoveRange(_largestCount, files.Count - _largestCount);
            return files;
        }

        private static void Fill(DiskNode node, Dir d)
        {
            node.OwnBytes = d.OwnBytes;
            node.OwnFiles = d.OwnFiles;
            if (d.Kinds is not null) Array.Copy(d.Kinds, node.OwnKindBytes, d.Kinds.Length);
        }
    }

    /// <summary>Where the file table is and how its records are laid out, from the volume's boot sector.</summary>
    private sealed record Volume(int SectorSize, int ClusterSize, long MftCluster, int RecordSize);

    private static Volume? ReadBoot(SafeFileHandle handle)
    {
        var b = new byte[4096]; // a whole sector even on drives with 4 KB sectors
        ReadExactly(handle, b, 0);
        if (Encoding.ASCII.GetString(b, 3, 8) != "NTFS    ") return null;
        int sector = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x0B));
        int perCluster = b[0x0D];
        if (sector is not (512 or 1024 or 2048 or 4096)) return null;
        long cluster = perCluster <= 0x80 ? (long)sector * perCluster : 1L << (256 - perCluster);
        if (cluster < sector || cluster > (2 << 20) || (cluster & (cluster - 1)) != 0) return null;
        long mft = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x30));
        sbyte perRecord = (sbyte)b[0x40];
        long record = perRecord > 0 ? perRecord * cluster : 1L << -perRecord;
        if (record < 512 || record > 65536 || (record & (record - 1)) != 0 || mft <= 0) return null;
        return new Volume(sector, (int)cluster, mft, (int)record);
    }

    private static Table? Read(SafeFileHandle handle, int largestCount, DiskScanProgress progress, CancellationToken ct)
    {
        if (ReadBoot(handle) is not { } vol) return null;
        int rs = vol.RecordSize;

        // Record 0 is the file table's own entry: where its pieces lie on the drive.
        var first = new byte[Math.Max(rs, vol.SectorSize)];
        ReadExactly(handle, first, vol.MftCluster * vol.ClusterSize);
        var rec0 = first.AsSpan(0, rs);
        if (!Fixup(rec0)) return null;
        var runs = new List<(long Vcn, long Lcn, long Count)>();
        if (!MftRuns(rec0, runs, out long length, out bool hasList)) return null;
        var mft = new Runs(handle, vol.ClusterSize, runs);
        if (hasList && !MoreMftRuns(rec0, mft, rs, runs)) return null;
        long records = Math.Min(length / rs, int.MaxValue);
        if (records <= RootRecord || (long)runs.Sum(r => r.Count) * vol.ClusterSize < records * rs) return null;

        var table = new Table(largestCount);
        var partial = new Dictionary<long, Entry>();
        long chunk = Math.Max(ChunkBytes, Math.Max(rs, vol.ClusterSize));
        var buffer = new byte[chunk + vol.ClusterSize];
        long total = records * rs;
        for (long at = 0; at < total; at += chunk)
        {
            ct.ThrowIfCancellationRequested();
            long want = Math.Min(chunk, total - at);
            long read = (want + vol.ClusterSize - 1) / vol.ClusterSize * vol.ClusterSize; // whole clusters, so every read lines up with the sectors
            mft.Read(at, buffer.AsSpan(0, (int)read));
            for (int off = 0; off + rs <= want; off += rs)
            {
                long index = (at + off) / rs;
                var r = buffer.AsSpan(off, rs);
                if (!Fixup(r)) continue; // unused or half-written while being read
                var e = Parse(r, index, out long owner);
                if (e is null) continue;
                if (owner == index && !e.HasList)
                {
                    if (e.InUse) table.Add(index, e, progress);
                    continue;
                }
                // Part of a record spread over several (many hard links, a very fragmented file): put the parts together at the end.
                if (!partial.TryGetValue(owner, out var whole)) partial[owner] = whole = new Entry();
                if (owner == index)
                {
                    whole.HasBase = true;
                    whole.InUse = e.InUse;
                    whole.IsDir = e.IsDir;
                    whole.Attributes = e.Attributes;
                    whole.Modified = e.Modified;
                }
                whole.Take(e);
            }
        }
        foreach (var (index, e) in partial)
            if (e.HasBase && e.InUse) table.Add(index, e, progress);
        return table;
    }

    /// <summary>Puts back the two bytes at the end of each sector that NTFS swaps out to spot half-written records.</summary>
    internal static bool Fixup(Span<byte> r)
    {
        if (r.Length < 48 || BinaryPrimitives.ReadUInt32LittleEndian(r) != 0x454C4946) return false; // "FILE"
        int usa = BinaryPrimitives.ReadUInt16LittleEndian(r[4..]);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(r[6..]);
        if (count < 2 || usa + count * 2 > r.Length) return false;
        int stride = r.Length / (count - 1);
        if (stride < 256 || stride * (count - 1) != r.Length) return false;
        ushort check = BinaryPrimitives.ReadUInt16LittleEndian(r[usa..]);
        for (int i = 1; i < count; i++)
        {
            int end = i * stride - 2;
            if (BinaryPrimitives.ReadUInt16LittleEndian(r[end..]) != check) return false;
            r[end] = r[usa + 2 * i];
            r[end + 1] = r[usa + 2 * i + 1];
        }
        return true;
    }

    /// <summary>The attributes of one record, as (type, offset, length) within it.</summary>
    private static List<(uint Type, int Offset, int Length)> AttributesOf(ReadOnlySpan<byte> r)
    {
        var list = new List<(uint, int, int)>();
        int off = BinaryPrimitives.ReadUInt16LittleEndian(r[0x14..]);
        int used = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(r[0x18..]), (uint)r.Length);
        while (off + 16 <= used)
        {
            uint type = BinaryPrimitives.ReadUInt32LittleEndian(r[off..]);
            if (type == 0xFFFF_FFFF) break;
            int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(r[(off + 4)..]);
            if (len < 16 || off + len > used) break;
            list.Add((type, off, len));
            off += len;
        }
        return list;
    }

    /// <summary>A resident attribute's value, or empty when it isn't resident or doesn't fit.</summary>
    private static ReadOnlySpan<byte> Value(ReadOnlySpan<byte> a)
    {
        if (a[8] != 0 || a.Length < 0x18) return default;
        int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(a[0x10..]);
        int off = BinaryPrimitives.ReadUInt16LittleEndian(a[0x14..]);
        return off + (long)len <= a.Length ? a.Slice(off, len) : default;
    }

    /// <summary>The name, folder, size and dates in one record. <paramref name="owner"/> is the record it belongs to (itself unless it's an extension).</summary>
    private static Entry? Parse(ReadOnlySpan<byte> r, long index, out long owner)
    {
        int flags = BinaryPrimitives.ReadUInt16LittleEndian(r[0x16..]);
        long baseRecord = BinaryPrimitives.ReadInt64LittleEndian(r[0x20..]) & RecordMask;
        owner = baseRecord == 0 ? index : baseRecord;
        var e = new Entry { InUse = (flags & 1) != 0, IsDir = (flags & 2) != 0 };
        int nameRank = -1;
        foreach (var (type, off, len) in AttributesOf(r))
        {
            var a = r.Slice(off, len);
            switch (type)
            {
                case 0x10: // $STANDARD_INFORMATION
                    var si = Value(a);
                    if (si.Length >= 0x24)
                    {
                        e.Modified = BinaryPrimitives.ReadInt64LittleEndian(si[0x08..]);
                        e.Attributes = BinaryPrimitives.ReadUInt32LittleEndian(si[0x20..]);
                    }
                    break;
                case 0x20: // $ATTRIBUTE_LIST: the rest of this file is in other records
                    e.HasList = true;
                    break;
                case 0x30: // $FILE_NAME (one per hard link, plus a short 8.3 name)
                    var fn = Value(a);
                    if (fn.Length < 0x42) break;
                    int nameLength = fn[0x40], space = fn[0x41];
                    if (space == 2 || 0x42 + nameLength * 2 > fn.Length) break; // the short 8.3 name
                    int rank = space is 1 or 3 ? 2 : 1; // the Windows name over a POSIX one
                    if (rank <= nameRank) break;
                    nameRank = rank;
                    e.Name = Encoding.Unicode.GetString(fn.Slice(0x42, nameLength * 2));
                    e.Parent = BinaryPrimitives.ReadInt64LittleEndian(fn) & RecordMask;
                    break;
                case 0x80: // $DATA: only the main contents (no name), as the folder walk counts
                    if (a[9] != 0) break;
                    if (a[8] == 0) e.Size = BinaryPrimitives.ReadUInt32LittleEndian(a[0x10..]);
                    else if (a.Length >= 0x40 && BinaryPrimitives.ReadInt64LittleEndian(a[0x10..]) == 0) e.Size = BinaryPrimitives.ReadInt64LittleEndian(a[0x30..]);
                    break;
            }
        }
        return e;
    }

    /// <summary>The file table's own pieces, from its record 0.</summary>
    private static bool MftRuns(ReadOnlySpan<byte> rec0, List<(long Vcn, long Lcn, long Count)> runs, out long length, out bool hasList)
    {
        length = 0;
        hasList = false;
        bool found = false;
        foreach (var (type, off, len) in AttributesOf(rec0))
        {
            var a = rec0.Slice(off, len);
            if (type == 0x20) hasList = true;
            if (type != 0x80 || a[9] != 0 || a[8] == 0 || a.Length < 0x40) continue;
            if (BinaryPrimitives.ReadInt64LittleEndian(a[0x10..]) != 0) continue;
            length = BinaryPrimitives.ReadInt64LittleEndian(a[0x38..]); // initialized size: records past it were never written
            if (!DecodeRuns(a, 0, runs)) return false;
            found = true;
        }
        return found && length > 0;
    }

    /// <summary>A very fragmented file table keeps the rest of its pieces in other records, named in record 0's attribute list.</summary>
    private static bool MoreMftRuns(ReadOnlySpan<byte> rec0, Runs mft, int rs, List<(long Vcn, long Lcn, long Count)> runs)
    {
        var parts = new List<(long Vcn, long Record)>();
        foreach (var (type, off, len) in AttributesOf(rec0))
        {
            if (type != 0x20) continue;
            var list = Value(rec0.Slice(off, len));
            if (list.IsEmpty) return false; // a list kept outside the record: too unusual, walk the folders instead
            for (int p = 0; p + 0x1A <= list.Length;)
            {
                uint t = BinaryPrimitives.ReadUInt32LittleEndian(list[p..]);
                int l = BinaryPrimitives.ReadUInt16LittleEndian(list[(p + 4)..]);
                if (l < 0x1A || p + l > list.Length) return false;
                long vcn = BinaryPrimitives.ReadInt64LittleEndian(list[(p + 8)..]);
                long record = BinaryPrimitives.ReadInt64LittleEndian(list[(p + 0x10)..]) & RecordMask;
                if (t == 0x80 && list[p + 6] == 0 && record != 0) parts.Add((vcn, record));
                p += l;
            }
        }
        var buffer = new byte[Math.Max(rs, mft.ClusterSize) + mft.ClusterSize];
        foreach (var (vcn, record) in parts.OrderBy(p => p.Vcn))
        {
            long at = record * rs, start = at / mft.ClusterSize * mft.ClusterSize;
            long read = (at + rs - start + mft.ClusterSize - 1) / mft.ClusterSize * mft.ClusterSize;
            if (!mft.Covers(start + read)) return false;
            mft.Read(start, buffer.AsSpan(0, (int)read));
            var r = buffer.AsSpan((int)(at - start), rs);
            if (!Fixup(r)) return false;
            bool added = false;
            foreach (var (type, off, len) in AttributesOf(r))
            {
                var a = r.Slice(off, len);
                if (type != 0x80 || a[9] != 0 || a[8] == 0 || BinaryPrimitives.ReadInt64LittleEndian(a[0x10..]) != vcn) continue;
                if (!DecodeRuns(a, vcn, runs)) return false;
                added = true;
            }
            if (!added) return false;
            mft.Refresh();
        }
        return true;
    }

    /// <summary>Reads a non-resident attribute's list of pieces (each a cluster count, and where it starts relative to the previous one).</summary>
    internal static bool DecodeRuns(ReadOnlySpan<byte> a, long vcn, List<(long Vcn, long Lcn, long Count)> runs)
    {
        int p = BinaryPrimitives.ReadUInt16LittleEndian(a[0x20..]);
        long lcn = 0;
        while (p < a.Length)
        {
            byte head = a[p];
            if (head == 0) return true;
            int lengthBytes = head & 0xF, offsetBytes = head >> 4;
            if (lengthBytes is 0 or > 8 || offsetBytes > 8 || p + 1 + lengthBytes + offsetBytes > a.Length) return false;
            long count = 0;
            for (int i = 0; i < lengthBytes; i++) count |= (long)a[p + 1 + i] << (8 * i);
            if (count <= 0) return false;
            if (offsetBytes == 0) runs.Add((vcn, -1, count)); // sparse: nothing on the drive
            else
            {
                long delta = 0;
                for (int i = 0; i < offsetBytes; i++) delta |= (long)a[p + 1 + lengthBytes + i] << (8 * i);
                if (offsetBytes < 8 && (a[p + lengthBytes + offsetBytes] & 0x80) != 0) delta |= -1L << (8 * offsetBytes);
                lcn += delta;
                if (lcn < 0) return false;
                runs.Add((vcn, lcn, count));
            }
            vcn += count;
            p += 1 + lengthBytes + offsetBytes;
        }
        return false;
    }

    /// <summary>The file table as one continuous stream over its pieces on the drive.</summary>
    private sealed class Runs
    {
        private readonly SafeFileHandle _handle;
        private readonly List<(long Vcn, long Lcn, long Count)> _runs;
        private (long Vcn, long Lcn, long Count)[] _sorted = Array.Empty<(long, long, long)>();
        public int ClusterSize { get; }

        public Runs(SafeFileHandle handle, int clusterSize, List<(long Vcn, long Lcn, long Count)> runs)
        {
            _handle = handle;
            ClusterSize = clusterSize;
            _runs = runs;
            Refresh();
        }

        public void Refresh() => _sorted = _runs.OrderBy(r => r.Vcn).ToArray();

        public bool Covers(long end)
        {
            long next = 0;
            foreach (var r in _sorted)
            {
                if (r.Vcn != next) return false;
                next += r.Count;
                if (next * ClusterSize >= end) return true;
            }
            return false;
        }

        public void Read(long offset, Span<byte> dest)
        {
            int i = 0;
            while (dest.Length > 0)
            {
                long vcn = offset / ClusterSize;
                while (i < _sorted.Length && _sorted[i].Vcn + _sorted[i].Count <= vcn) i++;
                if (i == _sorted.Length || _sorted[i].Vcn > vcn) throw new InvalidDataException("The file table has a gap.");
                var r = _sorted[i];
                long within = offset - r.Vcn * ClusterSize;
                int n = (int)Math.Min(dest.Length, r.Count * ClusterSize - within);
                if (r.Lcn < 0) dest[..n].Clear();
                else ReadExactly(_handle, dest[..n], r.Lcn * ClusterSize + within);
                dest = dest[n..];
                offset += n;
            }
        }
    }

    private static void ReadExactly(SafeFileHandle handle, Span<byte> dest, long offset)
    {
        while (dest.Length > 0)
        {
            int n = RandomAccess.Read(handle, dest, offset);
            if (n <= 0) throw new InvalidDataException("The drive ended early.");
            dest = dest[n..];
            offset += n;
        }
    }
}
