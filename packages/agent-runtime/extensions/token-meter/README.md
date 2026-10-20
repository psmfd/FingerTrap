# Token meter

Imported source for per-session usage accounting. FingerTrap does not load
this extension yet; see the [package status](../../README.md). This document
describes the retained behavior for future integration, not installation steps.

## Records and aggregation

An enabled `message_end` handler appends a record for each assistant turn.
It returns `undefined`, never replaces the message, and makes no network calls.
It reads usage and model/provider labels, not message bodies. Labels and the
operator's policy tag are recorded verbatim; they are not secret-scanned.

```json
{
  "ts": "2026-07-04T14:32:01Z",
  "turn": 5,
  "model": "example-model",
  "provider": "example-provider",
  "input": 812,
  "cacheRead": 4200,
  "cacheWrite": 0,
  "output": 340,
  "totalTokens": 5352,
  "costTotal": 0.0193,
  "policy": "baseline"
}
```

`input` is fresh input. `totalTokens` uses the provider's finite total when
present, otherwise the sum of input/cacheRead/cacheWrite/output. Non-finite
or absent token counts become zero. Missing reported cost remains `null` and
renders as `n/a`, not a fabricated zero. A total with partial cost coverage
sums only reported costs. Model and provider come from the same message,
with a context-provider fallback when the message omits its provider.

`record.ts` supplies model, provider, tier and policy aggregators and output
formatters. `tiers.json` is the inherited provider classification; unlisted
providers stay `unmapped`. Missing/empty policy labels aggregate as `untagged`,
including older records without that field. This is measurement metadata;
nothing routes on the tag.

## Enablement and child accounting

Once explicitly loaded by a host, the factory registers its flag, command and
`token_usage` tool. Recording is disabled by default. Existing enablement inputs
are `TOKEN_METER_ENABLED=1`, `--token-meter`, or the user-layer
`extensionSettings.tokenMeter.enabled` setting. Project settings do not enable it.
The `/token-meter` command supports `on`, `off`, `status`, and `policy <tag>`.
The tool reports current log totals, with verbose model/provider/tier tables.

An enabled root exports `TOKEN_METER_SESSION`, `TOKEN_METER_ENABLED`, and
`TOKEN_METER_POLICY_TAG`. Descendants that load the extension and inherit those
values record to the root session's file. A child's turn counter is local to
that process. Changes affect future children; already-spawned children keep
their environment snapshot. The migrated package does not supply child hosting
or prove the future launcher's environment propagation.

With UI support, the footer reads the session log at most every two seconds,
except explicit interactions/resume refreshes. Headless status updates skip the
read. Recording errors are swallowed to avoid interrupting the agent turn,
so an absent record is not proof that no usage occurred.

## Storage and limits

The retained default is
`~/.pi/agent/extensions/token-meter/sessions/<session-id>.jsonl`.
`state.ts` accepts an explicit agent directory for tests. The extension entry
point still reads settings and logs through legacy home paths; managed-profile
isolation remains [#201](https://github.com/psmfd/FingerTrap/issues/201).

Session IDs are sanitized before becoming path components. Reads tolerate
missing files and malformed trailing JSON lines. Logs are append-only; the
current reader loads a whole file without size limits, retention, or rotation.
Cross-process write/ordering guarantees and bounded native reads still require
qualification before product activation. The source's shell aggregation
helpers have not been imported.

## Validation

From the package root, run `pnpm test`, `pnpm typecheck`, and `pnpm lint`.
The four inherited test files cover records/formatting, state/session keys,
tiers, and enablement/environment propagation. File provenance and local
changes are recorded in [provenance.json](../../provenance.json).
