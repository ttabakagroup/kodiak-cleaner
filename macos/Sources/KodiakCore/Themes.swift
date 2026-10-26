import Foundation

/// Honey (default), Classic, Glacier and Lagoon color schemes, each light and dark, plus the optional accent colors.
///
/// This is the only file that holds colors (tests/check_source.py enforces it). Only colors, fonts and corner radii
/// differ between schemes; the pages use the token names and never a color of their own.
public enum Themes {
    public static let tokens = [
        "bg", "surface", "surface_alt", "row_alt", "row_hover", "row_line", "column_divider", "line",
        "text", "muted", "link", "primary", "on_primary", "accent", "accent_text", "accent_tint",
        "success", "on_success", "toggle_on", "control_border", "focus_ring", "warn_bg", "warn_text", "warn_border",
        "danger", "danger_bg", "danger_fg", "danger_border", "chip_bg", "chip_text", "brand_1", "brand_2",
    ]

    public struct Scheme {
        public var light: [String: String]
        public var dark: [String: String]
        public var radius: Double
        public var radiusCard: Double
        /// "fredoka", "system" or "avenir"
        public var displayFont: String
        public var bodyFont: String
    }

    // The exact colors from Ted's build standard (assets/theme.css), the same tables as the Linux edition.
    public static let schemes: [String: Scheme] = [
        "honey": Scheme(
            light: ["bg": "#F5EFE6", "surface": "#FFFFFF", "surface_alt": "#FBF7F0", "row_alt": "#FBF7F0",
                    "row_hover": "rgba(217,164,65,0.18)", "row_line": "#E8DFD1", "column_divider": "#E8DFD1", "line": "#E8DFD1",
                    "text": "#3D2B1F", "muted": "#6F6152", "link": "#6B4A34", "primary": "#6B4A34", "on_primary": "#FFFFFF",
                    "accent": "#D9A441", "accent_text": "#86611E", "accent_tint": "rgba(217,164,65,0.18)", "success": "#7C9463",
                    "on_success": "#FFFFFF", "toggle_on": "#7C9463", "control_border": "#B27C4B", "focus_ring": "#86611E",
                    "warn_bg": "#F7DDD4", "warn_text": "#A6452F", "warn_border": "#F7DDD4", "danger": "#B4432E",
                    "danger_bg": "#B4432E", "danger_fg": "#FFFFFF", "danger_border": "#B4432E", "chip_bg": "#F6E6C8",
                    "chip_text": "#8A5A16", "brand_1": "#6B4A34", "brand_2": "#86611E"],
            dark: ["bg": "#1E1815", "surface": "#2C2420", "surface_alt": "#241D19", "row_alt": "#241D19",
                   "row_hover": "rgba(224,169,76,0.16)", "row_line": "#3A2F28", "column_divider": "#3A2F28", "line": "#3A2F28",
                   "text": "#EDE6DC", "muted": "#A69A8D", "link": "#E0A94C", "primary": "#EDE6DC", "on_primary": "#1E1815",
                   "accent": "#E0A94C", "accent_text": "#E0A94C", "accent_tint": "rgba(224,169,76,0.16)", "success": "#8FA876",
                   "on_success": "#1E1815", "toggle_on": "#8FA876", "control_border": "#7F6F5F", "focus_ring": "#E0A94C",
                   "warn_bg": "#4A2A22", "warn_text": "#F0A08A", "warn_border": "#4A2A22", "danger": "#F0A08A",
                   "danger_bg": "#B4432E", "danger_fg": "#FFFFFF", "danger_border": "#B4432E", "chip_bg": "#4A3A1D",
                   "chip_text": "#E8C176", "brand_1": "#EDE6DC", "brand_2": "#E0A94C"],
            radius: 10, radiusCard: 16, displayFont: "fredoka", bodyFont: "system"),
        "classic": Scheme(
            light: ["bg": "#F5F6F8", "surface": "#FFFFFF", "surface_alt": "#F2F4F7", "row_alt": "#F1F4F8", "row_hover": "#E6EDF8",
                    "row_line": "#DCE1E7", "column_divider": "#9AA3AE", "line": "#E3E6EA", "text": "#1B1F24", "muted": "#5F6670",
                    "link": "#1D4ED8", "primary": "#2563EB", "on_primary": "#FFFFFF", "accent": "#2563EB", "accent_text": "#1D4ED8",
                    "accent_tint": "rgba(37,99,235,0.18)", "success": "#16A34A", "on_success": "#FFFFFF", "toggle_on": "#2563EB",
                    "control_border": "#828A95", "focus_ring": "#2563EB", "warn_bg": "#FFF4CE", "warn_text": "#5C4400",
                    "warn_border": "#D9B84A", "danger": "#C42B1C", "danger_bg": "#C42B1C", "danger_fg": "#FFFFFF",
                    "danger_border": "#C42B1C", "chip_bg": "#E8EEFD", "chip_text": "#1D4ED8", "brand_1": "#1B1F24",
                    "brand_2": "#2563EB"],
            dark: ["bg": "#1C1D21", "surface": "#25272C", "surface_alt": "#2D2F35", "row_alt": "#2E3036", "row_hover": "#363A42",
                   "row_line": "#3D4047", "column_divider": "#6E7580", "line": "#33363C", "text": "#F1F2F4", "muted": "#A3A9B2",
                   "link": "#7FA6F8", "primary": "#2563EB", "on_primary": "#FFFFFF", "accent": "#60A5FA", "accent_text": "#93BBFB",
                   "accent_tint": "rgba(59,130,246,0.25)", "success": "#22C55E", "on_success": "#0F1A12", "toggle_on": "#3B82F6",
                   "control_border": "#7B828C", "focus_ring": "#60A5FA", "warn_bg": "#3B3116", "warn_text": "#F3D98A",
                   "warn_border": "#8A7330", "danger": "#F87171", "danger_bg": "#DC2626", "danger_fg": "#FFFFFF",
                   "danger_border": "#DC2626", "chip_bg": "#23324D", "chip_text": "#93BBFB", "brand_1": "#F1F2F4",
                   "brand_2": "#60A5FA"],
            radius: 8, radiusCard: 10, displayFont: "system", bodyFont: "system"),
        "glacier": Scheme(
            light: ["bg": "#F1F3F3", "surface": "#F7F8F8", "surface_alt": "#EAEEEF", "row_alt": "#EAEEEF",
                    "row_hover": "rgba(108,147,166,0.16)", "row_line": "#D5DCDF", "column_divider": "#D5DCDF", "line": "#D5DCDF",
                    "text": "#4C5A63", "muted": "#6A6C64", "link": "#4A6F82", "primary": "#6C93A6", "on_primary": "#1E2528",
                    "accent": "#6C93A6", "accent_text": "#3E6072", "accent_tint": "rgba(108,147,166,0.16)", "success": "#6C93A6",
                    "on_success": "#1E2528", "toggle_on": "#4A6F82", "control_border": "#778992", "focus_ring": "#3E6072",
                    "warn_bg": "#F7F8F8", "warn_text": "#735750", "warn_border": "#8E7069", "danger": "#735750",
                    "danger_bg": "#F7F8F8", "danger_fg": "#735750", "danger_border": "#D5DCDF", "chip_bg": "#8FA39B",
                    "chip_text": "#26301F", "brand_1": "#4C5A63", "brand_2": "#4C5A63"],
            dark: ["bg": "#1E2528", "surface": "#252D31", "surface_alt": "#21292C", "row_alt": "#21292C",
                   "row_hover": "rgba(143,182,199,0.14)", "row_line": "#333D42", "column_divider": "#333D42", "line": "#333D42",
                   "text": "#EAEEEE", "muted": "#B7B9AE", "link": "#8FB6C7", "primary": "#8FB6C7", "on_primary": "#1E2528",
                   "accent": "#8FB6C7", "accent_text": "#8FB6C7", "accent_tint": "rgba(143,182,199,0.14)", "success": "#8FB6C7",
                   "on_success": "#1E2528", "toggle_on": "#8FB6C7", "control_border": "#AEB9BE", "focus_ring": "#8FB6C7",
                   "warn_bg": "#252D31", "warn_text": "#B39690", "warn_border": "#B39690", "danger": "#B39690",
                   "danger_bg": "#252D31", "danger_fg": "#B39690", "danger_border": "#333D42", "chip_bg": "#A9BDB4",
                   "chip_text": "#26301F", "brand_1": "#EAEEEE", "brand_2": "#EAEEEE"],
            radius: 6, radiusCard: 6, displayFont: "avenir", bodyFont: "avenir"),
        "lagoon": Scheme(
            light: ["bg": "#F2F7FB", "surface": "#FFFFFF", "surface_alt": "#E8F1F8", "row_alt": "#E8F1F8",
                    "row_hover": "rgba(10,132,214,0.16)", "row_line": "#D3E2EE", "column_divider": "#D3E2EE", "line": "#D3E2EE",
                    "text": "#0B1630", "muted": "#4A5B70", "link": "#0053FD", "primary": "#0053FD", "on_primary": "#FFFFFF",
                    "accent": "#0A84D6", "accent_text": "#0053FD", "accent_tint": "rgba(10,132,214,0.16)", "success": "#0B8378",
                    "on_success": "#FFFFFF", "toggle_on": "#0B8378", "control_border": "#6F87A0", "focus_ring": "#0053FD",
                    "warn_bg": "#FFF1E0", "warn_text": "#8A4B08", "warn_border": "#E9B97A", "danger": "#C2362B",
                    "danger_bg": "#C2362B", "danger_fg": "#FFFFFF", "danger_border": "#C2362B", "chip_bg": "#DCEFF8",
                    "chip_text": "#075A7A", "brand_1": "#0B1630", "brand_2": "#0053FD"],
            dark: ["bg": "#0A1424", "surface": "#111E33", "surface_alt": "#0D192C", "row_alt": "#0D192C",
                   "row_hover": "rgba(46,196,232,0.18)", "row_line": "#20324B", "column_divider": "#20324B", "line": "#20324B",
                   "text": "#E8F1F8", "muted": "#9FB3C8", "link": "#5CC6F2", "primary": "#5CC6F2", "on_primary": "#0A1424",
                   "accent": "#2EC4E8", "accent_text": "#5CC6F2", "accent_tint": "rgba(46,196,232,0.18)", "success": "#2DD4BF",
                   "on_success": "#0A1424", "toggle_on": "#2DD4BF", "control_border": "#5F7896", "focus_ring": "#5CC6F2",
                   "warn_bg": "#3A2A14", "warn_text": "#F6C88A", "warn_border": "#8A6A3A", "danger": "#FF8A80",
                   "danger_bg": "#C2362B", "danger_fg": "#FFFFFF", "danger_border": "#C2362B", "chip_bg": "#12324A",
                   "chip_text": "#8EDCF5", "brand_1": "#E8F1F8", "brand_2": "#5CC6F2"],
            radius: 12, radiusCard: 16, displayFont: "fredoka", bodyFont: "system"),
    ]

    /// Color Scheme tiles, always in this order. Honey is free and the default; Classic, Glacier and Lagoon are Supporter themes.
    public static let schemeOrder = ["honey", "classic", "glacier", "lagoon"]
    public static let schemeNames = ["honey": "Honey", "classic": "Classic", "glacier": "Glacier", "lagoon": "Lagoon"]
    public static let schemeBlurbs = ["honey": "Warm cream and gold", "classic": "Crisp white and blue", "glacier": "Calm blue-grey",
                                      "lagoon": "Deep blue and clear water"]
    public static let supporterSchemes: Set<String> = ["classic", "glacier", "lagoon"]
    /// Ted's reasons for each scheme (Settings > Appearance > Why These Colors), word for word as in the Windows edition.
    public static let schemeReasons = [
        "honey": "is inspired by the bear behind Kodiak Cleaner. Its warm golds and creams are part of the Kodiak Cleaner brand.",
        "classic": "is my thank-you to supporters: a timeless, crisp white and blue that show a little class without getting in the way.",
        "glacier": "is another thank-you to supporters, with cool, calm blues inspired by the quiet beauty of glacier ice.",
        "lagoon": "is one more thank-you to supporters: fresh and clear like shallow water, with ocean blues and aqua, and deep water at night in dark mode.",
    ]

    /// Accent colors for everyone, in the standard's fixed order after the scheme's own accent (""). "macOS" follows
    /// the accent color chosen in System Settings, the Mac's equivalent of the standard's Windows swatch.
    public static let systemAccent = "macOS"
    public static let accents: [(String, String)] = [
        ("Blue", "#2563EB"), ("Teal", "#0D9488"), ("Green", "#16A34A"), ("Purple", "#7C3AED"),
        ("Pink", "#DB2777"), ("Orange", "#EA580C"), ("Slate", "#525F73"),
    ]
    /// Used for the macOS swatch when the system accent can't be read (it's Apple's default blue).
    public static let defaultSystemAccent = "#007AFF"
    static let renamedAccents = ["Graphite": "Slate"]
    /// A scheme whose own accent is one of the swatches (Classic is blue) hides that duplicate swatch.
    public static let schemeSwatch = ["classic": "Blue"]

    public static func accentNames(_ scheme: String) -> [String] {
        [""] + [systemAccent] + accents.map(\.0).filter { $0 != schemeSwatch[scheme] }
    }

    public static func effectiveAccent(_ scheme: String, _ accent: String) -> String {
        let accent = renamedAccents[accent] ?? accent
        if accent == systemAccent { return accent }
        if !accents.contains(where: { $0.0 == accent }) || accent == schemeSwatch[scheme] { return "" }
        return accent
    }

    // MARK: Color math (accentTokens() from the standard's accent.js, ported like the Linux edition's themes.py)

    public struct RGB: Equatable {
        public var r, g, b: Double
        public var a: Double = 1
    }

    public static func parse(_ color: String) -> RGB {
        let text = color.trimmingCharacters(in: .whitespaces)
        if text.hasPrefix("rgba(") {
            let parts = text.dropFirst(5).dropLast().split(separator: ",").map {
                Double($0.trimmingCharacters(in: .whitespaces)) ?? 0
            }
            return RGB(r: parts[0], g: parts[1], b: parts[2], a: parts[3])
        }
        let hex = text.hasPrefix("#") ? String(text.dropFirst()) : text
        let value = Int(hex, radix: 16) ?? 0
        return RGB(r: Double((value >> 16) & 0xFF), g: Double((value >> 8) & 0xFF), b: Double(value & 0xFF))
    }

    static func hex(_ r: Double, _ g: Double, _ b: Double) -> String {
        // JavaScript's Math.round rounds halves up.
        func channel(_ c: Double) -> Int { Int(max(0, min(255, c)) + 0.5) }
        return String(format: "#%02X%02X%02X", channel(r), channel(g), channel(b))
    }

    static func linear(_ v: Double) -> Double {
        let v = v / 255
        return v <= 0.03928 ? v / 12.92 : pow((v + 0.055) / 1.055, 2.4)
    }

    static func luminance(_ c: RGB) -> Double {
        0.2126 * linear(c.r) + 0.7152 * linear(c.g) + 0.0722 * linear(c.b)
    }

    /// WCAG contrast ratio between two colors.
    public static func contrast(_ a: String, _ b: String) -> Double {
        let x = luminance(parse(a)), y = luminance(parse(b))
        return (max(x, y) + 0.05) / (min(x, y) + 0.05)
    }

    /// A color as it looks drawn on a ground (handles the translucent rgba tokens).
    public static func over(_ color: String, _ ground: String) -> String {
        let fg = parse(color), bg = parse(ground)
        return hex(fg.r * fg.a + bg.r * (1 - fg.a), fg.g * fg.a + bg.g * (1 - fg.a), fg.b * fg.a + bg.b * (1 - fg.a))
    }

    static func toHSL(_ color: String) -> (Double, Double, Double) {
        let c = parse(color)
        let r = c.r / 255, g = c.g / 255, b = c.b / 255
        let high = max(r, g, b), low = min(r, g, b)
        let light = (high + low) / 2
        if high == low { return (0, 0, light) }
        let d = high - low
        let sat = light > 0.5 ? d / (2 - high - low) : d / (high + low)
        var hue: Double
        if high == r {
            hue = (g - b) / d + (g < b ? 6 : 0)
        } else if high == g {
            hue = (b - r) / d + 2
        } else {
            hue = (r - g) / d + 4
        }
        return (hue / 6, sat, light)
    }

    static func fromHSL(_ hue: Double, _ sat: Double, _ light: Double) -> String {
        if sat == 0 { return hex(light * 255, light * 255, light * 255) }
        let q = light < 0.5 ? light * (1 + sat) : light + sat - light * sat
        let p = 2 * light - q
        func channel(_ t: Double) -> Double {
            var t = t.truncatingRemainder(dividingBy: 1)
            t = (t + 1).truncatingRemainder(dividingBy: 1)
            if t < 1.0 / 6 { return p + (q - p) * 6 * t }
            if t < 1.0 / 2 { return q }
            if t < 2.0 / 3 { return p + (q - p) * (2.0 / 3 - t) * 6 }
            return p
        }
        return hex(channel(hue + 1.0 / 3) * 255, channel(hue) * 255, channel(hue - 1.0 / 3) * 255)
    }

    static func fit(_ color: String, against: [String], need: Double, darker: Bool) -> String {
        let color = color.uppercased()
        if against.allSatisfy({ contrast(color, $0) >= need }) { return color }
        var (hue, sat, light) = toHSL(color)
        for _ in 0..<200 {
            let candidate = fromHSL(hue, sat, light)
            if against.allSatisfy({ contrast(candidate, $0) >= need }) { return candidate }
            light = min(1, max(0, light + (darker ? -0.005 : 0.005)))
        }
        return darker ? "#000000" : "#FFFFFF"
    }

    /// The accent's shades for one scheme and mode: keep hue and saturation, move only the lightness until every use
    /// meets WCAG 2.2 AA (fills, switches and the focus ring 3:1; links and accent text 4.5:1, also on the tint; white
    /// button labels 4.5:1 on the primary fill). A color that already passes is kept exactly.
    public static func accentTokens(_ base: String, table: [String: String], dark: Bool) -> [String: String] {
        let grounds = [table["bg"]!, table["surface"]!, table["surface_alt"]!]
        let darker = !dark
        let accent = fit(base, against: grounds, need: 3, darker: darker)
        let alpha = dark ? "0.24" : "0.18"
        let c = parse(accent)
        let tint = "rgba(\(Int(c.r)),\(Int(c.g)),\(Int(c.b)),\(alpha))"
        let accentText = fit(base, against: grounds + [over(tint, table["surface"]!)], need: 4.5, darker: darker)
        let primary = fit(base, against: ["#FFFFFF"], need: 4.5, darker: true)
        return ["accent": accent, "toggle_on": accent, "focus_ring": accent, "accent_text": accentText, "link": accentText,
                "accent_tint": tint, "primary": primary, "on_primary": "#FFFFFF"]
    }

    /// The full token table for a scheme and mode, with an accent color laid over it when one is chosen.
    public static func tokensFor(_ scheme: String, dark: Bool, accent: String = "",
                                 systemAccentColor: String? = nil) -> [String: String] {
        let scheme = schemes[scheme] == nil ? "honey" : scheme
        let info = schemes[scheme]!
        var table = dark ? info.dark : info.light
        let accent = effectiveAccent(scheme, accent)
        let base: String? = accent.isEmpty ? nil
            : accent == systemAccent ? (systemAccentColor ?? defaultSystemAccent)
            : accents.first(where: { $0.0 == accent })?.1
        if let base {
            table.merge(accentTokens(base, table: table, dark: dark)) { _, new in new }
            if scheme != "classic" { table["row_hover"] = table["accent_tint"] }
        }
        return table
    }

    /// The color a swatch shows: the shade that accent gives in this scheme and mode.
    public static func swatchColor(_ accent: String, scheme: String, dark: Bool, systemAccentColor: String? = nil) -> String {
        tokensFor(scheme, dark: dark, accent: accent, systemAccentColor: systemAccentColor)["accent"]!
    }

    /// The colors on each Color Scheme tile's strip (Ted, 0.8.15): primary, secondary and tertiary, a third each.
    /// The same theme colors as the Windows and Linux editions.
    public static let schemePreviewKeys = ["honey": ["primary", "accent", "bg"], "classic": ["primary", "text", "bg"],
                                           "glacier": ["primary", "chip_bg", "bg"], "lagoon": ["primary", "success", "bg"]]

    /// Three colors a Color Scheme tile shows: its primary, secondary and tertiary (the page background).
    public static func schemePreview(_ scheme: String, dark: Bool) -> [String] {
        let table = dark ? schemes[scheme]!.dark : schemes[scheme]!.light
        return schemePreviewKeys[scheme]!.map { table[$0]! }
    }
}
