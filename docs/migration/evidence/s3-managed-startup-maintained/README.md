# Maintained managed-startup evidence

Candidate Pi `1f37eea4bf71f95ed9a4e62674949de3a9feb8b0`; fixture source
`faa4242f1add364191078d9bc12f4aaf154983e6`. Exact executable/closure identities
are in artifacts.json and sidecar-local.json. No release or activation claim.

The rpc-baseline and rpc-candidate directories retain all twelve normalized
transcripts plus both raw recordings from synthetic, keyless local-provider
sessions. Raw paths, timestamps and UUIDs intentionally remain for audit.
No operator credentials or personal project contents were supplied. The
production-pin drift.patch is a review artifact, not an accepted golden update.
The two maintained-version recording sets match exactly after documented
normalization; the pre-existing 0.85.1 committed goldens remain unchanged.

crash-red.json is the unfixed forward-port reproduction. Mutation reports are
expected failures; runtime-mutations.json records their detection. Host mutation
was restored. Successful direct/host/Tart reports concern the immutable candidate.
The baseline attestation applies only to the released comparison artifact.

Patch accounting counts additions plus deletions against upstream v0.87.1 for
each patch's registered paths, including shared paths once per owner. Limits
and retirement conditions are recorded in ADR-0039.
