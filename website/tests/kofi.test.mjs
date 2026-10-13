// Ko-fi webhook: token check, which payments earn a code, code format and the emails, with fetch mocked.
import { test, before, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { webcrypto } from "node:crypto";
import { onRequestPost, whyNot, cleanName, codeId, makeCode, sameToken, MIN_PER_5_USD } from "../functions/api/kofi.js";

let privatePem, publicKey;
before(async () => {
  const pair = await webcrypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, true, ["sign", "verify"]);
  const der = Buffer.from(await webcrypto.subtle.exportKey("pkcs8", pair.privateKey)).toString("base64");
  privatePem = `-----BEGIN PRIVATE KEY-----\n${der.match(/.{1,64}/g).join("\n")}\n-----END PRIVATE KEY-----\n`;
  publicKey = pair.publicKey;
});

const env = () => ({ KOFI_VERIFICATION_TOKEN: "tok-123", SUPPORTER_PRIVATE_KEY: privatePem, SUPPORTER_FROM: "Kodiak Cleaner <supporters@kodiakcleaner.app>", RESEND_API_KEY: "re_key", CONTACT_TO: "ted@example.com" });
const payment = (extra = {}) => ({
  verification_token: "tok-123", message_id: "m1", timestamp: "2026-10-11T05:00:00Z", type: "Donation", is_public: true,
  from_name: "Pat Bear", message: "Love it", amount: "5.00", email: "pat@example.com", currency: "USD",
  is_subscription_payment: false, is_first_subscription_payment: false, kofi_transaction_id: "tx-0001", ...extra,
});
const post = (data, e = env()) => {
  const form = new FormData();
  form.append("data", typeof data === "string" ? data : JSON.stringify(data));
  return onRequestPost({ request: new Request("https://x/api/kofi", { method: "POST", body: form }), env: e });
};

let mails;
function mockFetch({ failTo = null } = {}) {
  mails = [];
  globalThis.fetch = async (url, init) => {
    const mail = JSON.parse(init.body);
    mails.push(mail);
    if (failTo && mail.to[0] === failTo) return new Response(JSON.stringify({ message: "domain is not verified" }), { status: 403 });
    return new Response("{}", { status: 200 });
  };
}
beforeEach(() => mockFetch());

const fromB64Url = (s) => Buffer.from(s.replace(/-/g, "+").replace(/_/g, "/"), "base64");
async function check(code) {
  const [p, s] = code.split(".");
  const ok = await webcrypto.subtle.verify({ name: "ECDSA", hash: "SHA-256" }, publicKey, fromB64Url(s), fromB64Url(p));
  return { ok, payload: JSON.parse(fromB64Url(p).toString("utf8")) };
}
const codeIn = (mail) => mail.text.split("\n").find((l) => /^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(l));

test("a qualifying donation emails a valid code to the supporter and a note to Ted", async () => {
  const res = await post(payment());
  assert.equal(res.status, 200);
  assert.equal(mails.length, 2);
  const [toSupporter, toTed] = mails;
  assert.deepEqual(toSupporter.to, ["pat@example.com"]);
  assert.equal(toSupporter.from, "Kodiak Cleaner <supporters@kodiakcleaner.app>");
  assert.equal(toSupporter.reply_to, undefined);
  assert.doesNotMatch(toSupporter.text, /@gmail\.com/i);
  const { ok, payload } = await check(codeIn(toSupporter));
  assert.ok(ok);
  assert.deepEqual(payload, { id: await codeId("tx-0001"), name: "Pat Bear", issued: "2026-10-11" });
  assert.deepEqual(toTed.to, ["ted@example.com"]);
  assert.match(toTed.subject, /code sent/);
  assert.equal(codeIn(toTed), undefined);
});

test("a wrong or missing token is refused and nothing is sent", async () => {
  assert.equal((await post(payment({ verification_token: "nope" }))).status, 403);
  assert.equal((await post(payment({ verification_token: undefined }))).status, 403);
  assert.equal((await post("not json")).status, 400);
  assert.equal(mails.length, 0);
  assert.equal((await post(payment(), {})).status, 503);
});

test("which payments earn a code", () => {
  assert.equal(whyNot(payment(), 5), null);
  assert.equal(whyNot(payment({ type: "Shop Order", amount: "9.99" }), 5), null);
  assert.equal(whyNot(payment({ type: "Subscription", is_first_subscription_payment: true }), 5), null);
  assert.match(whyNot(payment({ type: "Subscription", is_first_subscription_payment: false }), 5), /repeat/);
  assert.match(whyNot(payment({ type: "Commission" }), 5), /isn't a donation/);
  assert.match(whyNot(payment({ amount: "3.00" }), 5), /below/);
  assert.equal(whyNot(payment({ amount: "4.50", currency: "EUR" }), 5), null);
  assert.match(whyNot(payment({ amount: "10", currency: "USD" }), 20), /below the 20 USD/);
  assert.match(whyNot(payment({ currency: "XYZ" }), 5), /isn't on the list/);
  assert.deepEqual(Object.keys(MIN_PER_5_USD), Object.keys(MIN_PER_5_USD).sort());
});

test("a small payment sends Ted a note and no code", async () => {
  await post(payment({ amount: "3.00" }));
  assert.equal(mails.length, 1);
  assert.deepEqual(mails[0].to, ["ted@example.com"]);
  assert.match(mails[0].text, /No supporter code was sent: 3 USD is below the 5 USD minimum/);
});

test("when the supporter email fails, Ted gets the code to send by hand", async () => {
  mockFetch({ failTo: "pat@example.com" });
  const res = await post(payment());
  assert.equal(res.status, 200);
  assert.equal(mails.length, 2);
  assert.match(mails[1].subject, /send the code by hand/);
  assert.ok((await check(codeIn(mails[1]))).ok);
});

test("no email address from Ko-fi also goes to Ted with the code", async () => {
  await post(payment({ email: "" }));
  assert.equal(mails.length, 1);
  assert.match(mails[0].text, /didn't include an email address/);
  assert.ok(codeIn(mails[0]));
});

test("a missing private key tells Ted instead of failing the webhook", async () => {
  const e = env();
  delete e.SUPPORTER_PRIVATE_KEY;
  assert.equal((await post(payment(), e)).status, 200);
  assert.equal(mails.length, 1);
  assert.match(mails[0].subject, /needs a code by hand/);
});

test("the same Ko-fi transaction always gets the same code ID", async () => {
  assert.equal(await codeId("tx-0001"), await codeId("tx-0001"));
  assert.notEqual(await codeId("tx-0001"), await codeId("tx-0002"));
  assert.match(await codeId("tx-0001"), /^[0-9a-f]{12}$/);
});

test("names are tidied and capped", async () => {
  assert.equal(cleanName("  Pat \n  Bear\u0007 "), "Pat Bear");
  assert.equal(cleanName(""), "Supporter");
  assert.equal(cleanName("x".repeat(100)).length, 60);
  const { ok, payload } = await check(await makeCode({ id: "abc", name: "Zoë 🐻", issued: "2026-10-11" }, privatePem));
  assert.ok(ok);
  assert.equal(payload.name, "Zoë 🐻");
});

test("token compare", () => {
  assert.ok(sameToken("abc", "abc"));
  assert.ok(!sameToken("abc", "abd"));
  assert.ok(!sameToken("", ""));
  assert.ok(!sameToken(undefined, "abc"));
});
