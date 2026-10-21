import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { verifyPackage } from "./lib/import-manifest.mjs";

const packageRoot = fileURLToPath(new URL("../", import.meta.url));
const { testFiles } = verifyPackage(packageRoot);
const scratch = mkdtempSync(join(tmpdir(), "fingertrap-runtime-"));

try {
  const testHome = join(scratch, "home");
  const testTemp = join(scratch, "tmp");
  mkdirSync(testHome);
  mkdirSync(testTemp);

  // Pass only OS process-launch essentials. In particular, do not inherit
  // NODE_OPTIONS, provider credentials, Pi settings or meter enablement.
  const env = {};
  for (const key of ["PATH", "Path", "SystemRoot", "SYSTEMROOT", "WINDIR", "ComSpec", "PATHEXT"]) {
    if (process.env[key] !== undefined) env[key] = process.env[key];
  }
  Object.assign(env, {
    HOME: testHome,
    USERPROFILE: testHome,
    TMPDIR: testTemp,
    TMP: testTemp,
    TEMP: testTemp,
    XDG_CONFIG_HOME: join(scratch, "config"),
    XDG_CACHE_HOME: join(scratch, "cache"),
    XDG_DATA_HOME: join(scratch, "data"),
  });

  const result = spawnSync(process.execPath, ["--import", "tsx", "--test", ...testFiles], {
    cwd: packageRoot,
    env,
    stdio: "inherit",
    timeout: 120_000,
  });
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
} finally {
  // Includes temporary directories created by inherited tests which predate
  // per-test cleanup. Nothing outside this newly created tree is removed.
  rmSync(scratch, { recursive: true, force: true });
}
