#!/bin/bash
# Builds, tests and packages Kodiak Cleaner for Mac. Needs a Mac with Xcode 26 or newer.
#
#   ./build.sh test               every test and check
#   ./build.sh package            the app, KodiakCleaner-<Branch>-Mac.zip and KodiakCleaner-<Branch>-Mac.dmg (each with
#                                 the Docs folder) and the Docs folder itself, in dist/
#   ./build.sh package --release  the same for publishing: no Developer page, refuses the test signing key and a
#                                 version without a CHANGELOG entry
#   ./build.sh screenshots        every page in every color scheme, light and dark, in dist/screenshots
#   ./build.sh self-test          checks the built app on this Mac without changing anything
set -euo pipefail
cd "$(dirname "$0")"

COMMAND=${1:-package}
RELEASE=false
[[ "${2:-}" == "--release" ]] && RELEASE=true

VERSION=$(python3 -c 'import json; print(json.load(open("version.json"))["version"])')
CHANNEL=$(python3 -c 'import json; print(json.load(open("version.json"))["channel"])')
BRANCH=$(python3 -c "print('$CHANNEL'.title())")
VERSION_NAME="$VERSION"; [[ "$CHANNEL" != "stable" ]] && VERSION_NAME="$VERSION $BRANCH"
DIST=dist
APP="$DIST/Kodiak Cleaner.app"
ZIP="KodiakCleaner-$BRANCH-Mac.zip"
DMG="KodiakCleaner-$BRANCH-Mac.dmg"

run_tests() {
  python3 tests/check_source.py
  swift test
}

release_guard() {
  [[ -f Resources/keys/supporter-public.pem ]] || { echo "Release refused: Ted's supporter-public.pem isn't in Resources/keys (the test key is in use)."; exit 1; }
  grep -q "## \[$VERSION_NAME\]" CHANGELOG.md || { echo "Release refused: CHANGELOG.md has no entry for $VERSION_NAME."; exit 1; }
}

build_app() {
  $RELEASE && release_guard
  grep -q "## \[$VERSION_NAME\]" CHANGELOG.md || { echo "CHANGELOG.md has no entry for $VERSION_NAME."; exit 1; }
  swift build -c release --arch arm64 --arch x86_64
  local binary=.build/apple/Products/Release/KodiakCleaner
  rm -rf "$APP"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  cp "$binary" "$APP/Contents/MacOS/KodiakCleaner"
  sed -e "s/@VERSION@/$VERSION/g" -e "s/@CHANNEL@/$CHANNEL/g" -e "s/@RELEASE@/$RELEASE/g" packaging/Info.plist > "$APP/Contents/Info.plist"
  plutil -lint "$APP/Contents/Info.plist"
  cp packaging/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
  cp -R Resources/Fonts Resources/keys Resources/logo.png "$APP/Contents/Resources/"
  cp CHANGELOG.md PRIVACY.md terms-of-use.md "$APP/Contents/Resources/"
  # Ted's key replaces the test key once it exists; release builds never carry the test key.
  if [[ -f Resources/keys/supporter-public.pem ]]; then rm -f "$APP/Contents/Resources/keys/supporter-test-public.pem"; fi
  # Ad-hoc signature: Apple silicon needs one to run. A Developer ID signature and notarization replace it once Ted has
  # an Apple Developer account.
  codesign --force --sign - --timestamp=none "$APP"
  codesign --verify --strict "$APP"
  echo "Built $APP ($VERSION_NAME)"
}

package() {
  build_app
  rm -rf "$DIST/Docs" "$DIST/stage" "$DIST/$ZIP" "$DIST/$DMG"
  python3 tools/make_docs.py "$DIST/Docs" "$DMG"
  mkdir -p "$DIST/stage/zip" "$DIST/stage/dmg"
  ditto "$APP" "$DIST/stage/zip/Kodiak Cleaner.app"
  ditto "$DIST/Docs" "$DIST/stage/zip/Docs"
  (cd "$DIST/stage/zip" && ditto -c -k --sequesterRsrc . "../../$ZIP")
  ditto "$APP" "$DIST/stage/dmg/Kodiak Cleaner.app"
  ditto "$DIST/Docs" "$DIST/stage/dmg/Docs"
  ln -s /Applications "$DIST/stage/dmg/Applications"
  hdiutil create -volname "Kodiak Cleaner" -srcfolder "$DIST/stage/dmg" -ov -format UDZO "$DIST/$DMG"
  rm -rf "$DIST/stage"
  echo "Packaged $DIST/$ZIP and $DIST/$DMG"
}

case "$COMMAND" in
  test) run_tests ;;
  build) build_app ;;
  package) package ;;
  self-test) "$APP/Contents/MacOS/KodiakCleaner" --self-test ;;
  screenshots) rm -rf "$DIST/screenshots"; "$APP/Contents/MacOS/KodiakCleaner" --screenshots "$PWD/$DIST/screenshots" ;;
  *) sed -n '2,12p' "$0"; exit 1 ;;
esac
