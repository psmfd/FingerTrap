import { fileURLToPath } from "node:url";
import { verifyPackage } from "./lib/import-manifest.mjs";

const packageRoot = fileURLToPath(new URL("../", import.meta.url));
const result = verifyPackage(packageRoot);
console.log(`Verified ${result.importedFiles} imported files across ${result.slices} reviewed slice(s); ${result.testFiles.length} test suites registered.`);
