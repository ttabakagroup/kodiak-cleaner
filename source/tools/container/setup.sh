#!/usr/bin/env bash
# Sets up a fresh Linux cloud container to build, test and screenshot Kodiak Cleaner (never shipped).
#
#   sudo tools/container/setup.sh            .NET 8 SDK with the WPF (WindowsDesktop) SDK: dotnet build and dotnet test work
#   sudo tools/container/setup.sh --wine     also Wine, Xvfb and stand-in Windows fonts, to run the portable exe and take screenshots
#
# dot.net downloads are blocked by the proxy, so the SDK comes from Ubuntu's apt and the WindowsDesktop SDK (missing from
# Ubuntu's package) is copied out of Microsoft's Debian package of the same version. Then:
#   dotnet build KodiakCleaner.sln -c Release -p:EnableWindowsTargeting=true
#   dotnet test tests/KodiakCleaner.Core.Tests -c Release -p:EnableWindowsTargeting=true
#   dotnet publish src/KodiakCleaner.App -c Release -r win-x64 --self-contained -p:EnableWindowsTargeting=true -p:PublishSingleFile=true -o <dir>
# and with --wine: WINEPREFIX=<prefix> DISPLAY=:97 wine <dir>/KodiakCleaner.exe --page=<page key>  (Xvfb :97 is started for you).
set -euo pipefail
WINE=0; [ "${1:-}" = "--wine" ] && WINE=1
WORK="${KODIAK_WORK:-/tmp/kodiak-container}"; mkdir -p "$WORK"
unset TMPDIR  # Wine crashes ("free(): invalid pointer") when TMPDIR is set

if ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  apt-get update -qq && apt-get install -y -qq dotnet-sdk-8.0 >/dev/null
fi
SDK=$(dotnet --list-sdks | awk '/^8\./{print $1}' | tail -1)
SDKDIR=$(dotnet --list-sdks | awk '/^8\./{gsub(/[\[\]]/,"",$2); print $2}' | tail -1)/$SDK
if [ ! -d "$SDKDIR/Sdks/Microsoft.NET.Sdk.WindowsDesktop" ]; then
  curl -sSf -o "$WORK/sdk.deb" "https://packages.microsoft.com/debian/12/prod/pool/main/d/dotnet-sdk-8.0/dotnet-sdk-8.0_${SDK}-1_amd64.deb"
  rm -rf "$WORK/deb" && mkdir -p "$WORK/deb" && dpkg-deb -x "$WORK/sdk.deb" "$WORK/deb"
  cp -r "$WORK/deb/usr/share/dotnet/sdk/$SDK/Sdks/Microsoft.NET.Sdk.WindowsDesktop" "$SDKDIR/Sdks/"
fi
echo "dotnet SDK $SDK with WindowsDesktop: ready"
[ $WINE = 1 ] || exit 0

apt-get install -y -qq wine64 xvfb imagemagick xdotool fonts-liberation python3-fonttools >/dev/null
export WINEPREFIX="${WINEPREFIX:-$WORK/wineprefix}" WINEDEBUG=-all
[ -d "$WINEPREFIX/drive_c/windows" ] || wineboot -i >/dev/null 2>&1
FONTS="$WINEPREFIX/drive_c/windows/Fonts"; mkdir -p "$FONTS"
# WPF stops with a FailFast under Wine unless its fonts exist both in Fonts and in the registry. Liberation fonts stand in for
# Arial, Segoe UI and Consolas; the icon fonts get Fluent UI System Icons (MIT) drawn at the codepoints Kodiak Cleaner uses,
# and a plain square for every other one. Real Windows uses its own fonts.
(cd "$WORK" && { [ -d package ] || { npm pack @fluentui/svg-icons >/dev/null 2>&1 && tar xzf fluentui-svg-icons-*.tgz; }; })
python3 "$(dirname "$0")/wine-fonts.py" "$WORK/package/icons" "$FONTS"
KEY='HKLM\Software\Microsoft\Windows NT\CurrentVersion\Fonts'
for pair in "Arial=arial.ttf" "Arial Bold=arialbd.ttf" "Segoe UI=segoeui.ttf" "Segoe UI Bold=segoeuib.ttf" \
            "Segoe UI Variable Text=segoeuivar.ttf" "Consolas=consola.ttf" "Segoe Fluent Icons=SegoeIcons.ttf" "Segoe MDL2 Assets=segmdl2.ttf"; do
  wine reg add "$KEY" /v "${pair%%=*} (TrueType)" /t REG_SZ /d "${pair#*=}" /f >/dev/null 2>&1
done
pgrep -f "Xvfb :97" >/dev/null || (Xvfb :97 -screen 0 1500x950x24 >/dev/null 2>&1 &)
echo "Wine ready: WINEPREFIX=$WINEPREFIX DISPLAY=:97"
echo "For clean screenshots put this KodiakCleaner.ini beside the exe:"
echo "  [General] ShowBetaNotice=false, LastSeenVersion=<version>, ShowDonationPrompt=false, LaunchCount=50; [Developer] Preview=Supporter"
