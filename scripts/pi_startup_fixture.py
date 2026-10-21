"""Synthetic, keyless filesystem/process fixtures shared by S3 qualifiers."""

from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import threading
import time

SENTINEL = "AMBIENT_PROMPT_MUST_NOT_BE_READ"


def digest(path):
    with path.open("rb") as stream:
        result = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024*1024), b""):
            result.update(chunk)
        return result.hexdigest()


def snapshot(root):
    result = {}
    for path in sorted(root.rglob("*")):
        result[str(path.relative_to(root))] = (
            "link:" + os.readlink(path) if path.is_symlink()
            else digest(path) if path.is_file() else "directory")
    return result


def seed(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)


def records(path):
    if not path.exists():
        return []
    # The writer may still be appending the last record.
    return [json.loads(line) for line in path.read_text().splitlines(keepends=True) if line.endswith("\n")]


def wait_until(predicate, child=None, timeout=20):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        if predicate():
            return
        if child is not None and child.poll() is not None:
            raise RuntimeError("child exited before expected observation")
        time.sleep(0.02)
    raise TimeoutError("observation deadline")


def reap(child, timeout=10):
    try:
        child.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        os.killpg(child.pid, signal.SIGKILL)
        child.wait(timeout=5)


def probe_extension(observed_expression):
    return '''import { appendFileSync } from "node:fs";
export default function(pi) {
 const observed = ''' + observed_expression + ''';
 const record = (value) => appendFileSync(observed, JSON.stringify(value)+"\\n");
 record({kind:"load"});
 pi.on("session_start", (event,ctx) => record({kind:"start",reason:event.reason,cwd:ctx.cwd,
   prompt:ctx.getSystemPrompt(),pid:process.pid,home:process.env.HOME,
   agent:process.env.PI_CODING_AGENT_DIR,sessionDir:process.env.PI_CODING_AGENT_SESSION_DIR,
   profile:process.env.FINGERTRAP_PROFILE_ID,sentinel:process.env.FT_ISOLATION_SENTINEL,
   nodeOptions:process.env.NODE_OPTIONS}));
 pi.registerCommand("ft-probe-reload", {description:"Synthetic reload",handler:async (_a,ctx)=>{await ctx.reload();}});
 pi.registerCommand("ft-probe-new", {description:"Synthetic new session",handler:async (_a,ctx)=>{await ctx.newSession();}});
 pi.registerCommand("ft-probe-ready", {description:"Synthetic readiness",handler:async()=>{record({kind:"ready"});}});
 pi.registerCommand("ft-probe-reject", {description:"Synthetic unsupported transition",handler:async (_a,ctx)=>{
   let rejected=false; try { await ctx.switchSession("/synthetic/must-not-read.jsonl"); } catch { rejected=true; }
   record({kind:"rejected",rejected});
 }});
}
'''


@dataclass
class Fixture:
    root: Path
    project: Path
    home: Path
    agent: Path
    literal: Path
    extension: Path
    observed: Path
    tools: Path
    env: dict
    before: dict

    @classmethod
    def create(cls, root, poison=False, linked=False, proxy=None, legacy=True):
        project, home, agent, temp, tools = [root / name for name in ("project", "home", "agent", "tmp", "tools")]
        for path in (project, home, agent, temp, tools):
            path.mkdir(parents=True)
        tracked = [project, home]
        if linked:
            target = root / "linked-pi"
            target.mkdir()
            (project / ".pi").symlink_to(target, target_is_directory=True)
            tracked.append(target)
        for location in (project, root, home, agent):
            for name in ("AGENTS.md", "SYSTEM.md", "APPEND_SYSTEM.md"):
                seed(location / name, SENTINEL)
        if legacy:
            seed(project / ".pi/commands/synthetic.md", "Legacy command stays here.\n")
        seed(project / ".pi/settings.json", '{"packages":["npm:synthetic-must-not-resolve"]}')
        literal = root / "literal-system-prompt.txt"
        literal.write_text(SENTINEL)
        # Pi 0.87.1 startup reads and marks recent crashes as notified. A valid
        # record detects that discovery; malformed JSON would silently pass.
        seed(agent / "crashes.json", json.dumps([{
            "timestamp": datetime.now(timezone.utc).isoformat(), "version": "0.87.1",
            "kind": "fatal_error", "message": SENTINEL, "stack": None,
            "sessionFile": None, "cwd": str(project), "notified": False,
        }]))
        if poison:
            for name in ("settings.json", "auth.json", "models.json", "keybindings.json", "trust.json"):
                seed(agent / name, "{invalid synthetic config")
            if legacy:
                seed(agent / "commands/legacy.md", "Legacy profile command stays here.\n")
            seed(agent / "oauth.json", '{"synthetic":{"access":"synthetic-only"}}')
        observed, extension = root / "observations.jsonl", root / "explicit-probe.mjs"
        extension.write_text(probe_extension(json.dumps(str(observed))))
        ambient = 'import {writeFileSync} from "node:fs"; export default () => writeFileSync('
        ambient += json.dumps(str(root / "ambient-extension-ran")) + ', "unexpected");\n'
        for location in (project / ".pi", agent, home / ".pi/agent", root / ".pi"):
            seed(location / "extensions/ambient.mjs", ambient)
            seed(location / "skills/ambient/SKILL.md", "---\nname: ambient\ndescription: must not load\n---\n"+SENTINEL)
            # A pre-existing prompts directory suppresses the old migration.
            # Keep migration inputs separate from current-layout resource cases.
            if not legacy or location not in (project / ".pi", agent):
                seed(location / "prompts/ambient.md", SENTINEL)
            seed(location / "themes/ambient.json", "{invalid ambient theme")
        command_log = root / "commands.log"
        for name in ("npm", "fd", "rg", "curl", "wget", "git"):
            # Git metadata queries are ordinary project semantics; a package
            # clone/fetch or any installer/helper invocation is a failure.
            allow = 'case "$1" in clone|fetch|pull) ;; *) exit 1 ;; esac\n' if name == "git" else ""
            script = '#!/bin/sh\n' + allow + "printf '%s\\n' '" + name + "' >> '" + str(command_log) + "'\nexit 1\n"
            seed(tools / name, script)
            (tools / name).chmod(0o755)
        env = {
            "HOME": str(home), "USERPROFILE": str(home), "PATH": str(tools) + ":/usr/bin:/bin",
            "XDG_CONFIG_HOME": str(home / "config"), "XDG_DATA_HOME": str(home / "data"),
            "XDG_STATE_HOME": str(home / "state"), "XDG_CACHE_HOME": str(home / "cache"),
            "APPDATA": str(home / "roaming"), "LOCALAPPDATA": str(home / "local"),
            "TMPDIR": str(temp), "TMP": str(temp), "TEMP": str(temp), "LANG": "en_US.UTF-8",
            "PI_CODING_AGENT_DIR": str(agent), "PI_CODING_AGENT_SESSION_DIR": str(agent / "sessions"),
            "PI_OFFLINE": "1", "TERM": "xterm-256color",
        }
        if proxy:
            env.update(HTTP_PROXY=proxy, HTTPS_PROXY=proxy, http_proxy=proxy, https_proxy=proxy)
        before = {str(path): snapshot(path) for path in tracked}
        before[str(agent)] = snapshot(agent)
        return cls(root, project, home, agent, literal, extension, observed, tools, env, before)

    def arguments(self, binary, baseline=False, rpc=True):
        args = [str(binary)]
        if not baseline:
            args += ["--startup-policy", "managed-v1"]
        if rpc:
            args += ["--mode", "rpc"]
        return args + ["--no-extensions", "--no-skills", "--no-prompt-templates", "--no-themes",
            "--no-context-files", "--no-approve", "--offline", "--session-dir", str(self.agent / "sessions"),
            "--system-prompt", str(self.literal), "--extension", str(self.extension)]

    def checks(self, generations=3, prompt=None):
        events = records(self.observed)
        starts = [item for item in events if item["kind"] == "start"]
        actual = snapshot(self.agent)
        checks = {"projectUnchanged": snapshot(self.project) == self.before[str(self.project)],
            "homeUnchanged": snapshot(self.home) == self.before[str(self.home)],
            "profileInputsUnchanged": all(actual.get(key) == value for key, value in self.before[str(self.agent)].items()),
            "noAmbientExtension": not (self.root / "ambient-extension-ran").exists(),
            "noPackageOrHelperCommand": not (self.root / "commands.log").exists(),
            "oneExtensionPerGeneration": sum(item["kind"] == "load" for item in events) == generations,
            "actualCwd": bool(starts) and all(item["cwd"] == str(self.project) for item in starts),
            "literalPrompt": bool(starts) and all((prompt or str(self.literal)) in item["prompt"] and SENTINEL not in item["prompt"] for item in starts)}
        if str(self.root / "linked-pi") in self.before:
            checks["linkedTargetUnchanged"] = snapshot(self.root / "linked-pi") == self.before[str(self.root / "linked-pi")]
        return checks


class NetworkCanary:
    """Local proxy endpoint: record attempted HTTP/proxy activity, serve nothing."""
    def __enter__(self):
        import http.server
        self.attempts = []
        attempts = self.attempts
        class Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                attempts.append(self.requestline)
                self.send_error(502)
            do_CONNECT = do_POST = do_GET
            def log_message(self, *_args):
                pass
        self.server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.url = f"http://127.0.0.1:{self.server.server_port}"
        return self
    def __exit__(self, *_args):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)
