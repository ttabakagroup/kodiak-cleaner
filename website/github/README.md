# GitHub Documents

Files used on the Kodiak Cleaner GitHub page and its releases.

| Path | What it is |
|------|------------|
| `images/` | The logo and screenshots shown on the repository's main README. |
| `release-notes/` | The text for each GitHub release, one file per version: `<version>-beta.md` from 0.9.0 (for example `0.9.0-beta.md`). Create GitHub Release uses it as the release description. |

The main page itself is the `README.md` at the top of the repository. The app's own documents (CHANGELOG, PRIVACY, SECURITY, Terms of Use and LICENSE) live in `source/`, because the app and the packages use them.

## Making a Release

1. Copy the newest section of `source/CHANGELOG.md` into `release-notes/<version>-beta.md`, under the download lines.
2. Tag the commit to match `source/version.json` (for example `v0.9.0-beta`) and push the tag. GitHub Actions builds the release, attaches the installer and the Portable ZIP, and refreshes the latest-beta release that Download.url points to.
3. Paste the release notes into the release description.
4. Replace the files in `releases/` with the new Windows and Linux downloads (Windows in `releases/Windows-<version>-beta/`).
