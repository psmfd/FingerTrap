# S3-M2: await the golden scenario handshake before EOF

Post-merge dev `9d8b892b1569fa5b75d360a663c54c0063a8f6ed` exposes a race in
the malformed-line replay scenario. Linux run 39683025954, job 119510033054,
passes compilation but fails one test (408 pass, 12 skip). Artifact 12385226610
is downloaded and verified against SHA-256
`c4a428d52dfda2eaaa6a6ea2b49824af1633ee5ba1e18510e99e507c32d09a47`.
Retain its [exact test detail](original-linux-failure.txt).

The golden orders parse response, startup hello, then stdin EOF. The scenario
waited for the response only; shutdown could overtake the stdout pump's hello
record. This is a scenario-driver race. It does not establish a production
client failure, and no production client change is made.

The regression uses existing RecordScenarioHost and a local FakePi script that
emits the expected parse response, delays hello one second, then accepts EOF.
It fails before the change at the hello-before-EOF assertion while all twelve
existing replay cases pass: [red](red.txt), [detail](red-detail.txt).

The scenario now awaits the client's existing readiness gate before shutdown.
Its test-harness HelloGrace uses the existing 60-second event budget, so the
default 500ms legacy fallback cannot falsely resolve this deliberately delayed
handshake. Spawn arguments, production defaults and golden bytes are unchanged.
No sleeps or sorting are added to the scenario, and no mismatch is normalized
away. The one-second delay belongs only to the regression's child fixture.

All 13 [focused tests](green.txt) pass; the [full suite](full-suite.txt) passes
410 tests with 12 opt-in skips. The golden-data diff is empty. Protected CI and
merge identities are in [#213](https://github.com/psmfd/FingerTrap/pull/213);
this record attributes these local
results to the reviewed patch rather than inventing a self-referential hash.
