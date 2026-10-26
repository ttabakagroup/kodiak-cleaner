import { icons } from "../icons.mjs";
import { pageHead } from "../layout.mjs";

// Support (at /donate/, kept for old links): one clear way to support Kodiak Cleaner on Ko-fi. It stays free either way.
// Never say "donate" or "tip" on the page: Supporter perks make it a purchase, not a gift (Ted, 2026-10-24).
export default function donate(ctx) {
  const { cfg } = ctx;
  const body = `
${pageHead("Support Kodiak Cleaner", "Kodiak Cleaner is free and always will be. If it helps you, you can support it on Ko-fi.")}

<section class="tight">
  <div class="wrap narrow center">
    <div class="card pad reveal">
      <span class="icon">${icons.heart}</span>
      <h2>Support on Ko-fi</h2>
      <p>Pick any amount, one time or monthly. It goes straight to Ted Tabaka, who builds and tests Kodiak Cleaner for Windows, Mac and Linux in his own time.</p>
      <div class="btn-row centered mt">
        <a class="btn primary lg" href="${cfg.kofi}" target="_blank" rel="noopener">${icons.heart}<span>Support on Ko-fi<small>Opens ko-fi.com in a new tab</small></span></a>
      </div>
      <p class="muted mt">Ko-fi handles the payment. This website never sees your card or account details. Changed your mind? You can get a full refund within 14 days: see the <a href="/refunds/">Refund Policy</a>.</p>
    </div>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="grid cols-3">
      <div class="card reveal"><span class="icon">${icons.check}</span><h3>Everything Stays Free</h3><p>Every feature is free for everyone, with no ads, no upsells, no trials and no telemetry. Supporting never unlocks cleaning features, and not supporting never takes anything away.</p></div>
      <div class="card reveal"><span class="icon">${icons.gift}</span><h3>A Thank-You</h3><p>Supporting with $10 or more gets you a supporter code by email. One supporter code covers up to 10 of your computers. Need more? Use the contact form at kodiakcleaner.app/contact/. Enter it in Kodiak Cleaner to unlock the Classic, Glacier and Lagoon color schemes, and it works in the Windows, Mac and Linux editions. <a href="/features/list/">See what's free and what's Supporter</a>.</p></div>
      <div class="card reveal"><span class="icon">${icons.github}</span><h3>Other Ways to Help</h3><p><a href="/contact/?topic=Bug%20Report">Report a bug</a>, <a href="/contact/?topic=Feature%20Request">request a feature</a>, star the project on <a href="${cfg.repo}" rel="noopener">GitHub</a>, or tell a friend who needs a cleaner without the junk.</p></div>
    </div>
  </div>
</section>`;
  return {
    id: "donate",
    path: "/donate/",
    out: "donate/index.html",
    title: "Support",
    description: "Kodiak Cleaner is free, with no ads or upsells. If it helps you, you can support it on Ko-fi and get a supporter code for the Classic, Glacier and Lagoon color schemes.",
    body,
  };
}
