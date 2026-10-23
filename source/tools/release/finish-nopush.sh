#!/usr/bin/env bash
# finish.sh <win-ver> <linux-ver> <prev-win-ver>: sync live website, write release notes, test, rebuild website ZIP, commit and push.
set -euo pipefail
V=$1; LV=$2; P=$3; SP=$(cd "$(dirname "$0")" && pwd); REPO=/home/claude/kodiakcleaner; R=$SP/repo-$V
cd $R
rm -rf $SP/gh && cp -a website/github $SP/gh && rm -rf website && cp -a /mnt/project-files/website website
rm -rf website/.dev.vars website/.wrangler website/node_modules website/github && mv $SP/gh website/github
# SELF_SITE=1: the website thread hasn't prepared this build, so list it, sync the app's docs and update file here.
if [ "${SELF_SITE:-0}" = 1 ]; then
  size() { node -e 'const b=+process.argv[1]; console.log(b>=1048576 ? (b/1048576).toFixed(1)+" MB" : Math.round(b/1024)+" KB")' "$(stat -c %s "$1")"; }
  jq --arg v "$V" --arg i "$(size /mnt/project-files/release-$V/KodiakCleaner-Dev-Setup.msi)" --arg p "$(size /mnt/project-files/release-$V/KodiakCleaner-Dev-Portable.zip)" \
     '.releases = ({($v): {installerSize: $i, portableSize: $p}} + (.releases | del(.[$v])))' website/site.config.json > website/site.config.json.new && mv website/site.config.json.new website/site.config.json
  for f in CHANGELOG.md PRIVACY.md terms-of-use.md why-it-was-made.md LICENSE version.json; do cp source/$f website/content/$f; done
  cp /mnt/project-files/release-$V/updates/dev.json website/src/static/updates/dev.json
  # The newest Linux build ships with this release, so the Linux page shows its version and sizes.
  for f in CHANGELOG.md version.json; do cp /mnt/project-files/kodiak-linux/$f website/content/linux/$f; done
  jq --arg a "$(size /mnt/project-files/release-$V/KodiakCleaner-Dev-AppImage.zip)" --arg d "$(size /mnt/project-files/release-$V/KodiakCleaner-Dev-Deb.zip)" \
     '.linux.appimageSize = $a | .linux.debSize = $d' website/site.config.json > website/site.config.json.new && mv website/site.config.json.new website/site.config.json
fi
jq -e --arg v "$V" '.releases[$v]' website/site.config.json >/dev/null || { echo "site.config.json lacks $V"; exit 1; }
[ "$(jq -r .msi.sha256 website/src/static/updates/dev.json)" = "$(sha256sum /mnt/project-files/release-$V/KodiakCleaner-Dev-Setup.msi | cut -c1-64)" ] || { echo "dev.json mismatch"; exit 1; }
python3 - website/github/release-notes "$V" "$P" source/CHANGELOG.md <<'PY'
import sys,re
d,v,p,cl=sys.argv[1:]
s=open(f'{d}/{p}-dev.md').read().replace(f'# Kodiak Cleaner {p} Dev',f'# Kodiak Cleaner {v} Dev').replace(f'KodiakCleaner-{p}-source.zip',f'KodiakCleaner-{v}-source.zip')
c=open(cl).read()
# Every version since the previous release (newest first), each under its own heading.
secs=re.findall(r'^## \[([0-9.]+) Dev\][^\n]*\n(.*?)(?=^## )',c,re.S|re.M)
key=lambda x: tuple(map(int,x.split('.')))
picked=[(ver,b.strip()) for ver,b in secs if key(p)<key(ver)<=key(v)]
body=picked[0][1] if len(picked)==1 else "\n\n".join(f"### {ver} Dev\n\n"+b.replace("\n### ","\n#### ").replace("### ","#### ",1 if b.startswith("### ") else 0) for ver,b in picked)
start=s.index(f"## What's New in Windows {p}"); end=s.index("## What's New in Linux")
open(f'{d}/{v}-dev.md','w').write(s[:start]+f"## What's New in Windows {v}\n\n{body}\n\n"+s[end:])
PY
cd website && node build.mjs | tail -1 && node --test tests/*.test.mjs 2>&1 | grep -E "^# (pass|fail)" && node --test tests/*.test.mjs >/dev/null 2>&1
[ "${SELF_SITE:-0}" = 1 ] || { rm -rf public && cp -a /mnt/project-files/website/public public; }
[ "${SELF_SITE:-0}" = 1 ] || python3 - "$V" <<'PY'
import os,sys,zipfile
v=sys.argv[1]; src='/mnt/project-files/website'; out=f'/mnt/project-files/release-{v}/KodiakCleaner-website.zip'
skip={'.dev.vars','.wrangler','node_modules'}
with zipfile.ZipFile(out+'.new','w',zipfile.ZIP_DEFLATED) as z:
    for root,dirs,files in os.walk(src):
        dirs[:]=sorted(x for x in dirs if x not in skip); rel=os.path.relpath(root,'/mnt/project-files'); z.write(root,rel+'/')
        for f in sorted(files):
            if f not in skip: z.write(os.path.join(root,f),os.path.join(rel,f))
os.replace(out+'.new',out)
PY
cd $REPO && git ls-files -z | xargs -0 rm -f && find . -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} + && cp -a $R/. . && git add -A
T=$'\n\nCo-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>\nClaude-Session: https://claude.ai/code/session_01HeMzPKYkQSwE4ajj9UwYpw'
git restore --staged releases/
git commit -qm "Kodiak Cleaner $V Dev: app, website and release notes$T"
git add -A releases/Windows-$P-dev releases/Windows-$V-dev/KodiakCleaner-Dev-Portable.zip && git commit -qm "Release files: Windows $V Dev portable ZIP$T"
git add -A releases && git commit -qm "Release files: Windows $V Dev MSI$T"
[ -z "$(git status --short)" ]
echo NOPUSH
git rev-parse --short HEAD
