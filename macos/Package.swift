// swift-tools-version: 6.2
import PackageDescription

// Kodiak Cleaner for Mac. KodiakCore holds the logic (Foundation only, unit tested); KodiakCleaner is the SwiftUI app.
// build.sh turns the KodiakCleaner executable into Kodiak Cleaner.app.
let package = Package(
    name: "KodiakCleaner",
    platforms: [.macOS(.v26)],
    products: [
        .executable(name: "KodiakCleaner", targets: ["KodiakCleaner"]),
    ],
    targets: [
        .target(name: "KodiakCore"),
        .executableTarget(name: "KodiakCleaner", dependencies: ["KodiakCore"]),
        .testTarget(name: "KodiakCoreTests", dependencies: ["KodiakCore"]),
    ],
    swiftLanguageModes: [.v5]
)
