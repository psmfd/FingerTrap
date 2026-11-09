# Migration evidence records

Keep observations, tested source/run identities, limitations, acceptance status
and independent review together in the primary evidence record. When a bulky
path-to-hash mapping obstructs review, prefer smaller, separately reviewable JSON
manifests linked by relative paths from that record. This is a reviewability
pattern, not a claim about a fixed GitHub or CodeRabbit size limit. Existing
records do not need mechanical conversion when they remain reviewable.

For a split record:

- Version the summary schema and document the reader migration. Each manifest
  carries a schema version, the same run identity and complete path/hash pairs;
  never split an entry or publish private raw evidence.
- Record each manifest's relative path, entry count and byte SHA-256 in the
  summary. Retain the original record's immutable Git commit, path and byte
  SHA-256 for provenance; preserve all other observation fields exactly.
- Parse JSON with duplicate-key rejection. Resolve references relative to the
  summary, verify hashes/run IDs/counts, reject overlap, then combine mappings.
  Compare the result exactly against the original committed mapping, including
  every path and hash value. Counts alone cannot establish equivalence.
- Verify repository/docs checks and inspect the published PR files API for
  nonempty patches on the summary and every manifest. Request a full bot review
  when authorized and inspect its Review info for selected, skipped and incomplete
  files. Smaller bytes, available patches or a green check alone do not prove
  complete review. Record unresolved coverage explicitly.

The [repeated-launch schema and verification recipe](client-baseline/README.md#repeated-launch-evidence-schema)
are the first use of this pattern. This concerns evidence storage only; it does
not change tested commits, observations, baseline acceptance or activation gates.
