# Store Packages

Kodiak Cleaner for Windows is published to two package managers:

| Store | Package | Install with |
|---|---|---|
| Winget | `TedTabaka.KodiakCleaner` (MSI) | `winget install TedTabaka.KodiakCleaner` |
| Chocolatey | `kodiak-cleaner` (MSI inside the package) | `choco install kodiak-cleaner --pre` |
| Chocolatey | `kodiak-cleaner.portable` (no installer) | `choco install kodiak-cleaner.portable --pre` |

`--pre` is needed while the releases are Beta.

## How Updates Are Sent

`.github/workflows/store-packages.yml` runs after **Create GitHub Release** makes a new release (or by hand with
Run workflow and a tag such as `v0.9.2-beta`). It:

1. Reads the tag and finds the release's `...Setup.msi` and `...Portable.zip`.
2. **Chocolatey:** fills in the templates in `chocolatey/` (version, file names, SHA-256), puts the MSI inside
   `kodiak-cleaner`, packs both and pushes them. Needs the repo secret `CHOCOLATEY_API_KEY`.
3. **Winget:** runs `wingetcreate update` to open a pull request on `microsoft/winget-pkgs`. Needs the repo secret
   `WINGET_TOKEN`, a classic GitHub token with the `public_repo` scope.

A store whose secret isn't set is skipped, and so is a version that's already there.

## Templates

The files in `chocolatey/` hold placeholders that the workflow replaces: `__VERSION__` (`0.9.2-beta`), `__TAG__`
(`v0.9.2-beta`), `__LABEL__` (`0.9.2 Beta`), `__MSI__`, `__ZIP__`, `__MSI_SHA256__`, `__ZIP_SHA256__` and `__NOTE__`
(the beta warning, empty for a stable release).

The portable package unpacks the Portable ZIP into the Chocolatey package folder, adds a "Kodiak Cleaner (Portable)"
Start Menu shortcut and a `kodiakcleaner` command, lets normal users write `KodiakCleaner.ini` and the Backups, Logs
and Reports folders beside the exe, and keeps them across upgrades (in `%USERPROFILE%\kodiak-cleaner_temp` while the
package is replaced, and left there on uninstall).
