#!/usr/bin/env python3
"""Compare retained real-Pi recordings without changing production goldens."""
from collections import Counter, defaultdict
import difflib
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
def read(path):
    result = []
    for line in path.read_text().splitlines():
        record = json.loads(line)
        if "line" in record:
            wire = record.pop("line")
            try:
                record["payload"] = json.loads(wire)
            except ValueError:
                record["payload"] = {"malformedLine": wire}
        result.append(record)
    return result

def inbound(rows):
    counts = Counter()
    result = {}
    for row in rows:
        if row.get("dir") != "in":
            continue
        value = row["payload"]
        message = value.get("message")
        key = (value.get("type"), value.get("id"), message.get("role") if isinstance(message, dict) else None)
        counts[key] += 1
        result[json.dumps([*key, counts[key]])] = value
    return result

def summary(value):
    text = json.dumps(value, ensure_ascii=False)
    return text if len(text) <= 160 else {"type": type(value).__name__, "length": len(text), "sha256": hashlib.sha256(text.encode()).hexdigest()}

def compare(a, b, path=""):
    if type(a) is not type(b):
        return [{"path": path, "before": summary(a), "after": summary(b)}]
    if isinstance(a, dict):
        result = []
        for key in sorted(a.keys() | b.keys()):
            child = path + "/" + key
            if key not in a:
                result.append({"path": child, "added": summary(b[key])})
            elif key not in b:
                result.append({"path": child, "removed": summary(a[key])})
            else:
                result.extend(compare(a[key], b[key], child))
        return result
    if isinstance(a, list):
        if len(a) != len(b):
            return [{"path": path, "beforeLength": len(a), "afterLength": len(b), "before": summary(a), "after": summary(b)}]
        return [change for index, (left, right) in enumerate(zip(a, b)) for change in compare(left, right, path + "/" + str(index))]
    return [] if a == b else [{"path": path, "before": summary(a), "after": summary(b)}]

records = []
patch = []
for path in sorted((root / "rpc-baseline/recordings").glob("*.jsonl")):
    candidate = root / "rpc-candidate/recordings" / path.name
    a, b = read(path), read(candidate)
    outgoing_a = [r for r in a if r.get("dir") == "out"]
    outgoing_b = [r for r in b if r.get("dir") == "out"]
    assert outgoing_a == outgoing_b, (path.name, "outgoing command drift")
    control_a = [r for r in a if "ctl" in r]
    control_b = [r for r in b if "ctl" in r]
    assert control_a == control_b, (path.name, "process-control drift")
    left, right = inbound(a), inbound(b)
    changes = compare(left, right)
    records.append({"file": path.name, "outboundCommandsIdentical": True, "processControlsIdentical": True,
                    "baselineEventCounts": dict(Counter(r["payload"].get("type") for r in a if r.get("dir") == "in")),
                    "candidateEventCounts": dict(Counter(r["payload"].get("type") for r in b if r.get("dir") == "in")),
                    "changes": changes})
    patch.extend(difflib.unified_diff(path.read_text().splitlines(keepends=True), candidate.read_text().splitlines(keepends=True),
                                    fromfile="0.87.1/" + path.name, tofile="1.3.0/" + path.name))
(root / "protocol-review.json").write_text(json.dumps(records, indent=2) + "\n")
(root / "protocol-drift.patch").write_text("".join(patch))
print(json.dumps([{key: value for key, value in row.items() if key != "changes"} | {"changedPaths": len(row["changes"])} for row in records], indent=2))
