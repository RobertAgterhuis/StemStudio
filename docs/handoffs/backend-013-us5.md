# Hand-off: backend / 013 US5 - merge (T062-T063)

**Cut in this wave** (US5 is P3 and cuttable; CTO-DECISIONS row). `mode: merge` answers `400` with a field error on `mode`
("Merging a backup is not available in this version."). The validation, staging, consent and reconcile pieces of US2 are
reusable for a `LibraryMerger` (research R-07 rules) in a follow-up. T062/T063 stay unticked; the 011 rule for merges
(`AgentSecrets` of the archive never imported) applies to that follow-up.

## Next owner

CTO (decide on the follow-up); frontend T064 should keep the merge option hidden until then.
