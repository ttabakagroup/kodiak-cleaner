import Foundation

// Drive Wiper's rules, ported from the Windows edition (WipeMethods.cs, 0.8.112): wipe methods and their passes, wipe
// levels, the format a drive is set up with afterwards, the recommended format for each drive type and the checks that
// stop a format that can't work. Everything here is plain data, so it is unit tested without touching a disk.

/// How a drive or its free space is overwritten.
public enum WipeMethod: String, CaseIterable, Codable {
    /// One pass of zeros: enough for any modern hard drive (NIST SP 800-88 Clear).
    case zeros
    /// DoD 5220.22-M: zeros, ones, then random data.
    case dod3
    /// DoD 5220.22-M ECE: the 3-pass pattern, a random pass, then the 3-pass pattern again (7 passes).
    case dod7
    /// The DoD 3-pass pattern ten times over. Not a published standard; no modern drive needs more than one pass.
    case pass30
}

/// One overwrite pass: a fixed byte, or random data (`fill` nil).
public struct WipePass: Equatable {
    public var fill: UInt8?
    public init(_ fill: UInt8?) { self.fill = fill }
    public var isRandom: Bool { fill == nil }
    public var name: String {
        switch fill {
        case nil: return "Random Data"
        case 0x00?: return "Zeros"
        case 0xFF?: return "Ones"
        case let byte?: return String(format: "0x%02X", byte)
        }
    }
    public static let zero = WipePass(0x00), one = WipePass(0xFF), random = WipePass(nil)
}

/// How far an Entire Drive wipe goes.
public enum WipeLevel: String, CaseIterable, Codable {
    /// Removes the partition map (MBR or GPT) only: the drive looks empty, but the data is still on it.
    case quick
    /// Every sector once: zeros on hard drives and flash drives, random data on SSDs.
    case thorough
    /// The chosen method's passes on hard drives and flash drives; random data on SSDs.
    case secure
}

/// What a drive is, as far as erasing goes.
public enum MediaKind: String, Codable {
    case flashDrive, hardDrive, ssd, unknown
}

/// The file systems a wiped drive can be set up with, in the order they are offered.
public enum WipeFileSystem: String, CaseIterable, Codable {
    case apfs, hfs, exfat, fat32

    /// The name Disk Utility uses.
    public var label: String {
        switch self {
        case .apfs: return "APFS"
        case .hfs: return "Mac OS Extended (Journaled)"
        case .exfat: return "exFAT"
        case .fat32: return "MS-DOS (FAT32)"
        }
    }

    /// The short name for running text.
    public var short: String {
        switch self {
        case .apfs: return "APFS"
        case .hfs: return "Mac OS Extended"
        case .exfat: return "exFAT"
        case .fat32: return "FAT32"
        }
    }

    /// The file system personality diskutil takes.
    public var personality: String {
        switch self {
        case .apfs: return "APFS"
        case .hfs: return "JHFS+"
        case .exfat: return "ExFAT"
        case .fat32: return "FAT32"
        }
    }

    /// The longest volume name the file system holds (FAT32 and exFAT: 11 characters).
    public var maxName: Int { self == .fat32 || self == .exfat ? 11 : 32 }
}

/// How the drive is set up after wiping. Nil everywhere means "leave it uninitialized, like new from the factory".
public struct DiskFormat: Equatable {
    public var fileSystem: WipeFileSystem
    public var gpt: Bool
    public var label: String

    public init(_ fileSystem: WipeFileSystem, gpt: Bool, label: String) {
        self.fileSystem = fileSystem
        self.gpt = gpt
        self.label = label
    }

    public var partitionStyle: String { gpt ? "GPT" : "MBR" }
    /// diskutil's name for the partition map.
    public var scheme: String { gpt ? "GPT" : "MBR" }

    /// Only letters, digits, spaces, hyphens and underscores, no longer than the file system allows, and in capitals
    /// for FAT32 (which stores names that way).
    public var safeLabel: String {
        let kept = label.filter { c in (c.isASCII && (c.isLetter || c.isNumber)) || c == " " || c == "-" || c == "_" }
        var text = kept.trimmingCharacters(in: .whitespaces)
        text = String(text.prefix(fileSystem.maxName)).trimmingCharacters(in: .whitespaces)
        return fileSystem == .fat32 ? text.uppercased() : text
    }

    /// The name handed to diskutil, which always needs one.
    public var diskutilName: String {
        let name = safeLabel
        if !name.isEmpty { return name }
        return fileSystem == .fat32 ? "UNTITLED" : "Untitled"
    }

    public func with(gpt: Bool) -> DiskFormat { DiskFormat(fileSystem, gpt: gpt, label: label) }
}

public enum WipeMethods {
    public static let gb: Int64 = 1024 * 1024 * 1024

    public static func levelName(_ level: WipeLevel) -> String {
        switch level {
        case .quick: return "Quick"
        case .thorough: return "Thorough"
        case .secure: return "Secure"
        }
    }

    public static func name(_ method: WipeMethod) -> String {
        switch method {
        case .zeros: return "1 Pass (Zeros)"
        case .dod3: return "DoD 3 Passes"
        case .dod7: return "DoD 7 Passes"
        case .pass30: return "30 Passes"
        }
    }

    public static func standard(_ method: WipeMethod) -> String {
        switch method {
        case .zeros: return "NIST SP 800-88 Clear"
        case .dod3: return "DoD 5220.22-M"
        case .dod7: return "DoD 5220.22-M ECE"
        case .pass30: return "DoD 5220.22-M pattern, 10 times (not a standard)"
        }
    }

    /// The passes of a method, written exactly (free space wiping writes these bytes itself).
    public static func passes(_ method: WipeMethod) -> [WipePass] {
        switch method {
        case .zeros: return [.zero]
        case .dod3: return [.zero, .one, .random]
        case .dod7: return [.zero, .one, .random, .random, .zero, .one, .random]
        case .pass30: return Array(repeating: [WipePass.zero, .one, .random], count: 10).flatMap { $0 }
        }
    }

    /// The passes an Entire Drive wipe makes (nil = no overwrite, only the partition map is removed). macOS has no
    /// firmware erase for external SSDs, so they always get one pass of random data, the most macOS can do for them.
    /// macOS's disk tool writes zeros or random data but not ones, so the ones pass is random data on a whole drive.
    public static func drivePasses(_ level: WipeLevel, kind: MediaKind, method: WipeMethod) -> [WipePass]? {
        switch level {
        case .quick: return nil
        case .thorough: return kind == .ssd ? [.random] : [.zero]
        case .secure: return kind == .ssd ? [.random] : passes(method).map { $0 == .one ? .random : $0 }
        }
    }

    /// The Method column for a drive.
    public static func methodText(_ level: WipeLevel, kind: MediaKind, method: WipeMethod) -> String {
        switch level {
        case .quick: return "Partition Map Only"
        case .thorough: return kind == .ssd ? "1 Pass (Random)" : "1 Pass (Zeros)"
        case .secure: return kind == .ssd ? "1 Pass (Random)" : name(method)
        }
    }

    public static func kindName(_ kind: MediaKind) -> String {
        switch kind {
        case .flashDrive: return "Flash Drive"
        case .hardDrive: return "Hard Drive"
        case .ssd: return "SSD"
        case .unknown: return "Drive"
        }
    }

    /// Why a format can't be used on a drive of this size (nil = it can).
    public static func formatProblem(size: Int64, format: DiskFormat?) -> String? {
        guard let format else { return nil }
        if !format.gpt && size > 2048 * gb { return "MBR can only use the first 2 TB of a drive this size. Choose GPT." }
        if format.fileSystem == .fat32 && size > 32 * gb {
            return "Kodiak Cleaner only formats drives up to 32 GB as MS-DOS (FAT32), so that Windows and other devices read them. Choose exFAT."
        }
        if format.fileSystem == .apfs && !format.gpt { return "APFS needs GPT. Choose GPT, or another file system." }
        return nil
    }

    /// The suggested format for a drive (Ted): flash drives MBR with FAT32 up to 32 GB or exFAT above, which TVs, cars
    /// and cameras read; other external drives GPT with exFAT, so Windows PCs can use them too. MBR can't use more than
    /// 2 TB, so bigger drives are always GPT.
    public static func recommended(kind: MediaKind, size: Int64, label: String) -> DiskFormat {
        if kind == .flashDrive { return DiskFormat(size <= 32 * gb ? .fat32 : .exfat, gpt: size > 2048 * gb, label: label) }
        return DiskFormat(.exfat, gpt: true, label: label)
    }

    /// What the drive is called in running text.
    public static func describe(_ kind: MediaKind) -> String {
        switch kind {
        case .flashDrive: return "flash drive"
        case .hardDrive: return "external hard drive"
        case .ssd: return "external SSD"
        case .unknown: return "external drive"
        }
    }

    /// Why the suggestion was made, for the notice above the format choices.
    public static func recommendedWhy(kind: MediaKind, size: Int64) -> String {
        let format = recommended(kind: kind, size: size, label: "")
        let why: String
        if kind == .flashDrive {
            why = format.fileSystem == .fat32 ? "works in almost every device, including old ones"
                                              : "works in Macs, Windows PCs, TVs, cars and cameras"
        } else {
            why = "works in Macs and Windows PCs, and on drives over 2 TB"
        }
        let name = describe(kind)
        let article = "aeiou".contains(name.first ?? "x") ? "an" : "a"
        return "Recommended for \(article) \(name): \(format.partitionStyle) with \(format.fileSystem.short), which \(why)."
    }

    /// Passes for free space wiping, by method (each byte is written exactly).
    public static func freeSpacePasses(_ method: WipeMethod) -> [WipePass] { passes(method) }
}
