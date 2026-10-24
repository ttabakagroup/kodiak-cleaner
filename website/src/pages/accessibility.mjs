import { pageHead } from "../layout.mjs";

// Accessibility statement (Ted, 2026-10-24). A commitment to work toward WCAG 2.1 and the ADA, not a claim of full compliance.
export default function accessibility() {
  const body = `
${pageHead("Accessibility", "Kodiak Cleaner should be easy to use for everyone, including people who use a keyboard, a screen reader or other assistive technology.")}
<section class="tight"><div class="wrap narrow prose">
  <div class="card pad" id="commitment">
    <h2>Our Commitment</h2>
    <p>We are working to make Kodiak Cleaner and this website meet the Web Content Accessibility Guidelines (WCAG) 2.1 at level AA or higher, and to be accessible under the Americans with Disabilities Act (ADA), wherever possible.</p>
    <p>This is an ongoing effort, not a claim that everything already meets those standards. Kodiak Cleaner is built by one person, so some parts may still fall short. When they do, we want to hear about it and fix it.</p>
  </div>
  <div class="card pad mt-xl" id="what-we-do">
    <h2>What We Do</h2>
    <ul>
      <li><strong>Keyboard use.</strong> Pages and app screens are meant to work without a mouse, with a visible focus outline. Every page of this website has a Skip to Content link.</li>
      <li><strong>Screen readers.</strong> Buttons, icons and images get text labels, and pages use real headings, lists and form labels.</li>
      <li><strong>Readable colors.</strong> Light and Dark appearances, with text and controls chosen for good contrast.</li>
      <li><strong>Less motion.</strong> This website tones down its animations when your device asks for reduced motion.</li>
      <li><strong>Plain language.</strong> Clear labels and warnings that say what will happen before anything is deleted.</li>
    </ul>
  </div>
  <div class="card pad mt-xl" id="feedback">
    <h2>Tell Us About a Problem</h2>
    <p>If something in Kodiak Cleaner or on this website is hard to use with your setup, please tell us. Use the <a href="/contact/?topic=Accessibility">Contact Us</a> form, choose Accessibility, and say what you were trying to do, which page or screen it was on, and what device and assistive technology you use if you're happy to share it. Ted reads every message and will try to answer within a few days.</p>
  </div>
</div></section>`;
  return { id: "accessibility", path: "/accessibility/", out: "accessibility/index.html", title: "Accessibility", description: "Kodiak Cleaner is working toward WCAG 2.1 and ADA accessibility for the app and this website. How to report an accessibility problem.", body };
}
