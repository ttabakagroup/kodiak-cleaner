"""Stand-in Windows fonts for running Kodiak Cleaner under Wine (never shipped; run by tools/container/setup.sh).

Usage: python3 wine-fonts.py <fluentui-svg-icons package/icons folder> <Wine Fonts folder>

Liberation fonts are renamed to Arial, Segoe UI, Segoe UI Variable Text and Consolas. "Segoe Fluent Icons" and "Segoe MDL2 Assets"
are built from Fluent UI System Icons (MIT) at the codepoints Kodiak Cleaner uses; every other private-use codepoint is a square.
"""
import os
import sys

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.svgLib.path import SVGPath
from fontTools.ttLib import TTFont

ICONS, OUT = sys.argv[1], sys.argv[2]
LIBERATION = "/usr/share/fonts/truetype/liberation/"

# Segoe codepoint -> Fluent UI icon name ("@filled" picks the filled style).
GLYPHS = {
    "E70D": "chevron_down", "E70E": "chevron_up", "E70F": "edit", "E710": "add", "E711": "dismiss", "E713": "settings",
    "E71D": "apps", "E721": "search", "E722": "camera", "E72B": "arrow_left", "E72C": "arrow_clockwise", "E72E": "lock_closed",
    "E734": "star", "E735": "star@filled", "E736": "book_open", "E73E": "checkmark", "E74A": "arrow_up", "E74C": "app_generic",
    "E74D": "delete", "E74E": "save", "E756": "window_console", "E768": "play", "E769": "pause", "E76C": "chevron_right",
    "E774": "globe", "E777": "arrow_counterclockwise", "E783": "error_circle", "E787": "calendar_ltr", "E789": "megaphone",
    "E790": "color", "E7A7": "arrow_undo", "E7B8": "box", "E7BA": "warning", "E7C4": "apps_list", "E7E8": "power",
    "E7EF": "shield_person", "E7F4": "desktop", "E7F8": "laptop", "E81C": "history", "E823": "clock", "E82F": "lightbulb",
    "E838": "folder_open", "E895": "arrow_sync", "E896": "arrow_download", "E897": "question_circle", "E8A5": "document",
    "E8A7": "open", "E8AC": "rename", "E8B7": "folder", "E8BB": "dismiss", "E8BD": "chat", "E8C8": "copy",
    "E8E5": "document_arrow_right", "E8F1": "library", "E90F": "wrench", "E916": "timer", "E928": "fingerprint", "E943": "code", "E946": "info",
    "E950": "developer_board", "E95E": "heart_pulse", "E9CE": "question_circle", "E9D9": "stethoscope",
    "E9F5": "settings_cog_multiple", "E9F9": "document_text", "EA18": "shield", "EA86": "puzzle_piece", "EB05": "data_pie",
    "EB51": "heart", "EBE8": "bug", "EC4A": "top_speed", "EC50": "folder", "EC7A": "wrench_screwdriver", "ED25": "folder_open",
    "ED1A": "eye_off", "EDA2": "hard_drive",
}


def svg(name):
    style = "filled" if name.endswith("@filled") else "regular"
    path = os.path.join(ICONS, f"{name.split('@')[0]}_20_{style}.svg")
    return path if os.path.exists(path) else None


def icon_font(family, out):
    upm = 2048
    scale = upm / 20
    known = {k: svg(v) for k, v in GLYPHS.items()}
    known = {k: v for k, v in known.items() if v}
    names = [".notdef", "sq"] + ["u" + k for k in known]
    fb = FontBuilder(upm, isTTF=True)
    fb.setupGlyphOrder(names)
    cmap = {c: "sq" for c in range(0xE700, 0xF900)}
    cmap.update({int(k, 16): "u" + k for k in known})
    fb.setupCharacterMap(cmap)
    glyphs = {".notdef": TTGlyphPen(None).glyph()}
    pen = TTGlyphPen(None)
    pen.moveTo((300, 300)); pen.lineTo((300, 1700)); pen.lineTo((1700, 1700)); pen.lineTo((1700, 300)); pen.closePath()
    glyphs["sq"] = pen.glyph()
    for k, path in known.items():
        pen = TTGlyphPen(None)
        SVGPath(path).draw(TransformPen(Cu2QuPen(pen, 1.0, reverse_direction=True), (scale, 0, 0, -scale, 0, upm)))
        glyphs["u" + k] = pen.glyph()
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics({g: (upm, 0) for g in names})
    fb.setupHorizontalHeader(ascent=upm, descent=0)
    fb.setupNameTable({"familyName": family, "styleName": "Regular"})
    fb.setupOS2(sTypoAscender=upm, sTypoDescender=0, usWinAscent=upm, usWinDescent=0)
    fb.setupPost()
    fb.save(os.path.join(OUT, out))


def renamed(source, family, style, out):
    font = TTFont(LIBERATION + source)
    for record in font["name"].names:
        if record.nameID in (1, 16):
            record.string = family
        elif record.nameID == 4:
            record.string = family if style == "Regular" else f"{family} {style}"
        elif record.nameID == 6:
            record.string = f"{family}-{style}".replace(" ", "")
    font.save(os.path.join(OUT, out))


renamed("LiberationSans-Regular.ttf", "Arial", "Regular", "arial.ttf")
renamed("LiberationSans-Bold.ttf", "Arial", "Bold", "arialbd.ttf")
renamed("LiberationMono-Regular.ttf", "Consolas", "Regular", "consola.ttf")
renamed("LiberationSans-Regular.ttf", "Segoe UI", "Regular", "segoeui.ttf")
renamed("LiberationSans-Bold.ttf", "Segoe UI", "Bold", "segoeuib.ttf")
renamed("LiberationSans-Regular.ttf", "Segoe UI Variable Text", "Regular", "segoeuivar.ttf")
icon_font("Segoe Fluent Icons", "SegoeIcons.ttf")
icon_font("Segoe MDL2 Assets", "segmdl2.ttf")
print(f"Fonts written to {OUT}")
