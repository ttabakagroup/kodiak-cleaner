#!/usr/bin/env bash
# stage.sh <win-ver> <linux-ver>: rebuild repo layout for a new Windows build from the current repo HEAD.
set -euo pipefail
V=$1; LV=$2; SP=$(cd "$(dirname "$0")" && pwd); REPO=/home/claude/kodiakcleaner
rm -rf $SP/kodiakcleaner $SP/repo-* $SP/x-*; mkdir $SP/kodiakcleaner
git -C $REPO archive HEAD | tar -x -C $SP/kodiakcleaner
cd $SP && bash make-repo2.sh $V $LV
cp $REPO/releases/README.md $SP/repo-$V/releases/
