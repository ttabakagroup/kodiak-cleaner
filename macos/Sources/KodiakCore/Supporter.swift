import Foundation
#if canImport(CryptoKit)
import CryptoKit
#endif

/// Free and Supporter plans. Supporter codes are checked offline against the public key built into Kodiak Cleaner;
/// nothing is sent anywhere. The same codes work in the Windows and Linux editions.
///
/// A code is "<payload>.<signature>" in base64url. The payload is JSON {"id", "name", "issued"}, signed with Ted's
/// private ECDSA P-256 key (SHA-256, signature as r||s).
public enum Supporter {
    public enum Status: String { case valid, empty, malformed, badSignature, withdrawn, unverifiable }

    public static let messages: [Status: String] = [
        .valid: "Thank you! Your supporter code is active.",
        .empty: "Enter the code you received after supporting Kodiak Cleaner.",
        .malformed: "That doesn't look like a supporter code. Check that you copied all of it.",
        .badSignature: "That code isn't valid. Check that you copied all of it, or contact us for help.",
        .withdrawn: "That code has been withdrawn. Contact us if you think this is a mistake.",
        .unverifiable: "This copy of Kodiak Cleaner can't check supporter codes.",
    ]

    public struct License: Equatable {
        public var id: String
        public var name: String
        public var issued: String
    }

    /// Ted's public key once he has made one; otherwise the test key, which release builds refuse.
    nonisolated(unsafe) public static var keysFolder: String = Resources.path("keys") ?? ""

    public static var publicKey: (pem: String, isTest: Bool) {
        for (name, test) in [("supporter-public.pem", false), ("supporter-test-public.pem", true)] {
            if let text = try? String(contentsOfFile: keysFolder + "/" + name, encoding: .ascii) { return (text, test) }
        }
        return ("", true)
    }

    public static func withdrawnIDs() -> Set<String> {
        guard let text = try? String(contentsOfFile: keysFolder + "/withdrawn-codes.txt", encoding: .utf8) else { return [] }
        return Set(text.split(whereSeparator: \.isNewline).map { $0.trimmingCharacters(in: .whitespaces).lowercased() }
            .filter { !$0.isEmpty && !$0.hasPrefix("#") })
    }

    static func base64URL(_ text: Substring) -> Data? {
        var s = text.replacingOccurrences(of: "-", with: "+").replacingOccurrences(of: "_", with: "/")
        while s.count % 4 != 0 { s += "=" }
        return Data(base64Encoded: s)
    }

    public static func verify(_ code: String, publicKey: String? = nil, withdrawn: Set<String>? = nil) -> (Status, License?) {
        let code = code.filter { !$0.isWhitespace }
        if code.isEmpty { return (.empty, nil) }
        let parts = code.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 2, let payload = base64URL(parts[0]), let signature = base64URL(parts[1]) else {
            return (.malformed, nil)
        }
        #if canImport(CryptoKit)
        guard signature.count == 64 else { return (.badSignature, nil) }
        do {
            let key = try P256.Signing.PublicKey(pemRepresentation: publicKey ?? Self.publicKey.pem)
            let sig = try P256.Signing.ECDSASignature(rawRepresentation: signature)
            guard key.isValidSignature(sig, for: payload) else { return (.badSignature, nil) }
        } catch {
            return (.unverifiable, nil)
        }
        #else
        return (.unverifiable, nil)
        #endif
        guard let json = try? JSONSerialization.jsonObject(with: payload) as? [String: Any] else { return (.malformed, nil) }
        let id = "\(json["id"] ?? "")".trimmingCharacters(in: .whitespaces)
        if json["id"] == nil || id.isEmpty { return (.malformed, nil) }
        if (withdrawn ?? withdrawnIDs()).contains(id.lowercased()) { return (.withdrawn, nil) }
        return (.valid, License(id: id, name: json["name"] as? String ?? "", issued: json["issued"] as? String ?? ""))
    }

    /// What the settings unlock. The Developer page's plan preview counts only in development builds.
    public struct Plan {
        public var license: License?
        public var previewSupporter: Bool
        public var previewName: String

        public init(settings: AppSettings) {
            let (status, license) = Supporter.verify(settings.supporterCode)
            self.license = status == .valid ? license : nil
            previewSupporter = AppInfo.hasDeveloperTools && settings.preview == "supporter"
            previewName = settings.previewName.trimmingCharacters(in: .whitespaces)
        }

        public var isSupporter: Bool { previewSupporter || license != nil }
        public var name: String { isSupporter ? "Supporter" : "Free" }
        public var howUnlocked: String { license != nil ? "Supporter code" : (previewSupporter ? "Developer preview" : "None") }
        public var supporterName: String? {
            if previewSupporter && license == nil { return previewName.isEmpty ? nil : previewName }
            return license?.name.isEmpty == false ? license?.name : nil
        }
        public var badgeText: String { previewSupporter ? "Supporter · Dev" : "Supporter" }
        /// Classic, Glacier and Lagoon fall back to Honey without a code.
        public func scheme(_ chosen: String) -> String {
            Themes.supporterSchemes.contains(chosen) && !isSupporter ? "honey" : chosen
        }
    }
}

/// Files that ship inside Kodiak Cleaner.app (Contents/Resources), or the Resources folder when running from source.
public enum Resources {
    public static func path(_ name: String) -> String? {
        if let url = Bundle.main.resourceURL?.appendingPathComponent(name), FileManager.default.fileExists(atPath: url.path) {
            return url.path
        }
        let source = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent()
        for folder in ["Resources", ""] {
            let url = source.appendingPathComponent(folder).appendingPathComponent(name)
            if FileManager.default.fileExists(atPath: url.path) { return url.path }
        }
        return nil
    }

    public static func text(_ name: String) -> String {
        guard let path = path(name) else { return "" }
        return (try? String(contentsOfFile: path, encoding: .utf8)) ?? ""
    }
}
