# Revision Retrieval and Ingestion Design

## Inputs and Scope
- **Work item scope**: Descendants of configured backlog roots for the selected product owner; scope is resolved before ingestion and reused for the run.
- **Start date inference**: Initial backfill starts one day before the earliest `CreatedDate` (or `TfsChangedDate`) among scoped cached items; falls back to a 180-day window when nothing is cached.
- **Field whitelist**: Revisions persist only whitelisted fields (type, title, state, reason, iteration, area, created, changed, closed, effort, tags, severity, changedBy). Relations are intentionally excluded.

## Retrieval Flow
- Primary path uses the TFS reporting endpoint `/_apis/wit/reporting/workitemrevisions` without relations and requests only whitelisted fields.
- Pagination is deterministic: the client sends either `startDateTime` (first page) or `continuationToken` (subsequent pages), never both.
- Ingestion runs inside rolling time windows; windows shrink on stalls down to a 6-hour minimum to isolate problematic spans.

## Scoping and Filtering
- All raw revisions are filtered to the scoped work item id set; out-of-scope rows are discarded.
- Window filtering keeps only revisions whose `ChangedDate` falls inside the active window; out-of-window scoped rows are counted but not persisted.
- Deduplication key is `WorkItemId + RevisionNumber`; duplicates are skipped with explicit drop accounting.

## Persistence Model
- Each persisted revision is a snapshot of the whitelist; missing fields remain null (never treated as clears).
- Field/relationship deltas are ignored during ingestion to keep persistence fast and deterministic.
- A per-product-owner watermark records run outcome, last stable changed date, hashed continuation token, and fallback progress for resumability.

## Pagination Rules and Termination
- Continue paging only when: raw rows exist, token advances (not seen before), and `HasMoreResults` is true.
- Terminate as **pagination anomaly** when:
  - Continuation token repeats or does not advance.
  - `HasMoreResults == true` (or token present) with zero raw rows.
  - MaxTotalPages exceeded or client signals termination.
- **Retry policy**: configurable `MaxPageRetries` with exponential backoff + jitter; retries re-fetch the same page and roll back token tracking.
- After retries:
  - **Fail fast** (default) – mark run as `CompletedWithPaginationAnomaly`.
  - **Fallback** – stop paging and switch to per-work-item retrieval.

## Fallback Behavior
- Fallback fetches revisions per scoped work item via the per-item revisions API (no relations), honoring the same whitelist.
- Progress is rate-limited by the shared TFS throttler and is resumable via a stored fallback index on the watermark.
- Backfill is **not** marked complete when fallback is used; the run outcome becomes `CompletedWithFallback` with explicit logging.

## Observability
- Per page: logs page index, raw/scoped counts, distinct work item count, persisted count, token hash + advancement, has-more flag, retry attempt/backoff (when applied), duration, memory, and drop accounting.
- Per window: logs outcome, pages processed, persisted counts, stall reason, and raw min/max `ChangedDate` coverage.
- Per run: logs total persisted, distinct work items, min/max `ChangedDate`, final outcome (`CompletedNormally`, `CompletedWithFallback`, `CompletedWithPaginationAnomaly`, `Failed`), and whether fallback was used.
- Watermark fields capture the last stable continuation token hash, last stable changed date, last run outcome, and fallback resume index to enable safe resumption without silent drops.
