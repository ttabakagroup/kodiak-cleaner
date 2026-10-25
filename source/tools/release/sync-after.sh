#!/usr/bin/env bash
# sync-after.sh <V> <PV>: after a push, sync website working copy release files, repo staging copy and the app's working copy.
# KC_WORKING_COPY: the project folder's working copy of the app that gets the changed files and dist/ (required).
set -euo pipefail
A=${KC_WORKING_COPY:?Set KC_WORKING_COPY to the app working copy in the project folder (it gets the changed files and dist/)}
V=$1; PV=$2; cd /home/claude/kodiakcleaner; W=/mnt/project-files/website
for f in content/CHANGELOG.md content/version.json content/PRIVACY.md content/terms-of-use.md content/why-it-was-made.md content/linux/CHANGELOG.md content/linux/version.json content/mac/CHANGELOG.md content/mac/version.json src/static/updates/dev.json; do [ -f website/$f ] && cp website/$f $W/$f; done
python3 - <<'PY'
import json
a=json.load(open('/home/claude/kodiakcleaner/website/site.config.json')); p='/mnt/project-files/website/site.config.json'; b=json.load(open(p))
b['releases']=a['releases']
for k in ('linux','mac'):
    if k in a and k in b:
        for kk,vv in a[k].items():
            if kk.endswith('Size'): b[k][kk]=vv
json.dump(b,open(p,'w'),indent=2); open(p,'a').write('\n')
PY
(cd $W && node build.mjs | tail -1)
rm -rf /mnt/project-files/kodiakcleaner-repo.new && mkdir /mnt/project-files/kodiakcleaner-repo.new && git archive HEAD | tar -x -C /mnt/project-files/kodiakcleaner-repo.new && rm -rf /mnt/project-files/kodiakcleaner-repo && mv /mnt/project-files/kodiakcleaner-repo.new /mnt/project-files/kodiakcleaner-repo
git archive --format=zip --prefix=kodiakcleaner/ -o /mnt/project-files/KodiakCleaner-github-repo.zip HEAD
S="/home/claude/k/Kodiak Cleaner/Source"; cd "$S"
git diff --name-only --diff-filter=AM v$PV-dev v$V-dev | while read f; do mkdir -p "$A/$(dirname "$f")"; cp "$f" "$A/$f"; done
rm -rf "$A/dist" && mkdir -p "$A/dist" && cp dist-out/* "$A/dist/"
echo synced
