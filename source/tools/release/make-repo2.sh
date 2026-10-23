#!/usr/bin/env bash
# make-repo2.sh <win-version> <linux-version>: builds the GitHub repo layout from /mnt/project-files/release-<win-version>/
# (0.8.31+ file names). Website comes from the live /mnt/project-files/website/.
set -euo pipefail
V=$1; LV=$2; REL=/mnt/project-files/release-$V; SP=$(cd "$(dirname "$0")" && pwd); OLD=$SP/kodiakcleaner; R=$SP/repo-$V; X=$SP/x-$V
rm -rf "$R" "$X"; mkdir -p "$R" "$X"
unzip -qo $REL/KodiakCleaner-$V-github-repo.zip -d $X/src
unzip -qo $REL/KodiakCleaner-logo.zip -d $X/logo
cp -a $X/src/kodiakcleaner $R/source
mv $R/source/.github $R/.github
cp $R/source/SECURITY.md $R/.github/SECURITY.md
# Repo-only workflow (R2 upload + site deploy), not part of the app's source.
for w in publish-downloads.yml create-release.yml macos.yml; do [ -f $OLD/.github/workflows/$w ] && cp $OLD/.github/workflows/$w $R/.github/workflows/; done
mv $R/source/docs/images $X/images; rmdir $R/source/docs
cp -a /mnt/project-files/website $R/website
rm -rf $R/website/.dev.vars $R/website/.wrangler $R/website/node_modules
mkdir -p $R/website/github/release-notes; cp -a $X/images $R/website/github/images
cp $OLD/website/github/README.md $R/website/github/README.md
cp -a $OLD/website/github/release-notes/. $R/website/github/release-notes/
mkdir -p $R/assets; cp -a $X/logo/kodiak-cleaner-logo $R/assets/logo
W=$R/releases/Windows-$V-dev; L=$R/releases/Linux-$LV-dev; mkdir -p $W $L
# No Setup.zip (Ted, 2026-10-06): the bare MSI is enough.
cp $REL/KodiakCleaner-Dev-Portable.zip $W/
if [ -f $REL/KodiakCleaner-Dev-Setup.msi ]; then cp $REL/KodiakCleaner-Dev-Setup.msi $W/; else unzip -qjo $REL/KodiakCleaner-Dev-Setup.zip '*.msi' -d $W/; fi
cp $REL/KodiakCleaner-Dev-AppImage.zip $REL/KodiakCleaner-Dev-Deb.zip $L/
cp $OLD/.gitignore $R/.gitignore
# Mac app (top-level macos/) and its newest release files are not in the Windows release ZIPs: carry them over.
[ -d $OLD/macos ] && cp -a $OLD/macos $R/macos
for m in $OLD/releases/Mac-*; do [ -d "$m" ] && cp -a "$m" $R/releases/; done
cp $R/source/LICENSE $R/LICENSE
cd $R && python3 "$SP/transform.py"
