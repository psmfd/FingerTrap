# Apple Silicon candidate validation

## Candidate status

The `apple-silicon-candidate` workflow builds an Apple Silicon `.app` and DMG,
checks the architecture of the shell, sidecar and PTY library, and drives the
sidecar **inside the app bundle** through a real shell session. Its artifact
includes `build.json`, `SHA256SUMS`, the smoke log, and this checklist.

These are unsigned, unnotarized validation artifacts. Their current configured
app version is recorded in `build.json`; it is not a declaration of the next
release version. Reconcile the app version with the release tag before public
distribution. No tag, GitHub Release or updater feed is produced by this workflow.

Passing CI does not certify the Tauri bridge, Finder activation, keychain,
interactive pi, or shutdown behavior. A missing pi executable is explicitly
reported as a skip by the automated sidecar smoke and remains an open gate.

## Run in Tart on an Apple Silicon workstation

Use a disposable macOS 15 VM for this release's acceptance checks. Tart requires
an Apple Silicon macOS host; the Linux development workspace cannot run it.
GitHub-hosted macOS runners do not support the nested virtualization needed to
host Tart. The workflow builds the app; Tart provides the clean install test.
See the [Tart quick start](https://tart.run/quick-start/) and
[GitHub runner restrictions](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

If Tart is already installed, skip installation. Use an existing clean macOS 15
base image if available; otherwise the initial image download is approximately
25 GB. No runner registration or CI orchestration is required.

On the workstation, extract the downloaded Actions artifact into a dedicated
directory, then run:

```bash
brew install cirruslabs/cli/tart
tart clone ghcr.io/cirruslabs/macos-sequoia-base:latest fingertrap-release-smoke
# Replace the path below with the extracted artifact directory.
tart run --dir=candidate:/absolute/path/to/candidate:ro fingertrap-release-smoke
```

The VM name must be unused; never replace an existing VM to run this checklist.
Record the base image/tag or digest used and the guest's `sw_vers` output with
the results. The stock image's console login is `admin` / `admin`. The shared
artifact directory appears at `/Volumes/My Shared Files/candidate` in the guest.

In the guest, copy that folder locally, verify the checksums, and open the DMG:

```bash
ditto '/Volumes/My Shared Files/candidate' "$HOME/Downloads/FingerTrap-candidate"
cd "$HOME/Downloads/FingerTrap-candidate"
shasum -a 256 -c SHA256SUMS
open ./*.dmg
```

Drag FingerTrap into Applications and launch it from Finder. Record any
Gatekeeper prompt and whether an explicit Open Anyway action was required for
this unsigned candidate; do not disable Gatekeeper globally. That result does
not establish notarized distribution readiness.

## Acceptance checklist

Record pass/fail and observations for each row against the commit in `build.json`.
Do not mark skipped checks as passing.

| Check | Required observation |
| --- | --- |
| Clean Finder launch | App opens with a usable pane without a development shell or toolchains on PATH. |
| Shell transport | Typed commands execute, output appears, terminal resize works, and split/close preserves other panes. |
| Real pi PTY | Install the supported real pi version in the guest; a pi pane starts, renders, accepts input and closes cleanly. Record its version. |
| Native pi RPC | Start a native session; prompt, abort, answer a dialog, and resume a saved session. Record provider/model and outcome without recording credentials. |
| Keychain | Save a test credential, quit/relaunch, confirm availability, clear it, and confirm it stays cleared after another relaunch. |
| Repeat launch | Finder relaunch and `open -n /Applications/FingerTrap.app` activate the existing window and preserve sessions, with one app and one sidecar. Repeat while minimized and hidden. |
| Startup and quit | Rapid repeat launch during startup remains usable. A launch during quit does not cancel shutdown; launch again after exit. |
| Normal cleanup | Quit with live shell, PTY pi and native RPC panes. App, sidecar and their recorded child processes terminate. |
| Forced fallback | In the disposable VM, stop the known sidecar with `kill -STOP <pid>`, then quit. The app must exit within its twenty-second watchdog plus scheduling allowance. Record and clean up surviving descendants; the fallback only kills the direct sidecar. |
| Upgrade/recovery | With the prior app closed, install the candidate and verify existing sessions/settings are readable. Check recovery of a recorded interrupted RPC session without terminating unrelated processes. |

The packaged sidecar can also be checked independently in the guest:

```bash
python3 "$HOME/Downloads/FingerTrap-candidate/smoke-pty.py" \
  --sidecar /Applications/FingerTrap.app/Contents/MacOS/fingertrap-sidecar \
  --require-pi
```

This command requires Python 3 and a real pi executable on the guest's PATH
(or `FINGERTRAP_PI`). It fails if pi is absent. It bypasses the Tauri bridge and
does not replace the GUI checks above. Run the clean Finder-launch check before
installing optional tooling, to avoid hiding a dependency on a developer setup.

## Review and promotion

Attach the result table, build commit, host/guest macOS versions, Tart version,
pi version and any failures to the release review. Keep release promotion on
hold until the required acceptance checks pass. Intel Macs remain unsupported;
Linux and Windows CI provides regression coverage for this release cycle.

The jsdom major upgrade remains deferred in #165. Dependency alerts on the
default branch must be rechecked after the reviewed `dev` changes reach `main`;
merging fixes into `dev` alone does not close default-branch alerts.
