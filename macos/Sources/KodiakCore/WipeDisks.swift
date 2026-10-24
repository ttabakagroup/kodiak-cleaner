import Foundation

/// A whole physical disk that can be erased, or the reason it can't be.
public struct WipeDisk: Identifiable, Equatable {
    /// diskutil's identifier, such as disk4.
    public var id: String
    public var mediaName: String
    public var size: Int64
    public var kind: MediaKind
    public var isInternal: Bool
    public var isRemovable: Bool
    public var bus: String
    public var volumes: [String]
    public var reason: String?

    public init(id: String, mediaName: String, size: Int64, kind: MediaKind, isInternal: Bool, isRemovable: Bool,
                bus: String = "", volumes: [String] = [], reason: String? = nil) {
        self.id = id
        self.mediaName = mediaName
        self.size = size
        self.kind = kind
        self.isInternal = isInternal
        self.isRemovable = isRemovable
        self.bus = bus
        self.volumes = volumes
        self.reason = reason
    }

    public var canWipe: Bool { reason == nil }
    public var kindName: String { WipeMethods.kindName(kind) }
    public var name: String { "\(mediaName) (\(id))" }
    public var volumesText: String { volumes.isEmpty ? "No Volumes" : volumes.joined(separator: ", ") }

    /// The same drive as when it was listed: same identifier, size and media name.
    public func isSameDrive(as other: WipeDisk) -> Bool {
        id == other.id && size == other.size && mediaName == other.mediaName
    }
}

/// A mounted volume whose free space can be overwritten.
public struct WipeVolume: Identifiable, Equatable {
    /// Where it is mounted.
    public var mount: String
    /// The folder the filler files go in: the volume itself, or the home folder on the startup volume (the startup
    /// volume's top folder is read-only).
    public var folder: String
    public var name: String
    public var format: String
    public var typeName: String
    public var size: Int64
    public var free: Int64
    public var solidState: Bool?
    public var isStartup: Bool

    public init(mount: String, folder: String, name: String, format: String, typeName: String, size: Int64, free: Int64,
                solidState: Bool?, isStartup: Bool) {
        self.mount = mount
        self.folder = folder
        self.name = name
        self.format = format
        self.typeName = typeName
        self.size = size
        self.free = free
        self.solidState = solidState
        self.isStartup = isStartup
    }

    public var id: String { mount }
    public var isAPFS: Bool { typeName.lowercased() == "apfs" }
    public var isSSD: Bool { solidState == true }
    public var kindName: String { solidState == true ? "SSD" : (solidState == false ? "Hard Drive" : "Drive") }
    /// Room left free on the startup volume so macOS keeps running while the rest is full.
    public var reserve: Int64 { isStartup ? WipeMethods.gb : 0 }
    public var wipeable: Int64 { max(0, free - reserve) }
}

/// Reads the Mac's disks with diskutil and decides which ones may be erased.
public enum WipeDisks {
    public static let diskutilPath = "/usr/sbin/diskutil"

    /// Runs diskutil (read-only listing commands here); tests put a fake in its place.
    nonisolated(unsafe) public static var diskutil: ([String]) -> Run.Result = { arguments in
        Run.program(diskutilPath, arguments, timeout: 60)
    }

    public static let identifierPattern = try! NSRegularExpression(pattern: "^disk[0-9]{1,4}$")

    /// A whole-disk identifier such as disk4 (never a partition, a path or anything with other characters).
    public static func isWholeDisk(_ identifier: String) -> Bool {
        identifierPattern.firstMatch(in: identifier, range: NSRange(identifier.startIndex..., in: identifier)) != nil
    }

    /// disk0s2 -> disk0, /dev/disk4 -> disk4.
    public static func wholeDisk(of identifier: String) -> String? {
        var text = identifier
        if text.hasPrefix("/dev/") { text.removeFirst(5) }
        if text.hasPrefix("r") { text.removeFirst() }
        guard text.hasPrefix("disk") else { return nil }
        let digits = text.dropFirst(4).prefix { $0.isNumber }
        return digits.isEmpty ? nil : "disk" + digits
    }

    public static func plist(_ data: Data) -> [String: Any]? {
        (try? PropertyListSerialization.propertyList(from: data, options: [], format: nil)) as? [String: Any]
    }

    public static func plist(_ text: String) -> [String: Any]? { plist(Data(text.utf8)) }

    /// `diskutil info -plist <disk or path>`.
    public static func info(_ target: String) -> [String: Any]? {
        let result = diskutil(["info", "-plist", target])
        guard result.code == 0 else { return nil }
        return plist(result.output)
    }

    /// The physical whole disks a volume, container or disk sits on, following APFS containers to their physical stores.
    public static func backingDisks(of start: [String: Any], physical: Set<String>, lookup: (String) -> [String: Any]?) -> Set<String> {
        var found = Set<String>()
        var seen = Set<String>()
        var queue = [start]
        while let info = queue.popLast() {
            var candidates: [String] = []
            for key in ["DeviceIdentifier", "ParentWholeDisk", "APFSContainerReference"] {
                if let value = info[key] as? String { candidates.append(value) }
            }
            for store in info["APFSPhysicalStores"] as? [Any] ?? [] {
                if let value = store as? String { candidates.append(value) }
                if let entry = store as? [String: Any] {
                    for key in ["APFSPhysicalStore", "DeviceIdentifier"] {
                        if let value = entry[key] as? String { candidates.append(value) }
                    }
                }
            }
            for candidate in candidates {
                guard let whole = wholeDisk(of: candidate) else { continue }
                if physical.contains(whole) {
                    found.insert(whole)
                } else if !seen.contains(whole), let next = lookup(whole) {
                    seen.insert(whole)
                    queue.append(next)
                }
            }
        }
        return found
    }

    private static func bool(_ info: [String: Any], _ key: String) -> Bool? { info[key] as? Bool }

    private static func int64(_ info: [String: Any], _ keys: [String]) -> Int64 {
        for key in keys {
            if let number = info[key] as? NSNumber, number.int64Value > 0 { return number.int64Value }
        }
        return 0
    }

    public static func kind(_ info: [String: Any]) -> MediaKind {
        if bool(info, "RemovableMedia") == true || bool(info, "Removable") == true { return .flashDrive }
        switch bool(info, "SolidState") {
        case true?: return .ssd
        case false?: return .hardDrive
        default: return .unknown
        }
    }

    /// Volume names on each whole disk, from `diskutil list -plist physical`.
    static func volumeNames(_ list: [String: Any]) -> [String: [String]] {
        var names: [String: [String]] = [:]
        for disk in list["AllDisksAndPartitions"] as? [[String: Any]] ?? [] {
            guard let id = disk["DeviceIdentifier"] as? String else { continue }
            var found: [String] = []
            if let name = disk["VolumeName"] as? String, !name.isEmpty { found.append(name) }
            for part in disk["Partitions"] as? [[String: Any]] ?? [] {
                if (part["Content"] as? String) == "EFI" { continue }
                if let name = part["VolumeName"] as? String, !name.isEmpty, name != "EFI" { found.append(name) }
            }
            names[id] = found
        }
        return names
    }

    /// The disks in a `diskutil list -plist physical` listing, each with the reason it can't be erased (nil = it can).
    /// `protected` maps whole disks to the reason they must stay (macOS starts from it, Kodiak Cleaner runs from it,
    /// the home folder is on it). `bootKnown` false refuses every disk: if the startup disk can't be found, none is safe.
    public static func parse(list: [String: Any], info: (String) -> [String: Any]?, protected: [String: String],
                             bootKnown: Bool) -> [WipeDisk] {
        let whole = list["WholeDisks"] as? [String] ?? []
        let names = volumeNames(list)
        var disks: [WipeDisk] = []
        for id in whole where isWholeDisk(id) {
            let details = info(id) ?? [:]
            let media = ((details["MediaName"] as? String) ?? (details["IORegistryEntryName"] as? String) ?? "")
                .trimmingCharacters(in: .whitespaces)
            let size = int64(details, ["Size", "TotalSize"])
            let isInternal = bool(details, "Internal") ?? true
            let removable = bool(details, "RemovableMediaOrExternalDevice") ?? bool(details, "Ejectable") ?? false
            let bus = details["BusProtocol"] as? String ?? ""
            var reason: String?
            if details.isEmpty { reason = "macOS didn't describe this disk, so it can't be erased." }
            else if !bootKnown { reason = "Kodiak Cleaner couldn't tell which disk macOS starts from, so it won't erase any disk." }
            else if let why = protected[id] { reason = why }
            else if bus == "Disk Image" || (details["VirtualOrPhysical"] as? String) == "Virtual" { reason = "This is a disk image, not a drive." }
            // A memory card in the Mac's own card slot counts as internal but is removable, so it may be erased.
            else if isInternal && kind(details) != .flashDrive { reason = "This is an internal disk. To erase this Mac, use Erase All Content and Settings in System Settings." }
            else if size <= 0 { reason = "macOS doesn't report this disk's size." }
            disks.append(WipeDisk(id: id, mediaName: media.isEmpty ? "Disk" : media, size: size, kind: kind(details),
                                  isInternal: isInternal, isRemovable: removable || !isInternal, bus: bus,
                                  volumes: names[id] ?? [], reason: reason))
        }
        return disks.sorted { $0.id.compare($1.id, options: .numeric) == .orderedAscending }
    }

    /// Every physical disk, read now. Reads only; nothing is changed. Returns the disks and a problem if diskutil
    /// couldn't list them.
    public static func list(protectedPaths: [(path: String, reason: String)]) -> (disks: [WipeDisk], problem: String?) {
        let listed = diskutil(["list", "-plist", "physical"])
        guard listed.code == 0, let list = plist(listed.output) else {
            let why = listed.error.trimmingCharacters(in: .whitespacesAndNewlines)
            return ([], why.isEmpty ? "diskutil couldn't list the disks." : why)
        }
        let physical = Set((list["WholeDisks"] as? [String]) ?? [])
        var cache: [String: [String: Any]] = [:]
        let lookup: (String) -> [String: Any]? = { target in
            if let hit = cache[target] { return hit }
            let found = info(target)
            if let found { cache[target] = found }
            return found
        }
        var protected: [String: String] = [:]
        var bootKnown = false
        for (index, entry) in protectedPaths.enumerated() {
            guard let start = lookup(entry.path) else { continue }
            let disks = backingDisks(of: start, physical: physical, lookup: lookup)
            if index == 0 && !disks.isEmpty { bootKnown = true }
            for disk in disks where protected[disk] == nil { protected[disk] = entry.reason }
        }
        return (parse(list: list, info: lookup, protected: protected, bootKnown: bootKnown), nil)
    }

    /// The paths whose disks are never erased, the startup volume first.
    public static func defaultProtectedPaths(appPath: String) -> [(path: String, reason: String)] {
        [("/", "macOS starts from this disk."),
         (appPath, "Kodiak Cleaner is running from this disk."),
         (Paths.home, "Your home folder is on this disk.")]
    }

    /// Mounted volumes whose free space can be wiped: the startup volume (through the home folder) and every other
    /// local volume that can be written to.
    public static func volumes() -> [WipeVolume] {
        let keys: [URLResourceKey] = [.volumeNameKey, .volumeTotalCapacityKey, .volumeAvailableCapacityKey, .volumeIsReadOnlyKey,
                                      .volumeLocalizedFormatDescriptionKey, .volumeIsRootFileSystemKey, .volumeIsLocalKey,
                                      .volumeTypeNameKey]
        let urls = FileManager.default.mountedVolumeURLs(includingResourceValuesForKeys: keys, options: [.skipHiddenVolumes]) ?? []
        var found: [WipeVolume] = []
        for url in urls {
            guard let values = try? url.resourceValues(forKeys: Set(keys)) else { continue }
            if values.volumeIsLocal == false { continue }
            let isStartup = values.volumeIsRootFileSystem == true || url.path == "/"
            let folder = isStartup ? Paths.home : url.path
            if !isStartup && values.volumeIsReadOnly == true { continue }
            guard FileManager.default.isWritableFile(atPath: folder) else { continue }
            // The startup volume's own numbers come from the Data volume the home folder is on.
            let measured = isStartup ? (try? URL(fileURLWithPath: folder).resourceValues(forKeys: Set(keys))) ?? values : values
            let details = info(url.path) ?? [:]
            found.append(WipeVolume(mount: url.path, folder: folder,
                                    name: values.volumeName ?? url.lastPathComponent,
                                    format: values.volumeLocalizedFormatDescription ?? "",
                                    typeName: measured.volumeTypeName ?? values.volumeTypeName ?? "",
                                    size: Int64(measured.volumeTotalCapacity ?? 0),
                                    free: Int64(measured.volumeAvailableCapacity ?? 0),
                                    solidState: details["SolidState"] as? Bool, isStartup: isStartup))
        }
        return found.sorted { ($0.isStartup ? 0 : 1, $0.name.lowercased()) < ($1.isStartup ? 0 : 1, $1.name.lowercased()) }
    }
}
