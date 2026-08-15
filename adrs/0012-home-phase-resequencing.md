# 0012 — Re-sequence the milestone plan into Home phases and a parallel native track

- Status: Accepted
- Date: 2026-08-15

## Context and problem statement

`pi_config`'s curated feature plan (`notes/curated-feature-plan.md`, Track 6)
elevated FingerTrap on 2026-08-11 from a long-horizon item to **the Home
interface** — the shell the operator lives in — and re-sequenced this repo
around four phases: FT-0 revive + host, FT-1 chrome, FT-2 structured control
and observability, FT-3 tool host. Each phase carries an explicit gate, and
FT-1's is a hard gate on `pi_config`'s `repo-dash` extension having landed and
been used, because its data-layer and usage lessons de-risk the native panels.

This repo's `docs/milestones.md` still described the original M0–M8 sequence.
Two documents describing the same repo's roadmap, in two repos, is drift — and
the kind that gets discovered when someone plans work against the wrong one.

Three forces shape the resolution:

1. **`pi_config` is the governance home.** The curated plan states the phase
   gates, the credential policy, and the migration map. This repo renders and
   hosts; it does not own policy. So the Home phases are not ours to redefine —
   they arrive as a constraint.
2. **M0 and M1 are done and their acceptance criteria are still accurate.**
   Whatever shape the plan takes, that text is worth preserving rather than
   rewriting.
3. **The Home phases do not cover four existing milestones.** This is the
   substantive problem. Mapping them out:

   | Milestone | Covered by a Home phase? |
   |---|---|
   | M2 local terminal panes | yes — split across FT-0 and FT-1 |
   | M5 status providers | yes — FT-1 (already names Octokit and ADO clients) |
   | M6 command palette and keymap | yes — FT-1 |
   | **M3 SSH terminal** | **no** |
   | **M4 SFTP tree** | **no** |
   | **M7 settings and persistence** | **no** |
   | **M8 packaging** | **no** |

   The curated plan describes FingerTrap *as the pi Home* and is silent on
   general terminal features, because that is not what it is planning. Adopting
   the four phases as the whole roadmap would therefore delete M3, M4, M7 and
   M8 by omission — including M7, which [#41](https://github.com/psmfd/FingerTrap/issues/41)
   already depends on, and M8, which is what makes the app installable.

## Considered options

- **A — Adopt FT-0…FT-3 as the entire roadmap.** Simplest and matches the
  curated plan exactly. Silently drops M3/M4/M7/M8.
- **B — Adopt the Home phases and fold M7/M8 into them as supporting work,
  explicitly deferring M3/M4 with a revisit trigger.** Leaner. Narrows
  FingerTrap to "the pi shell" and makes settings and packaging subordinate to
  Home phases they do not naturally belong to.
- **C — Adopt the Home phases and drop M3/M4 outright as out of scope.**
  Smallest roadmap. Forecloses the general-terminal direction, which was an
  original reason the app exists.
- **D — Two parallel tracks: Home (FT-0…FT-3) and Native (N-1…N-4).** Nothing
  is dropped; the tracks interleave by capacity.

## Decision outcome

Chosen option: **D — two parallel tracks**.

The Home track carries the curated plan's phases verbatim, gates included,
because those are `pi_config`'s to define. The Native track carries what the
Home framing does not reach:

```text
HOME  (drives pi integration)      NATIVE (FingerTrap's own features)
  FT-0  revive + host                N-1  settings + persistence  (was M7)
  FT-1  chrome                       N-2  packaging               (was M8)
  FT-2  structured control           N-3  SSH terminal            (was M3)
  FT-3  tool host                    N-4  SFTP tree               (was M4)
```

Why not the others. **A** loses work by omission, which is precisely the
failure the curated plan's own "explicit non-moves" section exists to prevent —
it records disposition rather than letting items evaporate, and this plan
should hold itself to the same standard. **B** and **C** both make a *product*
decision (FingerTrap is only the pi shell) as a side effect of a *sequencing*
decision, and they are not the same question. If SSH and SFTP should be
dropped, that deserves its own ADR with its own reasoning, not a silent
consequence of adopting someone else's phase names.

**Native ordering puts N-1 and N-2 ahead of N-3 and N-4** deliberately: #41
already depends on the settings system, and packaging is what makes the app
installable. SSH and SFTP are real features but nothing blocks on them. This
inverts the original M3 → M8 order, which is the point — the original order
reflected a build sequence, not a dependency structure.

**M-numbers are retired but mapped.** `docs/milestones.md` carries a mapping
table, because existing issues and ADRs cite M-numbers (#39 and #41 both do)
and those references must stay resolvable.

**M2 splits across two phases rather than landing in one.** The typed-pane
concept is FT-0's deliverable — it is what "pi as a first-class pane type"
means — while splits, focus management, and pane lifecycle chrome belong to
FT-1. Forcing M2 whole into either phase would have misrepresented one of them.

### Consequences

- Good: nothing is dropped, and every original milestone has a stated
  destination that a reader can check.
- Good: the two roadmaps stop disagreeing. `pi_config`'s curated plan and this
  document now describe the same sequence, with `pi_config` authoritative on
  the Home phases and this repo authoritative on the Native ones.
- Good: the Native track is where FingerTrap keeps its own identity. Without
  it, adopting the Home framing would have quietly converted the project into a
  single-purpose pi host.
- Bad: two tracks are more to hold in mind than one list, and "interleave by
  capacity" is a real scheduling decision deferred to the moment rather than
  resolved here.
- Bad: the tracks are not fully independent in practice even though they are
  sequenced as if they were. FT-1's configurable keybindings and FT-0's
  pi-binary resolution both want N-1's settings system; `docs/milestones.md`
  flags this rather than pretending otherwise, but it will need a real answer
  when FT-1 starts.
- Neutral: N-2 is partially standing already — `semantic-release` runs on
  `main` and has cut releases through v0.3.0 — so that item is narrower than
  the original M8 text suggests.
- Neutral: the plugin host behind [#39](https://github.com/psmfd/FingerTrap/issues/39)
  (ADR-0012, doc-only) has no phase in either track. Left unscheduled
  deliberately rather than assigned somewhere convenient.
