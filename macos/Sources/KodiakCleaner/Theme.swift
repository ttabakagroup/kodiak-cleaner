import AppKit
import CoreText
import KodiakCore
import SwiftUI

/// The chosen color scheme and mode as SwiftUI colors and fonts. Every color comes from KodiakCore's Themes tables;
/// pages use these names and never a color of their own.
struct Theme {
    let scheme: String
    let dark: Bool
    let tokens: [String: String]
    let radius: CGFloat
    let radiusCard: CGFloat
    let displayFont: String
    let bodyFont: String

    init(scheme: String, dark: Bool, accent: String, systemAccent: String?) {
        self.scheme = scheme
        self.dark = dark
        tokens = Themes.tokensFor(scheme, dark: dark, accent: accent, systemAccentColor: systemAccent)
        let info = Themes.schemes[scheme] ?? Themes.schemes["honey"]!
        radius = info.radius
        radiusCard = info.radiusCard
        displayFont = info.displayFont
        bodyFont = info.bodyFont
    }

    static func color(_ token: String) -> Color {
        let c = Themes.parse(token)
        return Color(.sRGB, red: c.r / 255, green: c.g / 255, blue: c.b / 255, opacity: c.a)
    }

    subscript(_ name: String) -> Color { tokens[name].map(Theme.color) ?? .clear }

    var bg: Color { self["bg"] }
    var surface: Color { self["surface"] }
    var surfaceAlt: Color { self["surface_alt"] }
    var rowAlt: Color { self["row_alt"] }
    var rowHover: Color { self["row_hover"] }
    var rowLine: Color { self["row_line"] }
    var line: Color { self["line"] }
    var text: Color { self["text"] }
    var muted: Color { self["muted"] }
    var link: Color { self["link"] }
    var primary: Color { self["primary"] }
    var onPrimary: Color { self["on_primary"] }
    var accent: Color { self["accent"] }
    var accentText: Color { self["accent_text"] }
    var accentTint: Color { self["accent_tint"] }
    var success: Color { self["success"] }
    var toggleOn: Color { self["toggle_on"] }
    var controlBorder: Color { self["control_border"] }
    var focusRing: Color { self["focus_ring"] }
    var warnBg: Color { self["warn_bg"] }
    var warnText: Color { self["warn_text"] }
    var warnBorder: Color { self["warn_border"] }
    var danger: Color { self["danger"] }
    var dangerBg: Color { self["danger_bg"] }
    var dangerFg: Color { self["danger_fg"] }
    var dangerBorder: Color { self["danger_border"] }
    var chipBg: Color { self["chip_bg"] }
    var chipText: Color { self["chip_text"] }
    var brand1: Color { self["brand_1"] }
    var brand2: Color { self["brand_2"] }

    /// Headings: Fredoka in Honey and Lagoon (bundled), Avenir Next in Glacier, the system font in Classic.
    func display(_ size: CGFloat) -> Font {
        switch displayFont {
        case "fredoka": return .custom("Fredoka-SemiBold", size: size)
        case "avenir": return .custom("Avenir Next", size: size).weight(.semibold)
        default: return .system(size: size, weight: .semibold)
        }
    }

    func body(_ size: CGFloat = 13, weight: Font.Weight = .regular) -> Font {
        bodyFont == "avenir" ? .custom("Avenir Next", size: size).weight(weight) : .system(size: size, weight: weight)
    }

    /// Registers the bundled Fredoka font for this app only (nothing is installed on the Mac).
    static func registerFonts() {
        guard let path = Resources.path("Fonts/Fredoka-SemiBold.ttf") else { return }
        CTFontManagerRegisterFontsForURL(URL(fileURLWithPath: path) as CFURL, .process, nil)
    }

    /// The accent color chosen in System Settings, as #RRGGBB, for the macOS swatch.
    static func systemAccentHex() -> String? {
        guard let color = NSColor.controlAccentColor.usingColorSpace(.sRGB) else { return nil }
        return String(format: "#%02X%02X%02X", Int(color.redComponent * 255 + 0.5), Int(color.greenComponent * 255 + 0.5),
                      Int(color.blueComponent * 255 + 0.5))
    }
}

private struct ThemeKey: EnvironmentKey {
    static let defaultValue = Theme(scheme: "honey", dark: false, accent: "", systemAccent: nil)
}

extension EnvironmentValues {
    var theme: Theme {
        get { self[ThemeKey.self] }
        set { self[ThemeKey.self] = newValue }
    }
}
