# Cache meter

Imported observational recorder for prefix-cache measurements. FingerTrap
does not load this extension yet; see the [package status](../../README.md).
This document describes the retained contract, not installation steps.

When explicitly loaded, the factory registers nothing unless
`CACHE_METER_CONFIG` contains a nonblank measurement label. With a label it
registers `session_start` and `message_end`. Session start resets the turn
counter and sets a status indicator when UI support is available.

Each assistant `message_end` appends a JSONL record and returns `undefined`;
it never replaces the message or makes a network request. Other roles do
not produce records. Model/provider attribution uses the message itself,
with a context-provider fallback. Write errors are swallowed so measurement
cannot interrupt a turn; missing records can therefore mean failed writes.

```json
{
  "ts": "2026-06-11T00:00:00.000Z",
  "turn": 3,
  "model": "example-model",
  "provider": "example-provider",
  "input": 1200,
  "cacheRead": 8000,
  "cacheWrite": 0,
  "output": 300,
  "costTotal": 0.014,
  "config": "baseline"
}
```

`input` is fresh input; cache-hit ratio can be calculated as
`cacheRead / (cacheRead + input)` when the denominator is positive and the
provider supplies meaningful cache usage. Absent/non-finite token counts
become zero, and absent cost is `null`. Zero cache fields alone cannot
distinguish a cache miss from a provider that omits cache reporting.
The recorder reads usage and labels, not message bodies; model/provider/config
labels are stored verbatim without secret scanning.

The retained default path is
`~/.pi/agent/extensions/cache-meter/turns.jsonl`. `state.ts` accepts an explicit
agent directory for tests; the extension entry point still uses the legacy
home path. A shared append-only file and per-process turn counter do not
provide session identity, retention, or bounded native reads. Profile and
child-environment qualification remain
[#201](https://github.com/psmfd/FingerTrap/issues/201).

The old analysis/runbook shell scripts are outside this import. Native
read-only dashboards remain [#157](https://github.com/psmfd/FingerTrap/issues/157).
From the package root, `pnpm test` runs the two inherited record/state test
files; `pnpm typecheck` and `pnpm lint` check the entry point too.
See [provenance.json](../../provenance.json) for the source identity and local
changes. These unit tests do not establish live provider cache accuracy.
