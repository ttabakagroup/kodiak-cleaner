// The key tool, the Worker's signing and the copy-in checkers agree: a code the Worker signs with a key from
// tools/make-key.mjs passes the JavaScript and Python checkers, and a tampered one doesn't.
import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { makeCode } from "../worker.js";
import { verifySupporterCode } from "../verifiers/supporter-code.mjs";

const root = new URL("..", import.meta.url).pathname;
let dir, priv, pub, code;
before(async () => {
  dir = mkdtempSync(join(tmpdir(), "kofi-keys-"));
  execFileSync(process.execPath, [join(root, "tools/make-key.mjs"), "sample-app", dir]);
  priv = readFileSync(join(dir, "sample-app-private.pem"), "utf8");
  pub = readFileSync(join(dir, "sample-app-public.pem"), "utf8");
  code = await makeCode({ id: "abc123def456", name: "Zoë Bear", issued: "2026-10-23" }, priv);
});
after(() => rmSync(dir, { recursive: true, force: true }));

test("make-key refuses to replace an existing key", () => {
  const r = spawnSync(process.execPath, [join(root, "tools/make-key.mjs"), "sample-app", dir]);
  assert.notEqual(r.status, 0);
  assert.match(String(r.stderr), /already exists/);
});

test("JavaScript checker", async () => {
  assert.deepEqual(await verifySupporterCode(code, pub), { status: "valid", license: { id: "abc123def456", name: "Zoë Bear", issued: "2026-10-23" } });
  assert.equal((await verifySupporterCode(code, pub, ["ABC123DEF456"])).status, "withdrawn");
  assert.equal((await verifySupporterCode("x" + code, pub)).status, "bad-signature");
  assert.equal((await verifySupporterCode("nope", pub)).status, "malformed");
  assert.equal((await verifySupporterCode("", pub)).status, "empty");
});

test("Python checker", (t) => {
  const py = spawnSync("python3", ["-c", "import cryptography"]);
  if (py.status !== 0) return t.skip("python3 with cryptography isn't installed");
  const run = (c, withdrawn = "") =>
    execFileSync("python3", ["-I", "-c", `import sys; sys.path.insert(0, sys.argv[1]); import supporter_code as s; print(s.verify(sys.argv[2], open(sys.argv[3]).read(), [w for w in sys.argv[4:] if w])[0])`,
      join(root, "verifiers"), c, join(dir, "sample-app-public.pem"), withdrawn]).toString().trim();
  assert.equal(run(code), "valid");
  assert.equal(run(code, "abc123def456"), "withdrawn");
  assert.equal(run("x" + code), "bad-signature");
});
