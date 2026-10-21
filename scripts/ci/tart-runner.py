#!/usr/bin/env python3
"""Operate one disposable FingerTrap Actions runner; never expose the host token."""

import argparse
import datetime
import fcntl
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import signal
import socket
import stat
import subprocess
import threading
import time
import uuid

ORG = "psmfd"
REPO = "FingerTrap"
REPO_ID = 1221327956
GROUP = "fingertrap-tart-pilot"
WORKFLOW = (
    "psmfd/FingerTrap/.github/workflows/local-macos.yml"
    "@refs/heads/ci/tart-runner-pilot"
)
BASE = "ft-actions-base-20261020"
LABELS = ["self-hosted", "macOS", "ARM64", "fingertrap-tart"]
SOFTNET = Path("/Library/PrivilegedHelperTools/fingertrap-softnet/softnet")
SOFTNET_SHA256 = "ab333619fc8bd7277837545e49a771baa994c01c3e8c14904ae4cc4c1f37269e"


def command(args, **kwargs):
    return subprocess.run(args, check=True, text=True, timeout=60, **kwargs)


def api(env, method, path, payload=None):
    args = ["gh", "api", "--method", method, path]
    if payload is not None:
        args += ["--input", "-"]
    try:
        result = command(
            args, env=env, input=json.dumps(payload) if payload is not None else None,
            capture_output=True,
        )
    except subprocess.CalledProcessError as error:
        # Do not dump responses or submitted payloads: JIT material is secret.
        raise RuntimeError(f"GitHub {method} {path}: {error.stderr.strip()}") from None
    return json.loads(result.stdout) if result.stdout.strip() else None


def validate_group(group, repositories, workflows=None):
    """Require the server-side boundary; labels/if expressions are insufficient."""
    if (group["name"] != GROUP or group.get("inherited")
            or group["visibility"] != "selected"
            or group["allows_public_repositories"] is not True
            or group["restricted_to_workflows"] is not True
            or set(group["selected_workflows"]) != set(workflows or [WORKFLOW])
            or {r["id"] for r in repositories} != {REPO_ID}):
        raise RuntimeError("Runner group differs from the reviewed pilot policy")


def routing_policy(env, policy=None):
    """Only explicitly enrolled PRs from this repository may use local workers."""
    if policy is None:
        policy_path = Path(__file__).resolve().parents[2] / ".github/tart-policy.json"
        policy = json.loads(policy_path.read_text())
    prs = policy["trustedPullRequests"]
    if (set(policy) != {"trustedPullRequests", "workerRevision"} or not isinstance(prs, list)
            or any(type(number) is not int or number <= 0 for number in prs)
            or len(prs) != len(set(prs))):
        raise RuntimeError("Invalid trusted PR policy")
    workflows = [WORKFLOW]
    for number in prs:
        pr = api(env, "GET", f"repos/{ORG}/{REPO}/pulls/{number}")
        if (pr["head"]["repo"] is None or pr["head"]["repo"]["id"] != REPO_ID
                or pr["base"]["repo"]["id"] != REPO_ID
                or pr["base"]["ref"] not in ("dev", "main")):
            raise RuntimeError(f"PR {number} is not a trusted repository PR")
    revision = policy["workerRevision"]
    if revision is not None:
        if not isinstance(revision, str) or not re.fullmatch("[0-9a-f]{40}", revision):
            raise RuntimeError("workerRevision must pin a full commit SHA")
        workflows.append(f"{ORG}/{REPO}/.github/workflows/macos-worker.yml@{revision}")
    return workflows, prs


def configure_routing(env, group, workflows, prs):
    if len(workflows) != 2:
        raise RuntimeError("Publish and pin the reusable worker before cutover")
    # Only migrate the previously verified pilot boundary. Never edit a group
    # whose repository access or existing workflow policy is unexpected.
    repos = api(env, "GET",
                f"orgs/{ORG}/actions/runner-groups/{group['id']}/repositories")
    variables = api(env, "GET", f"repos/{ORG}/{REPO}/actions/variables?per_page=100")
    values = {v["name"]: v["value"] for v in variables["variables"]}
    if set(group["selected_workflows"]) == {WORKFLOW}:
        validate_group(group, repos["repositories"])
    elif set(group["selected_workflows"]) == set(workflows):
        # Recover only the safe, empty-repository intermediate state left by
        # GitHub's group PATCH. Normal read/check never accepts this state.
        validate_group(group, repos["repositories"] or [{"id": REPO_ID}], workflows)
    else:
        # This admin-controlled variable records the last applied policy.
        # Contents-read workflow tokens cannot modify it. Refuse unexplained
        # server drift before advancing to a newly reviewed worker revision.
        previous = json.loads(values.get("FINGERTRAP_TART_WORKFLOWS", "null"))
        if not isinstance(previous, list) or not previous:
            raise RuntimeError("No recorded prior runner policy; inspect server state")
        validate_group(group, repos["repositories"], previous)
    api(env, "PATCH", f"orgs/{ORG}/actions/runner-groups/{group['id']}", {
        "name": GROUP, "visibility": "selected",
        "allows_public_repositories": True,
        "restricted_to_workflows": True, "selected_workflows": workflows,
    })
    # GitHub may clear selected repository membership when patching the group's
    # visibility. Reapply exactly one repository, then verify via get_group.
    api(env, "PUT", f"orgs/{ORG}/actions/runner-groups/{group['id']}/repositories",
        {"selected_repository_ids": [REPO_ID]})
    for name, value in (("FINGERTRAP_TART_PRS", prs),
                        ("FINGERTRAP_TART_WORKFLOWS", workflows)):
        present = name in values
        path = f"repos/{ORG}/{REPO}/actions/variables"
        api(env, "PATCH" if present else "POST", path + ("/" + name if present else ""),
            {"name": name, "value": json.dumps(value, separators=(",", ":"))})


def get_group(env, create=False, cutover=False):
    groups = api(env, "GET", f"orgs/{ORG}/actions/runner-groups?per_page=100")
    matches = [g for g in groups["runner_groups"] if g["name"] == GROUP]
    if not matches and create:
        api(env, "POST", f"orgs/{ORG}/actions/runner-groups", {
            "name": GROUP, "visibility": "selected",
            "selected_repository_ids": [REPO_ID],
            "allows_public_repositories": True,
            "restricted_to_workflows": True, "selected_workflows": [WORKFLOW],
        })
        return get_group(env)
    if len(matches) != 1:
        raise RuntimeError("Expected exactly one dedicated pilot runner group")
    group = matches[0]
    repositories = api(
        env, "GET",
        f"orgs/{ORG}/actions/runner-groups/{group['id']}/repositories?per_page=100",
    )
    workflows, prs = routing_policy(env)
    if cutover:
        configure_routing(env, group, workflows, prs)
        return get_group(env)
    # The pilot remains valid before the explicit routing cutover.
    if set(group["selected_workflows"]) == {WORKFLOW}:
        validate_group(group, repositories["repositories"])
    else:
        validate_group(group, repositories["repositories"], workflows)
        variable = api(env, "GET",
                       f"repos/{ORG}/{REPO}/actions/variables/FINGERTRAP_TART_PRS")
        if json.loads(variable["value"]) != prs:
            raise RuntimeError("GitHub routing variable differs from the reviewed policy")
        recorded = api(env, "GET",
                       f"repos/{ORG}/{REPO}/actions/variables/FINGERTRAP_TART_WORKFLOWS")
        if set(json.loads(recorded["value"])) != set(workflows):
            raise RuntimeError("Recorded runner policy differs from the pinned worker")
    return group


def guest(vm, script, *, timeout=60, capture_output=True, **kwargs):
    return subprocess.run(
        ["tart", "exec", "-i", vm, "/bin/bash", "-s"],
        input=script, text=True, timeout=timeout, check=True,
        capture_output=capture_output, **kwargs,
    )


def verify_network(vm):
    """A real listening host endpoint must time out; refusal is not evidence."""
    route = guest(vm, "/sbin/route -n get default").stdout
    gateway = str(ipaddress.IPv4Address(next(
        line.split()[1] for line in route.splitlines()
        if line.strip().startswith("gateway:"))))
    if ipaddress.IPv4Address(gateway).is_unspecified:
        raise RuntimeError("Guest gateway must name a specific host interface")
    # Bind only the VM bridge, never the workstation's LAN/VPN interfaces.
    # Binding also proves the discovered gateway belongs to this host.
    with socket.socket() as listener:
        listener.bind((gateway, 0))
        listener.listen()
        listener.settimeout(1)
        port = listener.getsockname()[1]
        stop = threading.Event()

        def serve():
            while not stop.is_set():
                try:
                    conn, _ = listener.accept()
                    with conn:
                        conn.sendall(b"fingertrap-host-canary\n")
                except socket.timeout:
                    continue
                except OSError:
                    return

        thread = threading.Thread(target=serve, daemon=True)
        thread.start()
        try:
            with socket.create_connection((gateway, port), timeout=2) as sock:
                if sock.recv(64) != b"fingertrap-host-canary\n":
                    raise RuntimeError("Host network canary failed")
            result = guest(vm, f"""set -euo pipefail
python3 - <<'PY'
import json, socket, urllib.request
gateway = {gateway!r}
try:
    with socket.create_connection((gateway, {port}), timeout=3):
        raise RuntimeError('Guest reached the host canary')
except socket.timeout:
    pass
with urllib.request.urlopen('https://api.github.com', timeout=20) as response:
    assert response.status == 200
print(json.dumps({{'host_canary_blocked': True, 'github_https': True}}))
PY
""")
            return json.loads(result.stdout)
        finally:
            stop.set()
            thread.join(timeout=2)


def run_one(env, group, state_root):
    info = SOFTNET.lstat()
    parent = SOFTNET.parent.lstat()
    if (not stat.S_ISREG(info.st_mode) or info.st_uid != 0
            or not info.st_mode & stat.S_ISUID or info.st_mode & 0o022
            or not stat.S_ISDIR(parent.st_mode) or parent.st_uid != 0
            or parent.st_mode & 0o022
            or hashlib.sha256(SOFTNET.read_bytes()).hexdigest() != SOFTNET_SHA256):
        raise RuntimeError("Install the reviewed privileged Softnet helper first")
    os.environ["PATH"] = str(SOFTNET.parent) + os.pathsep + os.environ["PATH"]
    state_root.mkdir(parents=True, exist_ok=True, mode=0o700)
    os.chmod(state_root, 0o700)
    # One VM at a time, including across separate controller invocations.
    with (state_root / "controller.lock").open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        execute_one(env, group, state_root)


def execute_one(env, group, state_root):
    run_id = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    vm = f"ft-actions-job-{run_id.lower()}-{uuid.uuid4().hex[:8]}"
    directory = state_root / vm
    directory.mkdir(mode=0o700)
    state = {"vm": vm, "base": BASE, "group_id": group["id"], "runner_id": None}

    def record(phase, **fields):
        state.update(phase=phase, **fields)
        temp = directory / "state.tmp"
        temp.write_text(json.dumps(state, indent=2) + "\n")
        temp.replace(directory / "state.json")
        print(f"{vm}: {phase}", flush=True)

    def interrupted(signum, _frame):
        raise InterruptedError(f"Controller interrupted by signal {signum}")

    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    clone_created = False
    vm_process = None
    runner_process = None
    cleanup_errors = []
    record("creating")
    try:
        # The base name is fixed and the clone name is generated, never supplied
        # by a workflow. Disable Tart's unrelated-image auto-pruning.
        command(["tart", "clone", BASE, vm],
                env=dict(os.environ, TART_NO_AUTO_PRUNE="1"))
        clone_created = True
        # Block every host IPv4 address as well as private/link-local networks.
        addresses = re.findall(r"^\s+inet (\S+)", command(
            ["/sbin/ifconfig"], capture_output=True).stdout, re.MULTILINE)
        blocks = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
                  "169.254.0.0/16", "127.0.0.0/8"]
        blocks += [str(ipaddress.IPv4Address(a)) + "/32" for a in addresses]
        with (directory / "vm.log").open("w") as vm_log:
            vm_process = subprocess.Popen([
                "tart", "run", "--no-graphics", "--no-audio", "--no-clipboard",
                "--net-softnet", "--net-softnet-block=" + ",".join(blocks), vm,
            ], stdout=vm_log, stderr=subprocess.STDOUT)
            record("booting")
            deadline = time.monotonic() + 180
            while time.monotonic() < deadline:
                if vm_process.poll() is not None:
                    raise RuntimeError(f"Isolated VM boot failed; see {directory / 'vm.log'}")
                try:
                    guest(vm, "test -x \"$HOME/fingertrap-actions-runner/run.sh\"\n",
                          timeout=5)
                    break
                except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
                    time.sleep(2)
            else:
                raise TimeoutError("Guest did not boot within three minutes")
            # Private-address blocks also deny the host's DNS proxy. Configure
            # public resolvers inside the disposable guest, never on the host.
            guest(vm, "sudo -n /usr/sbin/networksetup -setdnsservers Ethernet "
                      "1.1.1.1 8.8.8.8\n")
            record("network_verified", network=verify_network(vm))
            guest(vm, """set -euo pipefail
/opt/homebrew/bin/bash -c 'test "${BASH_VERSINFO[0]}" -ge 4'
cd "$HOME/fingertrap-actions-runner"
test ! -e .runner
test ! -e .credentials
test ! -e .credentials_rsaparams
test ! -e "$HOME/actions-runner/.runner"
test ! -e "$HOME/actions-runner/.credentials"
""")
            # Recheck immediately before credentials are minted.
            group = get_group(env)
            jit = api(env, "POST", f"orgs/{ORG}/actions/runners/generate-jitconfig", {
                "name": vm, "runner_group_id": group["id"],
                "labels": LABELS, "work_folder": "_work",
            })
            state["runner_id"] = jit["runner"]["id"]
            record("registered")
            with (directory / "runner.log").open("w") as runner_log:
                runner_process = subprocess.Popen([
                    "tart", "exec", "-i", vm, "/bin/bash", "-c",
                    'set -euo pipefail; cd "$HOME/fingertrap-actions-runner"; '
                    'IFS= read -r jit; exec ./run.sh --jitconfig "$jit"',
                ], stdin=subprocess.PIPE, stdout=runner_log,
                    stderr=subprocess.STDOUT, text=True)
                # Only the JIT credential crosses into the guest, via stdin.
                runner_process.stdin.write(jit["encoded_jit_config"] + "\n")
                runner_process.stdin.close()
                del jit
                record("listening")
                result = runner_process.wait(timeout=70 * 60)
                record("runner_exited", runner_exit_code=result)
                if result != 0:
                    raise RuntimeError("Runner failed; inspect its private diagnostic log")
    finally:
        # Keep cancellation from interrupting cleanup a second time.
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        signal.signal(signal.SIGINT, signal.SIG_IGN)
        if runner_process and runner_process.poll() is None:
            runner_process.terminate()
        if vm_process and vm_process.poll() is None:
            try:
                with (directory / "runner-diag.tar.gz").open("wb") as archive:
                    subprocess.run([
                        "tart", "exec", vm, "/usr/bin/tar", "-czf", "-", "-C",
                        "/Users/admin/fingertrap-actions-runner", "_diag",
                    ], stdout=archive, stderr=subprocess.DEVNULL,
                        timeout=30, check=True)
            except (OSError, subprocess.SubprocessError) as error:
                cleanup_errors.append(f"diagnostic capture: {type(error).__name__}")
        if clone_created:
            try:
                command(["tart", "stop", vm], capture_output=True)
                command(["tart", "delete", vm], capture_output=True)
            except (OSError, subprocess.SubprocessError) as error:
                cleanup_errors.append(f"VM cleanup: {type(error).__name__}")
        if vm_process:
            try:
                vm_process.wait(timeout=30)
            except subprocess.TimeoutExpired:
                cleanup_errors.append("Tart host process did not exit")
        if runner_process:
            try:
                runner_process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                runner_process.kill()
                runner_process.wait(timeout=10)
        if state["runner_id"]:
            path = f"orgs/{ORG}/actions/runners/{state['runner_id']}"
            result = subprocess.run(["gh", "api", "--method", "DELETE", path],
                                    env=env, capture_output=True, text=True, timeout=60)
            if result.returncode and "HTTP 404" not in result.stderr:
                cleanup_errors.append("GitHub runner removal failed")
        record("cleanup_incomplete" if cleanup_errors else "cleaned",
               cleanup_errors=cleanup_errors)
        if cleanup_errors:
            raise RuntimeError(f"Inspect cleanup state at {directory / 'state.json'}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["configure", "cutover", "check", "run", "drain"])
    parser.add_argument("--github-user", default="TheSemicolon")
    parser.add_argument("--state-root", type=Path,
                        default=Path(".artifacts/tart-runner"))
    parser.add_argument("--max-jobs", type=int, default=5)
    args = parser.parse_args()
    os.umask(0o077)
    token = command(["gh", "auth", "token", "--hostname", "github.com",
                     "--user", args.github_user], capture_output=True).stdout.strip()
    env = dict(os.environ, GH_TOKEN=token)
    if not 1 <= args.max_jobs <= 20:
        parser.error("--max-jobs must be between 1 and 20")
    group = get_group(env, create=args.action == "configure",
                      cutover=args.action == "cutover")
    print(f"Verified group {group['id']}: {GROUP}", flush=True)
    if args.action == "run":
        run_one(env, group, args.state_root.resolve())
    elif args.action == "drain":
        for _ in range(args.max_jobs):
            run_one(env, get_group(env), args.state_root.resolve())


if __name__ == "__main__":
    main()
