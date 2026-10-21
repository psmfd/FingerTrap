// Preserve the source's type-aware and security lint policy with package-local
// dependency resolution. Original policy: pi_config eslint.config.js at the
// source revision recorded in provenance.json.
import tseslint from "typescript-eslint";
import security from "eslint-plugin-security";

export default tseslint.config(
  { ignores: ["node_modules/**"] },
  ...tseslint.configs.recommendedTypeChecked,
  { ...security.configs.recommended, files: ["extensions/**/*.ts", "runtime/**/*.ts"] },
  {
    files: ["extensions/**/*.ts", "runtime/**/*.ts"],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      // Keep the source policy: computed filesystem paths and typed property
      // access are expected here; process execution and eval remain errors.
      "security/detect-object-injection": "off",
      "security/detect-non-literal-fs-filename": "off",
      "security/detect-eval-with-expression": "error",
      "security/detect-child-process": "error",
      "security/detect-non-literal-require": "error",
      "@typescript-eslint/no-floating-promises": "error",
      "@typescript-eslint/no-misused-promises": "error",
      "@typescript-eslint/await-thenable": "error",
      "@typescript-eslint/no-unused-vars": ["error", {
        argsIgnorePattern: "^_",
        varsIgnorePattern: "^_",
        caughtErrorsIgnorePattern: "^_",
      }],
      "@typescript-eslint/no-explicit-any": "warn",
      "@typescript-eslint/consistent-type-imports": [
        "error", { prefer: "type-imports", fixStyle: "inline-type-imports" },
      ],
      "@typescript-eslint/require-await": "warn",
      "@typescript-eslint/no-unsafe-assignment": "warn",
      "@typescript-eslint/no-unsafe-member-access": "warn",
      "@typescript-eslint/no-unsafe-call": "warn",
      "@typescript-eslint/no-unsafe-return": "warn",
      "@typescript-eslint/no-unsafe-argument": "warn",
    },
  },
  {
    // node:test owns the promises returned by top-level test registrations.
    files: ["extensions/**/*.test.ts", "runtime/**/*.test.ts"],
    rules: { "@typescript-eslint/no-floating-promises": "off" },
  },
);
