# Revision Ingestor V2

## Why V2 Exists

The original `RevisionIngestionService` (V1) evolved organically to handle a variety of TFS OData API anomalies including:

- Segment-based cursor reseek when pagination stalls
- Fabricated continuation tokens for recovery
- Complex retry/fallback logic with per-work-item retrieval

While effective, these mechanisms make the ingestion flow hard to reason about and can mask underlying data source issues.

**V2** is a clean, streaming, token-only ingestor that enforces a single invariant:

> The only progress primitive is the server continuation token. We never fabricate, modify, or reseek tokens.

V2 is modeled after the validator tool's `Program.cs` paging behavior: fetch page → persist → checkpoint → fetch next page.

## Invariants

1. **Token-only paging**: The continuation token comes exclusively from the server response (`page.ContinuationToken`). V2 never synthesizes `seg:` tokens or cursor-based alternatives.

2. **No segmentation**: Work item ID ranges are not segmented. A single aggregated scope (union of all configured product backlog root descendants) is passed to the revision source.

3. **Deterministic termination**: A window terminates when:
   - `ContinuationToken` becomes `null` (normal completion), OR
   - A repeated token is detected (fatal: `StallReason=RepeatedToken`), OR
   - Empty pages with tokens exceed `V2MaxEmptyPageRetries` (fatal: `StallReason=EmptyPageWithToken`)

4. **Per-page transparency**: Every page emits a structured log line with raw count, scoped count, in-window count, persist attempt, persisted count, and rejection reasons.

5. **Checkpoint after persist**: The continuation token is checkpointed only after successful persistence of a page's data.

## How to Enable

### Configuration

Add or modify the `RevisionIngestionV2` section in `appsettings.json`:

```json
{
  "RevisionIngestionV2": {
    "RevisionIngestionMode": "V2",
    "V2PageSize": 200,
    "V2EnableWindowing": true,
    "V2WindowDays": 30,
    "V2MaxEmptyPageRetries": 2,
    "V2MaxConsecutiveEmptyPages": 3
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `RevisionIngestionMode` | `"V1"` | Set to `"V2"` to activate the new ingestor |
| `V2PageSize` | `200` | OData page size for revision queries |
| `V2EnableWindowing` | `true` | Split ingestion into time windows |
| `V2WindowDays` | `30` | Size of each time window in days |
| `V2MaxEmptyPageRetries` | `2` | Max retries for empty pages with non-null token |
| `V2MaxConsecutiveEmptyPages` | `3` | Max consecutive empty pages before stall |

### DI Registration

Both V1 and V2 are registered in the DI container. The mode toggle determines which service handles ingestion at runtime. V1 remains the default and is untouched.

## How to Interpret Logs

All V2 log entries use the `REV_INGEST_V2_` prefix for easy filtering.

### Log Events

| Log Event | Level | Description |
|-----------|-------|-------------|
| `REV_INGEST_V2_SCOPE` | Info | Emitted once per run with product count and work item count |
| `REV_INGEST_V2_WINDOW_START` | Info | Emitted at the start of each window with start/end timestamps |
| `REV_INGEST_V2_PAGE` | Info | Per-page summary with raw, scoped, in-window, persisted counts and token hashes |
| `REV_INGEST_V2_PERSIST_GATE_ZERO` | Warning | Scoped revisions exist but none fell within the window |
| `REV_INGEST_V2_EMPTY_PAGE_RETRY` | Warning | Empty page with token, retrying with backoff |
| `REV_INGEST_V2_WINDOW_FAIL` | Warning | Window failed with stall reason and token hash |
| `REV_INGEST_V2_WINDOW_END` | Info | Window completed with total persisted, pages, and duration |

### Example Log Sequence

```
REV_INGEST_V2_SCOPE products=2 workItems=150
REV_INGEST_V2_WINDOW_START start=2000-01-01 end=2026-02-22
REV_INGEST_V2_PAGE page=0 raw=200 scoped=180 inWindow=180 persistAttempt=180 persisted=175 rejects_duplicate=5 rejects_missing=0 rejects_other=0 token=null next=a1b2c3d4e5f6
REV_INGEST_V2_PAGE page=1 raw=150 scoped=140 inWindow=140 persistAttempt=140 persisted=140 rejects_duplicate=0 rejects_missing=0 rejects_other=0 token=a1b2c3d4e5f6 next=null
REV_INGEST_V2_WINDOW_END persistedTotal=315 pages=2 duration=1234ms
```

### Diagnosing Issues

- **`StallReason=EmptyPageWithToken`**: The server returned empty pages but kept providing continuation tokens. This suggests a server-side anomaly. Check the `tokenHash` and `retries` values.

- **`StallReason=RepeatedToken`**: The server returned the same continuation token twice in a row. This would cause an infinite loop in a naive implementation. V2 fails fast.

- **`PERSIST_GATE_ZERO`**: Revisions were in scope but fell outside the current window's time range. This is normal during windowed ingestion when data overlaps window boundaries.
