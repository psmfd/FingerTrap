import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";
import { verifyPackage } from "../lib/import-manifest.mjs";

function fileIdentity(path, text, slice = "fixture", source = { repo: "fixture/source", commit: "a".repeat(40) }) {
  const bytes = Buffer.from(text);
  return {
    path, source_path: `agent/${path}`,
    source_blob: createHash("sha1").update(`blob ${bytes.length}\0`).update(bytes).digest("hex"),
    sha256: createHash("sha256").update(bytes).digest("hex"),
    transformations: [], slice, source,
  };
}

function fixture(t) {
  const root = realpathSync(mkdtempSync(join(tmpdir(), "fingertrap-import-fixture-")));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const text = "// inherited regression fixture\n";
  const file = fileIdentity("extensions/probe/test/probe.test.ts", text);
  const selectionFile = ({ path, source_path, source_blob }) => ({ path, source_path, source_blob });
  const selection = {
    schema_version: 1,
    slices: [{ id: "fixture", source: file.source, files: [selectionFile(file)], tests: [file.path] }],
    authored_tests: [],
  };
  const provenance = { schema_version: 2, files: [file], authored_files: ["import-slices.json"] };
  const write = (path, contents) => {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), contents);
  };
  const save = () => {
    write("import-slices.json", JSON.stringify(selection));
    write("provenance.json", JSON.stringify(provenance));
  };
  write(file.path, text);
  save();
  return { root, file, text, selection, provenance, write, save, selectionFile };
}

test("a complete reviewed selection returns exactly its registered suites", t => {
  const f = fixture(t);
  assert.deepEqual(verifyPackage(f.root), {
    importedFiles: 1, slices: 1, testFiles: [join(f.root, f.file.path)],
  });
});

test("changed imported bytes fail their recorded digest", t => {
  const f = fixture(t);
  f.write(f.file.path, "// changed without review\n");
  assert.throws(() => verifyPackage(f.root), /update provenance/u);
});

test("changing a digest alone cannot claim unchanged source", t => {
  const f = fixture(t);
  const text = "// different implementation\n";
  f.write(f.file.path, text);
  f.file.sha256 = createHash("sha256").update(text).digest("hex");
  f.save();
  assert.throws(() => verifyPackage(f.root), /claimed unchanged/u);
});

test("changed source selection cannot hide in the provenance record", t => {
  const f = fixture(t);
  f.file.source = { ...f.file.source, commit: "b".repeat(40) };
  f.save();
  assert.throws(() => verifyPackage(f.root), /source identity changed/u);
});

test("missing imported files fail before executing tests", t => {
  const f = fixture(t);
  rmSync(join(f.root, f.file.path));
  assert.throws(() => verifyPackage(f.root), /missing or unaccounted files/u);
});

test("unaccounted source files fail even when imported hashes are intact", t => {
  const f = fixture(t);
  f.write("extensions/probe/extra.ts", "// unreviewed\n");
  assert.throws(() => verifyPackage(f.root), /missing or unaccounted files/u);
});

test("omitting an inherited suite cannot silently reduce test coverage", t => {
  const f = fixture(t);
  f.selection.slices[0].tests = [];
  f.save();
  assert.throws(() => verifyPackage(f.root), /unregistered or missing test suite/u);
});

test("authored suites must be both inventoried and registered", t => {
  const f = fixture(t);
  f.write("runtime/test/new.test.ts", "// new authored suite\n");
  f.provenance.authored_files.push("runtime/test/new.test.ts");
  f.save();
  assert.throws(() => verifyPackage(f.root), /unregistered or missing test suite/u);
  f.selection.authored_tests.push("runtime/test/new.test.ts");
  f.save();
  assert.equal(verifyPackage(f.root).testFiles.length, 2);
});

test("a reviewed second source preserves original import accounting", t => {
  const f = fixture(t);
  const text = "// second source regression fixture\n";
  const second = fileIdentity("extensions/second/test/second.test.ts", text,
    "second", { repo: "fixture/second", commit: "b".repeat(40) });
  f.selection.slices.push({
    id: "second", source: second.source, files: [f.selectionFile(second)], tests: [second.path],
  });
  f.provenance.files.push(second);
  f.write(second.path, text);
  f.save();
  const verified = verifyPackage(f.root);
  assert.equal(verified.slices, 2);
  assert.equal(verified.importedFiles, 2);
  assert.equal(verified.testFiles.length, 2);
});

test("an unsupported test suffix requires an explicit runner change", t => {
  const f = fixture(t);
  f.write("runtime/test/new.test.js", "// unregistered test format\n");
  f.provenance.authored_files.push("runtime/test/new.test.js");
  f.save();
  assert.throws(() => verifyPackage(f.root), /Unsupported test filename/u);
});

test("path escapes are rejected before filesystem reads", t => {
  for (const path of ["../escape", "/absolute", "C:\\escape", "extensions/../escape", "node_modules/injected.ts"]) {
    const f = fixture(t);
    f.selection.slices[0].files[0].path = path;
    f.save();
    assert.throws(() => verifyPackage(f.root), /relative path|Reserved package path/u);
  }
});

test("case aliases and duplicate provenance cannot disguise duplicate imports", t => {
  const f = fixture(t);
  f.selection.slices[0].files.push({ ...f.selection.slices[0].files[0], path: f.file.path.toUpperCase() });
  f.save();
  assert.throws(() => verifyPackage(f.root), /Duplicate imported path/u);
  f.selection.slices[0].files.pop();
  f.provenance.files.push(f.file);
  f.save();
  assert.throws(() => verifyPackage(f.root), /count differs/u);
});

test("unreviewed patch provenance is rejected", t => {
  const f = fixture(t);
  f.file.patch_sources = [{ repo: "fixture/fixes", commit: "c".repeat(40), path: "fix.ts", blob: "d".repeat(40) }];
  f.save();
  assert.throws(() => verifyPackage(f.root), /patch sources changed/u);
});

test("a reviewed local transformation retains its original source identity", t => {
  const f = fixture(t);
  const text = "// reviewed local transformation\n";
  const originalBlob = f.file.source_blob;
  f.write(f.file.path, text);
  f.file.sha256 = createHash("sha256").update(text).digest("hex");
  f.file.transformations = ["Rewrote the synthetic fixture comment for portability."];
  f.save();
  assert.equal(verifyPackage(f.root).importedFiles, 1);
  assert.equal(JSON.parse(readFileSync(join(f.root, "provenance.json"), "utf8")).files[0].source_blob, originalBlob);
});

test("source symlinks are rejected", t => {
  const f = fixture(t);
  f.write("target.ts", f.text);
  rmSync(join(f.root, f.file.path));
  try {
    symlinkSync(join(f.root, "target.ts"), join(f.root, f.file.path), "file");
  } catch (error) {
    if (error.code === "EPERM" && process.platform === "win32") {
      t.skip("Windows runner lacks symlink privilege");
      return;
    }
    throw error;
  }
  assert.throws(() => verifyPackage(f.root), /Package symlink/u);
});
