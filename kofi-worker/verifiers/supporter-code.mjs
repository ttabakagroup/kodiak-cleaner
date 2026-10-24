// Checks a supporter code offline, for JavaScript apps (browser extensions, web apps, Electron, Node 18+).
// Copy this file into the app with the product's public key. Nothing is sent anywhere.
//
//   import { verifySupporterCode } from "./supporter-code.mjs";
//   const { status, license } = await verifySupporterCode(code, PUBLIC_KEY_PEM, ["withdrawn-id"]);
//   // status: "valid" | "empty" | "malformed" | "bad-signature" | "withdrawn" | "unverifiable"
//
// A code is "<payload>.<signature>" in base64url: JSON {"id","name","issued"} signed with ECDSA P-256 /
// SHA-256, signature as r||s (64 bytes).

const fromBase64Url = (s) => {
  const b = s.replace(/-/g, "+").replace(/_/g, "/");
  const bin = atob(b + "=".repeat((4 - (b.length % 4)) % 4));
  return Uint8Array.from(bin, (c) => c.charCodeAt(0));
};

export async function verifySupporterCode(code, publicKeyPem, withdrawnIds = []) {
  code = String(code || "").replace(/\s+/g, "");
  if (!code) return { status: "empty", license: null };
  const parts = code.split(".");
  if (parts.length !== 2) return { status: "malformed", license: null };
  let payload, signature;
  try {
    payload = fromBase64Url(parts[0]);
    signature = fromBase64Url(parts[1]);
  } catch {
    return { status: "malformed", license: null };
  }
  try {
    const der = Uint8Array.from(atob(publicKeyPem.replace(/-----(BEGIN|END) PUBLIC KEY-----/g, "").replace(/\s+/g, "")), (c) => c.charCodeAt(0));
    const key = await crypto.subtle.importKey("spki", der, { name: "ECDSA", namedCurve: "P-256" }, false, ["verify"]);
    if (!(await crypto.subtle.verify({ name: "ECDSA", hash: "SHA-256" }, key, signature, payload))) return { status: "bad-signature", license: null };
  } catch {
    return { status: "unverifiable", license: null };
  }
  let data;
  try {
    data = JSON.parse(new TextDecoder().decode(payload));
  } catch {
    return { status: "malformed", license: null };
  }
  const id = typeof data?.id === "string" ? data.id.trim() : "";
  if (!id) return { status: "malformed", license: null };
  if (withdrawnIds.some((w) => String(w).trim().toLowerCase() === id.toLowerCase())) return { status: "withdrawn", license: null };
  return { status: "valid", license: { id, name: String(data.name || ""), issued: String(data.issued || "") } };
}
