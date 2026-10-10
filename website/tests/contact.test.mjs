// Contact Us function: validation, honeypot, spam check and email sending, with fetch mocked.
import { test, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { onRequestGet, onRequestPost, validate, buildEmail, settings, explainMailError, TOPICS } from "../functions/api/contact.js";

const env = { TURNSTILE_SITE_KEY: "site", TURNSTILE_SECRET_KEY: "secret", RESEND_API_KEY: "re_key" };
const good = { topic: "Bug Report", name: "Pat", email: "pat@example.com", message: "Cleaner froze on Analyze.", "cf-turnstile-response": "tok" };
let calls;
function mockFetch({ human = true, turnstileStatus = 200, mailOk = true, mailStatus = 500, mailBody = {} } = {}) {
  calls = [];
  globalThis.fetch = async (url, init) => {
    calls.push({ url: String(url), init });
    if (String(url).includes("turnstile")) return new Response(turnstileStatus === 200 ? JSON.stringify({ success: human }) : "<html>error</html>", { status: turnstileStatus });
    if (init.headers.authorization !== init.headers.authorization.trim() || /\s/.test(init.headers.authorization.slice(7))) throw new TypeError("Invalid header value");
    return new Response(JSON.stringify(mailBody), { status: mailOk ? 200 : mailStatus });
  };
}
const post = (body, e = env) => onRequestPost({ request: new Request("https://x/api/contact", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(body) }), env: e });
beforeEach(() => mockFetch());

test("topics are A to Z", () => assert.deepEqual(TOPICS, [...TOPICS].sort()));

test("GET reports whether the form is set up", async () => {
  assert.deepEqual(await (await onRequestGet({ env })).json(), { ready: true, siteKey: "site" });
  assert.equal((await (await onRequestGet({ env: {} })).json()).ready, false);
});

test("sends a valid message to Ted with the visitor as reply-to", async () => {
  const res = await post(good);
  assert.equal(res.status, 200);
  assert.equal(calls.length, 2);
  const mail = JSON.parse(calls[1].init.body);
  assert.deepEqual(mail.to, ["www.kodiakcleaner.app/contact"]);
  assert.equal(mail.reply_to, "pat@example.com");
  assert.match(mail.subject, /^\[Kodiak Cleaner\] Bug Report: /);
  assert.match(mail.text, /Cleaner froze on Analyze\./);
});

test("rejects bad input before any network call", async () => {
  for (const bad of [{ ...good, topic: "Spam" }, { ...good, email: "nope" }, { ...good, message: "hi" }, { ...good, message: "x".repeat(5001) }, { ...good, name: "n".repeat(101) }]) {
    assert.equal((await post(bad)).status, 400);
  }
  assert.equal(calls.length, 0);
});

test("honeypot pretends success and sends nothing", async () => {
  const res = await post({ ...good, website: "http://spam" });
  assert.equal((await res.json()).ok, true);
  assert.equal(calls.length, 0);
});

test("failed spam check sends nothing", async () => {
  mockFetch({ human: false });
  assert.equal((await post(good)).status, 400);
  assert.equal(calls.length, 1);
});

test("mail failure and missing settings give friendly errors", async () => {
  mockFetch({ mailOk: false });
  const failed = await post(good);
  assert.equal(failed.status, 200); // never 5xx: Cloudflare would replace the reply with its own error page
  assert.equal((await failed.json()).code, "mail_failed");
  assert.equal((await (await post(good, {})).json()).code, "not_set_up");
});

test("validate trims and normalizes", () => {
  const { data } = validate({ topic: "Security", name: "  A   B ", email: " a@b.co ", message: "  hello there \r\n " });
  assert.deepEqual(data, { topic: "Security", name: "A B", email: "a@b.co", message: "hello there" });
  assert.equal(buildEmail(data, settings({ CONTACT_TO: " x@y.z\n" })).to[0], "x@y.z");
});

test("keys pasted with a stray space or line break still work", async () => {
  const res = await post(good, { TURNSTILE_SITE_KEY: "site\n", TURNSTILE_SECRET_KEY: " secret ", RESEND_API_KEY: "re_key\r\n" });
  assert.equal(res.status, 200);
  assert.equal(JSON.parse(calls[1].init.body).to[0], "www.kodiakcleaner.app/contact");
});

test("a crash still answers with JSON the form can show", async () => {
  globalThis.fetch = async () => { throw new TypeError("network down"); };
  const res = await post(good);
  assert.equal(res.status, 200);
  const out = await res.json();
  assert.equal(out.ok, false);
  assert.match(out.error, /network down/);
});

test("Resend refusals are explained", async () => {
  mockFetch({ mailOk: false, mailStatus: 403, mailBody: { message: "You can only send testing emails to your own email address (someone@else.com)." } });
  const out = await (await post(good)).json();
  assert.match(out.error, /Sign up for Resend with the address in CONTACT_TO/);
  assert.match(out.error, /Resend: "You can only send testing emails/);
  assert.match(explainMailError(401, { message: "API key is invalid" }), /RESEND_API_KEY/);
  assert.match(explainMailError(403, { message: "The kodiakcleaner.app domain is not verified." }), /CONTACT_FROM/);
});

test("a Turnstile outage is reported as such and logged, not blamed on the visitor", async () => {
  mockFetch({ turnstileStatus: 503 });
  const logged = [];
  const orig = console.error;
  console.error = (...a) => logged.push(a.join(" "));
  try {
    const res = await post(good);
    assert.equal(res.status, 200); // never 5xx: Cloudflare would replace the reply with its own error page
    const out = await res.json();
    assert.equal(out.ok, false);
    assert.equal(out.code, "spam_check_unavailable");
    assert.equal(calls.length, 1); // no email sent
    assert.ok(logged.some((l) => /siteverify/.test(l) && /503/.test(l)), logged.join("\n"));
    assert.ok(!logged.some((l) => l.includes("secret")), "the Turnstile secret must not be logged");
  } finally {
    console.error = orig;
  }
});

test("the Turnstile check is done server-side with the secret, the token and the visitor's IP", async () => {
  const res = await onRequestPost({ request: new Request("https://x/api/contact", { method: "POST", headers: { "content-type": "application/json", "CF-Connecting-IP": "203.0.113.7" }, body: JSON.stringify(good) }), env });
  assert.equal((await res.json()).ok, true);
  const sent = Object.fromEntries(calls[0].init.body.entries());
  assert.deepEqual(sent, { secret: "secret", response: "tok", remoteip: "203.0.113.7" });
});
