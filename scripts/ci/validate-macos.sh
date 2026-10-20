#!/bin/bash
# Runs in a disposable Actions guest. Publishing rewrites its disposable locks.
set -euo pipefail
test "$(uname -s)" = Darwin
test "$(uname -m)" = arm64
test -n "${RUNNER_TEMP:?Run through the local-macos Actions workflow}"
test -n "${GITHUB_RUN_ID:?Run through the local-macos Actions workflow}"
bash scripts/check.sh
pnpm --dir packages/agent-runtime install --frozen-lockfile --ignore-scripts
pnpm --dir packages/agent-runtime check:provenance
pnpm --dir packages/agent-runtime lint
pnpm --dir packages/agent-runtime typecheck
pnpm --dir packages/agent-runtime test
pnpm --dir src-ui install --frozen-lockfile
pnpm --dir src-ui lint
pnpm --dir src-ui typecheck
pnpm --dir src-ui test
pnpm --dir src-ui build
dotnet restore src-sidecar/FingerTrap.slnx --locked-mode
dotnet build src-sidecar -c Release --no-restore
dotnet test src-sidecar -c Release --no-build --no-restore

publish_dir="$RUNNER_TEMP/sidecar-publish"
dotnet publish src-sidecar/src/FingerTrap.Sidecar/FingerTrap.Sidecar.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -p:DebugType=embedded -o "$publish_dir"
mkdir -p src-tauri/binaries
cp "$publish_dir/fingertrap-sidecar" src-tauri/binaries/fingertrap-sidecar-aarch64-apple-darwin
cp "$publish_dir/libporta_pty.dylib" src-tauri/binaries/libporta_pty.dylib
(
  cd src-tauri
  cargo fmt --check
  cargo clippy --all-targets -- -D warnings
  cargo check --locked
  cargo test --locked
  pnpm dlx @tauri-apps/cli@2.11.4 build --ci --no-sign --verbose \
    --target aarch64-apple-darwin --bundles app,dmg -- --locked
)

bundle_dir=src-tauri/target/aarch64-apple-darwin/release/bundle
app_dir="$bundle_dir/macos/FingerTrap.app"
for executable in fingertrap fingertrap-sidecar libporta_pty.dylib; do
  test -f "$app_dir/Contents/MacOS/$executable"
  test "$(lipo -archs "$app_dir/Contents/MacOS/$executable")" = arm64
done
mkdir -p candidate
python3 scripts/smoke-pty.py --sidecar "$app_dir/Contents/MacOS/fingertrap-sidecar" \
  2>&1 | tee candidate/packaged-sidecar-smoke.log
ditto -c -k --sequesterRsrc --keepParent "$app_dir" candidate/FingerTrap-apple-silicon.app.zip
cp "$bundle_dir"/dmg/*.dmg candidate/
cp scripts/smoke-pty.py docs/apple-silicon-release-validation.md candidate/
python3 - <<'PY'
import json, os, pathlib, subprocess
config = json.loads(pathlib.Path("src-tauri/tauri.conf.json").read_text())
record = {
    "commit": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
    "run": f"https://github.com/{os.environ['GITHUB_REPOSITORY']}/actions/runs/{os.environ['GITHUB_RUN_ID']}",
    "runner": os.environ["RUNNER_NAME"],
    "app_version": config["version"],
    "target": "aarch64-apple-darwin",
    "tauri_cli": "2.11.4",
    "distribution": "unsigned candidate; not notarized; not a published release",
    "manual_validation": "pending clean Tart GUI, keychain, real pi and shutdown smoke",
}
pathlib.Path("candidate/build.json").write_text(json.dumps(record, indent=2) + "\n")
PY
(
  cd candidate
  shasum -a 256 ./*.zip ./*.dmg > SHA256SUMS
)
