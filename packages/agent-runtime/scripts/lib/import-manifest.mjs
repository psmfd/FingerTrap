import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { lstatSync, readFileSync, readdirSync, realpathSync } from "node:fs";
import { isAbsolute, join, relative, resolve } from "node:path";

const sha1 = /^[a-f0-9]{40}$/u;
const sha256 = /^[a-f0-9]{64}$/u;
const testName = /\.test\.(?:ts|mjs)$/u;

function pathName(value) {
  assert(typeof value === "string" && value.length > 0, "Expected a relative file path");
  assert(!isAbsolute(value) && !value.includes("\\") && !value.includes(":") && !value.includes("\0"),
    `Invalid relative path: ${value}`);
  const parts = value.split("/");
  assert(parts.every(part => part && part !== "." && part !== ".."), `Invalid relative path: ${value}`);
  assert(!parts.includes("node_modules") && !parts.includes(".git"), `Reserved package path: ${value}`);
  return value;
}

function source(value) {
  assert(value && typeof value === "object", "Missing source identity");
  assert.match(value.repo, /^[a-zA-Z0-9_.-]+\/[a-zA-Z0-9_.-]+$/u);
  assert.match(value.commit, sha1);
}

function patches(value = []) {
  assert(Array.isArray(value), "patch_sources must be an array");
  for (const patch of value) {
    source(patch);
    pathName(patch.path);
    assert.match(patch.blob, sha1);
  }
  return value;
}

function packageFiles(root) {
  const files = [];
  function visit(directory, prefix = "") {
    for (const name of readdirSync(directory).sort()) {
      if (!prefix && name === "node_modules") continue;
      const path = prefix ? `${prefix}/${name}` : name;
      pathName(path);
      const stat = lstatSync(join(directory, name));
      assert(!stat.isSymbolicLink(), `Package symlink is not allowed: ${path}`);
      if (stat.isDirectory()) visit(join(directory, name), path);
      else {
        assert(stat.isFile(), `Expected regular package file: ${path}`);
        files.push(path);
      }
    }
  }
  visit(root);
  return files;
}

/**
 * Validate the public package without consulting pi_config or the operator's
 * installation. The reviewed selection and provenance are distinct records:
 * changing a source selection requires changing both explicitly in review.
 */
export function verifyPackage(packageRoot) {
  const root = realpathSync(packageRoot);
  const actualFiles = packageFiles(root);
  const readJson = path => JSON.parse(readFileSync(join(root, path), "utf8"));
  const selection = readJson("import-slices.json");
  const provenance = readJson("provenance.json");
  assert.equal(selection.schema_version, 1);
  assert.equal(provenance.schema_version, 2);
  assert(Array.isArray(selection.slices) && selection.slices.length > 0, "No reviewed import slices");
  assert(Array.isArray(selection.authored_tests), "Missing authored test registry");
  assert(Array.isArray(provenance.files), "Missing imported-file provenance");
  assert(Array.isArray(provenance.authored_files), "Missing authored-file inventory");

  const imported = new Map();
  const allNames = new Set(["provenance.json"]);
  const sliceIds = new Set();
  const selectedTests = new Set();
  function register(path, label) {
    pathName(path);
    // Check case aliases on every host, including hosts with case-sensitive FS.
    const key = path.toLowerCase();
    assert(!allNames.has(key), `Duplicate ${label} path: ${path}`);
    allNames.add(key);
  }
  function registerTest(path) {
    pathName(path);
    assert(testName.test(path), `Unsupported test filename: ${path}`);
    assert(!selectedTests.has(path), `Duplicate test registration: ${path}`);
    selectedTests.add(path);
  }

  for (const slice of selection.slices) {
    assert.match(slice.id, /^[a-zA-Z0-9-]+$/u);
    assert(!sliceIds.has(slice.id.toLowerCase()), `Duplicate slice: ${slice.id}`);
    sliceIds.add(slice.id.toLowerCase());
    source(slice.source);
    assert(Array.isArray(slice.files) && slice.files.length > 0, `Empty slice: ${slice.id}`);
    assert(Array.isArray(slice.tests), `Missing test disposition: ${slice.id}`);
    const slicePaths = new Set();
    for (const file of slice.files) {
      register(file.path, "imported");
      pathName(file.source_path);
      assert.match(file.source_blob, sha1);
      patches(file.patch_sources);
      slicePaths.add(file.path);
      imported.set(file.path, { ...file, slice: slice.id, source: slice.source });
    }
    for (const path of slice.tests) {
      assert(slicePaths.has(path), `Imported test is outside its slice: ${path}`);
      registerTest(path);
    }
  }
  const authored = new Set();
  for (const path of provenance.authored_files) {
    register(path, "authored");
    authored.add(path);
  }
  for (const path of selection.authored_tests) {
    assert(authored.has(path), `Authored test is not inventoried: ${path}`);
    registerTest(path);
  }
  assert(authored.has("import-slices.json"), "Import selection must be an authored file");
  assert.deepEqual(actualFiles.sort(), ["provenance.json", ...imported.keys(), ...authored].sort(),
    "Package has missing or unaccounted files");
  for (const path of actualFiles.filter(path => /\.test\.[^/]+$/u.test(path))) {
    assert(testName.test(path), `Unsupported test filename: ${path}`);
  }
  assert.deepEqual(actualFiles.filter(path => testName.test(path)).sort(), [...selectedTests].sort(),
    "Package has an unregistered or missing test suite");
  assert(selectedTests.size > 0, "No test suites registered");
  assert.equal(provenance.files.length, imported.size, "Imported-file count differs from reviewed selection");

  const verified = new Set();
  for (const file of provenance.files) {
    pathName(file.path);
    const expected = imported.get(file.path);
    assert(expected, `Unselected import: ${file.path}`);
    assert(!verified.has(file.path), `Duplicate provenance: ${file.path}`);
    verified.add(file.path);
    assert.equal(file.slice, expected.slice, `${file.path}: source slice changed`);
    assert.equal(file.source_path, expected.source_path, `${file.path}: source path changed`);
    assert.equal(file.source_blob, expected.source_blob, `${file.path}: source blob changed`);
    assert.deepEqual(file.source, expected.source, `${file.path}: source identity changed`);
    assert.deepEqual(patches(file.patch_sources), patches(expected.patch_sources),
      `${file.path}: patch sources changed`);
    assert.match(file.sha256, sha256);
    assert(Array.isArray(file.transformations)
      && file.transformations.every(note => typeof note === "string" && note.trim()),
    `${file.path}: transformations must be nonempty descriptions`);
    const destination = realpathSync(resolve(root, file.path));
    const rel = relative(root, destination);
    assert(rel && !rel.startsWith("..") && !isAbsolute(rel), `Escaped destination: ${file.path}`);
    const bytes = readFileSync(destination);
    assert.equal(createHash("sha256").update(bytes).digest("hex"), file.sha256,
      `${file.path}: update provenance when changing imported content`);
    if (file.transformations.length === 0) {
      assert.equal(createHash("sha1").update(`blob ${bytes.length}\0`).update(bytes).digest("hex"),
        file.source_blob, `${file.path}: claimed unchanged but differs from source blob`);
      assert.equal(patches(file.patch_sources).length, 0, `${file.path}: patched content needs a transformation note`);
    }
  }
  return {
    importedFiles: verified.size,
    slices: selection.slices.length,
    testFiles: [...selectedTests].sort().map(path => join(root, path)),
  };
}
