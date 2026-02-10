# Revision Retrieval & Ingestion Pipeline

## Purpose
Collect work item revisions from the TFS reporting endpoint for trend analysis, scoped to the pre-synced product work item set (approximately 854 IDs). The pipeline persists whitelisted field snapshots only and guarantees deterministic pagination.

## Scope & Inputs
- Source: `/_apis/wit/reporting/workitemrevisions` (no relations requested).
- Work item scope: pre-synced allowed IDs; rows outside the set are discarded.
- Start date: earliest `CreatedDate` among scoped cached work items (with 1-day buffer). Falls back to full history if none exist.

## Persistence Rules
- Each ingested revision stores a snapshot of the whitelisted fields (type, title, state, reason, iteration, area, created, changed, closed, effort, tags, severity, changedBy).
- Missing fields are recorded as “not provided” (never interpreted as a clear).
- No relation handling during revision ingestion; relation hydration is skipped.
- Run outcome is stored on the ingestion watermark (`CompletedNormally` or `CompletedWithPaginationAnomaly`).

## Pagination Rules
- **Continue** only when all hold: `RawRevisionCount > 0`, continuation token advances (not seen before), `HasMoreResults == true`.
- **Stop - normal:** `HasMoreResults == false`.
- **Stop - anomalies (Recorded as CompletedWithPaginationAnomaly):**
  - Dead page: `RawRevisionCount == 0` and (`HasMoreResults == true` or token repeats/does not advance).
  - Repeated or non-advancing continuation token.
  - Safety cap: `MaxTotalPages` exceeded.
- Scoped empty pages do **not** stop the run.

## Diagnostics
- Once per run, on the first page with scoped revisions, log all whitelisted fields for each scoped revision (bounded dump).
- Per-page diagnostics remain bounded; pagination anomalies are logged explicitly.
- Run classification is persisted and surfaced for observability.
