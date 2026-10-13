// Ko-fi webhook (Cloudflare Pages Function at /api/kofi): turns a Ko-fi payment into a supporter code.
//
// Ko-fi posts a form with one field, "data", holding JSON (verification_token, type, amount, currency,
// from_name, email, kofi_transaction_id, timestamp, ...). When the token matches and the payment is big
// enough, this signs a supporter code with Ted's private key and emails it to the supporter. The code
// is the same "<payload>.<signature>" format the Windows, Linux and Mac apps already check offline.
//
// Settings, under the Pages project's Settings > Variables and Secrets:
//   KOFI_VERIFICATION_TOKEN  the token from Ko-fi > Settings > API > Webhooks (secret)
//   SUPPORTER_PRIVATE_KEY    the whole of keys/supporter-private.pem from make-key (secret)
//   SUPPORTER_FROM           sender on a domain verified in Resend, e.g. "Kodiak Cleaner <supporters@kodiakcleaner.app>"
//   SUPPORTER_MIN            optional, smallest payment in US dollars that unlocks Supporter (default 5)
//   RESEND_API_KEY           shared with the contact form; CONTACT_TO (optional) is where Ted's copy goes
//
// Nothing is stored. Ted gets a short note for every payment, so he has a record and can step in by
// hand when a code couldn't be sent (no email address, an unknown currency, or a Resend refusal).

import { settings as contactSettings } from "./contact.js";

export const DEFAULT_MIN = 5;
// Rough amount in each currency that matches $5, kept low on purpose so nobody falls just short.
// Scaled by SUPPORTER_MIN / 5. Other currencies go to Ted to decide by hand.
export const MIN_PER_5_USD = { AUD: 7, CAD: 6.5, CHF: 4, EUR: 4.5, GBP: 3.5, JPY: 700, NZD: 8, SGD: 6.5, USD: 5 };
const NAME_MAX = 60;

function text(body, status = 200) {
  return new Response(body, { status, headers: { "content-type": "text/plain; charset=utf-8", "cache-control": "no-store" } });
}

export function config(env) {
  const get = (k) => (typeof env[k] === "string" ? env[k].trim() : "");
  const contact = contactSettings(env);
  const min = Number(get("SUPPORTER_MIN"));
  return {
    token: get("KOFI_VERIFICATION_TOKEN"),
    privateKey: get("SUPPORTER_PRIVATE_KEY"),
    from: get("SUPPORTER_FROM") || contact.from,
    resendKey: contact.resendKey,
    notify: contact.to,
    min: Number.isFinite(min) && min > 0 ? min : DEFAULT_MIN,
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

// Same as SupporterCode.Create in the app: base64url JSON payload, ".", base64url ECDSA P-256 / SHA-256
// signature as r||s.
export async function makeCode({ id, name, issued }, privateKeyPem) {
  const key = await crypto.subtle.importKey("pkcs8", pemToDer(privateKeyPem), { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
  const payload = new TextEncoder().encode(JSON.stringify({ id, name, issued }));
  const signature = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, payload);
  return `${toBase64Url(payload)}.${toBase64Url(signature)}`;
}

export function supporterEmail({ name, code }, cfg, to) {
  return {
    from: cfg.from,
    to: [to],
    subject: "Your Kodiak Cleaner Supporter Code",
    text:
      `Hi ${name},\n\n` +
      `Thank you for supporting Kodiak Cleaner. Here is your supporter code:\n\n` +
      `${code}\n\n` +
      `To use it, open Kodiak Cleaner, go to Settings > Supporter, paste the code and click Apply. ` +
      `It works in the Windows, Linux and Mac editions and is checked on your computer only; nothing is sent anywhere.\n\n` +
      `Keep this email so you can enter the code again on a new computer.\n\n` +
      `Questions? Use the contact form at https://www.kodiakcleaner.app/contact/\n\n` +
      `Thanks,\nTed\n`,
  };
}

export function tedNote(payment, outcome, cfg) {
  const who = cleanName(payment.from_name);
  return {
    from: cfg.from,
    to: [cfg.notify],
    subject: `[Kodiak Cleaner] Ko-fi ${payment.type || "payment"} from ${who}: ${outcome.summary}`,
    text:
      `${outcome.detail}\n\n` +
      `Name: ${who}\n` +
      `Email: ${payment.email || "(none)"}\n` +
      `Type: ${payment.type || ""}${payment.tier_name ? ` (${payment.tier_name})` : ""}\n` +
      `Amount: ${payment.amount || ""} ${payment.currency || ""}\n` +
      `Ko-fi transaction: ${payment.kofi_transaction_id || ""}\n` +
      (outcome.id ? `Code ID: ${outcome.id} (add it to keys/withdrawn-codes.txt if this payment is refunded)\n` : "") +
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

async function readPayment(request) {
  const type = request.headers.get("content-type") || "";
  let raw;
  if (type.includes("application/json")) raw = (await request.json())?.data;
  else raw = (await request.formData()).get("data");
  const payment = typeof raw === "string" ? JSON.parse(raw) : raw;
  return payment && typeof payment === "object" ? payment : null;
}

export async function onRequestPost({ request, env }) {
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
    if (cfg.resendKey) await send(tedNote(payment, { summary: "needs a code by hand", detail: `Something went wrong: ${String(err && err.message || err).slice(0, 200)}` }, cfg), cfg).catch(() => {});
  }
  return text("OK");
}

async function handle(payment, cfg) {
  const reason = whyNot(payment, cfg.min);
  if (reason) {
    if (cfg.resendKey) await send(tedNote(payment, { summary: "no code sent", detail: `No supporter code was sent: ${reason}.` }, cfg), cfg);
    return;
  }
  if (!cfg.privateKey || !cfg.resendKey) throw new Error("SUPPORTER_PRIVATE_KEY or RESEND_API_KEY isn't set in Cloudflare");

  const name = cleanName(payment.from_name);
  const id = await codeId(String(payment.kofi_transaction_id || payment.message_id || crypto.randomUUID()));
  const when = new Date(payment.timestamp || Date.now());
  const issued = (Number.isNaN(when.getTime()) ? new Date() : when).toISOString().slice(0, 10);
  const code = await makeCode({ id, name, issued }, cfg.privateKey);

  const email = String(payment.email || "").trim();
  const failed = email ? await send(supporterEmail({ name, code }, cfg, email), cfg) : "Ko-fi didn't include an email address";
  const outcome = failed
    ? { summary: "send the code by hand", detail: `The code couldn't be emailed to the supporter (${failed}).`, id, code }
    : { summary: "code sent", detail: `A supporter code was emailed to the supporter.`, id };
  await send(tedNote(payment, outcome, cfg), cfg);
}
