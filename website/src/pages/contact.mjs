import { icons } from "../icons.mjs";
import { pageHead } from "../layout.mjs";

// Topics, A to Z. They match TOPICS in functions/api/contact.js.
const TOPICS = [
  ["Bug Report", "Something isn't working"],
  ["Feature Request", "An idea to make it better"],
  ["General Feedback", "Thoughts, questions or thanks"],
  ["Security", "A security problem, sent privately"],
];

export default function contact(ctx) {
  const { cfg } = ctx;
  const body = `
${pageHead("Contact Us", "Questions, ideas, bug reports or a quick thank-you. Fill in the form and your message goes straight to Ted.")}
<section class="tight">
  <div class="wrap contact-layout">
    <form class="card" id="contact-form" action="/api/contact" method="post" novalidate>
      <fieldset class="topic">
        <legend>Topic</legend>
        <div class="topic-options">
          ${TOPICS.map(([t, hint], i) => `<label><input type="radio" name="topic" value="${t}"${i === 2 ? " checked" : ""} required><span>${t}<span class="hint">${hint}</span></span></label>`).join("\n          ")}
        </div>
      </fieldset>
      <div class="grid cols-2 form-row">
        <div class="field"><label for="name">Name <span class="hint">(Optional)</span></label><input id="name" name="name" type="text" maxlength="100" autocomplete="name"></div>
        <div class="field"><label for="email">Email</label><input id="email" name="email" type="email" maxlength="254" autocomplete="email" required aria-describedby="email-hint"><span class="hint" id="email-hint">So Ted can reply. It's never shared or added to a list.</span></div>
      </div>
      <div class="field">
        <label for="message">Message</label>
        <textarea id="message" name="message" minlength="10" maxlength="5000" required aria-describedby="message-hint message-count"></textarea>
        <span class="hint" id="message-hint">For a bug, say what you did, what happened and your Kodiak Cleaner version (shown under About).</span>
        <span class="counter" id="message-count" aria-live="polite">0 / 5,000</span>
      </div>
      <div class="hp" aria-hidden="true"><label for="website">Leave This Empty</label><input id="website" name="website" type="text" tabindex="-1" autocomplete="off"></div>
      <div class="turnstile-box" id="turnstile"></div>
      <button class="btn primary lg" type="submit">Send Message</button>
      <div class="form-status" id="form-status" role="status" tabindex="-1"></div>
      <noscript><p class="notice mt">This form needs JavaScript for its spam check. Please turn JavaScript on for this site to send a message.</p></noscript>
    </form>
    <aside class="grid">
      <div class="card"><span class="icon">${icons.shield}</span><h3>Security Problems</h3><p>Please report them privately: choose <strong>Security</strong> above, and don't post details in public until a fix is out.</p></div>
      <div class="card"><span class="icon">${icons.lock}</span><h3>Your Privacy</h3><p>Your message is emailed to Ted and isn't stored on this website. A Cloudflare Turnstile check keeps out spam. <a href="/privacy/#website">More details</a>.</p></div>
      <div class="card"><span class="icon">${icons.heart}</span><h3>Say Thanks</h3><p>If Kodiak Cleaner helps you, an optional thank-you on <a href="${cfg.kofi}" target="_blank" rel="noopener">Ko-fi</a> is always appreciated.</p></div>
    </aside>
  </div>
</section>`;

  return {
    id: "contact",
    path: "/contact/",
    out: "contact/index.html",
    title: "Contact Us",
    description: "Contact Ted Tabaka about Kodiak Cleaner: send feedback, report a bug, request a feature or report a security problem privately.",
    body,
    scripts: `<script src="/js/contact.js?v=${ctx.buildId}" defer></script>`,
  };
}
