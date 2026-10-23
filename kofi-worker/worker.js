// Ted's Ko-fi webhook for every product (Cloudflare Worker at https://kofi.tedtabaka.com/).
//
// Ko-fi allows one webhook URL per account, so this one Worker serves every product. It works out which
// product a payment is for, signs a supporter code with that product's own private key, and emails it to
// the supporter through Resend. Ted gets a short note for every payment. Nothing is stored.
//
// Which product a payment is for:
//   1. a Ko-fi Shop item whose link code is listed under the product's shopCodes
//   2. a membership tier whose name is listed under the product's tiers
//   3. otherwise the product marked default (plain donations)
//
// Settings (Worker > Settings > Variables and Secrets, all as secrets):
//   KOFI_VERIFICATION_TOKEN   from Ko-fi > Settings > API > Webhooks
//   RESEND_API_KEY            Resend API key (Sending access)
//   NOTIFY_TO                 where Ted's notes go (kept out of the code on purpose)
//   <KEY>                     each product's private .pem, named by its keySecret below (e.g. KODIAK_PRIVATE_KEY)
//   SUPPORTER_MIN             optional, smallest payment in US dollars (default 10, Ted 2026-10-23)
//
// Codes are "<payload>.<signature>" in base64url: JSON {"id","name","issued"} signed with ECDSA P-256 /
// SHA-256 (r||s), the format Kodiak Cleaner's Windows, Linux and Mac apps check offline.

// Add a product here when it starts selling on Ko-fi. Keep the list A to Z.
export const PRODUCTS = [
  {
    id: "kodiak",
    name: "Kodiak Cleaner",
    default: true,
    keySecret: "KODIAK_PRIVATE_KEY",
    from: "Kodiak Cleaner <supporters@kodiakcleaner.app>",
    shopCodes: [],
    tiers: [],
    howTo: "Open Kodiak Cleaner, go to Settings > Supporter, paste the code and click Apply. It works in the Windows, Linux and Mac editions and is checked on your computer only; nothing is sent anywhere.",
    contact: "https://www.kodiakcleaner.app/contact/",
  },
];

export const DEFAULT_MIN = 10;
// Rough amount in each currency that matches $5, kept low on purpose so nobody falls just short.
// Scaled by SUPPORTER_MIN / 5. Other currencies go to Ted to decide by hand.
export const MIN_PER_5_USD = { AUD: 7, CAD: 6.5, CHF: 4, EUR: 4.5, GBP: 3.5, JPY: 700, NZD: 8, SGD: 6.5, USD: 5 };
const NAME_MAX = 60;

function text(body, status = 200) {
  return new Response(body, { status, headers: { "content-type": "text/plain; charset=utf-8", "cache-control": "no-store" } });
}

export function config(env) {
  const get = (k) => (typeof env[k] === "string" ? env[k].trim() : "");
  const min = Number(get("SUPPORTER_MIN"));
  return {
    token: get("KOFI_VERIFICATION_TOKEN"),
    resendKey: get("RESEND_API_KEY"),
    notify: get("NOTIFY_TO"),
    min: Number.isFinite(min) && min > 0 ? min : DEFAULT_MIN,
    key: (product) => get(product.keySecret),
  };
}

// Compares without stopping at the first difference, so timing doesn't hint at the token.
export function sameToken(a, b) {
  if (typeof a !== "string" || typeof b !== "string" || a.length === 0 || a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

export function cleanName(name) {
  const n = String(name || "").replace(/[\u0000-\u001f\u007f]/g, "").replace(/\s+/g, " ").trim().slice(0, NAME_MAX);
  return n || "Supporter";
}

export function productFor(payment, products = PRODUCTS) {
  const codes = (Array.isArray(payment.shop_items) ? payment.shop_items : []).map((i) => String(i && i.direct_link_code || "").toLowerCase());
  const byShop = products.find((p) => p.shopCodes.some((c) => codes.includes(c.toLowerCase())));
  if (byShop) return byShop;
  const tier = String(payment.tier_name || "").toLowerCase();
  const byTier = tier && products.find((p) => p.tiers.some((t) => t.toLowerCase() === tier));
  if (byTier) return byTier;
  return products.find((p) => p.default) || null;
}

// Returns null when the payment earns a code, or the reason it doesn't (for Ted's note).
export function whyNot(payment, min) {
  const type = String(payment.type || "");
  if (!["Donation", "Shop Order", "Subscription"].includes(type)) return `type "${type}" isn't a donation, shop order or membership`;
  if (type === "Subscription" && payment.is_first_subscription_payment === false) return "a repeat membership payment (the first one already sent a code)";
  const currency = String(payment.currency || "").toUpperCase();
  const amount = Number(payment.amount);
  if (!Number.isFinite(amount)) return `the amount "${payment.amount}" couldn't be read`;
  const perFive = MIN_PER_5_USD[currency];
  if (perFive === undefined) return `${currency || "no currency"} isn't on the list of currencies the webhook knows`;
  const needed = Math.round(perFive * (min / 5) * 100) / 100;
  if (amount < needed) return `${amount} ${currency} is below the ${needed} ${currency} minimum`;
  return null;
}

// The code ID comes from Ko-fi's transaction ID, so a repeated webhook gives the same ID (and Ted can
// withdraw a refunded payment's code by that ID).
export async function codeId(transactionId) {
  const hash = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(`kofi:${transactionId}`));
  return [...new Uint8Array(hash)].slice(0, 6).map((b) => b.toString(16).padStart(2, "0")).join("");
}

const toBase64Url = (bytes) => btoa(String.fromCharCode(...new Uint8Array(bytes))).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");

function pemToDer(pem) {
  const body = pem.replace(/-----(BEGIN|END) PRIVATE KEY-----/g, "").replace(/\s+/g, "");
  return Uint8Array.from(atob(body), (c) => c.charCodeAt(0));
}

export async function makeCode({ id, name, issued }, privateKeyPem) {
  const key = await crypto.subtle.importKey("pkcs8", pemToDer(privateKeyPem), { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
  const payload = new TextEncoder().encode(JSON.stringify({ id, name, issued }));
  const signature = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, payload);
  return `${toBase64Url(payload)}.${toBase64Url(signature)}`;
}

export function supporterEmail({ name, code }, product, to) {
  return {
    from: product.from,
    to: [to],
    subject: `Your ${product.name} Supporter Code`,
    text:
      `Hi ${name},\n\n` +
      `Thank you for supporting ${product.name}. Here is your supporter code:\n\n` +
      `${code}\n\n` +
      `${product.howTo}\n\n` +
      `Keep this email so you can enter the code again on a new computer.\n\n` +
      `Questions? Use the contact form at ${product.contact}\n\n` +
      `Thanks,\nTed\n`,
  };
}

export function tedNote(payment, product, outcome) {
  const who = cleanName(payment.from_name);
  const items = (Array.isArray(payment.shop_items) ? payment.shop_items : []).map((i) => i && i.direct_link_code).filter(Boolean);
  return {
    from: product ? product.from : PRODUCTS[0].from,
    subject: `[${product ? product.name : "Ko-fi"}] Ko-fi ${payment.type || "payment"} from ${who}: ${outcome.summary}`,
    text:
      `${outcome.detail}\n\n` +
      `Product: ${product ? product.name : "(none matched)"}\n` +
      `Name: ${who}\n` +
      `Email: ${payment.email || "(none)"}\n` +
      `Type: ${payment.type || ""}${payment.tier_name ? ` (${payment.tier_name})` : ""}${items.length ? ` (shop items: ${items.join(", ")})` : ""}\n` +
      `Amount: ${payment.amount || ""} ${payment.currency || ""}\n` +
      `Ko-fi transaction: ${payment.kofi_transaction_id || ""}\n` +
      (outcome.id ? `Code ID: ${outcome.id} (add it to the app's withdrawn-codes.txt if this payment is refunded)\n` : "") +
      (outcome.code ? `\nCode (send it yourself if the email didn't go out):\n${outcome.code}\n` : "") +
      (payment.message ? `\nMessage: ${String(payment.message).slice(0, 500)}\n` : ""),
  };
}

async function send(mail, cfg) {
  const res = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: { authorization: `Bearer ${cfg.resendKey}`, "content-type": "application/json" },
    body: JSON.stringify(mail),
  });
  if (res.ok) return null;
  const body = await res.json().catch(() => null);
  return `Resend said: ${String((body && (body.message || body.error)) || `error ${res.status}`).slice(0, 200)}`;
}

const sendTed = (payment, product, outcome, cfg) =>
  cfg.resendKey && cfg.notify ? send({ ...tedNote(payment, product, outcome), to: [cfg.notify] }, cfg) : Promise.resolve(null);

async function readPayment(request) {
  const type = request.headers.get("content-type") || "";
  const raw = type.includes("application/json") ? (await request.json())?.data : (await request.formData()).get("data");
  const payment = typeof raw === "string" ? JSON.parse(raw) : raw;
  return payment && typeof payment === "object" ? payment : null;
}

async function handle(payment, cfg) {
  const product = productFor(payment);
  const reason = product ? whyNot(payment, cfg.min) : "no product matched this payment";
  if (reason) return sendTed(payment, product, { summary: "no code sent", detail: `No supporter code was sent: ${reason}.` }, cfg);

  const privateKey = cfg.key(product);
  if (!privateKey || !cfg.resendKey) throw new Error(`${product.keySecret} or RESEND_API_KEY isn't set in Cloudflare`);

  const name = cleanName(payment.from_name);
  const id = await codeId(String(payment.kofi_transaction_id || payment.message_id || crypto.randomUUID()));
  const when = new Date(payment.timestamp || Date.now());
  const issued = (Number.isNaN(when.getTime()) ? new Date() : when).toISOString().slice(0, 10);
  const code = await makeCode({ id, name, issued }, privateKey);

  const email = String(payment.email || "").trim();
  const failed = email ? await send(supporterEmail({ name, code }, product, email), cfg) : "Ko-fi didn't include an email address";
  const outcome = failed
    ? { summary: "send the code by hand", detail: `The code couldn't be emailed to the supporter (${failed}).`, id, code }
    : { summary: "code sent", detail: "A supporter code was emailed to the supporter.", id };
  return sendTed(payment, product, outcome, cfg);
}

export async function onPost(request, env) {
  const cfg = config(env);
  if (!cfg.token) return text("Not set up", 503);

  let payment;
  try {
    payment = await readPayment(request);
  } catch {
    payment = null;
  }
  if (!payment) return text("Bad request", 400);
  if (!sameToken(payment.verification_token, cfg.token)) return text("Forbidden", 403);

  // From here on always answer 200, so Ko-fi doesn't keep retrying; problems go to Ted by email.
  try {
    await handle(payment, cfg);
  } catch (err) {
    console.error("Ko-fi webhook error:", err && err.stack ? err.stack : err);
    const detail = `Something went wrong: ${String((err && err.message) || err).slice(0, 200)}`;
    await sendTed(payment, productFor(payment), { summary: "needs a code by hand", detail }, cfg).catch(() => {});
  }
  return text("OK");
}

export default {
  async fetch(request, env) {
    if (request.method === "POST") return onPost(request, env);
    return text("Ted's Ko-fi webhook. Nothing to see here.");
  },
};
