# Sprint Trends vs Revision Database — Engineering Report

**Generated:** 2026-02-10  
**Scope:** Code-as-is inspection of Sprint Trends data flow, revision DB schema, projection layer, and relations retrieval change impact.

---

## 1. Executive Summary

**Status: Partially Working — functional but with prerequisite gaps.**

The Sprint Trends page is fully wired from UI through API to a projection layer (`SprintTrendProjectionService`) that reads exclusively from the local revision database (`RevisionHeaders`, `RevisionFieldDeltas`). No direct TFS calls are made at query time. However, the projection depends on two **prerequisite stages that are not automatically chained in the sync pipeline**: (1) relation hydration via `RelationRevisionHydrator` must populate `RevisionRelationDeltas`, and (2) `WorkItemResolutionService.ResolveAllAsync()` must run to build the `ResolvedWorkItems` table. If either prerequisite has not run (or has incomplete data), sprint trends will silently return zeroes or partial results rather than erroring.

### Top 5 Risks / Blockers

| # | Risk | Code Location(s) |
|---|------|-------------------|
| 1 | **WorkItemResolutionService is not wired into the sync pipeline.** There is no sync stage that calls `ResolveAllAsync()`. Sprint trends depend on `ResolvedWorkItems` being populated, but this is never triggered automatically. | `SyncPipelineRunner.cs` (8 stages; none calls `WorkItemResolutionService`); `WorkItemResolutionService.cs` |
| 2 | **RelationRevisionHydrator runs during ingestion but relation hydration completeness is not guaranteed.** The hydrator uses per-item TFS calls (max 4 concurrent). If ingestion completes with a pagination anomaly or fallback, some work items may lack relation deltas, causing `BuildParentChainAsync()` in `WorkItemResolutionService` to produce orphans. | `RelationRevisionHydrator.cs:63-72`; `WorkItemResolutionService.cs:171-205` |
| 3 | **No revision-completeness check before computing projections.** `SprintTrendProjectionService.ComputeProjectionsAsync()` reads whatever is in `RevisionHeaders`; it never validates whether ingestion is complete or still running. | `SprintTrendProjectionService.cs:43-141` |
| 4 | **IncludedUpToRevisionId is set but never used for incremental computation.** The field is stored in `SprintMetricsProjectionEntity` but the service always recomputes from scratch. Incremental projection is not implemented. | `SprintTrendProjectionService.cs:241`; `SprintMetricsProjectionEntity.cs` |
| 5 | **FeatureProgressDto is defined but never populated.** The DTO exists in `SprintTrendDtos.cs:155-202` but no handler or service creates instances of it. Feature-level progress is not yet surfaced. | `PoTool.Shared/Metrics/SprintTrendDtos.cs:155-202` |

### Recommended Next 3 Actions

1. **Add a `WorkItemResolutionStage` to the sync pipeline** (between Stage 3 Revisions and Stage 6 Validations) that calls `WorkItemResolutionService.ResolveAllAsync()` after revision ingestion completes. Without this, the `ResolvedWorkItems` table may be stale or empty.
2. **Add a sprint-trend projection recompute step** to the sync pipeline (or to `MetricsComputeStage`) so that projections are refreshed after every sync, not only on user-triggered `recompute=true` requests.
3. **Validate revision ingestion status before projection computation.** If `RevisionIngestionWatermarkEntity.LastRunOutcome` is `Failed` or `IsInitialBackfillComplete` is `false`, the handler should warn the user or return a degraded-data flag.

---

## 2. Sprint Trends Page: What It Needs

### 2.1 Page Components and Routes

| Component | File | Route |
|-----------|------|-------|
| Sprint Trend page | `PoTool.Client/Pages/Home/SprintTrend.razor` | `@page "/home/sprint-trend"` |
| Trends workspace (navigation hub) | `PoTool.Client/Pages/Home/TrendsWorkspace.razor` | `/home/trends` |
| Route constant | `PoTool.Client/Models/WorkspaceRoutes.cs:109` | `SprintTrend = "/home/sprint-trend"` |

### 2.2 Backend Endpoint Called

| Endpoint | HTTP Method | Controller Action |
|----------|-------------|-------------------|
| `api/Metrics/sprint-trend` | `GET` | `MetricsController.GetSprintTrendMetrics()` |

**File:** `PoTool.Api/Controllers/MetricsController.cs:499-529`

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `productOwnerId` | `int` | Yes | Scopes to a ProductOwner |
| `sprintIds` | `int[]` | Yes (≥1) | Sprint IDs to compute trends for |
| `recompute` | `bool` | No (default `false`) | When `true`, recomputes projections from revision data |

**Client service:** `PoTool.Client/Services/SprintTrendService.cs:26-48`

### 2.3 DTOs Returned

| DTO | File | Key Fields |
|-----|------|------------|
| `GetSprintTrendMetricsResponse` | `PoTool.Shared/Metrics/SprintTrendDtos.cs:134-150` | `Success`, `ErrorMessage`, `Metrics` |
| `SprintTrendMetricsDto` | `SprintTrendDtos.cs:6-62` | `SprintId`, `SprintName`, `StartUtc`, `EndUtc`, `ProductMetrics`, `TotalPlannedCount/Effort`, `TotalWorkedCount/Effort`, `TotalBugsPlannedCount`, `TotalBugsWorkedCount` |
| `ProductSprintMetricsDto` | `SprintTrendDtos.cs:67-108` | `ProductId`, `ProductName`, `PlannedCount/Effort`, `WorkedCount/Effort`, `BugsPlannedCount`, `BugsWorkedCount` |
| `FeatureProgressDto` | `SprintTrendDtos.cs:155-202` | `FeatureId`, `FeatureTitle`, `EpicId`, `EpicTitle`, `ProductId`, `ProgressPercent`, `TotalEffort`, `DoneEffort`, `IsDone` — **defined but not yet populated** |

### 2.4 Trend Calculations

**Planned items:** Any work item where at least one revision has `IterationPath == Sprint.Path`.  
**Source:** `SprintTrendProjectionService.GetPlannedWorkItemsAsync()` — queries `RevisionHeaders` by `IterationPath`.

**Worked items:** Work items with a qualifying state-category change during the sprint date range, or with a late completion attributed to the sprint.  
**Source:** `SprintTrendProjectionService.GetWorkedWorkItemsAsync()` — queries `RevisionHeaders` joined with `FieldDeltas` for `System.State` changes.

**Activity detection rules** (`IsQualifyingActivity()` at `SprintTrendProjectionService.cs:332-391`):

| Work Item Type | Qualifying Activity |
|----------------|---------------------|
| Task | Any state-category change |
| PBI | InProgress → Done (New → InProgress does NOT count) |
| Bug | InProgress → Done, or Done → InProgress (reopened) |
| Feature / Epic | No direct activity tracking |

**Bugs** are tracked separately with `BugsPlannedCount` / `BugsWorkedCount`; they are excluded from `PlannedCount` / `WorkedCount`.

**Effort** is taken from the latest revision's `Effort` field (nullable double, rounded to int).

**Sprint** definition comes from `SprintEntity` (with `StartUtc`, `EndUtc`, `Path`); sprint is selected by the user in the UI.

### 2.5 Dependencies

| Dependency | How Used | Notes |
|------------|----------|-------|
| Work item hierarchy / relations | **Indirect** — via `ResolvedWorkItems` table (pre-computed by `WorkItemResolutionService`). Sprint Trends never queries relations directly. | Resolution depends on `RevisionRelationDeltas` being populated by `RelationRevisionHydrator`. |
| Iteration paths | Sprint matching: `RevisionHeaders.IterationPath == Sprint.Path` | Sprint entity paths must match TFS iteration paths exactly. |
| Team settings | Sprints are linked to teams, teams to products via `ProductTeamLinks`. | Used to scope sprints to product owners. |
| Revision history | **Primary data source.** All planned/worked calculations query `RevisionHeaders` and `RevisionFieldDeltas`. | No direct TFS calls at query time. |
| Cache vs live mode | Sprint Trends always reads from cached revision DB. The `recompute` flag recomputes projections from cached revisions, not from TFS. | There is no live-mode fallback. |

---

## 3. Revision Database: What It Delivers

### 3.1 Tables, Entities, and Indexes

| Table | Entity | File | Purpose |
|-------|--------|------|---------|
| `RevisionHeaders` | `RevisionHeaderEntity` | `PoTool.Api/Persistence/Entities/RevisionHeaderEntity.cs` | One row per work-item revision snapshot |
| `RevisionFieldDeltas` | `RevisionFieldDeltaEntity` | `PoTool.Api/Persistence/Entities/RevisionFieldDeltaEntity.cs` | Field-level changes per revision |
| `RevisionRelationDeltas` | `RevisionRelationDeltaEntity` | `PoTool.Api/Persistence/Entities/RevisionRelationDeltaEntity.cs` | Relation add/remove deltas per revision |
| `RevisionIngestionWatermarks` | `RevisionIngestionWatermarkEntity` | `PoTool.Api/Persistence/Entities/RevisionIngestionWatermarkEntity.cs` | Per-ProductOwner ingestion progress tracking |

**EF configuration:** `PoTool.Api/Persistence/PoToolDbContext.cs:545-646`

**Indexes on `RevisionHeaders`:**
- Unique: `(WorkItemId, RevisionNumber)`
- Non-unique: `WorkItemId`, `ChangedDate`, `IterationPath`, `WorkItemType`, `State`

**Indexes on `RevisionFieldDeltas`:**
- `RevisionHeaderId`, `FieldName`

**Indexes on `RevisionRelationDeltas`:**
- `RevisionHeaderId`, `TargetWorkItemId`

**Migrations:**

| Migration | File | Change |
|-----------|------|--------|
| Initial schema | `20260205163141_AddRevisionTrackingTables.cs` | Creates all four revision tables |
| Effort type fix | `20260209124731_UpdateRevisionEffortToDouble.cs` | Changed `Effort` from int to double |
| Run outcome field | `20260210134952_AddRevisionRunOutcome.cs` | Added `LastRunOutcome` to watermark |
| Fallback fields | `20260210212023_AddRevisionWatermarkFallbackFields.cs` | Added fallback tracking fields |

### 3.2 Columns Stored (RevisionHeaders)

| Column | Type | Populated From |
|--------|------|----------------|
| `WorkItemId` | int | TFS revision payload `id` |
| `RevisionNumber` | int | TFS `rev` field |
| `WorkItemType` | string | `System.WorkItemType` |
| `Title` | string | `System.Title` |
| `State` | string | `System.State` |
| `Reason` | string | `System.Reason` |
| `Effort` | double? | `Microsoft.VSTS.Scheduling.Effort` |
| `IterationPath` | string | `System.IterationPath` |
| `AreaPath` | string | `System.AreaPath` |
| `ChangedDate` | DateTimeOffset | `System.ChangedDate` |
| `CreatedDate` | DateTimeOffset | `System.CreatedDate` |
| `ClosedDate` | DateTimeOffset? | `Microsoft.VSTS.Common.ClosedDate` |
| `ChangedBy` | string | `System.ChangedBy` |
| `Tags` | string? | `System.Tags` |
| `Severity` | string? | `Microsoft.VSTS.Common.Severity` |
| `IngestedAt` | DateTimeOffset | Set to `UtcNow` at ingestion |

### 3.3 Ingestion Code Locations

| Stage | Service | File |
|-------|---------|------|
| Bulk revision ingestion | `RevisionIngestionService.IngestRevisionsAsync()` | `PoTool.Api/Services/RevisionIngestionService.cs` |
| Relation hydration (per-item) | `RelationRevisionHydrator.HydrateWorkItemAsync()` | `PoTool.Api/Services/RelationRevisionHydrator.cs` |
| Sync pipeline trigger | `RevisionSyncStage.ExecuteAsync()` | `PoTool.Api/Services/Sync/RevisionSyncStage.cs` |

### 3.4 Watermarks / Checkpoints

**Entity:** `RevisionIngestionWatermarkEntity` — one row per `ProductOwnerId`.

| Field | Purpose |
|-------|---------|
| `ContinuationToken` | TFS reporting API pagination token |
| `LastSyncStartDateTime` | Next incremental sync start time |
| `IsInitialBackfillComplete` | Whether historical backfill is done |
| `LastIngestionRevisionCount` | Count persisted in last run |
| `LastRunOutcome` | String: `CompletedNormally`, `CompletedWithPaginationAnomaly`, `CompletedWithFallback`, `Failed` |
| `FallbackUsedLastRun` | Boolean: whether per-item fallback was activated |
| `FallbackResumeIndex` | Resume point for interrupted fallback |
| `LastStableContinuationTokenHash` | 12-char hash of last known-good token |
| `LastStableChangedDateUtc` | Max ChangedDate from last successful persist |
| `LastErrorMessage` / `LastErrorAt` | Diagnostic fields |

### 3.5 Relations in Revision DB

**Yes, relations are stored** in `RevisionRelationDeltas` with columns: `ChangeType` (Added/Removed), `RelationType` (e.g., `System.LinkTypes.Hierarchy-Reverse`), `TargetWorkItemId`.

**How they are populated:** `RelationRevisionHydrator` fetches per-item revisions from TFS using `/_apis/wit/workItems/{id}/revisions?$expand=relations`, computes deltas between consecutive revisions, and persists them as `RevisionRelationDeltaEntity` records.

**Reliability/completeness:** Relation hydration runs with 4-concurrent per-item fetches. If ingestion completes with fallback or anomaly, some work items may have incomplete or missing relation deltas. There is no explicit completeness guarantee for relations.

### 3.6 Post-Processing / Projection Outputs

| Output Table | Entity | Service | Trigger |
|--------------|--------|---------|---------|
| `ResolvedWorkItems` | `ResolvedWorkItemEntity` | `WorkItemResolutionService.ResolveAllAsync()` | **Not wired into sync pipeline — manual/on-demand only** |
| `SprintMetricsProjections` | `SprintMetricsProjectionEntity` | `SprintTrendProjectionService.ComputeProjectionsAsync()` | On-demand via API `recompute=true` flag |

---

## 4. The Coupling: Projection / Query Path from Revisions to Sprint Trends

### 4.1 End-to-End Trace

```
[Client: SprintTrend.razor]
    └─ SprintTrendService.GetSprintTrendMetricsAsync(productOwnerId, sprintIds, recompute)
        └─ GET api/Metrics/sprint-trend?productOwnerId=X&sprintIds=1&sprintIds=2&recompute=false
            └─ MetricsController.GetSprintTrendMetrics()
                └─ Mediator.Send(GetSprintTrendMetricsQuery)
                    └─ GetSprintTrendMetricsQueryHandler.Handle()
                        ├─ IF recompute:
                        │   └─ SprintTrendProjectionService.ComputeProjectionsAsync()
                        │       ├─ READ Products WHERE ProductOwnerId = X
                        │       ├─ READ Sprints WHERE Id IN sprintIds
                        │       ├─ READ WorkItemStateClassifications
                        │       ├─ READ ResolvedWorkItems WHERE Resolved + matching products
                        │       ├─ READ RevisionHeaders (latest per work item)
                        │       └─ FOR EACH (sprint, product):
                        │           ├─ GetPlannedWorkItemsAsync() → RevisionHeaders by IterationPath
                        │           ├─ GetWorkedWorkItemsAsync() → RevisionHeaders + FieldDeltas in date range
                        │           └─ UPSERT SprintMetricsProjections
                        │
                        ├─ SprintTrendProjectionService.GetProjectionsAsync()
                        │   └─ READ SprintMetricsProjections WHERE matching sprint+product
                        │
                        ├─ READ Sprints (for display names)
                        ├─ READ Products (for display names)
                        └─ GROUP + MAP to SprintTrendMetricsDto[]
```

### 4.2 Projection Layer

**The projection layer exists and is functional.** It is implemented in two services:

#### SprintTrendProjectionService

| Aspect | Detail |
|--------|--------|
| **Location** | `PoTool.Api/Services/SprintTrendProjectionService.cs` |
| **Namespace** | `PoTool.Api.Services` |
| **Inputs** | `RevisionHeaders`, `RevisionFieldDeltas`, `ResolvedWorkItems`, `Products`, `Sprints`, `WorkItemStateClassifications` |
| **Outputs** | `SprintMetricsProjections` table (upserted) |
| **When it runs** | **On-demand only** — triggered by `GetSprintTrendMetricsQuery` with `Recompute = true`. Not triggered during sync pipeline. |
| **Incremental updates** | Not implemented. `IncludedUpToRevisionId` is stored but not used for delta computation. Full recompute on every call. |
| **Anomaly handling** | None. Silently skips work items with no revisions (`continue` on missing lookup). |

#### WorkItemResolutionService (prerequisite)

| Aspect | Detail |
|--------|--------|
| **Location** | `PoTool.Api/Services/WorkItemResolutionService.cs` |
| **Namespace** | `PoTool.Api.Services` |
| **Inputs** | `RevisionHeaders`, `RevisionRelationDeltas` (parent links), `Products` (root work item IDs), `Sprints` |
| **Outputs** | `ResolvedWorkItems` table (upserted) |
| **When it runs** | **Not wired into any pipeline.** Must be called manually or via an as-yet-unbuilt trigger. |
| **Incremental updates** | None. Resolves all work items from scratch each time. |
| **Anomaly handling** | Items without a reachable product root are marked `Orphan`. Circular references are detected and logged. |

### 4.3 What Happens Without the Projection

If `SprintMetricsProjections` is empty (no prior `recompute` call), the handler returns an empty `Metrics` list with `Success = true`. The UI shows no data. No error is surfaced to the user.

If `ResolvedWorkItems` is empty, `ComputeProjectionsAsync()` finds zero resolved work items, and all projections have `PlannedCount = 0`, `WorkedCount = 0`.

---

## 5. Relations Source-of-Truth Change Impact

### 5.1 Current Logic for Retrieving Relations

#### From revisions (current behavior for hierarchy resolution)

| Component | How Relations Are Retrieved |
|-----------|-----------------------------|
| **`RevisionIngestionService`** | Uses TFS reporting API (`/_apis/wit/reporting/workitemrevisions`) which does **not** support `$expand=relations`. Only field data is ingested. |
| **`RelationRevisionHydrator`** | Fetches per-item revisions (`/_apis/wit/workItems/{id}/revisions?$expand=relations`) and computes relation deltas by comparing consecutive revisions. Stores in `RevisionRelationDeltas`. |
| **`WorkItemResolutionService`** | Queries `RevisionRelationDeltas` for `System.LinkTypes.Hierarchy-Reverse` to build parent chains. This is the **only consumer** of `RevisionRelationDeltas`. |

#### From work items (current behavior for interactive use)

| Component | How Relations Are Retrieved |
|-----------|-----------------------------|
| **`RealTfsClient.GetWorkItemByIdAsync()`** | Fetches current relations via `/_apis/wit/workitemsbatch` with `Expand = "relations"`. Two-phase fetch (relations phase + fields phase). Stored as JSON in `WorkItemEntity.Relations`. |
| **`GetDependencyGraphQueryHandler`** | Reads `WorkItemDto.Relations` (deserialized from `WorkItemEntity.Relations` JSON) for dependency links. |

### 5.2 Code Still Expecting Relations in Revision Payloads or Revision DB

| File | Class / Method | What It Expects | Status |
|------|---------------|-----------------|--------|
| `RelationRevisionHydrator.cs` | `HydrateWorkItemAsync()` | Relations from per-item revisions endpoint with `$expand=relations` | ✅ Works — uses per-item API, not reporting API |
| `WorkItemResolutionService.cs:171-205` | `BuildParentChainAsync()` | `RevisionRelationDeltas` table populated with `Hierarchy-Reverse` links | ⚠️ **Depends on hydrator having run for all relevant work items** |
| `RealRevisionTfsClient.cs:248-257` | Validation in `GetReportingRevisionsAsync()` | Explicitly rejects `$expand=relations` on reporting endpoint | ✅ Correct guard |
| `RealRevisionTfsClient.cs:301` | `BuildWorkItemRevisionsUrl()` | `$expand=relations` on per-item endpoint | ✅ Per-item endpoint supports relations |

### 5.3 Code Using Current Work Item Relations (not revision-based)

| File | Class / Method | What It Uses |
|------|---------------|--------------|
| `RealTfsClient.WorkItems.cs:208-252` | `GetWorkItemByIdAsync()` | `WorkItemBatchRequest { Expand = "relations" }` — fetches current snapshot |
| `CachedWorkItemReadProvider.cs:154-164` | Deserialization | `WorkItemEntity.Relations` JSON → `WorkItemDto.Relations` list |
| `GetDependencyGraphQueryHandler.cs:95-130` | Dependency graph | `WorkItemDto.Relations` for `Dependency-Forward/Reverse` links |

### 5.4 Impact if "Relations from Work Items" Replaces "Relations from Revisions"

If hierarchy resolution switches from revision relation deltas to current work item relations, the following code must change:

| File | What Needs to Change | Impact |
|------|---------------------|--------|
| `WorkItemResolutionService.cs:171-205` | `BuildParentChainAsync()` currently queries `RevisionRelationDeltas`. Would need to query `WorkItemEntity.Relations` (JSON) or fetch from TFS instead. | **High** — core resolution logic change |
| `RelationRevisionHydrator.cs` | Would become **unused** if we no longer need revision-level relation deltas for hierarchy resolution. | **Medium** — could be removed or repurposed |
| `RevisionRelationDeltaEntity.cs` + EF config | Table/entity may become unused if no consumers remain. | **Low** — can be kept for audit trail |
| `SprintTrendProjectionService.cs` | **No change needed** — does not use relations directly; depends only on `ResolvedWorkItems` output. | **None** |
| `GetSprintTrendMetricsQueryHandler.cs` | **No change needed** — only reads projections. | **None** |

### 5.5 Delta List: "Needs Update"

| # | File | Notes |
|---|------|-------|
| 1 | `PoTool.Api/Services/WorkItemResolutionService.cs` | `BuildParentChainAsync()` must be rewritten to use current work item relations (from `WorkItemEntity` or live TFS) instead of `RevisionRelationDeltas`. |
| 2 | `PoTool.Api/Services/RelationRevisionHydrator.cs` | Evaluate whether to keep (for history) or remove (if hierarchy only needs current state). |
| 3 | `PoTool.Api/Persistence/Entities/RevisionRelationDeltaEntity.cs` | Keep for now (no urgent change), but document as potentially unused. |
| 4 | `PoTool.Api/Repositories/CacheStateRepository.cs` | Cleanup code still references `RevisionRelationDeltas.ExecuteDeleteAsync()`. Must be kept in sync with any schema change. |

---

## 6. Data Completeness & Trust Notes

### 6.1 Where We Rely on Historical Completeness

| Area | Completeness Dependency | Risk if Incomplete |
|------|------------------------|--------------------|
| **Planned items** | All revisions with `IterationPath == Sprint.Path` must be in `RevisionHeaders`. If a revision is missing, the item won't appear as planned. | Understated planned count. |
| **Worked items** | All state-change revisions during the sprint date range must be in `RevisionHeaders` + `RevisionFieldDeltas`. | Understated worked count. |
| **Parent chain resolution** | `RevisionRelationDeltas` must contain all add/remove events for `Hierarchy-Reverse` links. Missing deltas = broken chain = orphan. | Items classified as orphans; excluded from sprint trends entirely. |
| **Effort** | Latest revision's `Effort` field is used. If the latest revision is not ingested, effort is stale. | Inaccurate effort totals. |

### 6.2 What Happens When Ingestion Is `CompletedWithPaginationAnomaly` or `CompletedWithFallback`

| Outcome | Effect on Sprint Trends |
|---------|------------------------|
| `CompletedNormally` | Full data available. Projections are accurate. |
| `CompletedWithPaginationAnomaly` | Some revisions in the anomaly window may be missing. Sprint trends may undercount planned/worked items for affected sprints. **No warning surfaced to the user.** |
| `CompletedWithFallback` | Per-item fallback was used. Data should be complete for the fallback scope, but may be slower and the watermark may not advance fully. |
| `Failed` | Ingestion did not complete. Revision data is stale. Sprint trends use whatever was previously ingested. **No warning surfaced to the user.** |

### 6.3 Graceful Degradation vs Failure

| Component | Behavior on Missing Data |
|-----------|--------------------------|
| `SprintTrendProjectionService` | **Degrades silently.** Skips work items without revisions (`continue` on lookup miss). Returns zeroes instead of errors. |
| `GetSprintTrendMetricsQueryHandler` | **Degrades silently.** Returns empty `Metrics` list with `Success = true` when no projections exist. |
| `WorkItemResolutionService` | **Degrades with orphans.** Items without reachable product roots are marked `Orphan` and excluded from all trend calculations. Logs warnings for circular references. |
| `SprintTrend.razor` (UI) | Shows empty state or zero metrics. Does not display a warning about incomplete data. |

---

## 7. Appendix: Trace Tables

### 7.1 UI Component → API Endpoint → DTO → Backend Service

| UI Component | API Endpoint | DTO(s) | Backend Service(s) |
|-------------|-------------|--------|-------------------|
| `SprintTrend.razor` | `GET api/Metrics/sprint-trend` | `GetSprintTrendMetricsResponse` → `SprintTrendMetricsDto[]` → `ProductSprintMetricsDto[]` | `GetSprintTrendMetricsQueryHandler` → `SprintTrendProjectionService` |
| `TrendsWorkspace.razor` (navigation only) | — | — | — |
| *(planned)* Feature progress table | *(endpoint TBD)* | `FeatureProgressDto` | *(not yet implemented)* |

### 7.2 Backend Service → DB Tables / Entities → Key Queries

| Backend Service | DB Tables Read | Key Queries |
|----------------|----------------|-------------|
| `SprintTrendProjectionService.ComputeProjectionsAsync()` | `Products`, `Sprints`, `WorkItemStateClassifications`, `ResolvedWorkItems`, `RevisionHeaders`, `RevisionFieldDeltas`, `SprintMetricsProjections` | Latest revision per work item; revisions by IterationPath; revisions by ChangedDate range with FieldDeltas join; upsert projections |
| `SprintTrendProjectionService.GetProjectionsAsync()` | `Products`, `SprintMetricsProjections` | Projections WHERE sprintIds + productIds with Include(Sprint, Product) |
| `GetSprintTrendMetricsQueryHandler` | `Sprints`, `Products`, `SprintMetricsProjections` (via projection service) | Sprint/Product lookups by ID for display names |
| `WorkItemResolutionService.ResolveAllAsync()` | `Products`, `Sprints` (with Team + ProductTeamLinks), `RevisionHeaders`, `RevisionRelationDeltas`, `ResolvedWorkItems` | Latest revision per work item; relation deltas for Hierarchy-Reverse; upsert resolved items |

### 7.3 Projection Job → Input Tables → Output Tables

| Job / Trigger | Input Tables | Output Tables | Scheduling |
|-------------|-------------|---------------|------------|
| `SprintTrendProjectionService.ComputeProjectionsAsync()` | `RevisionHeaders`, `RevisionFieldDeltas`, `ResolvedWorkItems`, `Products`, `Sprints`, `WorkItemStateClassifications` | `SprintMetricsProjections` | On-demand (API `recompute=true`) |
| `WorkItemResolutionService.ResolveAllAsync()` | `RevisionHeaders`, `RevisionRelationDeltas`, `Products`, `Sprints` | `ResolvedWorkItems` | **Not scheduled** — must be called manually |
| `RevisionIngestionService.IngestRevisionsAsync()` | TFS reporting API (external) | `RevisionHeaders`, `RevisionFieldDeltas`, `RevisionIngestionWatermarks` | Sync pipeline Stage 3 (`RevisionSyncStage`) |
| `RelationRevisionHydrator.HydrateWorkItemAsync()` | TFS per-item revisions API (external) | `RevisionRelationDeltas` | Called by `RevisionIngestionService` during ingestion |

### 7.4 Sync Pipeline Stages (for context)

| Stage | Class | What It Does |
|-------|-------|-------------|
| 1 | `WorkItemSyncStage` | Sync work items from TFS |
| 2 | `TeamSprintSyncStage` | Sync sprint definitions |
| 3 | `RevisionSyncStage` | Ingest revisions + hydrate relations |
| 4 | `PullRequestSyncStage` | Sync PR data |
| 5 | `PipelineSyncStage` | Sync pipeline runs |
| 6 | `ValidationComputeStage` | Compute validation metrics |
| 7 | `MetricsComputeStage` | Compute general cached metrics (velocity, PR throughput, pipeline success) — **does NOT compute sprint trend projections** |
| 8 | `FinalizeCacheStage` | Finalize sync state |

---

*End of report.*
