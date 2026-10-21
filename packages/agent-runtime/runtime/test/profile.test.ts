import assert from "node:assert/strict";
import { mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { resolveRuntimeProfile } from "../profile.js";

interface ContractFixtures {
  layout: Record<string, string>;
  cases: { name: string; valid: boolean; input: unknown }[];
}
const fixtures = JSON.parse(readFileSync(
  new URL("../../contracts/runtime-profile.v1.cases.json", import.meta.url), "utf8",
)) as ContractFixtures;

for (const fixture of fixtures.cases) {
  test(`profile contract: ${fixture.name}`, () => {
    const appRoot = join(tmpdir(), "fingertrap-profile-contract");
    if (!fixture.valid) {
      assert.throws(() => resolveRuntimeProfile(fixture.input, appRoot));
      return;
    }
    const profile = resolveRuntimeProfile(fixture.input, appRoot);
    assert.equal(profile.root, join(appRoot, "profiles", profile.id));
    for (const [name, relativePath] of Object.entries(fixtures.layout)) {
      assert.equal(profile[name as keyof typeof profile], join(profile.root, ...relativePath.split("/")));
    }
    assert.deepEqual(profile.credentialRefs, (fixture.input as { credentialRefs: unknown }).credentialRefs);
    assert.equal(profile.runtimeId, (fixture.input as { runtimeId: string }).runtimeId);
  });
}

test("independent profiles resolve without creating files or changing process environment", () => {
  const appRoot = mkdtempSync(join(tmpdir(), "fingertrap-profile-contract-"));
  const before = { ...process.env };
  try {
    const alpha = resolveRuntimeProfile(fixtures.cases[0].input, appRoot);
    const beta = resolveRuntimeProfile(fixtures.cases[1].input, appRoot);
    assert.notEqual(alpha.sessionsDir, beta.sessionsDir);
    assert.notEqual(alpha.worktreeManifestsDir, beta.worktreeManifestsDir);
    assert.deepEqual(readdirSync(appRoot), []);
    assert.deepEqual({ ...process.env }, before);
    assert(Object.isFrozen(alpha));
    assert(Object.isFrozen(alpha.credentialRefs));
    assert(Object.isFrozen(alpha.credentialRefs[0]));
  } finally {
    rmSync(appRoot, { recursive: true, force: true });
  }
});

test("no cwd or home fallback for missing, relative or invalid app root", () => {
  const roots = ["", " ", ".", "../app", "relative/app", "/invalid\0root"];
  if (process.platform === "win32") roots.push("\\rooted-without-drive", "/rooted-without-drive", "C:relative");
  for (const root of roots) {
    assert.throws(() => resolveRuntimeProfile(fixtures.cases[0].input, root));
  }
});

test("credential reference count is bounded", () => {
  assert.throws(() => resolveRuntimeProfile({
    version: 1, id: "alpha", runtimeId: "pi-0.85.1",
    credentialRefs: Array.from({ length: 33 }, (_, i) => ({ provider: `provider-${i}`, reference: "fixture" })),
  }, tmpdir()));
});
