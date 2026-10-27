#!/usr/bin/env python3
"""Writes the standard's seven plain-text Docs files for Kodiak Cleaner for Mac.

  python3 tools/make_docs.py <folder> <download file name>

Download.url points straight at that file on the GitHub release for this channel (latest-dev, latest-beta, or the
latest release for Stable). plain_text() and shortcut() are the Linux edition's, unchanged.
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REPOSITORY = "https://github.com/ttabakagroup/kodiak-cleaner"
HOMEPAGE = "https://www.kodiakcleaner.app/"
DOCS_FOLDER = ("Changelog.txt", "Download.url", "Homepage.url", "License.txt", "Privacy.txt", "ReadMe.txt", "TermsOfUse.txt")
DOCS_SOURCES = {"Changelog.txt": "CHANGELOG.md", "License.txt": "LICENSE", "Privacy.txt": "PRIVACY.md",
                "ReadMe.txt": "packaging/ReadMe.md", "TermsOfUse.txt": "terms-of-use.md"}


def download_url(channel, file_name):
    """GitHub's releases/latest skips pre-releases, so Dev and Beta use a fixed release each build replaces."""
    if channel == "stable":
        return f"{REPOSITORY}/releases/latest/download/{file_name}"
    return f"{REPOSITORY}/releases/download/latest-{channel}/{file_name}"


def shortcut(url):
    """An Internet shortcut, with Windows line endings so it opens on any computer."""
    return f"[InternetShortcut]\r\nURL={url}\r\n"


def plain_text(markdown):
    """Markdown to clean text: underlined headings, "- " bullets, no # or ** marks. Empty sections (Unreleased) are dropped."""
    markdown = re.sub(r"<!--.*?-->", "", markdown, flags=re.S)
    blocks = []
    for raw in markdown.splitlines():
        line = raw.rstrip()
        heading = re.match(r"^(#{1,6})\s+(.*)$", line)
        if heading:
            blocks.append([len(heading.group(1)), heading.group(2)])
        elif re.match(r"^\s*[-*]\s+", line):
            blocks.append([8, re.sub(r"^\s*[-*]\s+", "", line)])
        elif not line.strip() or re.match(r"^\|?\s*-{3,}", line):
            blocks.append(None)
        elif blocks and blocks[-1] and blocks[-1][0] in (7, 8):
            blocks[-1][1] += " " + line.strip()
        else:
            blocks.append([7, line.strip()])
    blocks = [b for b in blocks if b]
    out = []
    previous = None
    for index, (level, text) in enumerate(blocks):
        text = re.sub(r"^\[([^\]]+)\]", r"\1", text)  # "[0.8.1 Dev] - date" headings
        text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", lambda m: m.group(1) if m.group(1) == m.group(2) else f"{m.group(1)} ({m.group(2)})", text)
        text = re.sub(r"\*\*(.+?)\*\*|__(.+?)__", lambda m: m.group(1) or m.group(2), text)
        text = re.sub(r"`([^`]+)`", r"\1", text)
        text = re.sub(r"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", r"\1", text)
        if level <= 6 and (index + 1 == len(blocks) or blocks[index + 1][0] <= level):
            continue  # a heading with nothing under it
        if previous is not None and not (level == 8 and previous == 8):
            out.append("")
        out.append(("- " if level == 8 else "") + text)
        if level == 1:
            out.append("=" * len(text))
        elif level == 2:
            out.append("-" * len(text))
        previous = level
    return "\n".join(out).strip() + "\n"


def main():
    target, download_file = Path(sys.argv[1]), sys.argv[2]
    info = json.loads((ROOT / "version.json").read_text(encoding="utf-8"))
    branch = info["channel"].title()
    target.mkdir(parents=True, exist_ok=True)
    for name in DOCS_FOLDER:
        if name == "Download.url":
            text = shortcut(download_url(info["channel"], download_file))
        elif name == "Homepage.url":
            text = shortcut(HOMEPAGE)
        elif name == "License.txt":
            text = (ROOT / DOCS_SOURCES[name]).read_text(encoding="utf-8").rstrip() + "\n"
        else:
            text = plain_text((ROOT / DOCS_SOURCES[name]).read_text(encoding="utf-8"))
        if name == "ReadMe.txt":
            text = text.replace("KodiakCleaner-Dev-", f"KodiakCleaner-{branch}-")
        (target / name).write_bytes(text.encode("utf-8"))
    print(f"Wrote {len(DOCS_FOLDER)} Docs files to {target}")


if __name__ == "__main__":
    main()
