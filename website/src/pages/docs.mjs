import { pageHead } from "../layout.mjs";
import { render } from "../markdown.mjs";

export function story(ctx) {
  const body = `
${pageHead("Why It Was Made", "The story behind Kodiak Cleaner, in Ted's own words.")}
<section class="tight"><div class="wrap narrow"><article class="card pad prose story">${render(ctx.content.story)}</article>
<div class="btn-row centered mt-xl"><a class="btn primary lg" href="/download/">Try Kodiak Cleaner</a><a class="btn lg" href="/donate/">Support</a></div></div></section>`;
  return { id: "story", path: "/why-it-was-made/", out: "why-it-was-made/index.html", title: "Why It Was Made", description: "Why Ted Tabaka built Kodiak Cleaner: a simple, private and practical Windows cleaner that respects your time.", body };
}

export function privacy(ctx) {
  const { cfg } = ctx;
  const body = `
${pageHead("Privacy Policy", "Kodiak Cleaner keeps everything on your PC, and this website collects as little as it can.")}
<section class="tight"><div class="wrap narrow prose">
  <div class="card pad" id="website">
    <h2>This Website</h2>
    <p class="muted">Last updated: October 25, 2026</p>
    <ul>
      <li><strong>No analytics, ads or tracking.</strong> This website has no trackers, no advertising and no cookies of its own. It keeps two small notes in your own browser, which never leave it: <code>kc-theme</code> remembers your Light or Dark choice, and <code>kc-intro-seen</code> remembers that you closed the video on the home page, so it doesn't open by itself again. Clearing this site's data in your browser removes both.</li>
      <li><strong>Hosting.</strong> The website is hosted on Cloudflare Pages. Like any web host, Cloudflare handles your IP address to deliver pages and protect the site from attacks.</li>
      <li><strong>Contact Us form.</strong> When you send a message, your topic, name (if given), email address and message are emailed to Ted through the Resend email service, so he can read and answer it. The website doesn't store them. Resend handles the email under its own privacy policy. Ted uses your email address only to reply.</li>
      <li><strong>Spam check.</strong> The Contact Us page uses Cloudflare Turnstile to tell people from bots. It loads only on that page and checks signals from your browser without showing ads or building a profile.</li>
      <li><strong>Downloads.</strong> The Download buttons get the files from this website, through Cloudflare. The Archive and Release Page links go to GitHub, which has its own privacy statement.</li>
      <li><strong>Supporting on Ko-fi.</strong> Ko-fi and its payment provider handle the payment, and this website never sees your card or account details. When you buy a supporter code, Ko-fi sends Kodiak Cleaner's supporter code service (which also runs on Cloudflare) your name, email address, the amount, the Ko-fi transaction number and any message you wrote. The service uses them to make your supporter code, email it to you through Resend, and send Ted a short note with the same details so he can help if the email goes astray or a refund is asked for. The service stores nothing.</li>
      <li><strong>Your information.</strong> Messages and supporter notes are kept only in Ted's email. To get a copy, or to have them deleted, use the <a href="/contact/">Contact Us</a> form and say what you'd like.</li>
    </ul>
    <p>Questions? Use the <a href="/contact/">Contact Us</a> form.</p>
  </div>
  <div class="card pad mt-xl">${render(ctx.content.privacy, { headingShift: 1 })}</div>
</div></section>`;
  return { id: "privacy", path: "/privacy/", out: "privacy/index.html", title: "Privacy Policy", description: "How Kodiak Cleaner and its website handle information. In short, everything stays on your PC.", body };
}

export function terms(ctx) {
  const body = `
${pageHead("Terms of Use", "The terms that come with Kodiak Cleaner, the same ones shown inside the app.")}
<section class="tight"><div class="wrap narrow prose"><div class="card pad">${render(ctx.content.terms, { skipTitle: true })}</div></div></section>`;
  return { id: "terms", path: "/terms/", out: "terms/index.html", title: "Terms of Use", description: "Kodiak Cleaner's Terms of Use: free and open source under the MIT License, beta software used at your own risk.", body };
}

export function notFound() {
  const body = `<section class="lost"><div class="wrap narrow"><img src="/images/logo.svg" alt="" width="160" height="160"><h1>Page Not Found</h1><p class="lead">This page seems to have been cleaned up. Try one of these instead.</p><div class="btn-row"><a class="btn primary" href="/">Home</a><a class="btn" href="/download/">Download</a><a class="btn" href="/features/">Features</a></div></div></section>`;
  return { id: "404", path: "/404.html", out: "404.html", title: "Page Not Found", description: "This page couldn't be found on the Kodiak Cleaner website.", body };
}
