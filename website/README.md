# Kodiak Cleaner Website

The website for Kodiak Cleaner (Windows) and Kodiak Cleaner for Linux, built for Cloudflare Pages. It is a static site plus one small Pages Function that sends the Contact Us form to Ted's inbox (set by `CONTACT_TO`).

No trackers, ads, cookies or outside fonts. The only outside script is Cloudflare Turnstile (the spam check), and it loads only on Contact Us.

## Pages

| Page | Address | Made from |
|------|---------|-----------|
| Home | `/` | `src/pages/home.mjs` |
| Features | `/features/` | `src/pages/features.mjs` |
| Linux | `/linux/` | `src/pages/linux.mjs` and `content/linux/CHANGELOG.md` |
| Mac | `/mac/` | `src/pages/mac.mjs` and `content/mac/CHANGELOG.md` |
| Download | `/download/` | `src/pages/download.mjs` (Windows, Mac and Linux) |
| Changelog | `/changelog/` | `content/CHANGELOG.md` |
| Archive | `/archive/` | `content/CHANGELOG.md` and `site.config.json` |
| Contact Us | `/contact/` | `src/pages/contact.mjs` and `functions/api/contact.js` |
| Donate | `/donate/` | `src/pages/donate.mjs` (Ko-fi link from `site.config.json` "kofi") |
| Full Feature List | `/features/list/` | `src/pages/featurelist.mjs` (every feature, Free or Supporter, per edition) |
| Why It Was Made | `/why-it-was-made/` | `content/why-it-was-made.md` (word for word) |
| Privacy Policy | `/privacy/` | website section plus `content/PRIVACY.md` |
| Terms of Use | `/terms/` | `content/terms-of-use.md` |

Every page links to GitHub and to tedtabaka.com.

## Deploy to Cloudflare Pages (first time)

The site's main address is **https://www.kodiakcleaner.app**. The bare `kodiakcleaner.app`, `kodiakcleaner.pages.dev` and every preview link such as `3c0fc5da.kodiakcleaner.pages.dev` send visitors to the same page there automatically, and `/downloads` goes to the Download page (`functions/_middleware.js`). To look at a deploy before it's live, use `npm run preview` on your PC, since preview links now redirect too.

You need a free Cloudflare account and Node.js 18 or newer on your PC.

### What gets uploaded

Keep the whole `website` folder on your PC, but don't drag it into the Cloudflare dashboard. Drag-and-drop uploads plain files only and skips the `functions` folder, so Contact Us would not work.

Instead, `npm run deploy` (step 1) uploads the right two things for you: the built pages in `public/` and the form code in `functions/`. Nothing else in the folder goes online.

Never upload or share `.dev.vars` (local test keys), `.wrangler/` (local cache) or `node_modules/`. They are listed in `.gitignore`.

**1. Put the site online.** Open a terminal in the `website` folder and run:

```
npx wrangler login
npm run deploy
```

Say yes when it asks to create the project `kodiakcleaner`. The site is then live at `https://kodiakcleaner.pages.dev`.

**2. Connect www.kodiakcleaner.app.** Cloudflare dashboard > Workers & Pages > kodiakcleaner > Custom domains > Set up a custom domain:
- Add `www.kodiakcleaner.app` and confirm. This is the main address.
- Add `kodiakcleaner.app` the same way, so the bare domain redirects to www.

If kodiakcleaner.app is registered with Cloudflare or already uses Cloudflare's nameservers, the DNS records are made for you. If it's at another registrar, first add the domain to Cloudflare (Add a domain) and switch its nameservers to the two Cloudflare gives you, then come back to this step. The security certificate is automatic; .app domains only work over HTTPS, which Cloudflare provides.

**3. Make the Turnstile spam check.** Cloudflare dashboard > Turnstile > Add widget. Name it "Kodiak Cleaner", add the hostnames `www.kodiakcleaner.app`, `kodiakcleaner.app` and `kodiakcleaner.pages.dev`, Widget Mode: Managed. Copy the **Site Key** and **Secret Key**.

**4. Make a Resend key (sends the emails).** Sign up free at resend.com **using the email address messages should go to**, then API Keys > Create API Key (Sending access). Copy the key. While you send from Resend's test address, messages can only go to the email you signed up with, which is exactly where they should go.

Optional, for a nicer sender: in Resend > Domains, add `kodiakcleaner.app` and add the DNS records it shows in Cloudflare (DNS > Records). Once it says Verified, set `CONTACT_FROM` below to `Kodiak Cleaner Website <contact@kodiakcleaner.app>`. Messages can then go to any address.

**5. Add the settings.** Cloudflare dashboard > Workers & Pages > kodiakcleaner > Settings > Variables and Secrets, for Production:

| Name | Type | Value |
|------|------|-------|
| `TURNSTILE_SITE_KEY` | Text | Site Key from step 3 |
| `TURNSTILE_SECRET_KEY` | Secret | Secret Key from step 3 |
| `RESEND_API_KEY` | Secret | Key from step 4 |
| `CONTACT_TO` | Text (optional) | Where messages go. Defaults to the address in functions/api/contact.js |
| `CONTACT_FROM` | Text (optional) | Sender, only after kodiakcleaner.app is verified in Resend |

**6. Deploy again** with `npm run deploy` so the settings take effect, then open https://www.kodiakcleaner.app/contact/ and send yourself a test message.

Leave Cloudflare Web Analytics off so the site stays tracker-free.

**Prefer GitHub over the terminal?** Put the `website` folder in a GitHub repository (the `.gitignore` keeps the local files out), then Workers & Pages > Create > Pages > Connect to Git. Build command `npm run build`, build output directory `public`. Every push then publishes the site, with the form included. Steps 2 to 5 stay the same.

## Downloads Served From kodiakcleaner.app

While `hostedDownloads` is in `site.config.json`, the Download page's buttons point at
`https://www.kodiakcleaner.app/download/KodiakCleaner-Installer.msi` (a bare MSI, Ted's choice on 2026-10-05) and `.../KodiakCleaner-Portable.zip`
instead of GitHub. The Linux buttons (from `linux` in `site.config.json`) point at `.../KodiakCleanerLinux-DEB.zip` and
`.../KodiakCleanerLinux-AppImage.zip` the same way, and the Mac buttons (from `mac`) at `.../KodiakCleanerMac.dmg` and `.../KodiakCleanerMac.zip`. The Windows files are about 70 MB, and Cloudflare Pages only takes files up to 25 MB, so they
live in Cloudflare R2 (free up to 10 GB, with no charge for downloads). `functions/download/[file].js` serves them.

One-time setup:
1. Cloudflare dashboard > R2 Object Storage. Turn on R2 if asked (Cloudflare asks for a payment method even on the free tier).
2. Create bucket, named `kodiakcleaner-downloads`.
3. Open the bucket > Upload, and add the six files under exactly these names: `KodiakCleaner-Installer.msi`, `KodiakCleaner-Portable.zip`, `KodiakCleanerLinux-AppImage.zip`, `KodiakCleanerLinux-DEB.zip`, `KodiakCleanerMac.dmg` and `KodiakCleanerMac.zip`.
4. Workers & Pages > kodiakcleaner > Settings > Bindings > Add > R2 bucket. Variable name `DOWNLOADS`, bucket `kodiakcleaner-downloads`.
5. `npm run deploy`.

Each new release: upload the new files over the old ones with the same names, update the sizes in `site.config.json`, and deploy. The release folder's `upload-to-r2/` has them ready-renamed: the MSI is `KodiakCleaner-Dev-Setup.msi`, the portable ZIP is `KodiakCleaner-Dev-Portable.zip`, the Linux ZIPs are `KodiakCleaner-Dev-AppImage.zip` and `KodiakCleaner-Dev-Deb.zip`, and the Mac files are `KodiakCleaner-Dev-Mac.dmg` and `KodiakCleaner-Dev-Mac.zip` from `../kodiak-mac/dist`.
To go back to GitHub downloads, delete `hostedDownloads` from `site.config.json` and deploy.

## Update File for Kodiak Cleaner for Windows

From 0.8.40, Kodiak Cleaner for Windows checks `https://www.kodiakcleaner.app/updates/dev.json` for updates and downloads
`/download/KodiakCleaner-Installer.msi` (spec: `plans/kodiak-update-file.md` in the project files).

- The file lives at `src/static/updates/dev.json` and is copied to `public/updates/dev.json` on build. `_headers` serves it with
  `Cache-Control: no-cache`, and downloads are served with `no-cache` too, so a new release shows up straight away.
- Every time `KodiakCleaner-Installer.msi` is replaced in R2, write that build's `version` and `sha256` into this file and deploy,
  in that order: MSI first, then the site. If the two disagree, the app throws the download away and tries again the next day.
- The release folder has a ready-made copy at `release-<ver>/updates/dev.json`. A test checks the file's shape.
- While Cloudflare Access protects the site, the app is sent to the login page and reports that it couldn't check.

## If Contact Us Says a Message Couldn't Be Sent

The form now says what went wrong. The common causes:

- **"error 404" or "error 405"**: the form code wasn't uploaded. Deploy with `npm run deploy` (or Git), not drag-and-drop.
- **"error 403"**: a Cloudflare security setting blocked the form. Check Security > Bots (Bot Fight Mode) and Security > WAF for kodiakcleaner.app.
- **"Sign up for Resend with the address in CONTACT_TO"**: Resend's test sender only delivers to the email the Resend account was made with.
- **"Resend API key was refused"**: make a new key in Resend and paste it again into `RESEND_API_KEY`.
- **"CONTACT_FROM isn't verified"**: delete `CONTACT_FROM`, or finish verifying kodiakcleaner.app in Resend.

After changing any setting in Cloudflare, deploy again; settings only reach the site on the next deploy. To see the details, open Workers & Pages > kodiakcleaner > Deployments > the newest one > Functions > Real-time logs, then send a test message.

## Supporter Codes From Ko-fi

`functions/api/kofi.js` is the Ko-fi webhook at `https://www.kodiakcleaner.app/api/kofi`. When someone donates, buys a shop item or
starts a membership on ko-fi.com/ttabaka, Ko-fi posts the payment here. If the token matches and the payment is at least
`SUPPORTER_MIN` (US dollars, default 5; a short table in the file covers EUR, GBP, CAD, AUD and a few more), it signs a supporter
code with Ted's private key and emails it to the supporter through Resend. Ted gets a short note for every payment, with the code
added whenever it couldn't be emailed, so he can send it by hand. Nothing is stored.

- Codes use the same format as `make-code` and work in the Windows, Linux and Mac apps. The code ID comes from the Ko-fi transaction,
  so a repeated webhook gives the same ID; add it to `keys/withdrawn-codes.txt` in the app if a payment is refunded.
- Settings (Settings > Variables and Secrets, as secrets): `KOFI_VERIFICATION_TOKEN`, `SUPPORTER_PRIVATE_KEY` (all of
  `keys/supporter-private.pem`), `SUPPORTER_FROM` (a sender on a domain verified in Resend), optional `SUPPORTER_MIN`.
- Resend's test sender only delivers to Ted, so supporter emails need kodiakcleaner.app verified in Resend first.
- While Cloudflare Access protects the site, `/api/kofi` needs a Bypass rule like the downloads, or Ko-fi gets the login page.
- Ko-fi > Settings > API > Webhooks has a Send Test button; it should leave a note in Ted's inbox (and a code if the test amount meets the minimum).

## Launch Video on the Homepage

The homepage opens with the launch video over the page when `src/assets/video/` holds the file named in `site.config.json` under `"launchVideo"` (`file`, optional `poster` image, `title`, and `id`). Without the file the build prints a note and the homepage has no overlay.

- It plays muted with Turn Sound On, and doesn't start by itself for visitors who prefer reduced motion.
- The X, Esc, a click outside it and Continue to the Site close it. Closing is remembered in the browser, so it opens by itself once per visitor; change `id` to show a new video to everyone again. Watch the Video, under the download buttons, opens it any time.
- Use an H.264 MP4 of 25 MiB or less (Cloudflare Pages' file limit). `/video/*` is cached for a week, so give a replacement video a new file name.

## Download Thank-You Pop-Up

Links to other websites open in a new tab: `build.mjs` adds `target="_blank" rel="noopener noreferrer"` to every outside link on every page, and a test checks it.

Every page carries a small pop-up (`#download-thanks` in `src/layout.mjs`, script at the end of `js/site.js`). Clicking any download button (Installer, Portable, DMG, Mac ZIP, AppImage, DEB, or an Archive release file) starts the download as normal and then shows a thank-you asking for a tip on Ko-fi. X, Esc, Maybe Later and a click outside close it. Ko-fi links open in a new tab.

## Each New Kodiak Cleaner Release

1. `npm run sync` copies CHANGELOG.md and the other documents from `../ccleaner-alt`, the Linux CHANGELOG.md and version.json from `../kodiak-linux` into `content/linux/`, and the Mac ones from `../kodiak-mac` into `content/mac/` (or copy them by hand).
2. In `site.config.json`, add the version under `releases` with its two file sizes. Only versions listed there get download links; the rest show "Not published".
3. `npm test`, then `npm run deploy`.

Archive links point at the GitHub release files, for example
`https://github.com/ttabakagroup/kodiakcleaner/releases/download/v0.8.37-dev/KodiakCleaner-Dev-Setup.msi`
(0.8.31 to 0.8.36: `KodiakCleaner-Dev-Setup.zip`; 0.8.30 and older: `KodiakCleaner-0.8.27-x64-msi-dev.zip`).
They work once that release exists on GitHub. Only the hosted installer and GitHub releases from 0.8.37 on are a bare `.msi`; a test fails if any other bare `.msi` is linked.

## Commands

| Command | What it does |
|---------|--------------|
| `npm run build` | Builds the site into `public/` |
| `npm test` | Builds, then checks links, images, downloads, Title Case, outside resources and the contact function |
| `npm run preview` | Runs the site and the form locally with Wrangler (put test keys in a `.dev.vars` file) |
| `npm run deploy` | Builds and uploads to Cloudflare Pages |
| `npm run sync` | Copies the documents from `../ccleaner-alt`, `../kodiak-linux` and `../kodiak-mac`, then builds |

Turnstile's test keys for local runs: site key `1x00000000000000000000AA`, secret `1x0000000000000000000000000000000AA`.

## Files

```
build.mjs               builds public/ from src/ and content/
site.config.json        site address, links and published releases
content/                CHANGELOG.md, PRIVACY.md, terms-of-use.md, why-it-was-made.md, LICENSE, version.json (copied from the app)
content/linux/          CHANGELOG.md and version.json (copied from Kodiak Cleaner for Linux)
content/mac/            CHANGELOG.md and version.json (copied from Kodiak Cleaner for Mac)
functions/api/contact.js  Contact Us handler (Turnstile check, then email through Resend; nothing stored)
functions/api/kofi.js   Ko-fi webhook: signs and emails supporter codes (nothing stored)
src/layout.mjs          header, footer and shared pieces
src/pages/              one file per page
src/markdown.mjs        renders the Markdown documents and reads the changelog
src/icons.mjs           line icons
src/assets/             CSS (theme.css from Ted's build standard, unchanged), scripts, Fredoka font, logo and screenshots
src/static/_headers     security and caching headers for Cloudflare
functions/download/[file].js  serves the hosted downloads from R2
src/static/_redirects   short links (/github, /releases)
src/static/updates/dev.json  update file Kodiak Cleaner for Windows reads (written by the publish workflow)
functions/_middleware.js  sends kodiakcleaner.app, pages.dev and preview links to www.kodiakcleaner.app
tests/                  site checks, contact and Ko-fi function tests
public/                 the built site (made by npm run build)
```

Windows screenshots come from the 0.8.36 Dev portable build under Wine with sample programs and startup entries (the Developer item was painted out of the Appearance shots, and the Application shot shows a sample folder path). Linux screenshots (`linux-*.png`) come from Kodiak Cleaner for Linux 0.8.1 Dev via its `tools/screenshots.py`. Mac screenshots (`mac-*.png`) come from Kodiak Cleaner for Mac 0.8.0 Dev (`dist/screenshots`, taken on a GitHub Mac); its Triggers shot shows that build machine's own launch agents. Retake the Windows ones on Windows when convenient; real program icons look better.

Copyright © 2026 Ted Tabaka. MIT License.
