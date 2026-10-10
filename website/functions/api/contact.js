// Contact Us form handler (Cloudflare Pages Function at /api/contact).
//
// Settings, under the Pages project's Settings > Variables and Secrets:
//   TURNSTILE_SITE_KEY    Turnstile widget site key (plain text)
//   TURNSTILE_SECRET_KEY  Turnstile secret key (secret)
//   RESEND_API_KEY        Resend API key used to email the message (secret)
//   CONTACT_TO            optional, where messages go (default www.kodiakcleaner.app/contact)
//   CONTACT_FROM          optional, sender (default "Kodiak Cleaner Website <onboarding@resend.dev>")
//
// Nothing is stored: each message is checked, emailed, and forgotten.

export const TOPICS = ["Bug Report", "Feature Request", "General Feedback", "Security"];
const DEFAULT_TO = "www.kodiakcleaner.app/contact";
const DEFAULT_FROM = "Kodiak Cleaner Website <onboarding@resend.dev>";
const LIMITS = { name: 100, email: 254, message: 5000, minMessage: 10 };
const EMAIL_RE = /^[^\s@<>()",;:]+@[^\s@<>()",;:]+\.[^\s@<>()",;:]{2,}$/;

// Failures we explain are sent with status 200 and ok:false. Cloudflare swaps 5xx replies on a
// custom domain for its own error page, which would hide the explanation from the form.
function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" },
  });
}

// Settings pasted into the dashboard often carry a stray space or line break, which breaks
// the Authorization header. Trim every value before use.
export function settings(env) {
  const get = (k) => (typeof env[k] === "string" ? env[k].trim() : "");
  return {
    siteKey: get("TURNSTILE_SITE_KEY"),
    secret: get("TURNSTILE_SECRET_KEY"),
    resendKey: get("RESEND_API_KEY"),
    to: get("CONTACT_TO") || DEFAULT_TO,
    from: get("CONTACT_FROM") || DEFAULT_FROM,
  };
}

function ready(cfg) {
  return Boolean(cfg.siteKey && cfg.secret && cfg.resendKey);
}

// Turns Resend's refusal into a plain explanation (shown on the form, so Ted can see what to fix).
export function explainMailError(status, body) {
  const detail = String((body && (body.message || body.error)) || "").slice(0, 200);
  if (status === 401 || /api key/i.test(detail)) return "the Resend API key was refused. Check RESEND_API_KEY in Cloudflare.";
  if (/verify a domain|testing emails|own email/i.test(detail)) return "Resend only sends test emails to the address its account was made with. Sign up for Resend with the address in CONTACT_TO, or verify kodiakcleaner.app in Resend.";
  if (/domain is not verified|not verified/i.test(detail)) return "the sender domain in CONTACT_FROM isn't verified in Resend. Remove CONTACT_FROM or finish verifying the domain.";
  return `Resend said: ${detail || "error " + status}`;
}

function withDetail(status, body) {
  const detail = String((body && (body.message || body.error)) || "").slice(0, 200);
  const why = explainMailError(status, body);
  return detail && !why.includes(detail) ? `${why} (Resend: "${detail}")` : why;
}

export function validate(input) {
  const clean = (v) => (typeof v === "string" ? v.replace(/\r\n?/g, "\n").trim() : "");
  const data = {
    topic: clean(input.topic),
    name: clean(input.name).replace(/\s+/g, " "),
    email: clean(input.email),
    message: clean(input.message),
  };
  if (!TOPICS.includes(data.topic)) return { error: "Please choose a topic." };
  if (data.name.length > LIMITS.name) return { error: "Please keep your name under 100 characters." };
  if (!EMAIL_RE.test(data.email) || data.email.length > LIMITS.email) return { error: "Please enter a valid email address so Ted can reply." };
  if (data.message.length < LIMITS.minMessage) return { error: "Please write a little more in your message." };
  if (data.message.length > LIMITS.message) return { error: "Please keep your message under 5,000 characters." };
  return { data };
}

// Returns true (human), false (check failed) or null (Turnstile itself couldn't answer).
async function verifyTurnstile(token, secret, ip) {
  if (!token) return false;
  const form = new FormData();
  form.append("secret", secret);
  form.append("response", token);
  if (ip) form.append("remoteip", ip);
  const res = await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", { method: "POST", body: form });
  const out = res.ok ? await res.json().catch(() => null) : null;
  if (!out) {
    console.error("Turnstile siteverify didn't answer:", res.status);
    return null;
  }
  if (out.success !== true) console.error("Turnstile check failed:", JSON.stringify(out["error-codes"] || out));
  return out.success === true;
}

export function buildEmail(data, cfg) {
  const subjectBits = data.message.split("\n")[0].slice(0, 60);
  return {
    from: cfg.from,
    to: [cfg.to],
    reply_to: data.email,
    subject: `[Kodiak Cleaner] ${data.topic}: ${subjectBits}`,
    text:
      `Topic: ${data.topic}\n` +
      `Name: ${data.name || "(not given)"}\n` +
      `Email: ${data.email}\n` +
      `Sent from the Contact Us page on the Kodiak Cleaner website.\n\n` +
      `${data.message}\n`,
  };
}

export async function onRequestGet({ env }) {
  const cfg = settings(env);
  return json({ ready: ready(cfg), siteKey: cfg.siteKey || null });
}

export async function onRequestPost(context) {
  try {
    return await handlePost(context);
  } catch (err) {
    // Never let a crash turn into Cloudflare's HTML error page; the form needs JSON.
    console.error("Contact form error:", err && err.stack ? err.stack : err);
    return json({ ok: false, code: "server_error", error: `Your message couldn't be sent (${String(err && err.message || err).slice(0, 150)}). Please try again in a few minutes.` });
  }
}

async function handlePost({ request, env }) {
  const cfg = settings(env);
  if (!ready(cfg)) return json({ ok: false, code: "not_set_up", error: "The contact form isn't set up yet. Please try again in a few minutes." });

  let input;
  try {
    const type = request.headers.get("content-type") || "";
    input = type.includes("application/json") ? await request.json() : Object.fromEntries(await request.formData());
  } catch {
    return json({ ok: false, code: "bad_request", error: "That message couldn't be read. Please try again." }, 400);
  }
  if (!input || typeof input !== "object") return json({ ok: false, code: "bad_request", error: "That message couldn't be read. Please try again." }, 400);

  // Honeypot: people never see this field, so anything in it came from a bot. Pretend it worked.
  if (typeof input.website === "string" && input.website.trim() !== "") return json({ ok: true });

  const { data, error } = validate(input);
  if (error) return json({ ok: false, code: "invalid", error }, 400);

  const token = input["cf-turnstile-response"] || input.token;
  const human = await verifyTurnstile(token, cfg.secret, request.headers.get("CF-Connecting-IP"));
  if (human === null) return json({ ok: false, code: "spam_check_unavailable", error: "The spam check couldn't be completed right now. Please try again in a few minutes." });
  if (!human) return json({ ok: false, code: "spam_check", error: "The spam check didn't pass. Please try it again." }, 400);

  const res = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: { authorization: `Bearer ${cfg.resendKey}`, "content-type": "application/json" },
    body: JSON.stringify(buildEmail(data, cfg)),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    console.error("Resend refused the message:", res.status, JSON.stringify(body));
    return json({ ok: false, code: "mail_failed", error: `Your message couldn't be sent: ${withDetail(res.status, body)} Please try again in a few minutes.` });
  }

  return json({ ok: true });
}
