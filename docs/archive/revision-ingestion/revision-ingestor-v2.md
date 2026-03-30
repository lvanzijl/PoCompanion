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
   - A token cycle is detected via HashSet (fatal: `StallReason=RepeatedTokenCycle`), OR
   - Consecutive empty pages with tokens exceed `V2MaxConsecutiveEmptyPages` (fatal: `StallReason=EmptyPageWithToken`)

4. **Per-page transparency**: Every page emits a structured log line with raw count, scoped count, in-window count, persist attempt, persisted count, and rejection reasons.

5. **Checkpoint after persist**: The continuation token is checkpointed only after successful persistence of a page's data.

## Changes A–D

### A) Realistic Backfill Start

V2 no longer hardcodes `2000-01-01` as the backfill start. Instead:

- An `IBackfillStartProvider` is called to derive the earliest relevant timestamp from work item metadata.
- If derivation fails or returns null, a fallback is used: `UtcNow - max(V2WindowDays * 2, 180)` days.
- The backfill start is always clamped to `<= UtcNow`.
- Log: `REV_INGEST_V2_BACKFILL_START derivedStart=... fallbackUsed=... reason=...`

### B) Empty Page Handling with V2MaxConsecutiveEmptyPages

Empty pages with non-null continuation tokens are no longer treated as "retries". They represent the server advancing its token, and V2 follows:

- `consecutiveEmptyPages` is incremented for each empty-with-token page.
- `consecutiveEmptyPages` resets to 0 when `rawCount > 0`.
- The window fails only when `consecutiveEmptyPages > V2MaxConsecutiveEmptyPages`.
- Log: `REV_INGEST_V2_EMPTY_WITH_TOKEN` (rate-limited: first 3, then every 50).

### C) Token Cycle Detection

V2 now maintains a `HashSet<string>` of seen token hashes per window. Before each page fetch:

- If the current continuation token hash is already in the set, the window fails with `StallReason=RepeatedTokenCycle`.
- Otherwise, the hash is added to the set.
- Null tokens are not tracked.
- Log: `REV_INGEST_V2_WINDOW_FAIL reason=RepeatedTokenCycle tokenHash=... pageIndex=... seenCount=...`

### D) Query Identity Logging

All `REV_INGEST_V2_WINDOW_FAIL` and `REV_INGEST_V2_EMPTY_WITH_TOKEN` logs now include structured query identity fields:

- `windowStartUtc`, `windowEndUtc`
- `allowedWorkItemIds` (count)
- `pageSize`
- `hasContinuationToken`, `tokenHash`, `nextTokenHash`
- `pageIndex`, `rawCount`, `scopedCount`, `inWindowCount`

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
| `V2MaxConsecutiveEmptyPages` | `3` | Max consecutive empty pages (with token) before window fails |

### DI Registration

Both V1 and V2 are registered in the DI container. A `RevisionIngestionDispatcher` (implementing `IRevisionIngestionService`) routes calls to V1 or V2 based on the `RevisionIngestionMode` setting at runtime. The `RevisionSyncStage` (sync pipeline stage 3) injects `IRevisionIngestionService`, so switching modes requires only a config change — no code changes needed.

V2 also requires:
- `IBackfillStartProvider` — derives the backfill start date from work item metadata. Default: `WorkItemBackfillStartProvider`.
- `TimeProvider` — for deterministic time in tests. Default: `TimeProvider.System`.

## Checkpoint Resume

V2 supports resuming ingestion across process restarts. After each successful page persistence, V2 saves a checkpoint containing:

- The current continuation token
- The window start timestamp
- Outcome marker `V2_InProgress`

On the next run, V2 checks for an existing `V2_InProgress` checkpoint. If found, it:

1. Skips windows that completed before the checkpoint window
2. Resumes the checkpoint window from the stored continuation token
3. Clears the checkpoint (sets outcome to `V2_Completed`, nulls the token) after the window completes

This ensures no data is lost or duplicated across restarts.

## How to Interpret Logs

All V2 log entries use the `REV_INGEST_V2_` prefix for easy filtering.

### Log Events

| Log Event | Level | Description |
|-----------|-------|-------------|
| `REV_INGEST_V2_SCOPE` | Info | Emitted once per run with product count and work item count |
| `REV_INGEST_V2_BACKFILL_START` | Info | Backfill start derivation result with `derivedStart`, `fallbackUsed`, `reason` |
| `REV_INGEST_V2_CHECKPOINT_RESUME` | Info | Resuming from a stored checkpoint token |
| `REV_INGEST_V2_WINDOW_START` | Info | Emitted at the start of each window with start/end timestamps |
| `REV_INGEST_V2_PAGE` | Info | Per-page summary with raw, scoped, in-window, persisted counts and token hashes |
| `REV_INGEST_V2_PERSIST_GATE_ZERO` | Warning | Scoped revisions exist but none fell within the window |
| `REV_INGEST_V2_EMPTY_WITH_TOKEN` | Info | Empty page with advancing token (rate-limited: first 3, then every 50) |
| `REV_INGEST_V2_WINDOW_FAIL` | Warning | Window failed — includes stall reason, query identity, and token hashes |
| `REV_INGEST_V2_WINDOW_END` | Info | Window completed with total persisted, pages, and duration |
| `REV_INGEST_DISPATCH` | Info | Dispatcher routing decision (mode=V1 or mode=V2) |

### Example Log Sequence

```
REV_INGEST_V2_SCOPE products=2 workItems=150
REV_INGEST_V2_BACKFILL_START derivedStart=2021-03-15 fallbackUsed=false reason=DerivedFromWorkItems
REV_INGEST_V2_WINDOW_START start=2021-03-15 end=2026-02-22
REV_INGEST_V2_PAGE page=0 raw=200 scoped=180 inWindow=180 persistAttempt=180 persisted=175 rejects_duplicate=5 rejects_missing=0 rejects_other=0 token=null next=a1b2c3d4e5f6
REV_INGEST_V2_PAGE page=1 raw=150 scoped=140 inWindow=140 persistAttempt=140 persisted=140 rejects_duplicate=0 rejects_missing=0 rejects_other=0 token=a1b2c3d4e5f6 next=null
REV_INGEST_V2_WINDOW_END persistedTotal=315 pages=2 duration=1234ms
```

### Diagnosing Issues

- **`StallReason=EmptyPageWithToken`**: The server returned consecutive empty pages but kept providing new continuation tokens. The window fails when `consecutiveEmptyPages > V2MaxConsecutiveEmptyPages`. The failure log includes `pageIndex`, `consecutiveEmptyPages`, and `maxAllowedEmpty` to help diagnose whether the threshold is too low or the server is misbehaving.

- **`StallReason=RepeatedTokenCycle`**: A continuation token hash that was previously seen reappeared, indicating a pagination loop. V2 fails immediately. The log includes `tokenHash`, `pageIndex`, and `seenCount` (number of unique tokens seen before the cycle).

- **`REV_INGEST_V2_EMPTY_WITH_TOKEN`**: Informational (rate-limited). Indicates the server returned an empty page but provided a new continuation token. V2 advances the token and continues. Includes full query identity fields (`windowStartUtc`, `windowEndUtc`, `pageSize`, etc.) to verify window bounds are preserved during paging.

- **`PERSIST_GATE_ZERO`**: Revisions were in scope but fell outside the current window's time range. This is normal during windowed ingestion when data overlaps window boundaries.
