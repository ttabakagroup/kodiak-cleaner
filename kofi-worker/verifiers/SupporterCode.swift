// Checks a supporter code offline, for Swift apps (macOS 10.15+ / iOS 13+, CryptoKit).
// Copy this file into the app with the product's public key. Nothing is sent anywhere.
//
//   let (status, license) = SupporterCode.verify(code, publicKeyPEM: key, withdrawn: ["withdrawn-id"])
//
// A code is "<payload>.<signature>" in base64url: JSON {"id","name","issued"} signed with ECDSA P-256 /
// SHA-256, signature as r||s (64 bytes).
import CryptoKit
import Foundation

public enum SupporterCode {
    public enum Status { case valid, empty, malformed, badSignature, withdrawn, unverifiable }
    public struct License { public let id: String; public let name: String; public let issued: String }

    static func base64URL(_ text: Substring) -> Data? {
        var s = text.replacingOccurrences(of: "-", with: "+").replacingOccurrences(of: "_", with: "/")
        while s.count % 4 != 0 { s += "=" }
        return Data(base64Encoded: s)
    }

    public static func verify(_ code: String, publicKeyPEM: String, withdrawn: Set<String> = []) -> (Status, License?) {
        let code = code.filter { !$0.isWhitespace }
        if code.isEmpty { return (.empty, nil) }
        let parts = code.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 2, let payload = base64URL(parts[0]), let signature = base64URL(parts[1]) else { return (.malformed, nil) }
        guard signature.count == 64 else { return (.badSignature, nil) }
        do {
            let key = try P256.Signing.PublicKey(pemRepresentation: publicKeyPEM)
            let sig = try P256.Signing.ECDSASignature(rawRepresentation: signature)
            guard key.isValidSignature(sig, for: payload) else { return (.badSignature, nil) }
        } catch {
            return (.unverifiable, nil)
        }
        guard let json = try? JSONSerialization.jsonObject(with: payload) as? [String: Any] else { return (.malformed, nil) }
        let id = "\(json["id"] ?? "")".trimmingCharacters(in: .whitespaces)
        if json["id"] == nil || id.isEmpty { return (.malformed, nil) }
        if withdrawn.contains(where: { $0.lowercased() == id.lowercased() }) { return (.withdrawn, nil) }
        return (.valid, License(id: id, name: json["name"] as? String ?? "", issued: json["issued"] as? String ?? ""))
    }
}
