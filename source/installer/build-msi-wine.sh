#!/usr/bin/env bash
# Builds the MSI on Linux by running the WiX v5 toolset (Windows-only) under Wine.
# On Windows, build.ps1 does this instead. Needs: the .NET 8 SDK, Wine and python3.
#
#   installer/build-msi-wine.sh [publish-folder] [output.msi] [docs-folder]
#
# The publish folder must hold KodiakCleaner.exe from:
#   dotnet publish src/KodiakCleaner.App -c Release -r win-x64 -o publish
# and the docs folder (default: publish/Docs) the seven Docs files from:
#   dotnet run --project tools/MakeDocs -- publish/Docs dev
set -euo pipefail
cd "$(dirname "$0")/.."
REPO=$(pwd)
PUBLISH=$(realpath "${1:-publish}")
DOCS=$(realpath "${3:-$PUBLISH/Docs}")
WIXVER=5.0.2
NUGET=${NUGET_PACKAGES:-$HOME/.nuget/packages}
HOST=${WIX_WINE_HOST:-$HOME/.cache/kodiakcleaner/wix-$WIXVER}
export WINEDEBUG=-all

# Product name and upgrade code for the channel in version.json, read from the wixproj.
read -r VERSION CHANNEL PRODUCT FOLDER UPGRADE < <(python3 - "$REPO" <<'PY'
import json, re, sys, xml.etree.ElementTree as ET
repo = sys.argv[1]
info = json.load(open(f"{repo}/version.json"))
channel = info["channel"].lower()
props = {}
for group in ET.parse(f"{repo}/installer/KodiakCleaner.Installer.wixproj").getroot().iter("PropertyGroup"):
    cond = group.get("Condition", "")
    equals = re.findall(r"== '(\w+)'", cond)
    not_equals = re.findall(r"!= '(\w+)'", cond)
    if cond and not ((equals and channel in equals) or (not equals and not_equals and channel not in not_equals)):
        continue
    for child in group:
        if child.tag in ("ProductName", "InstallFolderName", "UpgradeCode") and not child.get("Condition"):
            props[child.tag] = child.text
print(info["version"], channel, props["ProductName"].replace(" ", "_"), props["InstallFolderName"].replace(" ", "_"),
      props["UpgradeCode"])
PY
)
FOLDER=${FOLDER//_/ }
PRODUCT=${PRODUCT//_/ }
OUT=$(realpath -m "${2:-dist/${PRODUCT// /-}-$VERSION-x64.msi}")
OUT=${OUT/Kodiak-Cleaner/KodiakCleaner}
[ -f "$PUBLISH/KodiakCleaner.exe" ] || { echo "No KodiakCleaner.exe in $PUBLISH" >&2; exit 1; }
[ -f "$DOCS/ReadMe.txt" ] || { echo "No Docs files in $DOCS (run tools/MakeDocs)" >&2; exit 1; }

# WiX packages from NuGet (the wixproj lists them).
dotnet restore installer/KodiakCleaner.Installer.wixproj >/dev/null
TOOLS=$NUGET/wixtoolset.sdk/$WIXVER/tools/net6.0

# A self-contained Windows host for wix.dll, so Wine needs no .NET install.
if [ ! -f "$HOST/wix.exe" ]; then
    tmp=$(mktemp -d)
    cat > "$tmp/wix.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
<AssemblyName>wix</AssemblyName><RuntimeIdentifier>win-x64</RuntimeIdentifier><SelfContained>true</SelfContained></PropertyGroup></Project>
XML
    echo 'return 0;' > "$tmp/Program.cs"
    rm -rf "$HOST"; dotnet publish "$tmp" -c Release -o "$HOST" >/dev/null
    rm -f "$HOST/wix.dll" "$HOST/wix.pdb"
    for f in "$TOOLS"/*; do case $(basename "$f") in wix.deps.json|wix.runtimeconfig.json) ;; *) cp -r "$f" "$HOST/" ;; esac; done
    cp "$TOOLS/runtimes/win-x64/native/wixnative.exe" "$HOST/"
    python3 - "$HOST" "$TOOLS" <<'PY'
import json, os, sys
host, tools = sys.argv[1:]
path = f"{host}/wix.deps.json"
deps = json.load(open(path))
target = deps["targets"][".NETCoreApp,Version=v8.0/win-x64"]
entry = next(k for k in target if k.startswith("wix/"))
target[entry]["runtime"] = {f: {} for f in os.listdir(tools) if f.endswith(".dll")}
target[entry]["native"] = {"wixnative.exe": {}}
json.dump(deps, open(path, "w"), indent=1)
PY
    rm -rf "$tmp"
fi

win() { echo "Z:$(realpath -m "$1" | tr / '\\')"; }
mkdir -p "$(dirname "$OUT")"
cd installer
wine "$HOST/wix.exe" build Package.wxs -arch x64 -culture en-US \
    -ext "$(win "$NUGET/wixtoolset.ui.wixext/$WIXVER/wixext5/WixToolset.UI.wixext.dll")" \
    -ext "$(win "$NUGET/wixtoolset.util.wixext/$WIXVER/wixext5/WixToolset.Util.wixext.dll")" \
    -d "ProductVersion=$VERSION" -d "ProductName=$PRODUCT" -d "InstallFolderName=$FOLDER" -d "UpgradeCode=$UPGRADE" \
    -d "PublishDir=$(win "$PUBLISH")\\" -d "DocsDir=$(win "$DOCS")\\" \
    -o "$(win "$OUT")"
rm -f "${OUT%.msi}.wixpdb"
echo "Output: $OUT"
