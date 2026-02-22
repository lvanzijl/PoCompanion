# Revision ingestion divergence investigation: PoTool.Api vs TfsRetrievalValidator

## Scope

This report compares the runtime behavior of:

- `PoTool.Api` revision ingestion path
- `PoTool.Tools.TfsRetrievalValidator` revision retrieval path

with focus on the observed mismatch:

- API: `CompletedWithPaginationAnomaly` + `ProgressWithoutData`
- Validator: successful completion with seek paging and `ShortPage` stop

All conclusions below are based on repository code references.

---

## 1) EXECUTION FLOW COMPARISON

### A. PoTool.Api call graph (sync stage → ingestion → OData → anomaly detection → persistence)

1. `RevisionSyncStage.ExecuteAsync(...)` calls `RevisionIngestionService.IngestRevisionsAsync(...)`  
   (`PoTool.Api/Services/Sync/RevisionSyncStage.cs:26-48`)
2. `IngestRevisionsAsync(...)` resolves:
   - `IWorkItemRevisionSource` (registered as `RealODataRevisionTfsClient`)
   - allowed scope IDs for product owner  
   (`PoTool.Api/Services/RevisionIngestionService.cs:116-125,1873-1928`)
3. `IngestRevisionsAsync(...)` executes windowed retrieval via `ProcessWindowsAsync(...)`  
   (`PoTool.Api/Services/RevisionIngestionService.cs:214-227,954-1115`)
4. Per window, `ProcessSingleWindowAsync(...)` calls  
   `revisionSource.GetRevisionsForScopeAsync(allowedWorkItemIds, window.StartUtc, continuationToken, ...)`  
   (`PoTool.Api/Services/RevisionIngestionService.cs:1143-1227`)
5. `RealODataRevisionTfsClient.GetRevisionsForScopeAsync(...)` forwards to `GetRevisionsAsync(...)`  
   (`PoTool.Integrations.Tfs/Clients/RealODataRevisionTfsClient.cs:54-62`)
6. `RealODataRevisionTfsClient.GetRevisionsAsync(...)` builds OData URL via `ODataRevisionQueryBuilder`, sends request, parses rows, builds continuation (`nextLink` or seek token)  
   (`PoTool.Integrations.Tfs/Clients/RealODataRevisionTfsClient.cs:72-253`; `PoTool.Integrations.Tfs/Clients/ODataRevisionQueryBuilder.cs:17-117`)
7. API-side anomaly detection is done in `ProcessSingleWindowAsync(...)`, including `ProgressWithoutData` and repeated-token checks  
   (`PoTool.Api/Services/RevisionIngestionService.cs:1470-1519`)
8. Persistence happens via `PersistRevisionsAsync(...)`, storing only `scopedInWindow` rows and deduping by `(WorkItemId, RevisionNumber)`  
   (`PoTool.Api/Services/RevisionIngestionService.cs:1252-1258,1308-1311,1964-2139`)

### B. TfsRetrievalValidator call graph

1. Program builds DI and registers `IWorkItemRevisionSource => RealODataRevisionTfsClient`  
   (`PoTool.Tools.TfsRetrievalValidator/Program.cs:55-83`)
2. Retrieves root hierarchy work items via `ITfsClient.GetWorkItemsByRootIdsAsync(...)` and builds `allowedWorkItemIds`  
   (`PoTool.Tools.TfsRetrievalValidator/Program.cs:97-105`)
3. Computes one inferred start date (`min(CreatedDate/ChangedDate)-1d`)  
   (`PoTool.Tools.TfsRetrievalValidator/Program.cs:110-120`)
4. Runs one continuous pagination loop:
   `revisionSource.GetRevisionsAsync(startDateTime: inferredStartDateTime, continuationToken, scopedWorkItemIds: allowedWorkItemIds, ...)`
   until continuation token is null  
   (`PoTool.Tools.TfsRetrievalValidator/Program.cs:130-177`)
5. Tracks scoped/raw counters and reports termination only if source sets `page.Termination`  
   (`PoTool.Tools.TfsRetrievalValidator/Program.cs:140-175,181-225`)

### C. First code-level divergence point

Earliest behavioral split is **windowed date slicing in API**:

- API calls `ProcessWindowsAsync(...)` and then per-window retrieval with `startDateTime = window.StartUtc` (`RevisionIngestionService.cs:214-227`, `1143-1227`)
- Validator performs a single global loop with one fixed inferred start date (`Program.cs:130-177`)

This is the first point where identical OData client logic is driven with different pagination strategy.

---

## 2) ODATA REQUEST CONSTRUCTION DIFF

Both pipelines use the same query builder (`ODataRevisionQueryBuilder`) and same client (`RealODataRevisionTfsClient`), but with different calling context.

| Area | Api | Validator | Impact |
|---|---|---|---|
| Client implementation | `RealODataRevisionTfsClient` (`ApiServiceCollectionExtensions.cs:324`) | `RealODataRevisionTfsClient` (`Program.cs:82`) | Same core request/paging engine |
| Entry method | `GetRevisionsForScopeAsync(...)` (`RevisionIngestionService.cs:1222-1227`) | `GetRevisionsAsync(...)` (`Program.cs:133-138`) | No behavioral delta: `GetRevisionsForScopeAsync` forwards to `GetRevisionsAsync` (`RealODataRevisionTfsClient.cs:54-62`) |
| `$filter` ChangedDate lower bound | `ChangedDate ge window.StartUtc` (window changes each run/page loop) | `ChangedDate ge inferredStartDateTime` (single fixed lower bound) | API repeatedly re-anchors filter by time window; validator scans one continuous range |
| `$filter` WorkItemId scope | Built by shared `BuildScopeFilter(...)` with segmented ranges (`WorkItemId ge X and le Y`) when scope mode `Range` (`ODataRevisionQueryBuilder.cs:119-177`) | Same | Same filter logic if same scope IDs supplied |
| Segment handling | Continuation can carry `seg:<index>|<innerToken>` (`RealODataRevisionTfsClient.cs:517-585`); API diagnostics compute per-segment stats (`RevisionIngestionService.cs:2465-2532`) | Same continuation format; validator also parses `seg:` index for stats (`Program.cs:300-331`) | Same segment mechanics; different high-level loop context |
| `$orderby` | `ChangedDate asc,WorkItemId asc,Revision asc` when enabled (`ODataRevisionQueryBuilder.cs:111-114`) | Same | Deterministic order in both |
| `$top` | `ODataTop` for initial pages; `ODataSeekPageSize` for synthetic seek pages (`RealODataRevisionTfsClient.cs:74-76,216-233`) | Same | Same page sizes if config equal |
| Seek logic | If `@odata.nextLink` missing on a full page, builds tuple seek URL/token from max row tuple (`RealODataRevisionTfsClient.cs:199-233`) | Same | Both can run in seek mode |
| nextLink handling | If `nextLink` exists, continuation is trusted (`stopReason=NextLink`) (`RealODataRevisionTfsClient.cs:234-237`) | Same | Both trust nextLink when present |
| `StopReason=ShortPage` condition | Set when no nextLink and returned row count `< top` (`RealODataRevisionTfsClient.cs:238-241`) | Same | Both terminate naturally when short final page occurs |
| Request headers | Through `TfsRequestSender`: `Accept: application/json`, `User-Agent: PoTool/1.0` (`TfsRequestSender.cs:36-45`) | Same sender and headers | No `Prefer` or explicit `OData-Version` header difference in code |
| Extra HTTP handler | none | Adds `TfsCaptureHandler` for dump capture (`Program.cs:67-73,369-440`) | Observability-only; request semantics unchanged |

---

## 3) PAGINATION BEHAVIOR ANALYSIS

### Validator behavior (observed and code-aligned)

- Continuous loop over continuation token until null (`Program.cs:130-177`)
- Uses same seek/nextLink logic from `RealODataRevisionTfsClient`
- Natural completion path is continuation becoming null (including `ShortPage` case in client)

### Api behavior (observed and code-aligned)

- Runs many date windows (`ProcessWindowsAsync`) and restarts page loop per window (`RevisionIngestionService.cs:954-1115`, `1143-1170`)
- Per page anomaly checks happen in API layer, not OData client (`RevisionIngestionService.cs:1470-1519`)
- API sets `CompletedWithPaginationAnomaly` when termination/stall is observed (`RevisionIngestionService.cs:1087-1090`)

### Direct answers to required checks

- **Does API rely on nextLink instead of seek tuple?**  
  **No (not exclusively).** API uses whatever continuation client returns; client prefers nextLink when present, and falls back to seek tuple when nextLink is missing on full pages (`RealODataRevisionTfsClient.cs:199-237`).

- **Does API treat empty RAW page as fatal?**  
  **Yes, conditionally.** If `rawRevisionCount == 0` and `(hasMoreResults || !tokenAdvanced)`, API creates `ProgressWithoutData` termination (`RevisionIngestionService.cs:1492-1505`).

- **Does API treat empty SCOPED page as fatal?**  
  **No.** Fatal condition checks `rawRevisionCount`, not `scopedRevisionCount` (`RevisionIngestionService.cs:1492-1505`). Scoped empties are counted diagnostically (`2511-2517`).

- **Is API using different segment filters?**  
  **No in query construction.** Same query builder and same segment encoding are used (`ODataRevisionQueryBuilder.cs`, `RealODataRevisionTfsClient.cs`).

- **Is API missing WorkItemId range filter?**  
  **No.** WorkItemId filter is included via `BuildScopeFilter` when scope IDs exist (`ODataRevisionQueryBuilder.cs:80-85,119-177`).

- **Is API mixing window segmentation and WorkItemId segmentation?**  
  **Yes.** API adds time-window iteration on top of WorkItemId range segmentation (`RevisionIngestionService.cs:954-1115` + `RealODataRevisionTfsClient.cs:77-87`).

### Exact `ProgressWithoutData` trigger

There are two code paths:

1. **Primary path in API page loop**  
   `rawRevisionCount == 0 && (hasMoreResults || !tokenAdvanced)`  
   (`RevisionIngestionService.cs:1492-1496`)
2. **Mapped from client-thrown `InvalidOperationException`**  
   Caught and converted to `ProgressWithoutData` (`RevisionIngestionService.cs:1229-1233`)  
   Client throws on seek no-progress tuples (`RealODataRevisionTfsClient.cs:210-214`).

For the specific message *“returned no data on page X while indicating more results”*, trigger is path #1 (`rawRevisionCount == 0` with `hasMoreResults == true` and/or non-advancing token).

---

## 4) SCOPING DIFFERENCE

### Why API can show very low persisted/distinct counts vs validator

1. **Scope source differs at runtime**
   - API scope = product owner profile product roots from DB (`ResolveAllowedWorkItemIdsForProductOwnerAsync`)  
     (`RevisionIngestionService.cs:1873-1928`)
   - Validator scope = one explicit `RootWorkItemId` from tool config (`Program.cs:87-105`)
2. **API persists only rows inside current window bounds**
   - API fetches `scopedRevisions`, then keeps only `scopedInWindow` (`ChangedDate >= window.StartUtc && < window.EndUtc`)  
     (`RevisionIngestionService.cs:1248-1254`)
3. **API distinct-work-item metric is persisted-only**
   - Distinct IDs are collected from persisted rows (`windowResult.PersistedWorkItemIds`)  
     (`RevisionIngestionService.cs:1046-1049,1314-1317`)
4. **Persistence dedup/filtering further reduces rows**
   - Duplicate `(WorkItemId, RevisionNumber)` and missing-required rows are skipped  
     (`RevisionIngestionService.cs:2018-2028`)

### Required sub-questions

- **Why only 16 work items in API result?**  
  `DistinctWorkItemsIngested` reflects persisted rows, not total allowed scope (`RevisionIngestionService.cs:229-235,522-533,1046-1049`).

- **Is API filtering by ProductOwner incorrectly?**  
  No direct bug evidence in scope construction code; it intentionally scopes by profile products/root IDs (`1873-1928`).

- **Is API using a narrower scope list?**  
  Potentially yes at runtime, because source of truth differs (profile roots vs validator root). This is configuration/runtime scope divergence, not query-builder divergence.

- **Is API misapplying window segmentation before WorkItemId segmentation?**  
  It applies windowing first at orchestration layer, then OData scope segmentation inside client. This is intentional current behavior (`954-1115` + client segmenting).

- **Is API persisting only creation snapshots?**  
  No. It persists any revision row that passes required-field and dedup checks (`1964-2139`).

- **Is API discarding rows after anomaly detection?**  
  It stops window processing on anomaly (`1548-1550`), so remaining pages/windows are not fetched in that run.

---

## 5) CONFIGURATION DIFF (effective source and defaults)

> Note: repository does not include the live Debug DB values. API TFS runtime values come from persisted `TfsConfigEntity`; validator values come from tool `appsettings*.json`.

| Setting | PoTool.Api (Debug run path) | Validator |
|---|---|---|
| TFS base URL | From DB (`TfsConfigurationService.GetConfigEntityAsync`) (`PoTool.Api/Services/TfsConfigurationService.cs:151-159`) | `Tfs:Url` in appsettings (`PoTool.Tools.TfsRetrievalValidator/appsettings.json:2-10`) |
| Project | From DB (`TfsConfigurationService.cs:57-59,117-119`) | `Tfs:Project` (`validator appsettings.json:2-10`) |
| ProductOwnerId / root selector | ProductOwner from sync context (`RevisionSyncStage.cs:36-42`) | Explicit `TfsRetrievalValidator:RootWorkItemId` (`Program.cs:87-90`, `appsettings.json:12-14`) |
| ScopeCount source | Product-owner root hierarchy from API runtime TFS call (`RevisionIngestionService.cs:1904-1913`) | Root hierarchy from validator runtime TFS call (`Program.cs:97-105`) |
| BackfillStart/End | Calculated in API (`startDateTime` inference + `backfillEndUtc=UtcNow`) (`RevisionIngestionService.cs:169-212`) | Only one inferred `startDateTime`; no window end slicing (`Program.cs:110-120,130-177`) |
| PageSize (`$top`) | `RevisionIngestionPagination:ODataTop` (Debug override keeps 200) (`PoTool.Api/appsettings.Development.json:24`) | `RevisionIngestionPagination:ODataTop` = 200 (`validator appsettings.json:16`) |
| SeekTop | `ODataSeekPageSize` = 200 (`PoTool.Api/appsettings.Development.json:25`) | `ODataSeekPageSize` = 200 (`validator appsettings.json:17`) |
| Segment size | WorkItemId range segment builder default max span 500 (`WorkItemIdRangeSegmentBuilder.cs:10-16`) | Same |
| QuotedDateStrings | API Debug: `false` (`PoTool.Api/appsettings.Development.json:28`) | Validator: `false` (`validator appsettings.json:20`) |
| Fallback / anomaly policy | API reads options including `AnomalyPolicy`, `RetryEnabled`, fallback flow in service (`RevisionIngestionService.cs:240-271,280-385`) | Validator only reports termination; no API fallback orchestration loop |
| `MaxEmptyPages` | API Debug = 3 (`PoTool.Api/appsettings.Development.json:37`) | Validator = 50 (`validator appsettings.json:25`) |

---

## 6) FIRST DIVERGENCE POINT

**Method:** `RevisionIngestionService.IngestRevisionsAsync`  
**Nearest code block:** call to `ProcessWindowsAsync(...)` at `PoTool.Api/Services/RevisionIngestionService.cs:214-227`.

This introduces time-window orchestration absent from validator’s single-loop retrieval (`PoTool.Tools.TfsRetrievalValidator/Program.cs:130-177`).  
From there, API can terminate window processing early on anomaly (`1492-1505`) while validator continues until continuation is null.

---

## 7) ROOT CAUSE HYPOTHESIS (ranked, evidence-based)

1. **API orchestration adds windowed pagination semantics not used by validator**  
   Evidence: API `ProcessWindowsAsync` + per-window `window.StartUtc` (`RevisionIngestionService.cs:954-1115,1143-1227`) vs validator single continuous loop (`Program.cs:130-177`).

2. **API hard-stops on empty raw page with continuation/non-advancing token (`ProgressWithoutData`)**  
   Evidence: explicit check (`RevisionIngestionService.cs:1492-1505`), matching reported reason text.

3. **Metrics and persistence are window/persisted-row based in API, not full-retrieval based**  
   Evidence: only `scopedInWindow` persisted (`1252-1258`), distinct IDs from persisted rows only (`1046-1049,1314-1317`), anomaly stops further retrieval (`1548-1550`).

4. **Runtime scope source differs (product-owner roots vs validator root)**  
   Evidence: API scope resolver (`1873-1928`) vs validator root option (`87-105`). This can materially alter `ScopeCount`, segment distribution, and retrieved work item population.

5. **Core OData request builder is shared; divergence is primarily orchestration/termination, not query syntax**  
   Evidence: both use same `RealODataRevisionTfsClient` and `ODataRevisionQueryBuilder` (`ApiServiceCollectionExtensions.cs:324`, `Program.cs:82`, query builder file).

---

## 8) NO FIXES YET — Recommended alignment strategy (not implemented yet)

- Consider reusing **exact same retrieval control loop** in API and validator (single continuous loop over continuation) while retaining API persistence boundaries.
- Re-evaluate API anomaly condition for `rawRevisionCount == 0 && hasMoreResults` to avoid premature aborts where continuation still progresses.
- Reassess whether API window segmentation should remain enabled for OData path when seek paging already provides deterministic forward progress.
- Ensure persistence/reporting counters distinguish:
  - retrieved raw/scoped rows
  - persisted in-window rows
  so retrieval diagnostics are not conflated with persistence gating.

