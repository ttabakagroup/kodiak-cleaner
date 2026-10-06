#!/usr/bin/env bash
# Linux counterpart of build.ps1's packaging step: builds the MSI under Wine and writes the
# Setup ZIP (the MSI inside) and the Portable ZIP, with the same files build.ps1 puts in them. Development builds only.
#
#   installer/package-linux.sh [publish-folder] [output-folder]
#
# The publish folder must hold KodiakCleaner.exe from:
#   dotnet publish src/KodiakCleaner.App -c Release -r win-x64 -o publish
set -euo pipefail
cd "$(dirname "$0")/.."
REPO=$(pwd)
PUBLISH=$(realpath "${1:-publish}")
OUTDIR=$(realpath -m "${2:-dist}")
PRODUCT=KodiakCleaner
[ -f "$PUBLISH/KodiakCleaner.exe" ] || { echo "No KodiakCleaner.exe in $PUBLISH" >&2; exit 1; }
mkdir -p "$OUTDIR"
STAGE=$(mktemp -d)
trap 'rm -rf "${STAGE:?}"' EXIT

# The seven Docs files (Changelog.txt, Download.url, ...): the MSI installs them and both ZIPs carry them.
dotnet run --project tools/MakeDocs -- "$STAGE/portable/Docs" dev >/dev/null

# The bare MSI goes on the GitHub release (Docs/Download.url links it; Ted, 0.8.31), and the Setup ZIP holds it with Docs.
# Files handed to Ted directly are always the ZIPs.
mkdir -p "$STAGE/setup"
bash installer/build-msi-wine.sh "$PUBLISH" "$STAGE/setup/$PRODUCT-Dev-Setup.msi" "$STAGE/portable/Docs"
cp -r "$STAGE/portable/Docs" "$STAGE/setup/"
rm -f "$OUTDIR/$PRODUCT-Dev-Setup.msi" "$OUTDIR/$PRODUCT-Dev-Setup.zip" "$OUTDIR/$PRODUCT-Dev-Portable.zip"
cp "$STAGE/setup/$PRODUCT-Dev-Setup.msi" "$OUTDIR/$PRODUCT-Dev-Setup.msi"
(cd "$STAGE/setup" && zip -qr "$OUTDIR/$PRODUCT-Dev-Setup.zip" ./*)

# Portable ZIP: the app plus the Docs folder. Never tests, tools, keys or store assets.
cp "$PUBLISH/KodiakCleaner.exe" "$STAGE/portable/"
(cd "$STAGE/portable" && zip -qr "$OUTDIR/$PRODUCT-Dev-Portable.zip" ./*)
echo "Output: $OUTDIR/$PRODUCT-Dev-Setup.msi"
echo "Output: $OUTDIR/$PRODUCT-Dev-Setup.zip"
echo "Output: $OUTDIR/$PRODUCT-Dev-Portable.zip"
