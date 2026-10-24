// Ko-fi webhook: token check, which payments earn a code, code format and the emails, with fetch mocked.
import { test, before, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { webcrypto } from "node:crypto";
import { onPost, whyNot, cleanName, codeId, makeCode, sameToken, productFor, PRODUCTS, MIN_PER_5_USD } from "../worker.js";

let privatePem, publicKey;
before(async () => {
  const pair = await webcrypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, true, ["sign", "verify"]);
  const der = Buffer.from(await webcrypto.subtle.exportKey("pkcs8", pair.privateKey)).toString("base64");
  privatePem = `-----BEGIN PRIVATE KEY-----\n${der.match(/.{1,64}/g).join("\n")}\n-----END PRIVATE KEY-----\n`;
  publicKey = pair.publicKey;
});

const env = () => ({ KOFI_VERIFICATION_TOKEN: "tok-123", KODIAK_PRIVATE_KEY: privatePem, RESEND_API_KEY: "re_key", NOTIFY_TO: "ted@example.com", CONTACT_TO: "ted@example.com" });
const payment = (extra = {}) => ({
  verification_token: "tok-123", message_id: "m1", timestamp: "2026-10-11T05:00:00Z", type: "Donation", is_public: true,
  from_name: "Pat Bear", message: "Love it", amount: "10.00", email: "pat@example.com", currency: "USD",
  is_subscription_payment: false, is_first_subscription_payment: false, kofi_transaction_id: "tx-0001", ...extra,
});
const post = (data, e = env()) => {
  const form = new FormData();
  form.append("data", typeof data === "string" ? data : JSON.stringify(data));
  return onPost(new Request("https://kofi.tedtabaka.com/", { method: "POST", body: form }), e);
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
  assert.match(mails[0].text, /No supporter code was sent: 3 USD is below the 10 USD minimum/);
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
  delete e.KODIAK_PRIVATE_KEY;
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

test("payments are matched to a product: shop item, then tier, then the default", () => {
  const products = [
    { id: "a", default: true, shopCodes: [], tiers: [] },
    { id: "b", shopCodes: ["AbC123"], tiers: ["Bear Club"] },
  ];
  assert.equal(productFor(payment(), products).id, "a");
  assert.equal(productFor(payment({ type: "Shop Order", shop_items: [{ direct_link_code: "abc123", quantity: 1 }] }), products).id, "b");
  assert.equal(productFor(payment({ type: "Subscription", tier_name: "bear club" }), products).id, "b");
  assert.equal(productFor(payment({ shop_items: [{ direct_link_code: "zzz" }] }), products).id, "a");
  assert.equal(productFor(payment(), [{ id: "x", shopCodes: [], tiers: [] }]), null);
});

test("products are A to Z, have one default, and never carry an email in the code", () => {
  const ids = PRODUCTS.map((p) => p.name);
  assert.deepEqual(ids, [...ids].sort());
  assert.equal(PRODUCTS.filter((p) => p.default).length, 1);
  assert.doesNotMatch(JSON.stringify(PRODUCTS), /@gmail|tabakagroup/i);
});

test("nothing is mailed to Ted when NOTIFY_TO isn't set", async () => {
  const e = env();
  delete e.NOTIFY_TO;
  await post(payment({ amount: "1" }), e);
  assert.equal(mails.length, 0);
});

test("every product is complete and nothing is shared between products", () => {
  for (const p of PRODUCTS) {
    for (const k of ["id", "name", "keySecret", "from", "howTo", "contact"]) assert.ok(typeof p[k] === "string" && p[k].length > 0, `${p.id}.${k}`);
    assert.match(p.keySecret, /^[A-Z][A-Z0-9_]*_PRIVATE_KEY$/);
    assert.ok(Array.isArray(p.shopCodes) && Array.isArray(p.tiers));
    assert.match(p.contact, /^https:\/\//);
    if (p.min !== undefined) assert.ok(p.min > 0);
  }
  const all = (f) => PRODUCTS.flatMap(f);
  for (const list of [all((p) => [p.id]), all((p) => [p.keySecret]), all((p) => p.shopCodes.map((c) => c.toLowerCase()))])
    assert.equal(new Set(list).size, list.length);
});

test("a product's own minimum overrides SUPPORTER_MIN", () => {
  assert.equal(whyNot(payment({ amount: "4.99" }), 4.99), null);
  assert.match(whyNot(payment({ amount: "4.99" }), 10), /below/);
});
