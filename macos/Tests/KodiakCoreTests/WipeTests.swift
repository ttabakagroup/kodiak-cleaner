import XCTest
@testable import KodiakCore

/// Drive Wiper. Nothing here erases or writes to a disk: diskutil is always a fake, and free space wiping writes into
/// a throwaway folder with a fake "free space" that runs out after a few megabytes.
final class WipeMethodsTests: XCTestCase {
    let gb = WipeMethods.gb

    func testPassesMatchWindows() {
        XCTAssertEqual(WipeMethods.passes(.zeros), [.zero])
        XCTAssertEqual(WipeMethods.passes(.dod3), [.zero, .one, .random])
        XCTAssertEqual(WipeMethods.passes(.dod7).count, 7)
        XCTAssertEqual(WipeMethods.passes(.pass30).count, 30)
        XCTAssertEqual(WipeMethods.passes(.pass30).filter { $0 == .one }.count, 10)
        XCTAssertEqual(WipeMethod.allCases.map(WipeMethods.name), ["1 Pass (Zeros)", "DoD 3 Passes", "DoD 7 Passes", "30 Passes"])
    }

    func testDrivePasses() {
        XCTAssertNil(WipeMethods.drivePasses(.quick, kind: .hardDrive, method: .dod7))
        XCTAssertEqual(WipeMethods.drivePasses(.thorough, kind: .hardDrive, method: .dod7), [.zero])
        XCTAssertEqual(WipeMethods.drivePasses(.thorough, kind: .ssd, method: .zeros), [.random])
        XCTAssertEqual(WipeMethods.drivePasses(.secure, kind: .ssd, method: .pass30), [.random])
        // diskutil can't write ones, so a whole drive gets random data in their place.
        XCTAssertEqual(WipeMethods.drivePasses(.secure, kind: .flashDrive, method: .dod3), [.zero, .random, .random])
        XCTAssertEqual(WipeMethods.drivePasses(.secure, kind: .hardDrive, method: .pass30)?.count, 30)
        XCTAssertFalse(WipeMethods.drivePasses(.secure, kind: .hardDrive, method: .dod7)!.contains(.one))
        XCTAssertEqual(WipeMethods.methodText(.quick, kind: .ssd, method: .dod3), "Partition Map Only")
        XCTAssertEqual(WipeMethods.methodText(.secure, kind: .ssd, method: .dod3), "1 Pass (Random)")
        XCTAssertEqual(WipeMethods.methodText(.secure, kind: .hardDrive, method: .dod7), "DoD 7 Passes")
    }

    func testRecommendedFormat() {
        let small = WipeMethods.recommended(kind: .flashDrive, size: 16 * gb, label: "Wiped")
        XCTAssertEqual(small, DiskFormat(.fat32, gpt: false, label: "Wiped"))
        XCTAssertEqual(WipeMethods.recommended(kind: .flashDrive, size: 32 * gb, label: "").fileSystem, .fat32)
        let big = WipeMethods.recommended(kind: .flashDrive, size: 64 * gb, label: "")
        XCTAssertEqual(big.fileSystem, .exfat)
        XCTAssertFalse(big.gpt)
        XCTAssertTrue(WipeMethods.recommended(kind: .flashDrive, size: 4096 * gb, label: "").gpt)
        for kind in [MediaKind.hardDrive, .ssd, .unknown] {
            XCTAssertEqual(WipeMethods.recommended(kind: kind, size: 500 * gb, label: ""), DiskFormat(.exfat, gpt: true, label: ""))
        }
        XCTAssertEqual(WipeMethods.recommendedWhy(kind: .flashDrive, size: 8 * gb),
                       "Recommended for a flash drive: MBR with FAT32, which works in almost every device, including old ones.")
        XCTAssertEqual(WipeMethods.recommendedWhy(kind: .ssd, size: 1000 * gb),
                       "Recommended for an external SSD: GPT with exFAT, which works in Macs and Windows PCs, and on drives over 2 TB.")
    }

    func testFormatProblems() {
        XCTAssertNil(WipeMethods.formatProblem(size: 3000 * gb, format: nil))
        XCTAssertNil(WipeMethods.formatProblem(size: 32 * gb, format: DiskFormat(.fat32, gpt: false, label: "")))
        XCTAssertNotNil(WipeMethods.formatProblem(size: 33 * gb, format: DiskFormat(.fat32, gpt: true, label: "")))
        XCTAssertNotNil(WipeMethods.formatProblem(size: 2049 * gb, format: DiskFormat(.exfat, gpt: false, label: "")))
        XCTAssertNil(WipeMethods.formatProblem(size: 2048 * gb, format: DiskFormat(.exfat, gpt: false, label: "")))
        XCTAssertNotNil(WipeMethods.formatProblem(size: 8 * gb, format: DiskFormat(.apfs, gpt: false, label: "")))
        XCTAssertNil(WipeMethods.formatProblem(size: 8 * gb, format: DiskFormat(.hfs, gpt: false, label: "")))
        // Every recommendation passes its own check.
        for kind in [MediaKind.flashDrive, .hardDrive, .ssd, .unknown] {
            for size in [1 * gb, 32 * gb, 33 * gb, 2048 * gb, 2049 * gb, 20_000 * gb] {
                XCTAssertNil(WipeMethods.formatProblem(size: size, format: WipeMethods.recommended(kind: kind, size: size, label: "")),
                             "\(kind) \(size)")
            }
        }
    }

    func testSafeLabel() {
        XCTAssertEqual(DiskFormat(.fat32, gpt: false, label: "my stick; rm -rf ~").safeLabel, "MY STICK RM")
        XCTAssertEqual(DiskFormat(.exfat, gpt: true, label: "  Holiday \"Photos\" 2026  ").safeLabel, "Holiday Pho")
        XCTAssertEqual(DiskFormat(.apfs, gpt: true, label: "Backup_Drive-2").safeLabel, "Backup_Drive-2")
        XCTAssertEqual(DiskFormat(.apfs, gpt: true, label: "%$*").diskutilName, "Untitled")
        XCTAssertEqual(DiskFormat(.fat32, gpt: true, label: "").diskutilName, "UNTITLED")
        XCTAssertEqual(DiskFormat(.exfat, gpt: true, label: "Ünïcode").safeLabel, "ncode")
    }
}

final class WipeDisksTests: XCTestCase {
    func testIdentifiers() {
        XCTAssertTrue(WipeDisks.isWholeDisk("disk4"))
        XCTAssertFalse(WipeDisks.isWholeDisk("disk4s1"))
        XCTAssertFalse(WipeDisks.isWholeDisk("/dev/disk4"))
        XCTAssertFalse(WipeDisks.isWholeDisk("disk4; rm"))
        XCTAssertFalse(WipeDisks.isWholeDisk(""))
        XCTAssertEqual(WipeDisks.wholeDisk(of: "disk0s2"), "disk0")
        XCTAssertEqual(WipeDisks.wholeDisk(of: "/dev/rdisk12s1"), "disk12")
        XCTAssertNil(WipeDisks.wholeDisk(of: "Macintosh HD"))
    }

    static let list = """
    <?xml version="1.0" encoding="UTF-8"?>
    <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
    <plist version="1.0"><dict>
      <key>AllDisks</key><array><string>disk0</string><string>disk0s1</string><string>disk0s2</string><string>disk4</string><string>disk4s1</string><string>disk6</string><string>disk7</string></array>
      <key>AllDisksAndPartitions</key><array>
        <dict><key>DeviceIdentifier</key><string>disk0</string><key>Content</key><string>GUID_partition_scheme</string>
          <key>Partitions</key><array>
            <dict><key>DeviceIdentifier</key><string>disk0s1</string><key>Content</key><string>Apple_APFS_ISC</string></dict>
            <dict><key>DeviceIdentifier</key><string>disk0s2</string><key>Content</key><string>Apple_APFS</string></dict>
          </array></dict>
        <dict><key>DeviceIdentifier</key><string>disk4</string><key>Content</key><string>FDisk_partition_scheme</string>
          <key>Partitions</key><array>
            <dict><key>DeviceIdentifier</key><string>disk4s1</string><key>Content</key><string>DOS_FAT_32</string><key>VolumeName</key><string>STICK</string><key>MountPoint</key><string>/Volumes/STICK</string></dict>
          </array></dict>
        <dict><key>DeviceIdentifier</key><string>disk6</string><key>Content</key><string>GUID_partition_scheme</string>
          <key>Partitions</key><array>
            <dict><key>DeviceIdentifier</key><string>disk6s1</string><key>Content</key><string>EFI</string><key>VolumeName</key><string>EFI</string></dict>
            <dict><key>DeviceIdentifier</key><string>disk6s2</string><key>Content</key><string>Apple_HFS</string><key>VolumeName</key><string>Backup</string></dict>
          </array></dict>
        <dict><key>DeviceIdentifier</key><string>disk7</string><key>Content</key><string>GUID_partition_scheme</string></dict>
      </array>
      <key>WholeDisks</key><array><string>disk0</string><string>disk4</string><string>disk6</string><string>disk7</string></array>
    </dict></plist>
    """

    static let infos: [String: [String: Any]] = [
        "disk0": ["DeviceIdentifier": "disk0", "MediaName": "APPLE SSD AP0512Z", "Size": NSNumber(value: Int64(500_277_790_720)),
                  "Internal": true, "SolidState": true, "RemovableMedia": false, "BusProtocol": "Apple Fabric", "VirtualOrPhysical": "Physical"],
        "disk4": ["DeviceIdentifier": "disk4", "MediaName": "SanDisk Ultra", "Size": NSNumber(value: Int64(30_765_219_840)),
                  "Internal": false, "RemovableMedia": true, "RemovableMediaOrExternalDevice": true, "BusProtocol": "USB"],
        "disk6": ["DeviceIdentifier": "disk6", "MediaName": "Samsung T7", "Size": NSNumber(value: Int64(1_000_204_886_016)),
                  "Internal": false, "SolidState": true, "RemovableMedia": false, "RemovableMediaOrExternalDevice": true, "BusProtocol": "USB"],
        "disk7": ["DeviceIdentifier": "disk7", "MediaName": "WD Elements", "Size": NSNumber(value: Int64(4_000_787_030_016)),
                  "Internal": false, "SolidState": false, "RemovableMedia": false, "BusProtocol": "USB"],
        // The startup volume sits in APFS container disk3, whose physical store is disk0s2.
        "/": ["DeviceIdentifier": "disk3s1s1", "ParentWholeDisk": "disk3", "APFSContainerReference": "disk3"],
        "disk3": ["DeviceIdentifier": "disk3", "APFSPhysicalStores": [["APFSPhysicalStore": "disk0s2"]]],
    ]

    func fakeDiskutil(_ infos: [String: [String: Any]], list: String = WipeDisksTests.list) -> ([String]) -> Run.Result {
        return { arguments in
            if arguments == ["list", "-plist", "physical"] { return Run.Result(code: 0, output: list, error: "") }
            if arguments.count == 3, arguments[0] == "info", arguments[1] == "-plist", let info = infos[arguments[2]],
               let data = try? PropertyListSerialization.data(fromPropertyList: info, format: .xml, options: 0) {
                return Run.Result(code: 0, output: String(decoding: data, as: UTF8.self), error: "")
            }
            return Run.Result(code: 1, output: "", error: "Could not find disk: \(arguments.last ?? "")")
        }
    }

    private var saved: (([String]) -> Run.Result)!
    override func setUp() { super.setUp(); saved = WipeDisks.diskutil }
    override func tearDown() { WipeDisks.diskutil = saved; super.tearDown() }

    func testListRefusesTheStartupDiskAndKeepsExternals() {
        WipeDisks.diskutil = fakeDiskutil(Self.infos)
        let (disks, problem) = WipeDisks.list(protectedPaths: [("/", "macOS starts from this disk.")])
        XCTAssertNil(problem)
        XCTAssertEqual(disks.map(\.id), ["disk0", "disk4", "disk6", "disk7"])
        XCTAssertEqual(disks[0].reason, "macOS starts from this disk.")
        XCTAssertTrue(disks[1].canWipe)
        XCTAssertEqual(disks[1].kind, .flashDrive)
        XCTAssertEqual(disks[1].volumes, ["STICK"])
        XCTAssertEqual(disks[1].name, "SanDisk Ultra (disk4)")
        XCTAssertEqual(disks[2].kind, .ssd)
        XCTAssertEqual(disks[2].volumes, ["Backup"])
        XCTAssertEqual(disks[3].kind, .hardDrive)
        XCTAssertEqual(disks[3].size, 4_000_787_030_016)
        XCTAssertTrue(disks[2].canWipe && disks[3].canWipe)
    }

    func testInternalDisksAreRefusedEvenIfNotTheStartupDisk() {
        var infos = Self.infos
        infos["disk3"] = ["DeviceIdentifier": "disk3", "APFSPhysicalStores": ["disk6s2"]]   // started from the T7
        WipeDisks.diskutil = fakeDiskutil(infos)
        let disks = WipeDisks.list(protectedPaths: [("/", "macOS starts from this disk.")]).disks
        XCTAssertEqual(disks.first { $0.id == "disk6" }?.reason, "macOS starts from this disk.")
        XCTAssertTrue(disks.first { $0.id == "disk0" }!.reason!.contains("internal disk"))
        XCTAssertTrue(disks.first { $0.id == "disk4" }!.canWipe)
    }

    func testAppAndHomeDisksAreRefused() {
        var infos = Self.infos
        infos["/Volumes/STICK/Kodiak Cleaner.app"] = ["DeviceIdentifier": "disk4s1", "ParentWholeDisk": "disk4"]
        infos["/Users/jane"] = ["DeviceIdentifier": "disk7s2", "ParentWholeDisk": "disk7"]
        WipeDisks.diskutil = fakeDiskutil(infos)
        let disks = WipeDisks.list(protectedPaths: [("/", "macOS starts from this disk."),
                                                    ("/Volumes/STICK/Kodiak Cleaner.app", "Kodiak Cleaner is running from this disk."),
                                                    ("/Users/jane", "Your home folder is on this disk.")]).disks
        XCTAssertEqual(disks.first { $0.id == "disk4" }?.reason, "Kodiak Cleaner is running from this disk.")
        XCTAssertEqual(disks.first { $0.id == "disk7" }?.reason, "Your home folder is on this disk.")
        XCTAssertTrue(disks.first { $0.id == "disk6" }!.canWipe)
    }

    func testUnknownStartupDiskRefusesEverything() {
        var infos = Self.infos
        infos["/"] = nil
        WipeDisks.diskutil = fakeDiskutil(infos)
        let disks = WipeDisks.list(protectedPaths: [("/", "macOS starts from this disk.")]).disks
        XCTAssertFalse(disks.isEmpty)
        XCTAssertTrue(disks.allSatisfy { !$0.canWipe })
    }

    func testDiskImagesAndUnreadableDisksAreRefused() {
        var infos = Self.infos
        infos["disk7"]!["BusProtocol"] = "Disk Image"
        infos["disk6"] = nil
        WipeDisks.diskutil = fakeDiskutil(infos)
        let disks = WipeDisks.list(protectedPaths: [("/", "macOS starts from this disk.")]).disks
        XCTAssertEqual(disks.first { $0.id == "disk7" }?.reason, "This is a disk image, not a drive.")
        XCTAssertFalse(disks.first { $0.id == "disk6" }!.canWipe)
    }

    func testListFailure() {
        WipeDisks.diskutil = { _ in Run.Result(code: 1, output: "", error: "diskutil failed") }
        let (disks, problem) = WipeDisks.list(protectedPaths: [("/", "x")])
        XCTAssertTrue(disks.isEmpty)
        XCTAssertEqual(problem, "diskutil failed")
    }

    func testSameDrive() {
        let disk = WipeDisk(id: "disk4", mediaName: "SanDisk Ultra", size: 100, kind: .flashDrive, isInternal: false, isRemovable: true)
        var other = disk
        XCTAssertTrue(disk.isSameDrive(as: other))
        other.size = 101
        XCTAssertFalse(disk.isSameDrive(as: other))
        other = disk
        other.mediaName = "Other"
        XCTAssertFalse(disk.isSameDrive(as: other))
    }
}

/// A diskutil that records what it was asked and answers from a script. Nothing runs.
final class FakeRunner: DiskCommandRunner {
    var commands: [[String]] = []
    var answers: [(code: Int32, text: String)] = []
    var adminScripts: [String] = []
    var adminAnswer: (code: Int32, error: String) = (0, "")
    var adminLog: ((String) -> String)?
    var onRun: (([String]) -> Void)?

    func run(_ arguments: [String], output: @escaping (String) -> Void) -> (code: Int32, text: String) {
        commands.append(arguments)
        onRun?(arguments)
        let answer = answers.isEmpty ? (code: Int32(0), text: "Finished") : answers.removeFirst()
        output(answer.text)
        return answer
    }

    func interrupt() {}

    func runAsAdministrator(_ script: String) -> (code: Int32, error: String) {
        adminScripts.append(script)
        // Write what the real script would have logged.
        if let adminLog, let range = script.range(of: ">> '") {
            let rest = script[range.upperBound...]
            if let end = rest.firstIndex(of: "'") {
                let path = String(rest[..<end])
                try? adminLog(script).write(toFile: path, atomically: true, encoding: .utf8)
            }
        }
        return adminAnswer
    }
}

final class DiskEraserTests: XCTestCase {
    let gb = WipeMethods.gb
    lazy var stick = WipeDisk(id: "disk4", mediaName: "SanDisk Ultra", size: 16 * gb, kind: .flashDrive, isInternal: false, isRemovable: true)
    var folder = ""

    override func setUp() {
        super.setUp()
        folder = FileManager.default.temporaryDirectory.appendingPathComponent("kodiak-erase-\(UUID().uuidString)").path
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
    }

    override func tearDown() {
        try? FileManager.default.removeItem(atPath: folder)
        super.tearDown()
    }

    func testStepsForEachLevel() {
        let format = DiskFormat(.exfat, gpt: true, label: "Wiped")
        let quick = DiskEraser.steps(disk: "disk4", level: .quick, kind: .hardDrive, method: .dod7, format: format)
        XCTAssertEqual(quick.map(\.arguments), [["eraseDisk", "ExFAT", "Wiped", "GPT", "disk4"]])
        let thorough = DiskEraser.steps(disk: "disk4", level: .thorough, kind: .hardDrive, method: .dod7, format: nil)
        XCTAssertEqual(thorough.map(\.arguments), [["zeroDisk", "disk4"], ["eraseDisk", "free", "EMPTY", "GPT", "disk4"], ["zeroDisk", "short", "disk4"]])
        let ssd = DiskEraser.steps(disk: "disk6", level: .secure, kind: .ssd, method: .pass30, format: format)
        XCTAssertEqual(ssd.first?.arguments, ["randomDisk", "1", "disk6"])
        XCTAssertEqual(ssd.filter(\.isPass).count, 1)
        let dod3 = DiskEraser.steps(disk: "disk4", level: .secure, kind: .flashDrive, method: .dod3,
                                    format: DiskFormat(.fat32, gpt: false, label: "my stick"))
        XCTAssertEqual(dod3.map(\.arguments), [["zeroDisk", "disk4"], ["randomDisk", "1", "disk4"], ["randomDisk", "1", "disk4"],
                                               ["eraseDisk", "FAT32", "MY STICK", "MBR", "disk4"]])
        XCTAssertEqual(dod3[1].title, "pass 2 of 3 (Random Data)")
        XCTAssertEqual(DiskEraser.steps(disk: "disk7", level: .secure, kind: .hardDrive, method: .pass30, format: format).filter(\.isPass).count, 30)
    }

    func testFallbackOnlyWhenMBRFits() {
        XCTAssertEqual(DiskEraser.fallback(for: DiskFormat(.exfat, gpt: true, label: ""), size: gb), DiskFormat(.exfat, gpt: false, label: ""))
        XCTAssertNil(DiskEraser.fallback(for: DiskFormat(.exfat, gpt: false, label: ""), size: gb))
        XCTAssertNil(DiskEraser.fallback(for: DiskFormat(.exfat, gpt: true, label: ""), size: 3000 * gb))
        XCTAssertNil(DiskEraser.fallback(for: DiskFormat(.apfs, gpt: true, label: ""), size: gb))
    }

    func testProgressParsing() {
        let line = "Started erase on disk4\nUnmounting disk\n[ / 0%..10%..20%..................... ] 23% 0:41:02\r"
        let found = DiskEraser.progress(in: line)
        XCTAssertEqual(found.percent, 23)
        XCTAssertEqual(found.remaining, "0:41:02")
        XCTAssertEqual(DiskEraser.progress(in: "[ \\ 0%..10%..20%..30%.. ").percent, 30)
        XCTAssertEqual(DiskEraser.progress(in: "] 100% \nFinished erase on disk4").percent, 100)
        XCTAssertNil(DiskEraser.progress(in: "Started erase on disk4").percent)
    }

    func testErrorLines() {
        XCTAssertEqual(DiskEraser.errorLine("Started erase on disk4\nUnmounting disk\nError: -69888: Couldn't unmount disk\n"),
                       "Error: -69888: Couldn't unmount disk")
        XCTAssertEqual(DiskEraser.errorLine("Something odd\n"), "Something odd")
        XCTAssertNil(DiskEraser.errorLine("\n\n"))
        XCTAssertTrue(DiskEraser.needsAdministrator("Error: -69877: Couldn't open device"))
        XCTAssertTrue(DiskEraser.needsAdministrator("zeroDisk must be run as root"))
        XCTAssertFalse(DiskEraser.needsAdministrator("Error: -69888: Couldn't unmount disk"))
    }

    func testQuoting() {
        XCTAssertEqual(DiskEraser.shellQuote("it's"), "'it'\\''s'")
        let apple = DiskEraser.appleScript("echo \"a\\b\"")
        XCTAssertEqual(apple, "do shell script \"echo \\\"a\\\\b\\\"\" with administrator privileges")
    }

    func testAdministratorScript() {
        let steps = DiskEraser.steps(disk: "disk4", level: .thorough, kind: .hardDrive, method: .zeros,
                                     format: DiskFormat(.exfat, gpt: true, label: "Wiped"))
        let script = DiskEraser.administratorScript(steps, start: 0, disk: "disk4", size: gb, log: "/tmp/x y/log", stopFile: "/tmp/x y/stop")
        XCTAssertTrue(script.contains("if [ -e '/tmp/x y/stop' ]; then echo '@@stopped'"))
        XCTAssertTrue(script.contains("'/usr/sbin/diskutil' 'zeroDisk' 'disk4' >> '/tmp/x y/log' 2>&1"))
        XCTAssertTrue(script.contains("'/usr/sbin/diskutil' 'eraseDisk' 'ExFAT' 'Wiped' 'MBR' 'disk4'"), "falls back to MBR once")
        XCTAssertFalse(script.contains("\n"))
        let later = DiskEraser.administratorScript(steps, start: 1, disk: "disk4", size: gb, log: "/l", stopFile: "/s")
        XCTAssertFalse(later.contains("zeroDisk"))
        XCTAssertTrue(later.hasPrefix("echo '@@step 1'"))
    }

    func testReadLog() {
        let log = "@@step 0\nStarted erase\n[ 0%..50% ] 50% 0:01:00\n@@exit 0 0\n@@step 1\nError: -69888: Couldn't unmount disk\n@@exit 1 1\n@@fallback\nFinished erase\n@@exit fallback 0\n"
        let state = DiskEraser.readLog(log)
        XCTAssertEqual(state.exits, [0: 0, 1: 1])
        XCTAssertTrue(state.fellBack)
        XCTAssertEqual(state.fallbackExit, 0)
        XCTAssertEqual(state.current, 1)
    }

    func testEraseRunsEveryStep() {
        let runner = FakeRunner()
        let steps = DiskEraser.steps(disk: "disk4", level: .secure, kind: .flashDrive, method: .dod3,
                                     format: DiskFormat(.fat32, gpt: false, label: "Wiped"))
        var statuses: [DiskEraseStatus] = []
        runner.answers = [(0, "[ 0%..100% ] 100% 0:00:00"), (0, ""), (0, ""), (0, "Finished erase")]
        let result = DiskEraser.erase(disk: stick, steps: steps, runner: runner, stop: WipeStop(), workFolder: folder) { statuses.append($0) }
        XCTAssertEqual(runner.commands.count, 4)
        XCTAssertEqual(result.passesDone, 3)
        XCTAssertTrue(result.setUp)
        XCTAssertEqual(result.format?.fileSystem, .fat32)
        XCTAssertNil(result.error)
        XCTAssertFalse(result.stopped)
        XCTAssertTrue(statuses.contains { $0.percent == 100 })
    }

    func testSetupFallsBackToMBRAndReportsErrors() {
        let runner = FakeRunner()
        let steps = DiskEraser.steps(disk: "disk4", level: .quick, kind: .flashDrive, method: .zeros,
                                     format: DiskFormat(.exfat, gpt: true, label: "Wiped"))
        runner.answers = [(1, "Error: -69760: Unable to write to the last block of the device"), (0, "Finished")]
        let result = DiskEraser.erase(disk: stick, steps: steps, runner: runner, stop: WipeStop(), workFolder: folder) { _ in }
        XCTAssertEqual(runner.commands.last, ["eraseDisk", "ExFAT", "Wiped", "MBR", "disk4"])
        XCTAssertTrue(result.fellBackToMBR)
        XCTAssertEqual(result.format?.gpt, false)

        let failing = FakeRunner()
        failing.answers = [(1, "Error: first"), (1, "Started\nError: -69888: Couldn't unmount disk")]
        let failed = DiskEraser.erase(disk: stick, steps: steps, runner: failing, stop: WipeStop(), workFolder: folder) { _ in }
        XCTAssertEqual(failing.commands.count, 2, "MBR is tried once only")
        XCTAssertFalse(failed.setUp)
        XCTAssertEqual(failed.setupError, "Error: -69888: Couldn't unmount disk")
    }

    func testStopSkipsPassesButStillSetsUp() {
        let runner = FakeRunner()
        let stop = WipeStop()
        runner.onRun = { arguments in if arguments.first == "zeroDisk" { stop.stop() } }
        runner.answers = [(130, "interrupted"), (0, "Finished")]
        let steps = DiskEraser.steps(disk: "disk4", level: .secure, kind: .hardDrive, method: .dod7,
                                     format: DiskFormat(.exfat, gpt: true, label: "Wiped"))
        let result = DiskEraser.erase(disk: stick, steps: steps, runner: runner, stop: stop, workFolder: folder) { _ in }
        XCTAssertTrue(result.stopped)
        XCTAssertEqual(result.passesDone, 0)
        XCTAssertEqual(runner.commands.count, 2)
        XCTAssertEqual(runner.commands.last?.first, "eraseDisk")
        XCTAssertTrue(result.setUp)
    }

    func testPassFailureStops() {
        let runner = FakeRunner()
        runner.answers = [(1, "Error: -69825: Wrapped Error: the disk is write protected")]
        let steps = DiskEraser.steps(disk: "disk4", level: .thorough, kind: .hardDrive, method: .zeros, format: nil)
        let result = DiskEraser.erase(disk: stick, steps: steps, runner: runner, stop: WipeStop(), workFolder: folder) { _ in }
        XCTAssertEqual(runner.commands.count, 1)
        XCTAssertEqual(result.error, "Error: -69825: Wrapped Error: the disk is write protected")
    }

    func testNeedsRootRunsTheRestAsAdministratorOnce() {
        let runner = FakeRunner()
        runner.answers = [(0, "done"), (1, "Error: -69877: Couldn't open device")]
        runner.adminLog = { _ in "@@step 1\nFinished\n@@exit 1 0\n@@step 2\n@@exit 2 0\n@@step 3\nFinished erase\n@@exit 3 0\n" }
        let steps = DiskEraser.steps(disk: "disk4", level: .secure, kind: .flashDrive, method: .dod3,
                                     format: DiskFormat(.fat32, gpt: false, label: "Wiped"))
        let result = DiskEraser.erase(disk: stick, steps: steps, runner: runner, stop: WipeStop(), workFolder: folder) { _ in }
        XCTAssertEqual(runner.adminScripts.count, 1, "one password prompt for the whole job")
        XCTAssertTrue(runner.adminScripts[0].hasPrefix("if [ -e "))
        XCTAssertTrue(runner.adminScripts[0].contains("@@step 1"))
        XCTAssertFalse(runner.adminScripts[0].contains("@@step 0"))
        XCTAssertTrue(result.asAdministrator)
        XCTAssertEqual(result.passesDone, 3)
        XCTAssertTrue(result.setUp)
        XCTAssertNil(result.error)
    }

    func testCancelledPasswordPrompt() {
        let runner = FakeRunner()
        runner.answers = [(1, "Error: -69877: Couldn't open device")]
        runner.adminAnswer = (1, "Cancelled")
        let steps = DiskEraser.steps(disk: "disk4", level: .thorough, kind: .hardDrive, method: .zeros, format: nil)
        let result = DiskEraser.erase(disk: stick, steps: steps, runner: runner, stop: WipeStop(), workFolder: folder) { _ in }
        XCTAssertEqual(result.error, "The administrator password prompt was cancelled, so it wasn't erased.")
        XCTAssertEqual(result.passesDone, 0)
    }
}

final class FreeSpaceWiperTests: XCTestCase {
    var folder = ""

    override func setUp() {
        super.setUp()
        folder = FileManager.default.temporaryDirectory.appendingPathComponent("kodiak-free-\(UUID().uuidString)").path
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
    }

    override func tearDown() {
        try? FileManager.default.removeItem(atPath: folder)
        super.tearDown()
    }

    /// A pretend drive with `size` bytes free: whatever the filler folders hold is taken off it.
    func fakeFree(_ size: Int64) -> (String) -> Int64 {
        return { folder in
            var used: Int64 = 0
            let manager = FileManager.default
            for name in (try? manager.contentsOfDirectory(atPath: folder)) ?? [] where name.hasPrefix(FreeSpaceWiper.prefix) {
                let work = (folder as NSString).appendingPathComponent(name)
                for file in (try? manager.contentsOfDirectory(atPath: work)) ?? [] {
                    let attributes = try? manager.attributesOfItem(atPath: (work as NSString).appendingPathComponent(file))
                    used += (attributes?[.size] as? NSNumber)?.int64Value ?? 0
                }
            }
            return size - used
        }
    }

    func testFillsThenRemovesEverything() throws {
        var reports: [FreeSpaceWiper.Progress] = []
        let outcome = try FreeSpaceWiper.wipe(folder: folder, passes: WipeMethods.passes(.dod3), freeSpace: fakeFree(10 * 1024 * 1024),
                                              stop: WipeStop()) { reports.append($0) }
        XCTAssertEqual(outcome.passesDone, 3)
        XCTAssertEqual(outcome.bytes, 10 * 1024 * 1024)
        XCTAssertFalse(outcome.stopped)
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: folder), [], "nothing is left behind")
        XCTAssertEqual(reports.last?.pass, 3)
        XCTAssertEqual(reports.last?.fraction ?? 0, 1, accuracy: 0.0001)
        XCTAssertEqual(Set(reports.map(\.passName)), ["Zeros", "Ones", "Random Data"])
    }

    func testReserveStaysFree() throws {
        let outcome = try FreeSpaceWiper.wipe(folder: folder, passes: [.zero], reserve: 6 * 1024 * 1024,
                                              freeSpace: fakeFree(10 * 1024 * 1024), stop: WipeStop()) { _ in }
        XCTAssertEqual(outcome.bytes, 4 * 1024 * 1024)
    }

    func testStopRemovesTheFillerFiles() throws {
        let stop = WipeStop()
        let outcome = try FreeSpaceWiper.wipe(folder: folder, passes: WipeMethods.passes(.dod7), freeSpace: fakeFree(64 * 1024 * 1024),
                                              stop: stop) { progress in if progress.written > 0 { stop.stop() } }
        XCTAssertTrue(outcome.stopped)
        XCTAssertLessThan(outcome.passesDone, 7)
        XCTAssertTrue(stop.isDone)
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: folder), [])
    }

    func testCleanupRemovesLeftoversButNotLinksOrOtherFolders() throws {
        let manager = FileManager.default
        let target = (folder as NSString).appendingPathComponent("keep")
        try manager.createDirectory(atPath: target, withIntermediateDirectories: true)
        manager.createFile(atPath: target + "/file.txt", contents: Data("x".utf8))
        try manager.createDirectory(atPath: folder + "/" + FreeSpaceWiper.prefix + "old", withIntermediateDirectories: true)
        try manager.createSymbolicLink(atPath: folder + "/" + FreeSpaceWiper.prefix + "link", withDestinationPath: target)
        XCTAssertEqual(FreeSpaceWiper.cleanup(folder), 1)
        XCTAssertTrue(manager.fileExists(atPath: target + "/file.txt"))
    }

    func testRandomPassIsNotZeros() throws {
        var random = FastRandom()
        var buffer = [UInt8](repeating: 0, count: 4096)
        random.fill(&buffer)
        XCTAssertGreaterThan(Set(buffer).count, 200)
    }

    func testNoPassesIsAnError() {
        XCTAssertThrowsError(try FreeSpaceWiper.wipe(folder: folder, passes: [], stop: WipeStop()) { _ in })
    }
}
