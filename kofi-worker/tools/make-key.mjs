// Makes a supporter key pair for one product. Run once per product, on Ted's own computer:
//   node tools/make-key.mjs <product-id> [folder]
// Writes <folder>/<product-id>-private.pem (secret: goes only into the Worker as that product's keySecret,
// plus a backup) and <folder>/<product-id>-public.pem (goes into the app). Folder defaults to ./keys,
// which git ignores. Refuses to overwrite a key, because a new key would stop every issued code working.
import { generateKeyPairSync } from "node:crypto";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";

const [id, folder = "keys"] = process.argv.slice(2);
if (!id || !/^[a-z][a-z0-9-]*$/.test(id)) {
  console.error("Usage: node tools/make-key.mjs <product-id> [folder]   (product id like kodiak or coclear)");
  process.exit(1);
}
const dir = resolve(folder);
const privatePath = join(dir, `${id}-private.pem`);
const publicPath = join(dir, `${id}-public.pem`);
if (existsSync(privatePath) || existsSync(publicPath)) {
  console.error(`${privatePath} already exists. Making a new key would stop every code issued for ${id} from working.`);
  process.exit(1);
}
const { privateKey, publicKey } = generateKeyPairSync("ec", { namedCurve: "P-256" });
mkdirSync(dir, { recursive: true });
writeFileSync(privatePath, privateKey.export({ type: "pkcs8", format: "pem" }), { mode: 0o600 });
writeFileSync(publicPath, publicKey.export({ type: "spki", format: "pem" }));
console.log(`Private key: ${privatePath}`);
console.log("  Secret. Paste it into the Worker as this product's keySecret, keep a backup, never share or commit it.");
console.log(`Public key:  ${publicPath}`);
console.log("  Safe to share. It goes into the app, which uses it to check codes.");
