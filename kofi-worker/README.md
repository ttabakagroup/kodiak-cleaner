# Ko-fi Webhook

One Cloudflare Worker for every product Ted sells or takes donations for on Ko-fi (ko-fi.com/ttabaka).
Ko-fi allows a single webhook URL per account, so this Worker decides which product each payment is for,
signs a supporter code with that product's private key and emails it to the supporter through Resend.
Ted gets a note for every payment. Nothing is stored.

Address: https://kofi.tedtabaka.com/ (or the Worker's workers.dev address if tedtabaka.com isn't on Cloudflare).

## Products

`PRODUCTS` at the top of `worker.js`, A to Z. A payment goes to:
1. the product whose `shopCodes` lists a Ko-fi Shop item in the order (the code at the end of the item's link),
2. else the product whose `tiers` lists the membership tier,
3. else the product marked `default` (plain donations; Kodiak Cleaner).

To add a product: make its key pair (Kodiak Cleaner's `tools/SupporterCodes make-key` works for any app that checks the
same format), add an entry with its own `keySecret`, add that secret in Cloudflare, and deploy.

## Settings (Worker > Settings > Variables and Secrets, as secrets)

| Name | What |
|------|------|
| `KOFI_VERIFICATION_TOKEN` | Ko-fi > Settings > API > Webhooks > Advanced |
| `RESEND_API_KEY` | Resend API key (Sending access) |
| `NOTIFY_TO` | where Ted's notes go; kept out of the code so it never shows in the repo |
| `KODIAK_PRIVATE_KEY` | all of Kodiak Cleaner's `keys/supporter-private.pem` |
| `SUPPORTER_MIN` | optional, smallest payment in US dollars (default 10) |

Each product's `from` address must be on a domain verified in Resend (Kodiak Cleaner: kodiakcleaner.app).

## Test

`npm test` (13 tests, no installs needed). Ko-fi's Send Test button sends a fake payment; the note to Ted then carries
the code because the fake buyer address can't receive mail.
