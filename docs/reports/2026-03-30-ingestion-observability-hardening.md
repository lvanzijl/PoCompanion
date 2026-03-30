# Ingestion observability hardening

## What was instrumented

- Added a window-level `PersistRejectSummary` log that aggregates:
  - `totalRaw`, `totalScoped`, `attemptedPersist`, `persisted`
  - rejection buckets for out-of-window, out-of-scope, duplicate/already-exists, missing/invalid required fields, DB constraint violations, and other.
- Added explicit persistence lifecycle logs:
  - `PersistAttempt count=...` before `SaveChangesAsync`
  - `PersistCommit rowsAffected=...` after `SaveChangesAsync`
  - warning when `rowsAffected == 0` while `attemptedPersist > 0`.
- Added CRITICAL invariant logging when `totalScoped > 0` but `persisted == 0` for a window, including:
  - first 3 candidate revisions
  - DB unique key definition (`RevisionHeaderEntity` unique index: `WorkItemId+RevisionNumber`)
  - whether SaveChanges was invoked
  - rows affected
  - captured `DbUpdateException` type/message (if any).

## New invariants

- Ingestion now emits a CRITICAL diagnostic for `scopedRevisions > 0 && persisted == 0`.
- Run-end diagnostics now warn on potential scope mismatch when:
  - aggregated scope is non-zero
  - segmented mode is active
  - every segment produced zero scoped revisions.

## Paging anomaly resilience

- On `RawZeroWithHasMore` beyond configured tolerance:
  - logs paging context (page index, cursor, filter/order semantics)
  - performs one deterministic re-seek from last observed cursor
  - if re-seek still returns zero, skips only the current segment (when segmented) and continues backfill
  - window can complete as partial instead of hard-aborting the entire run.

## How to interpret `PersistRejectSummary`

- `totalRaw` vs `totalScoped` shows how much was filtered by scope.
- `attemptedPersist` is scoped+window-eligible candidates.
- `persisted` is successfully inserted revision headers.
- Rejection buckets explain why candidates were not persisted:
  - `reject_outOfWindow`: scoped but outside active window
  - `reject_outOfScope`: raw records not in aggregated scope
  - `reject_duplicateKey` / `reject_alreadyExists`: existing key collisions
  - `reject_invalidChangedDate` / `reject_invalidWorkItemId` / `reject_missingFields`: required field failures
  - `reject_dbConstraintViolation`: DB-side constraint failures during save
  - `reject_other`: residual drops not covered by explicit categories.
