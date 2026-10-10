import { icons } from "../icons.mjs";
import { pageHead } from "../layout.mjs";

// Donate: one clear way to tip Ted on Ko-fi. Kodiak Cleaner stays free either way.
export default function donate(ctx) {
  const { cfg } = ctx;
  const body = `
${pageHead("Donate", "Kodiak Cleaner is free and always will be. If it saves you time or space, a small tip helps Ted keep building it.")}

<section class="tight">
  <div class="wrap narrow center">
    <div class="card pad reveal">
      <span class="icon">${icons.heart}</span>
      <h2>Support Kodiak Cleaner on Ko-fi</h2>
      <p>Any amount helps, one time or monthly. Tips go straight to Ted Tabaka, who builds and tests Kodiak Cleaner for Windows, Mac and Linux in his own time.</p>
      <div class="btn-row centered mt">
        <a class="btn primary lg" href="${cfg.kofi}" target="_blank" rel="noopener">${icons.heart}<span>Donate on Ko-fi<small>Opens ko-fi.com in a new tab</small></span></a>
      </div>
      <p class="muted mt">Ko-fi handles the payment. This website never sees your card or account details.</p>
    </div>
  </div>
</section>

<section>
  <div class="wrap">
    <div class="grid cols-3">
      <div class="card reveal"><span class="icon">${icons.check}</span><h3>Everything Stays Free</h3><p>Every feature is free for everyone, with no ads, no upsells, no trials and no telemetry. Donating never unlocks cleaning features, and not donating never takes anything away.</p></div>
      <div class="card reveal"><span class="icon">${icons.gift}</span><h3>A Thank-You</h3><p>Supporters get a supporter code by email. Enter it in Kodiak Cleaner to unlock the Classic and Glacier color schemes, and it works in the Windows, Mac and Linux editions. <a href="/features/list/">See what's free and what's Supporter</a>.</p></div>
      <div class="card reveal"><span class="icon">${icons.github}</span><h3>Other Ways to Help</h3><p><a href="/contact/?topic=Bug%20Report">Report a bug</a>, <a href="/contact/?topic=Feature%20Request">request a feature</a>, star the project on <a href="${cfg.repo}" rel="noopener">GitHub</a>, or tell a friend who needs a cleaner without the junk.</p></div>
    </div>
  </div>
</section>`;
  return {
    id: "donate",
    path: "/donate/",
    out: "donate/index.html",
    title: "Donate",
    description: "Kodiak Cleaner is free, with no ads or upsells. If it helps you, support Ted Tabaka with a tip on Ko-fi and get a supporter code for the Classic and Glacier color schemes.",
    body,
  };
}
