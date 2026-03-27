# CDC Fallback Timestamp Hardening Report

## What was verified
- `PortfolioSnapshotCaptureOrchestrator.BuildEmptyCaptureSourceAsync` first selects the latest dated sprint by `EndDateUtc` descending and `Id` descending.
- If no dated sprint exists, the fallback remains `DateTime.UnixEpoch` with source `"Empty portfolio"`.
- Repeated empty-owner capture is deterministic because the logical uniqueness key remains `(ProductId, TimestampUtc, Source)`.
- Persisted snapshot selection already orders by `TimestampUtc` and then `SnapshotId`, so equal fallback timestamps remain stable.
- Cross-product reuse of `UnixEpoch` does not create correctness drift because reads are scoped by the requested product set and grouping is already explicit by timestamp and source.

## What was missing
- There was no direct test for the latest-sprint fallback path when an empty owner has no sources but dated sprints exist.
- There was no test that exercises deterministic selection ordering specifically when a fallback-derived timestamp is shared by multiple snapshot groups.
- UnixEpoch fallback behavior was already covered functionally, but not under the exact requested test name.

## Tests added
- `CaptureLatestAsync_EmptyOwner_UsesLatestSprintAsFallbackTimestamp`
  - verifies latest-sprint fallback selection, persisted timestamp, source name, and deterministic repeated capture
- `CaptureLatestAsync_EmptyOwner_NoSprints_UsesUnixEpochFallback`
  - verifies UnixEpoch fallback, `"Empty portfolio"` source, persisted empty snapshot, and idempotency
- `SnapshotSelection_WithFallbackTimestamps_RemainsDeterministic`
  - verifies stable ordering with equal fallback timestamps using `SnapshotId` tie-break behavior

## Whether fallback strategy was changed
- No production fallback logic was changed.
- The existing strategy was kept because analysis did not show a real correctness or ordering defect:
  - `UnixEpoch` is deterministic
  - uniqueness remains per product
  - read ordering remains explicit
  - no trend or aggregation math changes were needed

## Why this is now safe
- The fallback path is now covered in both empty-owner modes:
  - latest dated sprint available
  - no sprints available
- Deterministic ordering is now covered when fallback timestamps collide with other snapshots at the same timestamp.
- No unrelated CDC behavior changed:
  - aggregation unchanged
  - comparison unchanged
  - trend math unchanged
  - write-on-read remains absent
