import sys, pathlib
R = pathlib.Path('.')
# workflow: previously adapted version must match the app's workflow apart from our changes
new_app_wf = (R/'.github/workflows/build.yml').read_text()
t = new_app_wf
t = t.replace("""  push:
    branches: [main]
    tags: ['v*']
  pull_request:
""", """  push:
    branches: [main]
    tags: ['v*']
    paths-ignore: ['website/**', 'assets/**', 'releases/**', 'macos/**', '.github/workflows/macos.yml']
  pull_request:
    paths-ignore: ['website/**', 'assets/**', 'releases/**', 'macos/**', '.github/workflows/macos.yml']
""")
t = t.replace("""jobs:
  build:
    runs-on: windows-latest
""", """jobs:
  build:
    runs-on: windows-latest
    # The app lives in source/; the website, logos and release files sit beside it.
    defaults:
      run:
        working-directory: source
""")
t = t.replace("path: dist/*", "path: source/dist/*").replace("files: dist/*", "files: source/dist/*")
assert t.count("source/dist/*") >= 2 and "dist/*" not in t.replace("source/dist/*","") and "working-directory: source" in t and t.count("paths-ignore") == 2, "workflow changed shape"
(R/'.github/workflows/build.yml').write_text(t)
# website sync path
b = R/'website/build.mjs'; t = b.read_text()
old = """  const app = at("..", "ccleaner-alt");
  if (!existsSync(app)) throw new Error("--sync needs the app folder at ../ccleaner-alt");"""
if old in t:
    t = t.replace(old, """  const app = [at("..", "source"), at("..", "ccleaner-alt")].find((p) => existsSync(join(p, "version.json")));
  if (!app) throw new Error("--sync needs the app folder at ../source (or ../ccleaner-alt)");""")
    t = t.replace("documents from ../ccleaner-alt", "documents from ../source (or ../ccleaner-alt)")
    b.write_text(t)
else:
    print("NOTE: build.mjs sync line changed; check it by hand")
m = R/'website/README.md'; m.write_text(m.read_text().replace("`../ccleaner-alt`", "`../source` (or `../ccleaner-alt`)"))
# READMEs
src = (R/'source/README.md').read_text()
i = src.index("### Project Layout"); j = src.index("```", src.index("```", i) + 3) + 3
layout_old = src[i:j]
root_layout = """### Project Layout

```
.github/workflows/      GitHub Actions build and release (builds from source/)
assets/logo/            the logo and every file made from it (README.txt explains each)
macos/                  Kodiak Cleaner for Mac (SwiftUI), built and tested on a GitHub Mac
releases/               the newest Windows (MSI and Portable ZIP), Linux (AppImage and .deb ZIPs) and Mac (DMG and ZIP) downloads
source/                 the Windows app: everything needed to build Kodiak Cleaner
website/                the Kodiak Cleaner website (Cloudflare Pages)
website/github/         this page's screenshots and the release notes for GitHub releases
LICENSE                 MIT License
README.md               this page
```

Inside `source/`:

```
""" + "\n".join(l for l in layout_old.split("```")[1].strip("\n").split("\n")
               if not l.startswith((".github/", "docs/images/", "LICENSE ", "README.md "))) + "\n```"
root = src.replace(layout_old, root_layout)
root = root.replace("```powershell\n.\\build.ps1 ", "```powershell\ncd source\n.\\build.ps1 ", 1)
root = root.replace("docs/images/", "website/github/images/")
for f in ["SECURITY.md", "PRIVACY.md", "CHANGELOG.md", "terms-of-use.md", "why-it-was-made.md"]:
    root = root.replace(f"]({f})", f"](source/{f})")
import re
m_ = re.search(r"^Both are a single self-contained program.*\n", root, re.M)
assert m_
anchor = m_.group(0)
root = root.replace(anchor, anchor + "\nCopies of the newest Windows and Linux downloads are also kept in the [releases](releases/) folder of this repository. Older versions are on the Releases page.\n", 1)
(R/'README.md').write_text(root)
s = src.replace("docs/images/", "../website/github/images/")
s = "\n".join(l for l in s.split("\n") if not l.startswith(("docs/images/  ", ".github/workflows/  ", "../website/github/images/  ")))
(R/'source/README.md').write_text(s + ("\n" if src.endswith("\n") else ""))
