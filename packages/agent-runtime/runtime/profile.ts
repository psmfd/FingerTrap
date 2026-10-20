import { isAbsolute, join, parse, resolve } from "node:path";

export interface CredentialReference {
  readonly provider: string;
  readonly reference: string;
}

export interface RuntimeProfile {
  readonly id: string;
  readonly runtimeId: string;
  readonly credentialRefs: readonly CredentialReference[];
  readonly root: string;
  readonly agentDir: string;
  readonly sessionsDir: string;
  readonly extensionsDir: string;
  readonly worktreeManifestsDir: string;
  readonly cacheDir: string;
  readonly tempDir: string;
  readonly homeDir: string;
}

export function isIdentifier(value: unknown, profileId = false): value is string {
  if (typeof value !== "string" || value.length > (profileId ? 48 : 96)) return false;
  if (!(profileId ? /^[a-z][a-z0-9-]*$/u : /^[a-z0-9][a-z0-9.-]*$/u).test(value)) return false;
  const basename = value.split(".")[0];
  return !/^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])$/u.test(basename) && !value.endsWith(".");
}

function fields(value: unknown, names: readonly string[]): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error("runtime profile fields must be an object");
  }
  if (Object.keys(value).some(key => !names.includes(key))) throw new Error("unknown runtime profile field");
  for (const name of names) {
    if (!Object.hasOwn(value, name)) throw new Error(`missing runtime profile ${name}`);
  }
  return value as Record<string, unknown>;
}

function identifier(value: unknown, name: string, profileId = false): string {
  if (!isIdentifier(value, profileId)) throw new Error(`invalid runtime profile ${name}`);
  return value;
}

/**
 * Pure validation/path projection shared with the sidecar (ADR-0035).
 * Does not create paths, verify realpath containment, load resources or grant
 * credentials. The trusted launcher performs those checks before activation.
 */
export function resolveRuntimeProfile(input: unknown, applicationRoot: string): Readonly<RuntimeProfile> {
  const definition = fields(input, ["version", "id", "runtimeId", "credentialRefs"]);
  if (definition.version !== 1) throw new Error("unsupported runtime profile version");
  const id = identifier(definition.id, "id", true);
  const runtimeId = identifier(definition.runtimeId, "runtimeId");
  if (!Array.isArray(definition.credentialRefs) || definition.credentialRefs.length > 32) {
    throw new Error("credentialRefs must be an array of at most 32 references");
  }
  const providers = new Set<string>();
  const credentialRefs = definition.credentialRefs.map((entry: unknown) => {
    const ref = fields(entry, ["provider", "reference"]);
    const provider = identifier(ref.provider, "provider");
    if (providers.has(provider)) throw new Error("duplicate credential provider");
    providers.add(provider);
    return Object.freeze({ provider, reference: identifier(ref.reference, "reference") });
  });
  if (!applicationRoot.trim() || applicationRoot.includes("\0") || !isAbsolute(applicationRoot)
    || (process.platform === "win32" && parse(applicationRoot).root.length <= 1)) {
    throw new Error("an absolute application-data root is required");
  }
  const root = join(resolve(applicationRoot), "profiles", id);
  const agentDir = join(root, "agent");
  const extensionsDir = join(agentDir, "extensions");
  return Object.freeze({
    id, runtimeId, credentialRefs: Object.freeze(credentialRefs), root, agentDir,
    sessionsDir: join(agentDir, "sessions"),
    extensionsDir,
    worktreeManifestsDir: join(extensionsDir, "worktree", "sessions"),
    cacheDir: join(root, "cache"),
    tempDir: join(root, "tmp"),
    homeDir: join(root, "home"),
  });
}
