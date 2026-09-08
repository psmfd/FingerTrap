# Bounded shell-sidecar shutdown

- Status: Accepted
- Scope: Reliability release; supplements the shutdown behavior from #171

## Context and problem statement

The shell wrote the shutdown notification synchronously before starting its
fallback timer. A full stdin pipe, or a writer holding the child mutex, could
block native exit indefinitely. The eight-second shell deadline also expired
before the sidecar's RPC disposer budget (ten seconds plus five seconds of
drain), followed by up to two seconds of PTY cleanup.

## Considered options

- Move the timer but retain the shared writer/kill mutex.
- Kill by an independently stored numeric PID.
- Separate stdin ownership from a shared OS child handle and bound admission.

## Decision outcome

Convert Tauri's resolved sidecar command to a standard command, preserving its
path, environment and Windows flags. Use `shared_child` (already a transitive
dependency of the shell plugin) to keep kill and wait independent of stdin and
avoid PID-reuse races. A single dedicated writer serializes complete frames.
Its queue holds at most eight frames, each at most the four-MiB RPC ceiling plus
one KiB of framing. Queued buffers zeroize on drop.

Normal writes acknowledge actual delivery, with a five-second wait on a blocking
worker. A failed or timed-out partial write terminates the transport rather than
retrying on a potentially corrupted stream. Native credential commands also run
on blocking workers so neither keychain prompts nor input delivery block native
window events. Credential preload/override ordering remains serialized.

Native exit starts a twenty-second watchdog before attempting non-blocking
shutdown admission. Once exit begins, ordinary input is rejected. If shutdown
cannot be queued, the watchdog still has an independent child handle. Process
exit observation does not depend on EOF from inherited output pipes. Duplicate
exit requests wait for the same shutdown attempt.

## Known limitations and deferred work

- The forced shell fallback kills the direct sidecar, not arbitrary reparented
  descendants. Normal sidecar cleanup owns process trees; RPC orphan recovery
  runs on the next launch. A frozen sidecar can leave descendants behind. Do not
  describe forced fallback as verified whole-tree cleanup.
- Blocking output-reader threads can retain inherited handles until process
  exit; they do not delay the watchdog or native application exit.
- The shell grace must remain above sidecar cleanup budgets when those change.
- Packaged Apple Silicon normal and saturated-input quit tests remain required;
  passing headless process tests is not packaged-app certification.
