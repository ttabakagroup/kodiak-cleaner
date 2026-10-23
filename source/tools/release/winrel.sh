#!/usr/bin/env bash
# winrel.sh <V> <PV> <LINUX-ZIP-DIR> <shot.png> <whatsnew> <tested-md-file>: package a committed+tagged Windows build into release-V.
set -euo pipefail
V=$1; PV=$2; LD=$3; SHOT=$4; NEW=$5; TESTED=$6
S="/home/claude/k/Kodiak Cleaner/Source"; P="/home/claude/pf/Kodiak Cleaner"
cd "$S"; rm -rf /home/claude/docs && dotnet run --project tools/MakeDocs -- /home/claude/docs dev >/dev/null
rm -rf "$P/Source" && mkdir "$P/Source" && git ls-files -z | xargs -0 -I{} cp --parents {} "$P/Source/" && cp -r .git "$P/Source/"
cp /home/claude/docs/* "$P/Docs/"; cp /home/claude/docs/Changelog.txt "$P/ARCHIVE/ChangelogComplete.txt"; cp dist-out/* "$P/Application/Windows/"
cp $LD/KodiakCleaner-Dev-AppImage.zip $LD/KodiakCleaner-Dev-Deb.zip "$P/Application/Linux/"
git --git-dir="$P/Assets/Git" fetch -q "$P/Source" '+refs/heads/*:refs/heads/*' '+refs/tags/*:refs/tags/*'
W="$P/Website"; rm -rf "$W" && mkdir "$W" && (cd /mnt/project-files/website && tar --exclude=node_modules --exclude=.wrangler --exclude=.dev.vars -cf - .) | tar -xf - -C "$W"
{ printf 'Kodiak Cleaner website: where each file goes and how to publish it.\nThe full instructions are below (from README.md).\n\n'; cat "$W/README.md"; } > "$W/ReadMe.txt"
R=/home/claude/rel-$V; rm -rf $R; mkdir -p $R/updates
git archive --format=zip -o $R/KodiakCleaner-$V-source.zip v$V-dev
git archive --format=zip --prefix=kodiakcleaner/ -o $R/KodiakCleaner-$V-github-repo.zip v$V-dev
COMMIT=$(git rev-parse --short v$V-dev)
cp dist-out/* $R/; cp $LD/KodiakCleaner-Dev-AppImage.zip $LD/KodiakCleaner-Dev-Deb.zip $R/; cp /mnt/project-files/release-$PV/KodiakCleaner-logo.zip $R/
cp "$SHOT" $R/
cd /home/claude/pf && zip -qr "$R/Kodiak Cleaner.zip" "Kodiak Cleaner"
SHA=$(sha256sum $R/KodiakCleaner-Dev-Setup.msi | cut -d' ' -f1)
python3 - "$V" "$PV" "$SHA" "$COMMIT" "$NEW" "$TESTED" "$(basename "$SHOT")" "$(cat $LD/../version.json 2>/dev/null | python3 -c 'import json,sys;print(json.load(sys.stdin)["version"])' 2>/dev/null || echo ?)" <<'PY'
import os,sys,zipfile,json
V,PV,SHA,C,NEW,TESTED,SHOT,LV=sys.argv[1:]
R=f'/home/claude/rel-{V}'
json.dump({'channel':'dev','version':V,'msi':{'url':'https://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi','sha256':SHA}},open(R+'/updates/dev.json','w'),indent=2)
src='/mnt/project-files/website'; skip={'.dev.vars','.wrangler','node_modules'}
with zipfile.ZipFile(R+'/KodiakCleaner-website.zip','w',zipfile.ZIP_DEFLATED) as z:
    for root,dirs,files in os.walk(src):
        dirs[:]=sorted(x for x in dirs if x not in skip); rel=os.path.relpath(root,'/mnt/project-files'); z.write(root,rel+'/')
        for f in sorted(files):
            if f not in skip: z.write(os.path.join(root,f),os.path.join(rel,f))
def sz(f):
    b=os.path.getsize(f); return f"{b/1048576:.1f} MB" if b>=1048576 else f"{b/1024:.1f} KB"
def n(f): return len(zipfile.ZipFile(f).namelist()) if f.endswith('.zip') else 1
desc={
'KodiakCleaner-Dev-Setup.msi':f'Release download: the installer (Windows {V} Dev). Installs the Docs folder itself.',
'KodiakCleaner-Dev-Portable.zip':'Release download: the portable program plus the Docs folder.',
'KodiakCleaner-Dev-AppImage.zip':f'Linux download (Kodiak Cleaner for Linux {LV} Dev): the AppImage plus its Docs folder.',
'KodiakCleaner-Dev-Deb.zip':f'Linux download (Kodiak Cleaner for Linux {LV} Dev): the .deb package plus its Docs folder.',
f'KodiakCleaner-{V}-source.zip':f'Release source ZIP (tag v{V}-dev, commit {C}).',
f'KodiakCleaner-{V}-github-repo.zip':'The same source in a kodiakcleaner/ folder.',
'Kodiak Cleaner.zip':'The whole C:\\Projects\\Kodiak Cleaner folder (Source with .git, Website, Assets, Application\\Windows and Application\\Linux, Docs, ARCHIVE).',
'KodiakCleaner-website.zip':'Website snapshot from the live working copy.',
'KodiakCleaner-logo.zip':'The logo files, unchanged.',
'updates/dev.json':f"The website's update file for {V}.",
SHOT:'Screenshot under Wine.',
}
rows='\n'.join(f"| {k} | {sz(os.path.join(R,k))} | {n(os.path.join(R,k))} | {v} |" for k,v in desc.items())
open(R+'/FILE-LIST.md','w').write(f'''# Kodiak Cleaner {V} Dev: Release Files

Folder: /mnt/project-files/release-{V}/ (thread "Ctrl-click in Startup and Programs", 2026-10-10). Builds on {PV}. Pushed to the private repo.

What's new: {NEW}

| File | Size | Files Inside | What It Is |
|---|---|---|---|
{rows}

## Update file

MSI SHA-256: `{SHA}`

## Tested

{open(TESTED).read().strip()}
''')
PY
rm -rf /mnt/project-files/release-$V && mkdir -p /mnt/project-files/release-$V && cp -r $R/. /mnt/project-files/release-$V/
echo "release-$V ready ($SHA)"
