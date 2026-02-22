<!--
PR Summary:
- Analyzed validator OData retrieval path vs production ingestion path.
- Identified concrete, code-grounded differences that explain pagination anomalies and near-zero persistence.
- Produced an alignment strategy that preserves incremental behavior and multi-product scope.

Checklist:
- [x] Locate validator and ingestion entry points
- [x] Compare scope build, segmentation, query, continuation, anomaly and persistence behavior
- [x] Explain symptom linkage (DeadPageNoData / RawZeroWithHasMore / low persistence)
- [x] Provide Diff Matrix with grounded references
- [x] Define target alignment constraints (including multi-product aggregation)
-->

# OData Validator vs Ingestion Report

## Scope of this report
This report compares:
1. **Validator tool path** (`PoTool.Tools.TfsRetrievalValidator`)
2. **Real ingestion path** (`PoTool.Api.Services.RevisionIngestionService` + sync stage)

All claims below are grounded in repository code references.

---

## A) Validator tool path (what it does)

### Entry point and orchestration
- Entrypoint is top-level program in `PoTool.Tools.TfsRetrievalValidator/Program.cs`.
- DI wiring explicitly resolves:
  - `ITfsClient` -> `RealTfsClient`
  - `IWorkItemRevisionSource` -> `RealODataRevisionTfsClient`  
  (`PoTool.Tools.TfsRetrievalValidator/Program.cs:81-83`)
- Orchestration flow:
  1. Read one configured root (`RootWorkItemId`) (`Program.cs:87-91`, `333-336`)
  2. Retrieve hierarchy under that root (`Program.cs:97-101`)
  3. Build allowed scope IDs + range segments (`Program.cs:103-108`)
  4. Page revisions until continuation is null or source terminates early (`Program.cs:130-177`)

### Scope build for one product
- Scope is built from **one root ID** in config (`Program.cs:87-91`, `97-101`, `333-336`).
- `allowedWorkItemIds` comes from returned hierarchy (`Program.cs:103`).
- No multi-product aggregation exists in validator (single-root tool by design).

### Segmentation and Mode=Seek behavior
- Validator delegates retrieval to `RealODataRevisionTfsClient.GetRevisionsAsync(...)` with `scopedWorkItemIds` and continuation (`Program.cs:133-137`).
- The OData client resolves request mode:
  - no continuation -> `RequestMode.Seek` initial URL (`RealODataRevisionTfsClient.cs:342-349`)
  - decoded seek token -> `RequestMode.Seek` (`352-355`)
  - otherwise -> `RequestMode.NextLink` (`357`)
- Scope segmentation is range-based when enabled (`RealODataRevisionTfsClient.cs:77-85`, `360-366`) and uses segment state tokens (`389-412`, `517-591`).

### Exact OData query pattern
Built by `ODataRevisionQueryBuilder`:
- `$top=<ODataTop>` (`ODataRevisionQueryBuilder.cs:72`)
- `$filter` includes:
  - `ChangedDate ge <startDateTime>` when supplied (`75-78`)
  - scope filter (segment range or id list/range) (`80-83`, `119-177`)
  - seek cursor filter when seeking (`86-92`)
- `$orderby=ChangedDate asc,WorkItemId asc,Revision asc` when enabled (`111-114`)
- Optional minimal `$select/$expand` projection (`99-108`)

### Continuation / nextLink handling
- Parses `@odata.nextLink` (`RealODataRevisionTfsClient.cs:181`).
- If no nextLink but full page and seek fallback enabled, synthesizes a **seek continuation** from max tuple `(ChangedDate, WorkItemId, Revision)` (`199-233`, `461-475`, `509-563`).
- If segmented scope has more segments, continuation advances segment index (`389-412`).

### Retry/backoff logic
- In client path, one targeted retry exists: if 400 indicates date literal mismatch, retry once with alternate quote style (`127-151`, `414-420`).
- Validator loop itself does not add retry/backoff; it trusts client continuation and loops until done (`Program.cs:130-177`).

### Success/failure determination
- Validator marks success if no early termination reason was reported (`Program.cs:222-225`, `227-237`).
- It writes `SessionSucceeded` + `SessionOutcomeReason` into snapshot/summary (`Program.cs:211-225`, `248-260`; summary build `544-619`).

---

## B) Real ingestion pipeline (what it does)

### Entry point and wrappers
- Primary API entrypoint: `RevisionIngestionService.IngestRevisionsAsync(...)` (`PoTool.Api/Services/RevisionIngestionService.cs:87-90`).
- Stage wrapper: `RevisionSyncStage` calls ingestion and converts to sync stage success/failure (`PoTool.Api/Services/Sync/RevisionSyncStage.cs:40-47`, `49-72`, `91-95`).
- Pipeline runner marks stage failed in cache sync status if stage result is not successful (`PoTool.Api/Services/Sync/SyncPipelineRunner.cs:391-410`).

### Scope build for 1+ products
- Scope resolution uses **all configured products for a product owner**:
  - loads profile + products (`RevisionIngestionService.cs:1896-1899`)
  - collects distinct `BacklogRootWorkItemId` (`1910-1914`)
  - retrieves work items for all roots in one call (`1921-1925`)
  - filters descendants from all roots and unions IDs (`1927-1930`)
- Log message explicitly states concatenated scope across roots (`1938-1943`).

### Aggregation status (multi-product requirement)
- **Already implemented**: ingestion currently aggregates across roots/products before paging/segmenting (`1910-1930`, `1938-1943`).
- This must be preserved in any redesign.

### Segmentation algorithm
- Segment diagnostics are computed from contiguous ID spans (`2432-2437`, `2450-2480`).
- Runtime segment index is inferred from `seg:` continuation token (`2482-2512`).
- Actual segment selection/querying is done inside `RealODataRevisionTfsClient` range-segment logic (`RealODataRevisionTfsClient.cs:77-87`, `368-387`, `389-412`).

### Backfill windowing algorithm
- Run computes `[backfillStartUtc, backfillEndUtc)` (`RevisionIngestionService.cs:204-212`).
- Windows are processed with adaptive duration growth/shrink (`954-1004`, `1068-1076`, `1647-1672`).
- For each window, ingestion calls OData with `startDateTime = window.StartUtc` only (`1224-1227`), then locally gates rows to window bounds (`1254-1256`).

### OData continuation handling in ingestion
- Uses `result.ContinuationToken` + `result.HasMoreResults` (`1261`, `1271`).
- Tracks token advancement/repetition via `ReportingRevisionsPageTracker` (`1269-1270`, `1484-1497`, `1523-1535`, `1588-1589`).
- Applies retry/backoff on stalled pages when retryable (`1538-1563`, `1635-1645`).

### Persistence gating and drop points
Rows can be retrieved but not persisted due to:
1. **Window gating**: only revisions within `[window.StartUtc, window.EndUtc)` are candidates (`1254-1259`).
2. **Dedup** existing `(WorkItemId, RevisionNumber)` (`2035-2039`).
3. **Required field checks** (`2041-2045`).
4. DB constraint failures tracked in metrics (`1372-1391`, `2771-2774`).

This explains observed “raw/scoped > 0 but persisted ~0” scenarios when most rows are outside local window bounds.

### Anomaly detection and early termination
- Ingestion marks anomalies for:
  - repeated token (`1484-1497`)
  - non-advancing token with hasMore (`1523-1535`)
  - raw-empty pages with hasMore/advancing token beyond threshold (`1498-1516`) -> `DeadPageNoData`
- On exhaustion of retries, window is marked stalled (`1565-1567`) and run can degrade to pagination-anomaly outcome (`1088-1090`).

### Success/failure semantics (and UX impact)
- `RevisionIngestionService` can return success-with-warnings for pagination anomaly with progress (`387-395`, `494-505`, `506-514`).
- `RevisionSyncStage` fails stage only when `result.Success == false` (`49-72`), otherwise passes warnings (`91-95`).
- Pipeline marks sync failed only for stage failure (`SyncPipelineRunner.cs:391-410`), so warning outcomes affect UX differently from hard failures.

---

## C) Diff Matrix

| Concern | Validator behavior | Ingestion behavior | Risk / symptom linkage | Proposed alignment / fix |
|---|---|---|---|---|
| Scope build | Single root from config (`Program.cs:87-91`, `97-103`) | Multi-root union across products (`RevisionIngestionService.cs:1910-1930`) | Different scope cardinality can change page composition and token progression | Keep ingestion multi-root union; optionally let validator run multi-root mode for parity tests |
| Scope aggregation timing | One-root scope built once before loop (`Program.cs:103-108`) | Union scope built once before windows (`RevisionIngestionService.cs:120-128`, `1890-1945`) | If aggregation were per-product, segmenting would diverge; current code already correct | Preserve “aggregate-all-products before segmentation” as invariant and test it |
| Segmentation | Uses `RealODataRevisionTfsClient` range segments + `seg:` token (`Program.cs:132-137`, `RealODataRevisionTfsClient.cs:77-87`, `389-412`) | Same source, plus diagnostics for segment index (`RevisionIngestionService.cs:1220`, `2482-2512`) | Segment boundary behavior is mostly aligned; mismatch mainly comes from windows/gating | Keep shared segment logic; avoid extra window-induced divergence |
| Query ordering | Deterministic order in shared builder (`ODataRevisionQueryBuilder.cs:111-114`) | Same shared builder | Low risk here; already aligned | No change unless order is disabled in config |
| Filter model | Global lower bound (`ChangedDate ge start`) + scope filter (`ODataRevisionQueryBuilder.cs:75-83`) | Same query call, but per-window start only (`RevisionIngestionService.cs:1224-1227`) with local end gating (`1254-1256`) | Can fetch many rows outside window => candidate persist 0 despite retrieval | Add server-side upper-bound filter for window path or switch to cursor-driven windowless seek mode |
| Continuation handling | Loop until token null/termination (`Program.cs:130-177`); tolerant of empty pages if token progresses | Token repeat/non-advance/progress-without-data thresholds can stall/terminate (`1484-1535`) | Produces `DeadPageNoData` / `RawZeroWithHasMore` and early stop | Add deterministic recovery: re-seek from last stable tuple before declaring stall |
| Retry/backoff | Only date-literal fallback retry in client (`RealODataRevisionTfsClient.cs:127-151`) | Per-page anomaly retry/backoff + optional run-level retry (`RevisionIngestionService.cs:1538-1563`, `280-385`) | Retrying same bad token can still make no durable progress | Retry should alter cursor/query state, not only delay/replay |
| Persistence gating | Validator only accumulates scoped revisions in memory (`Program.cs:140-157`) | Persists only scoped+in-window+valid+non-duplicate (`RevisionIngestionService.cs:1254-1259`, `1981-2065`) | Major source of “retrieved many, persisted ~0” perception | Emit explicit counters for each drop gate and align query bounds to reduce outside-window drops |
| Success criteria | Success iff no early termination (`Program.cs:222-237`) | Success may include warnings when anomaly but some progress (`RevisionIngestionService.cs:494-514`) | UX can show non-failed run with severe data loss, or failed stage depending outcome | Add acceptance threshold metrics (e.g., scoped->persist ratio) for warning/error classification |

---

## D) Why validator succeeds while ingestion can fail or persist ~0

## 1) Validator has no local window-end persistence gate
Validator pages globally from inferred start date and keeps all scoped revisions (`Program.cs:133-157`). It does **not** discard rows based on a moving window end.

Ingestion, however, fetches with only `window.StartUtc` filter but persists only rows `< window.EndUtc` (`RevisionIngestionService.cs:1224-1227`, `1254-1259`). If returned pages are mostly outside the current window, persistence can remain near zero despite non-empty retrieval.

## 2) Ingestion has stricter stall rules on empty/progress pages
Ingestion can terminate with `ProgressWithoutData` (`DeadPageNoData`) after bounded empty progressing pages (`1498-1516`), and also on token repeat/non-advance (`1484-1497`, `1523-1535`).

Validator keeps iterating while token advances and only fails on explicit source termination (`Program.cs:166-177`, `222-225`).

## 3) Retry path may replay without changing effective cursor
Current page retry path backs off and retries (`1538-1563`), but if server keeps emitting pathological continuation behavior, this may not produce deterministic forward progress.

## 4) Success semantics differ
Validator success is binary and tied directly to early termination (`Program.cs:222-237`). Ingestion can return warning-success outcomes (`494-514`) that may hide substantial data loss unless operators inspect drop accounting logs (`1378-1391`).

---

## E) Target alignment design constraints

1. **Keep multi-product aggregation** exactly as currently implemented (`RevisionIngestionService.cs:1910-1930`).
2. **Align continuation semantics** with validator/client seek behavior by recovering via stable cursor tuple rather than replaying dead pages.
3. **Eliminate ambiguous empty-page outcomes** by deterministic branch: either seek-recover or terminate with explicit unretrievable reason.
4. **Make retrieval-vs-persistence accounting first-class** so “retrieved N scoped, persisted 0” is diagnostically explicit and testable.
5. **Preserve incremental safety and bounded memory** (windowing or equivalent cursor checkpoints must remain bounded).

---

## Evidence index (quick links)
- Validator orchestrator: `PoTool.Tools.TfsRetrievalValidator/Program.cs`
- OData client + continuation logic: `PoTool.Integrations.Tfs/Clients/RealODataRevisionTfsClient.cs`
- OData query builder: `PoTool.Integrations.Tfs/Clients/ODataRevisionQueryBuilder.cs`
- Ingestion service: `PoTool.Api/Services/RevisionIngestionService.cs`
- Revision sync stage: `PoTool.Api/Services/Sync/RevisionSyncStage.cs`
- Pipeline fail/warn propagation: `PoTool.Api/Services/Sync/SyncPipelineRunner.cs`
- Multi-product scope test: `PoTool.Tests.Unit/Services/RevisionIngestionServiceTests.cs:637-678`
- Seek fallback tests: `PoTool.Tests.Unit/Services/RealODataRevisionTfsClientTests.cs:470-564`
