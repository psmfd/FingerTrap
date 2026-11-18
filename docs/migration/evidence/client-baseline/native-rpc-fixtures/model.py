"""Guest-local CF1-06 canned OpenAI-compatible stream."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import sys
import threading
import time

root = Path(sys.argv[1])
log_lock = threading.Lock()
replies = {
    "cf106 stream": ("stream", ("CF106 STREAM FIRST. ", "CF106 STREAM LAST.")),
    "cf106 abort": ("abort", ("CF106 ABORT FIRST.",)),
    "cf106 recover": ("recover", ("CF106 RECOVERED.",)),
    "cf106 dialog": ("dialog", ("CF106 DIALOG CONTINUED.",)),
    "cf106 resume": ("resume", ("CF106 RESUMED.",)),
}


def event(turn, state):
    with log_lock, (root / "model-events.jsonl").open("a") as out:
        print(json.dumps(dict(turn=turn, state=state, epoch=time.time())), file=out)


class Model(BaseHTTPRequestHandler):
    def log_message(self, *_args):
        pass

    def emit(self, data):
        self.wfile.write(data.encode())
        self.wfile.flush()

    def chunk(self, delta, finish=None):
        payload = dict(id="chatcmpl-cf106", object="chat.completion.chunk",
                       created=0, model="canned-model",
                       choices=[dict(index=0, delta=delta, finish_reason=finish)])
        self.emit("data: " + json.dumps(payload) + "\n\n")

    def do_POST(self):
        turn = "unexpected"
        try:
            assert self.path == "/v1/chat/completions"
            size = int(self.headers.get("Content-Length", "0"))
            assert 0 < size < 250_000
            request = json.loads(self.rfile.read(size))
            last = next(m["content"] for m in reversed(request["messages"]) if m["role"] == "user")
            if isinstance(last, list):
                last = "".join(item["text"] for item in last if item["type"] == "text")
            turn, tokens = replies[last]
            assert request["model"] == "canned-model" and request["stream"] is True
        except (ValueError, KeyError, TypeError, StopIteration, AssertionError):
            event(turn, "unexpected-request")
            self.send_error(400)
            return
        event(turn, "request")
        try:
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Connection", "close")
            self.end_headers()
            self.connection.settimeout(5)
            self.chunk({"role": "assistant"})
            self.chunk({"content": tokens[0]})
            event(turn, "first")
            if turn in ("stream", "abort"):
                until = time.monotonic() + 180
                while True:
                    if time.monotonic() >= until:
                        event(turn, "hold-timeout")
                        return
                    if turn == "stream" and (root / "release-stream").exists():
                        break
                    self.emit(": ping\n\n")
                    time.sleep(0.2)
            for token in tokens[1:]:
                self.chunk({"content": token})
                event(turn, "last")
            self.chunk({}, "stop")
            self.emit("data: [DONE]\n\n")
            event(turn, "done")
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            event(turn, "disconnected")
        except TimeoutError:
            event(turn, "socket-timeout")


with ThreadingHTTPServer(("127.0.0.1", 0), Model) as server:
    (root / "port").write_text(str(server.server_port))
    server.serve_forever()
