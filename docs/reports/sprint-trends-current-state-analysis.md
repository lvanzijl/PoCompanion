# Sprint Trends — Current State Analysis

**Generated:** 2026-02-25  
**Scope:** Code-as-is inspection of the Trends → Sprint Trends feature, post-revision-removal.

---

## 1. Functional Description

### What the Sprint Trends feature is supposed to do

Sprint Trends is a historical analytics page (route: `/home/sprint-trend`) that lets a Product Owner look back over a configurable range of past sprints and compare what was **planned** for those sprints against what was actually **worked**.

The page is reached from the **Trends (Past)** workspace (`/home/trends`).

### Questions it tries to answer for an end user

- How many work items were planned vs worked across a selected sprint range?
- How many story-point-equivalent effort points were planned vs worked?
- How many bugs were planned vs worked separately from regular PBIs/tasks?
- How did each individual **Product** perform across the sprint range (planned vs worked progress bar)?
- What is the overall completion trend across the selected sprints?
- Which Features and Epics are progressing, and by how much? (Feature Progress table — partially wired)

### Metrics shown

| Metric | Description |
|--------|-------------|
| Planned Items | Count of work items whose iteration path matched the sprint during the sprint |
| Planned Effort | Sum of effort (story points) for planned items |
| Worked Items | Count of work items with a qualifying state-category change during the sprint date range |
| Worked Effort | Sum of effort for worked items |
| Bugs Planned | Count of Bug-type work items planned in the sprint |
| Bugs Worked | Count of Bug-type work items with qualifying activity in the sprint |
| Per-Product progress | Planned vs Worked count + progress bar per Product |
| Feature Progress | Epic / Feature / progress % table (hardcoded placeholder in current code) |

### Time range

The user selects a **Team**, a **From Sprint**, and a **To Sprint**. All sprints in the ID range between the two boundaries are included. Metrics are aggregated across the entire range and also broken down per sprint for display. This is a multi-sprint, historical comparison view.

### Hierarchy level

The projection is computed at the **work item** level (primarily PBIs, Tasks, and Bugs), grouped by **Product** and **Sprint**. Bugs are tracked in a separate counter. Feature/Epic level progress is shown as a separate section but is currently hardcoded as placeholder data.

### Assumptions about underlying data

- Work items are synced and available in the local cache (`WorkItems` table).
- Sprints are synced with known `StartUtc`, `EndUtc`, and `Path` for each team.
- Activity events (field-level state changes on work items) are available and attributed to the correct sprint by iteration path and timestamp.
- Work items are resolved into a Product hierarchy so that each work item can be attributed to its owning Product.

### Trend logic implied

- **Planned** = work item had its iteration path set to the sprint's path at some point during or before the sprint.
- **Worked** = work item had a qualifying state-category transition during the sprint's date range:
  - *Task*: any state-category change
  - *PBI*: InProgress → Done only (New → InProgress does NOT count)
  - *Bug*: InProgress → Done, or Done → InProgress (reopened)
- Bugs are excluded from the main Planned/Worked counts and tracked separately in `BugsPlannedCount` / `BugsWorkedCount`.
- Effort is taken from the work item's latest known effort value (nullable integer, in story points).

---

## 2. Technical Wiring Analysis

### End-to-end data flow

```
[Client: SprintTrend.razor]
    └─ SprintTrendService.GetSprintTrendMetricsAsync(productOwnerId, sprintIds, recompute)
        └─ GET api/Metrics/sprint-trend?productOwnerId=X&sprintIds=...&recompute=false
            └─ MetricsController.GetSprintTrendMetrics()
                └─ Mediator.Send(GetSprintTrendMetricsQuery)
                    └─ GetSprintTrendMetricsQueryHandler.Handle()
                        ├─ SprintTrendProjectionService.GetProjectionsAsync()
                        │   └─ READ SprintMetricsProjections WHERE sprintIds + productIds
                        │       (currently always returns empty list)
                        ├─ If empty → SprintTrendProjectionService.ComputeProjectionsAsync()
                        │   └─ STUB: returns Array.Empty<>() immediately
                        │       (REPLACE_WITH_ACTIVITY_SOURCE marker)
                        ├─ READ Sprints for display names
                        ├─ READ Products for display names
                        └─ GROUP + MAP → SprintTrendMetricsDto[] (empty list)
```

### Classes and methods involved

| Class / File | Method | Role |
|---|---|---|
| `SprintTrend.razor` | `LoadMetricsAsync()` | UI — calls service, aggregates metrics into display state |
| `SprintTrendService` (`PoTool.Client/Services/SprintTrendService.cs`) | `GetSprintTrendMetricsAsync()` | HTTP client wrapper — calls `api/Metrics/sprint-trend` |
| `MetricsController` (`PoTool.Api/Controllers/MetricsController.cs`) | `GetSprintTrendMetrics()` | API endpoint — dispatches `GetSprintTrendMetricsQuery` |
| `GetSprintTrendMetricsQuery` (`PoTool.Core/Metrics/Queries/`) | — | Mediator query record |
| `GetSprintTrendMetricsQueryHandler` (`PoTool.Api/Handlers/Metrics/`) | `Handle()` | Orchestrates projection reads; falls back to recompute if cache empty |
| `SprintTrendProjectionService` (`PoTool.Api/Services/`) | `GetProjectionsAsync()` | Reads `SprintMetricsProjections` table |
| `SprintTrendProjectionService` | `ComputeProjectionsAsync()` | **Stubbed** — should compute projections from activity source |
| `SprintTrendProjectionSyncStage` (`PoTool.Api/Services/Sync/`) | `ExecuteAsync()` | Sync pipeline Stage 6 — calls `ComputeProjectionsAsync()` during sync |
| `ActivityEventIngestionService` (`PoTool.Api/Services/`) | `IngestAsync()` | Ingests activity events from TFS into `ActivityEventLedgerEntries` |
| `ActivityIngestionSyncStage` (`PoTool.Api/Services/Sync/`) | `ExecuteAsync()` | Sync pipeline Stage 2 — calls `ActivityEventIngestionService.IngestAsync()` |
| `NoOpActivityEventSource` (`PoTool.Api/Services/`) | `GetActivityEventsAsync()` | Registered as `IActivityEventSource` — always returns empty |

### Data sources used

| Table / Source | Status | Notes |
|---|---|---|
| `SprintMetricsProjections` | ✅ **Table exists** | Populated by `ComputeProjectionsAsync()` — but currently never populated (stub) |
| `ActivityEventLedgerEntries` | ✅ **Table exists, data being ingested** | Populated by `ActivityEventIngestionService` during sync Stage 2 |
| `WorkItems` | ✅ Populated | Used by `ActivityEventIngestionService` for type/parent/iteration lookups |
| `Sprints` | ✅ Populated | Used for sprint names and date ranges |
| `Products` | ✅ Populated | Used for product attribution |
| `ResolvedWorkItems` | ✅ **Table exists, populated by sync Stage 5** | Used to map work items to Products — now wired in pipeline |
| `RevisionHeaders` | ❌ **Table dropped** (migration `20260223221758_RemoveRevisionPersistenceSchema`) | Was the previous primary data source |
| `RevisionFieldDeltas` | ❌ **Table dropped** (same migration) | Was the previous source for state-change detection |
| `RevisionRelationDeltas` | ❌ **Table dropped** (same migration) | Was the previous source for parent-chain resolution |
| `RevisionIngestionWatermarks` | ❌ **Table dropped** (same migration) | Was the ingestion progress tracker |

### Sync pipeline (current — 11 stages)

| # | Stage Class | What It Does | Relevant to Sprint Trends? |
|---|---|---|---|
| 1 | `WorkItemSyncStage` | Syncs work items from TFS | Indirectly — populates `WorkItems` table |
| 2 | `ActivityIngestionSyncStage` | Ingests activity events into `ActivityEventLedgerEntries` | **Yes** — this is the intended data source |
| 3 | `TeamSprintSyncStage` | Syncs sprint definitions | Yes — sprint metadata needed |
| 4 | `WorkItemRelationshipSnapshotStage` | Snapshots work item relationships | Yes — feeds resolution stage |
| 5 | `WorkItemResolutionSyncStage` | Resolves work items into product hierarchy (`ResolvedWorkItems`) | **Yes** — required by projection |
| 6 | `SprintTrendProjectionSyncStage` | Calls `ComputeProjectionsAsync()` to fill `SprintMetricsProjections` | **Yes** — this is the direct sprint trends stage |
| 7 | `PullRequestSyncStage` | Syncs pull requests | No |
| 8 | `PipelineSyncStage` | Syncs pipeline runs | No |
| 9 | `ValidationComputeStage` | Computes validation metrics | No |
| 10 | `MetricsComputeStage` | Computes general cached metrics | No |
| 11 | `FinalizeCacheStage` | Finalizes sync state | No |

### Dependencies

- **No OData revision ingestion dependency.** Revision tables (`RevisionHeaders`, `RevisionFieldDeltas`, `RevisionRelationDeltas`) and `IWorkItemRevisionSource` / `RealODataRevisionTfsClient` have all been removed.
- **No `WorkItemRevisions` table.** Dropped in migration `20260223221758_RemoveRevisionPersistenceSchema`.
- **No cached revision projections from revision data.** `SprintMetricsProjections` cannot be populated because `ComputeProjectionsAsync()` is stubbed.
- **`IActivityEventSource` is registered as `NoOpActivityEventSource`** — all callers of this interface receive an empty list.
- `SprintMetricsProjections` table is present in the schema and the EF model (`PoToolDbContext.SprintMetricsProjections`).
- `ActivityEventLedgerEntries` table is present and data is actually being written to it by `ActivityEventIngestionService` during sync.

### Assumptions that must be true for non-empty results

1. `SprintTrendProjectionService.ComputeProjectionsAsync()` must produce actual projections (currently it does not).
2. `SprintMetricsProjections` must contain rows matching the requested `sprintIds` and the products owned by the requesting profile.
3. At minimum one sprint must be selected by the user (from and to sprint IDs must both be set).
4. The user must have an active profile with at least one product configured.

---

## 3. Why It Returns Empty Now

### Root cause: `ComputeProjectionsAsync()` is a no-op stub

`SprintTrendProjectionService.ComputeProjectionsAsync()` (file: `PoTool.Api/Services/SprintTrendProjectionService.cs`, lines 22–32) was gutted when the OData revision pipeline was removed and replaced with this body:

```csharp
// REPLACE_WITH_ACTIVITY_SOURCE: compute sprint trend metrics from activity events.
return Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());
```

All three parameters (`productOwnerId`, `sprintIds`, `cancellationToken`) are explicitly discarded. The method does nothing and returns an empty list unconditionally.

### Broken dependency chain — step by step

1. **Sync runs** → Stage 6 (`SprintTrendProjectionSyncStage`) calls `ComputeProjectionsAsync()`.
2. **`ComputeProjectionsAsync()` returns `Array.Empty<>()`** → `SprintMetricsProjections` table is never written to.
3. **User loads Sprint Trends page** → `GetSprintTrendMetricsQueryHandler` calls `GetProjectionsAsync()`.
4. **`GetProjectionsAsync()` queries `SprintMetricsProjections`** → table is empty → returns empty list.
5. **Handler detects empty list** → triggers `ComputeProjectionsAsync()` again as fallback → same stub returns empty.
6. **Handler returns `GetSprintTrendMetricsResponse { Success = true, Metrics = [] }`** → empty list, no error surfaced.
7. **UI receives empty metrics list** → `_plannedCount`, `_workedCount`, etc. remain 0 → page shows zeroes.

### What the previous implementation relied on

The old `ComputeProjectionsAsync()` (described in `docs/reports/sprint-trends-vs-revisions-report.md`, generated 2026-02-10) queried these tables directly:
- `RevisionHeaders` — one row per work-item revision snapshot (iteration path, state, effort, dates)
- `RevisionFieldDeltas` — field-level change records for state-change detection
- `RevisionRelationDeltas` — hierarchy links built by `RelationRevisionHydrator`
- `ResolvedWorkItems` — product-hierarchy resolution output

All four revision tables were **dropped** by migration `20260223221758_RemoveRevisionPersistenceSchema` (applied 2026-02-23).

### What was removed

| Artifact | Removal event |
|---|---|
| `RevisionHeaders` table | Dropped by `20260223221758_RemoveRevisionPersistenceSchema` |
| `RevisionFieldDeltas` table | Dropped by same migration |
| `RevisionRelationDeltas` table | Dropped by same migration |
| `RevisionIngestionWatermarks` table | Dropped by same migration |
| `RevisionSource` column on `TfsConfigs` | Dropped by same migration |
| `RevisionSourceOverride` column on `Profiles` | Dropped by same migration |
| `IWorkItemRevisionSource` interface | Deleted |
| `RevisionModels` (OData DTOs) | Deleted |
| `RealODataRevisionTfsClient` | Deleted |
| `ODataRevisionQueryBuilder` | Deleted |
| Logic body of `ComputeProjectionsAsync()` | Replaced with no-op stub |

### What exists but is not yet connected

| Artifact | Status |
|---|---|
| `ActivityEventLedgerEntries` table | ✅ Schema exists; data **is** being populated by `ActivityEventIngestionService` during sync Stage 2 |
| `IActivityEventSource` interface | ✅ Defined in `PoTool.Core/Contracts/IActivityEventSource.cs` |
| `NoOpActivityEventSource` | ✅ Registered as the `IActivityEventSource` implementation — returns `Array.Empty<ActivityEvent>()` unconditionally |
| `SprintMetricsProjections` table | ✅ Schema exists; **always empty** because `ComputeProjectionsAsync()` is stubbed |
| `SprintTrendProjectionSyncStage` | ✅ Wired in pipeline (Stage 6); calls `ComputeProjectionsAsync()` — but receives empty result from stub |

### Summary

Sprint Trends returns empty because the **computation step** (`ComputeProjectionsAsync()`) is an explicit placeholder stub with marker comment `REPLACE_WITH_ACTIVITY_SOURCE`. The activity event data needed for the computation is being collected (`ActivityEventLedgerEntries` is populated by sync), but no implementation has yet been written to read from that table and produce sprint metrics projections. Until `ComputeProjectionsAsync()` is implemented using `ActivityEventLedgerEntries` as its source, `SprintMetricsProjections` will remain empty and the page will show all-zero metrics.
