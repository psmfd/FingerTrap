import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { isAbsolute, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const packageRoot = fileURLToPath(new URL("../", import.meta.url));
const provenance = JSON.parse(readFileSync(new URL("../provenance.json", import.meta.url), "utf8"));
assert.equal(provenance.schema_version, 1);
assert.match(provenance.source.commit, /^[a-f0-9]{40}$/);
assert.equal(provenance.files.length, 20, "M1a must account for all 20 selected source files");
const seen = new Set();

for (const file of provenance.files) {
  assert(!seen.has(file.path), `Duplicate destination: ${file.path}`);
  seen.add(file.path);
  const destination = resolve(packageRoot, file.path);
  const rel = relative(packageRoot, destination);
  assert(rel && !rel.startsWith("..") && !isAbsolute(rel), `Invalid destination: ${file.path}`);
  assert.match(file.source_blob, /^[a-f0-9]{40}$/);
  assert.match(file.sha256, /^[a-f0-9]{64}$/);
  const bytes = readFileSync(destination);
  assert.equal(createHash("sha256").update(bytes).digest("hex"), file.sha256,
    `${file.path}: update provenance when changing imported content`);
  if (file.transformations.length === 0) {
    const oid = createHash("sha1").update(`blob ${bytes.length}\0`).update(bytes).digest("hex");
    assert.equal(oid, file.source_blob, `${file.path}: claimed unchanged but differs from source blob`);
  }
}
console.log(`Verified ${seen.size} imported files against recorded provenance.`);
