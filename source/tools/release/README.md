# Release scripts

These are the scripts the code owner uses to package and ship each Windows build. They're kept here so they survive a lost session.
Paths inside are written for the build container (Source at `/home/claude/k/Kodiak Cleaner/Source`, a clone of the repo at
`/home/claude/kodiakcleaner`, the shared project folder at `/mnt/project-files`). Adjust them for a new setup.

## Order for a release V (previous PV, Linux LV)
1. In Source: apply patches, move CHANGELOG [Unreleased] under `## [V Dev] - date`, set version.json, run
   `dotnet test tests/KodiakCleaner.Core.Tests -p:EnableWindowsTargeting=true`, publish, `bash installer/package-linux.sh publish dist-out`,
   commit and `git tag vV-dev`.
2. `bash winrel.sh V PV <linux dist dir> <screenshot.png> "<what's new line>" <tested.md>`: builds release-V in the project folder.
   Copy the Mac DMG and ZIP over from release-PV.
3. In the repo clone: `git fetch origin main && git merge --ff-only origin/main`.
4. `bash stage.sh V LV && SELF_SITE=1 bash finish-nopush.sh V LV PV` (website tests must say `# fail 0`).
5. Known traps, fix before pushing:
   - Staging drops `.claude/settings.json` and can copy stale site screenshots: `git checkout origin/main -- .claude/settings.json website/src/assets/images/screens`,
     then `node build.mjs` and `node --test` in website/ and commit.
   - Check `website/content/mac` still shows the current Mac version.
   - The CI workflow lives in Source/.github/workflows/build.yml (keep `continue-on-error` and `retention-days: 2` on the artifact upload).
   - If origin moved (the website thread pushes too): rebase, take theirs for website/public, rebuild and rerun the site tests.
6. `git push origin main` and wait for Build MSI and Publish Downloads and Website to go green.
7. `KC_WORKING_COPY=<the project folder's working copy of the app> bash sync-after.sh V PV` updates the project's website, repo copy and
   that working copy (changed files and dist/). Keep only the newest two release-<ver> folders.
