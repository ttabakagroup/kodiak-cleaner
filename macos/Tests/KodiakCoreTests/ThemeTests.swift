import XCTest
@testable import KodiakCore

/// Every color scheme, light and dark, with its own accent and every accent swatch, meets WCAG 2.2 AA: text 4.5:1;
/// controls, focus rings and switches 3:1. Translucent tokens are measured on top of the surface color. The same pairs
/// as the standard's scripts/check_contrast.py and the Linux edition's tests.
final class ThemeTests: XCTestCase {
    static let text = [("text", "bg"), ("text", "surface"), ("text", "surface_alt"), ("text", "row_alt"), ("text", "row_hover"),
                       ("text", "tint@surface"), ("muted", "bg"), ("muted", "surface"), ("muted", "surface_alt"),
                       ("muted", "row_alt"), ("link", "bg"), ("link", "surface"), ("link", "surface_alt"), ("accent_text", "bg"),
                       ("accent_text", "surface"), ("accent_text", "tint@surface"), ("on_primary", "primary"), ("danger", "bg"),
                       ("danger", "surface"), ("danger_fg", "danger_bg"), ("warn_text", "warn_bg"), ("chip_text", "chip_bg")]
    static let ui = [("control_border", "surface"), ("control_border", "surface_alt"), ("control_border", "bg"),
                     ("focus_ring", "bg"), ("focus_ring", "surface"), ("focus_ring", "surface_alt"),
                     ("toggle_on", "surface"), ("toggle_on", "surface_alt"), ("surface", "toggle_on"),
                     ("success", "surface"), ("on_success", "success")]

    func failures(_ table: [String: String]) -> [String] {
        func color(_ name: String) -> String {
            name == "tint@surface" ? Themes.over(table["accent_tint"]!, table["surface"]!) : Themes.over(table[name]!, table["surface"]!)
        }
        var found: [String] = []
        for (pairs, need) in [(Self.text, 4.5), (Self.ui, 3.0)] {
            for (a, b) in pairs {
                let ratio = Themes.contrast(color(a), color(b))
                if ratio < need { found.append("\(a) on \(b): \(String(format: "%.2f", ratio)) (need \(need))") }
            }
        }
        return found
    }

    func testEverySchemeAndAccentPasses() {
        var checked = 0
        for scheme in Themes.schemeOrder {
            for dark in [false, true] {
                for accent in Themes.accentNames(scheme) {
                    let systems: [String?] = accent == Themes.systemAccent ? ["#007AFF", "#FFC600", "#8C8C8C", "#E0383E"] : [nil]
                    for system in systems {
                        let table = Themes.tokensFor(scheme, dark: dark, accent: accent, systemAccentColor: system)
                        XCTAssertEqual(failures(table), [], "\(scheme) \(dark ? "dark" : "light") \(accent) \(system ?? "")")
                    }
                    checked += 1
                }
            }
        }
        XCTAssertEqual(checked, 4 * 2 * 9 - 2)  // Classic has no separate Blue swatch
    }

    /// The Swift port gives exactly the shades the Linux edition's themes.py (and the standard's accent.js) give.
    func testAccentPortMatchesLinuxEdition() {
        let expected: [(String, Bool, String, [String: String])] = [
            ("honey", false, "Teal", ["accent": "#0D9488", "accent_text": "#0A736A", "primary": "#0C8479",
                                      "accent_tint": "rgba(13,148,136,0.18)", "row_hover": "rgba(13,148,136,0.18)"]),
            ("honey", true, "Pink", ["accent": "#DB2777", "accent_text": "#E97AAB", "primary": "#DB2777",
                                     "accent_tint": "rgba(219,39,119,0.24)"]),
            ("glacier", false, "Orange", ["accent": "#EA580C", "accent_text": "#AB4009", "primary": "#CD4D0B"]),
            ("glacier", true, "Slate", ["accent": "#66768F", "accent_text": "#9FAABA", "primary": "#525F73"]),
            ("lagoon", false, "Orange", ["accent": "#EA580C", "accent_text": "#B24309", "primary": "#CD4D0B"]),
            ("lagoon", true, "Purple", ["accent": "#7F3FED", "accent_text": "#AB80F3", "primary": "#7C3AED"]),
            ("classic", true, "Purple", ["accent": "#8F56F0", "accent_text": "#B590F5", "primary": "#7C3AED", "row_hover": "#363A42"]),
        ]
        for (scheme, dark, accent, values) in expected {
            let table = Themes.tokensFor(scheme, dark: dark, accent: accent)
            for (key, value) in values { XCTAssertEqual(table[key], value, "\(scheme) \(dark) \(accent) \(key)") }
        }
        let blue = Themes.accentTokens("#007AFF", table: Themes.schemes["honey"]!.light, dark: false)
        XCTAssertEqual(blue["accent_text"], "#0062CC")
        XCTAssertEqual(blue["primary"], "#0071ED")
    }

    func testSwatchOrderAndDuplicates() {
        XCTAssertEqual(Themes.accentNames("honey"), ["", "macOS", "Blue", "Teal", "Green", "Purple", "Pink", "Orange", "Slate"])
        XCTAssertFalse(Themes.accentNames("classic").contains("Blue"))
        XCTAssertEqual(Themes.effectiveAccent("classic", "Blue"), "")
        XCTAssertEqual(Themes.effectiveAccent("honey", "Graphite"), "Slate")
        XCTAssertEqual(Themes.effectiveAccent("honey", "Nope"), "")
        XCTAssertEqual(Themes.schemeOrder, ["honey", "classic", "glacier", "lagoon"])
        XCTAssertEqual(Themes.supporterSchemes, ["classic", "glacier", "lagoon"])
    }

    func testEveryTokenDefined() {
        for scheme in Themes.schemeOrder {
            for table in [Themes.schemes[scheme]!.light, Themes.schemes[scheme]!.dark] {
                XCTAssertEqual(Set(table.keys), Set(Themes.tokens), scheme)
            }
        }
    }

    /// Each Color Scheme tile shows a strip of its primary, secondary and tertiary colors (Ted, 0.8.15).
    func testSchemePreviewStrip() {
        for scheme in Themes.schemeOrder {
            for dark in [false, true] {
                let colors = Themes.schemePreview(scheme, dark: dark)
                XCTAssertEqual(colors.count, 3, scheme)
                XCTAssertEqual(Set(colors).count, 3, scheme)
            }
        }
        XCTAssertEqual(Themes.schemePreview("honey", dark: false), ["#6B4A34", "#D9A441", "#F5EFE6"])
        XCTAssertTrue(Themes.schemeReasons["classic"]!.contains("thank-you to supporters"))
    }
}
