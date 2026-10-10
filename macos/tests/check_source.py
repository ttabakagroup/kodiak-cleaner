#!/usr/bin/env python3
"""Source checks that don't need a Mac: Title Case, colors only in the theme files, no network code, A to Z lists.

Run: python3 tests/check_source.py   (build.sh test runs it too)
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCES = ROOT / "Sources"
THEME_FILES = {"Themes.swift", "Theme.swift"}

SMALL_WORDS = {"a", "an", "and", "as", "at", "but", "by", "for", "from", "in", "into", "nor", "of", "on", "or", "per",
               "the", "to", "vs", "via", "with"}
# Names that keep their own spelling.
LOWERCASE_NAMES = {"npm", "pip", "macOS", "e.g", "cron", "zsh", "eyJpZCI6Ij"}

# Calls and labels whose text is a title, label, button, option or chip. Sentences (WarnBox, Caption, Text) aren't.
TITLE_PATTERNS = [
    r'\bButton\("([^"\\]+)"',
    r'\bLabel\("([^"\\]+)"',
    r'\bToggle\("([^"\\]+)"',
    r'\bPageHeader\(title: "([^"\\]+)"',
    r'\bCard\(title: "([^"\\]+)"',
    r'\bSwitchRow\(title: "([^"\\]+)"',
    r'\bChip\(text: "([^"\\]+)"',
    r'\bDialogRequest\(title: "([^"\\]+)"',
    r'\bconfirm: "([^"\\]+)"',
    r'\bcancel: "([^"\\]+)"',
    r'\bmodel\.tell\("([^"\\]+)"',
    r'\(\s*"[a-z]+",\s*"([^"\\]+)"\)',             # ("key", "Label") options and sort choices
    r'\.(?:turnedOff|turnedOn|removed): "([^"\\]+)"',
    r'Text\("([^"\\]+)"\)\.font\(theme\.body\(1[13], weight: \.(?:semibold|bold)\)\)',  # headings and labels
]


def title_problems(text):
    words = re.findall(r"[A-Za-z][A-Za-z'’.-]*", text)
    bad = []
    for index, word in enumerate(words):
        bare = word.strip(".'’-")
        if not bare or bare in LOWERCASE_NAMES or word in LOWERCASE_NAMES:
            continue
        small = bare.lower() in SMALL_WORDS and 0 < index < len(words) - 1
        if small:
            if bare != bare.lower():
                bad.append(word)
        elif bare[0].islower():
            bad.append(word)
    return bad


def swift_files():
    return sorted(SOURCES.rglob("*.swift"))


def check_title_case(errors):
    checked = 0
    for path in swift_files():
        text = path.read_text(encoding="utf-8")
        for pattern in TITLE_PATTERNS:
            for match in re.finditer(pattern, text):
                value = match.group(1)
                checked += 1
                bad = title_problems(value)
                if bad:
                    line = text.count("\n", 0, match.start()) + 1
                    errors.append(f"Not Title Case: {value!r} ({', '.join(bad)}) in {path.name}:{line}")
    # Feature names, group names and Triggers groups are titles too.
    core = (SOURCES / "KodiakCleaner" / "HomeAbout.swift").read_text(encoding="utf-8")
    for name in re.findall(r'^\s*\("([^"]+)", "', core, flags=re.M):
        checked += 1
        if title_problems(name):
            errors.append(f"Not Title Case: feature {name!r}")
    triggers = (SOURCES / "KodiakCore" / "Triggers.swift").read_text(encoding="utf-8")
    block = triggers[triggers.index("groupNames"):triggers.index("groupBlurbs")]
    for name in re.findall(r': "([^"]+)"', block):
        checked += 1
        if title_problems(name):
            errors.append(f"Not Title Case: Triggers group {name!r}")
    if checked < 100:
        errors.append(f"Title Case check found only {checked} labels; the patterns may be out of date")
    return checked


def check_colors(errors):
    """Every color comes from Themes.swift (values) through Theme.swift (SwiftUI colors)."""
    hex_color = re.compile(r'"#[0-9A-Fa-f]{6}"|"rgba\(')
    system_color = re.compile(r'\bColor\.(red|blue|green|white|black|gray|grey|orange|yellow|pink|purple|accentColor|primary|secondary)\b'
                              r'|\.foregroundStyle\(\.(red|blue|green|white|black|gray|orange|yellow|pink|purple|primary|secondary|tint)\)'
                              r'|\bColor\((?:red|\.sRGB|nsColor|hue|white)')
    for path in swift_files():
        if path.name in THEME_FILES:
            continue
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if hex_color.search(line) or system_color.search(line):
                errors.append(f"Color outside the theme files: {path.name}:{number}: {line.strip()}")


def check_no_network(errors):
    """Kodiak Cleaner never goes online by itself: no networking code at all. Links open in the person's browser."""
    banned = re.compile(r"\b(URLSession|URLRequest|NWConnection|NWPathMonitor|CFNetwork|WKWebView|Data\(contentsOf: URL\(string)")
    for path in swift_files():
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if banned.search(line):
                errors.append(f"Network code: {path.name}:{number}: {line.strip()}")


def check_features_sorted(errors):
    core = (SOURCES / "KodiakCleaner" / "HomeAbout.swift").read_text(encoding="utf-8")
    block = core[core.index("let features"):core.index("struct HomeView")]
    names = re.findall(r'^\s*\("([^"]+)", "', block, flags=re.M)
    if names != sorted(names, key=str.lower):
        errors.append(f"Getting Started features aren't A to Z: {names}")


def check_docs(errors):
    for name in ("CHANGELOG.md", "PRIVACY.md", "terms-of-use.md", "LICENSE", "packaging/ReadMe.md", "README.md", "CLAUDE.md"):
        if not (ROOT / name).is_file():
            errors.append(f"Missing {name}")
    for name in ("README.md", "PRIVACY.md", "terms-of-use.md", "packaging/ReadMe.md", "CHANGELOG.md"):
        path = ROOT / name
        if path.is_file():
            text = path.read_text(encoding="utf-8")
            for dash in ("\u2013", "\u2014"):
                if dash in text:
                    errors.append(f"{name} has an en or em dash; use a plain hyphen")


def check_no_email(errors):
    """Ted's email address is never in the app or its docs; contact goes through the website's contact form."""
    address = ("ttabakagroup" + "@").encode()
    for path in sorted(ROOT.rglob("*")):
        parts = set(path.relative_to(ROOT).parts)
        if not path.is_file() or parts & {".git", ".build", "dist"}:
            continue
        try:
            data = path.read_bytes().lower()
        except OSError:
            continue
        if address in data:
            errors.append(f"Email address in {path.relative_to(ROOT)}; use the contact form link instead")


def main():
    errors = []
    labels = check_title_case(errors)
    check_colors(errors)
    check_no_network(errors)
    check_features_sorted(errors)
    check_docs(errors)
    check_no_email(errors)
    if errors:
        print("\n".join(errors))
        print(f"{len(errors)} problem(s)")
        return 1
    print(f"Source checks passed ({labels} labels in Title Case, colors only in the theme files, no network code).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
