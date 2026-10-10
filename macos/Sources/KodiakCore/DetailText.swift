import Foundation

/// Hover details (Ted, 0.8.3; same as Windows 0.8.63): one "Label: value" line per field, in the order given, skipping
/// empty values. Line breaks inside a value become spaces so each field stays on its own line.
public enum DetailText {
    public static func lines(_ fields: [(String, String)]) -> String {
        fields.compactMap { field -> String? in
            let value = field.1.components(separatedBy: .newlines).joined(separator: " ")
                .trimmingCharacters(in: .whitespaces)
            return value.isEmpty ? nil : "\(field.0): \(value)"
        }
        .joined(separator: "\n")
    }
}
