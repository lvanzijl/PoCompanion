# Sprint Trends — Current State Analysis

**Updated:** 2026-02-25  
**Scope:** Code-as-is inspection of the Trends → Sprint Trends feature, post activity-event implementation.

---

## 1. Functional Description

### What the Sprint Trends feature does

Sprint Trends is a historical analytics page (route: `/home/sprint-trend`) that lets a Product Owner look back over a configurable range of past sprints and compare what was **planned** for those sprints against what was actually **worked**.

The page is reached from the **Trends (Past)** workspace (`/home/trends`).

### Questions it answers for an end user

- How many PBIs were completed and how much effort was delivered in a sprint?
- What is the feature/epic progression delta for a sprint?
- How many bugs were created, worked on, and closed?
- How did each individual **Product** perform across the sprint range?
- What is the overall completion trend across up to 10 past sprints? (advanced mode)
- Which Features and Epics are progressing, and by how much? (Feature/Epic Progress tables)
- Is the displayed data potentially stale? (staleness detection)

### Metrics shown

| Metric | Description |
|--------|-------------|
| Completed PBIs | Count of PBIs that transitioned to Done during the sprint |
| Completed Effort | Sum of effort (story points) for completed PBIs |
| Progression Delta | Effort-weighted feature completion percentage change |
| Bugs Created | Count of Bug-type work items created during the sprint |
| Bugs Worked | Count of Bug-type work items with child task state changes during the sprint |
| Bugs Closed | Count of Bug-type work items that transitioned to Done/Closed |
| Missing Effort | Count of PBIs with null effort (triggers approximate data warning) |
| Per-Product breakdown | All metrics above broken down by Product |
| Feature Progress | Feature title, epic parent, progress % (effort-weighted PBI completion, capped at 90% unless Done) |
| Epic Progress | Epic title, progress % (effort-weighted child Feature completion), feature count |
| Trend Charts | 10-sprint graphs for progression, PBI count, PBI effort, bug trend (advanced mode) |

### Time range

Single sprint mode: user navigates forward/backward through sprints with arrow buttons.
Advanced mode: shows up to 10 consecutive sprints ending at the selected sprint, with trend charts.

### Hierarchy level

The projection is computed at the **work item** level (primarily PBIs, Tasks, and Bugs), grouped by **Product** and **Sprint**. Bugs are tracked in separate counters. Feature and Epic progress are computed from the resolved work item hierarchy.

### Assumptions about underlying data

- Work items are synced and available in the local cache (`WorkItems` table).
- Sprints are synced with known `StartUtc`, `EndUtc`, and `Path` for each team.
- Activity events (field-level state changes on work items) are ingested into `ActivityEventLedgerEntries`.
- Work items are resolved into a Product hierarchy via `ResolvedWorkItems` table (populated by `WorkItemResolutionService`).

### Activity-based metrics logic

- **Planned** = work item resolved to the sprint via its iteration path (matched through `ResolvedWorkItems.ResolvedSprintId`).
- **Completed PBI** = PBI with a `System.State` activity event transitioning to `Done` during the sprint date range.
- **Worked** = work item with any activity event during the sprint, or parent item with child activity (activity bubbles up).
- **Bug Created** = bug with `CreatedDate` within the sprint date range.
- **Bug Worked** = bug with a child task that had a `System.State` change during the sprint.
- **Bug Closed** = bug with a `System.State` transition to `Done` or `Closed` during the sprint.
- **Progression Delta** = average effort-weighted feature completion % across features with PBI activity in the sprint.
- **Feature Progress** = effort-weighted PBI completion; capped at 90% unless Feature.State == Done (then 100%).
- **Epic Progress** = effort-weighted child Feature completion; same 90% cap rule.
- **Missing Effort Approximation** = when PBIs have null effort, sibling-average is used and `IsApproximate` flag is set.

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
                        ├─ IF recompute:
                        │   └─ SprintTrendProjectionService.ComputeProjectionsAsync()
                        │       ├─ READ Products WHERE ProductOwnerId = X
                        │       ├─ READ Sprints WHERE Id IN sprintIds
                        │       ├─ READ ResolvedWorkItems WHERE matching products
                        │       ├─ READ WorkItems (by resolved IDs)
                        │       ├─ BATCH-LOAD ActivityEventLedgerEntries for full date range
                        │       ├─ BATCH-LOAD existing SprintMetricsProjections
                        │       └─ FOR EACH (sprint, product):
                        │           ├─ Filter activity events to sprint date range
                        │           ├─ Compute planned/completed/worked/bug metrics
                        │           └─ UPSERT SprintMetricsProjections
                        │
                        ├─ ELSE: SprintTrendProjectionService.GetProjectionsAsync()
                        │   └─ READ SprintMetricsProjections WHERE matching sprint+product
                        │   └─ If empty → fallback to ComputeProjectionsAsync()
                        │
                        ├─ ComputeFeatureProgressAsync() → FeatureProgressDto[]
                        ├─ ComputeEpicProgressAsync() → EpicProgressDto[]
                        ├─ Detect staleness (ActivityEventWatermark > SprintTrendProjectionAsOfUtc)
                        ├─ READ Sprints (for display names)
                        ├─ READ Products (for display names)
                        └─ GROUP + MAP → GetSprintTrendMetricsResponse
```

### Classes and methods involved

| Class / File | Method | Role |
|---|---|---|
| `SprintTrend.razor` | `LoadMetricsAsync()` | UI — calls service, aggregates metrics into display state |
| `SprintTrend.razor` | `RecomputeMetricsAsync()` | UI — recomputes projections on user request |
| `SprintTrendService` (`PoTool.Client/Services/`) | `GetSprintTrendMetricsAsync()` | HTTP client wrapper — calls `api/Metrics/sprint-trend` |
| `MetricsController` (`PoTool.Api/Controllers/`) | `GetSprintTrendMetrics()` | API endpoint — dispatches `GetSprintTrendMetricsQuery` |
| `GetSprintTrendMetricsQueryHandler` (`PoTool.Api/Handlers/Metrics/`) | `Handle()` | Orchestrates projection reads, feature/epic progress, staleness detection |
| `SprintTrendProjectionService` (`PoTool.Api/Services/`) | `GetProjectionsAsync()` | Reads cached `SprintMetricsProjections` table |
| `SprintTrendProjectionService` | `ComputeProjectionsAsync()` | ✅ Computes projections from `ActivityEventLedgerEntries` + `ResolvedWorkItems` |
| `SprintTrendProjectionService` | `ComputeFeatureProgressAsync()` | Computes feature-level progress from resolved hierarchy |
| `SprintTrendProjectionService` | `ComputeEpicProgressAsync()` | Computes epic-level progress from feature progress |
| `SprintTrendProjectionSyncStage` (`PoTool.Api/Services/Sync/`) | `ExecuteAsync()` | Sync pipeline Stage 5 — calls `ComputeProjectionsAsync()` during sync |
| `WorkItemResolutionService` (`PoTool.Api/Services/`) | `ResolveAllAsync()` | Resolves work items into product hierarchy via `ParentTfsId` walk |
| `WorkItemResolutionSyncStage` (`PoTool.Api/Services/Sync/`) | `ExecuteAsync()` | Sync pipeline Stage 4 — calls `ResolveAllAsync()` during sync |
| `ActivityEventIngestionService` (`PoTool.Api/Services/`) | `IngestAsync()` | Ingests activity events from TFS into `ActivityEventLedgerEntries` |
| `ActivityIngestionSyncStage` (`PoTool.Api/Services/Sync/`) | `ExecuteAsync()` | Sync pipeline Stage 2 — calls `ActivityEventIngestionService.IngestAsync()` |
| `LedgerActivityEventSource` (`PoTool.Api/Services/`) | `GetActivityEventsAsync()` | Reads from `ActivityEventLedgerEntries` — registered as `IActivityEventSource` |

### Data sources used

| Table / Source | Status | Notes |
|---|---|---|
| `ActivityEventLedgerEntries` | ✅ Populated | Primary data source — field-level state changes ingested during sync Stage 2 |
| `ResolvedWorkItems` | ✅ Populated | Product-hierarchy resolution output — populated during sync Stage 4 |
| `SprintMetricsProjections` | ✅ Populated | Pre-computed metrics — populated during sync Stage 5 and on-demand recompute |
| `WorkItems` | ✅ Populated | Work item snapshots — effort, state, type, parent chain |
| `Sprints` | ✅ Populated | Sprint metadata — names, date ranges, iteration paths |
| `Products` | ✅ Populated | Product metadata — names, backlog root work item IDs |
| `ProductOwnerCacheStates` | ✅ Populated | Cache watermarks for staleness detection |

### Sync pipeline (current — 11 stages)

| # | Stage Class | What It Does | Relevant to Sprint Trends? |
|---|---|---|---|
| 1 | `WorkItemSyncStage` | Syncs work items from TFS | Indirectly — populates `WorkItems` table |
| 2 | `ActivityIngestionSyncStage` | Ingests activity events into `ActivityEventLedgerEntries` | **Yes** — primary data source |
| 3 | `TeamSprintSyncStage` | Syncs sprint definitions | Yes — sprint metadata needed |
| 4 | `WorkItemRelationshipSnapshotStage` | Snapshots work item relationships | Yes — feeds resolution stage |
| 5 | `WorkItemResolutionSyncStage` | Resolves work items into product hierarchy (`ResolvedWorkItems`) | **Yes** — required by projection |
| 6 | `SprintTrendProjectionSyncStage` | Calls `ComputeProjectionsAsync()` to fill `SprintMetricsProjections` | **Yes** — produces the sprint trend data |
| 7 | `PullRequestSyncStage` | Syncs pull requests | No |
| 8 | `PipelineSyncStage` | Syncs pipeline runs | No |
| 9 | `ValidationComputeStage` | Computes validation metrics | No |
| 10 | `MetricsComputeStage` | Computes general cached metrics | No |
| 11 | `FinalizeCacheStage` | Finalizes sync state | No |

### Dependencies

- **No OData revision ingestion dependency.** Revision tables and `IWorkItemRevisionSource` / `RealODataRevisionTfsClient` have all been removed.
- **`IActivityEventSource` is registered as `LedgerActivityEventSource`** — reads from `ActivityEventLedgerEntries`.
- `SprintMetricsProjections` are populated during sync (Stage 6) and can be recomputed on-demand via the UI recompute button.
- `ResolvedWorkItems` are populated during sync (Stage 4) by `WorkItemResolutionService` using `ParentTfsId` hierarchy walk.
- Staleness is detected by comparing `ProductOwnerCacheStates.ActivityEventWatermark` with `SprintTrendProjectionAsOfUtc`.

### Requirements for non-empty results

1. At least one sync must have completed (activity events ingested, work items resolved, projections computed).
2. `SprintMetricsProjections` must contain rows matching the requested `sprintIds` and the products owned by the requesting profile.
3. At minimum one sprint must be selected by the user.
4. The user must have an active profile with at least one product configured.

---

## 3. Implementation Status

### ✅ Fully Implemented

| Component | Status |
|---|---|
| `ComputeProjectionsAsync()` | ✅ Implemented — reads `ActivityEventLedgerEntries`, `ResolvedWorkItems`, `WorkItems`; computes all metrics; upserts `SprintMetricsProjections` |
| `LedgerActivityEventSource` | ✅ Registered as `IActivityEventSource` — reads from `ActivityEventLedgerEntries` |
| `NoOpActivityEventSource` | ✅ Deleted |
| `WorkItemResolutionService` | ✅ Implemented — walks `ParentTfsId` hierarchy from product backlog roots |
| `WorkItemResolutionSyncStage` | ✅ Wired in sync pipeline (Stage 4) |
| `SprintTrendProjectionSyncStage` | ✅ Wired in sync pipeline (Stage 5) |
| Feature Progress | ✅ Computed from resolved hierarchy — `ComputeFeatureProgressAsync()` |
| Epic Progress | ✅ Computed from feature progress — `ComputeEpicProgressAsync()` |
| Staleness detection | ✅ Compares `ActivityEventWatermark` > `SprintTrendProjectionAsOfUtc` |
| Recompute button | ✅ UI button calls `recompute=true` to re-derive projections |
| Batch activity loading | ✅ Single query for full date range, filtered in-memory per sprint |
| Single sprint view | ✅ Nav arrows, metrics cards, per-product breakdown |
| Advanced mode | ✅ 10-sprint trend graphs (progression, PBI count, PBI effort, bug trend) |
| Approximate data warnings | ✅ Missing effort detection with sibling-average flag |

### What was removed (historical)

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
| `NoOpActivityEventSource` | Deleted |

### Test coverage

| Test Class | Test Count | Scope |
|---|---|---|
| `SprintTrendProjectionServiceTests` | 29 | `ComputeProductSprintProjection`, `ComputeProgressionDelta`, `ComputeFeatureProgress`, `ComputeEpicProgress` |
| `WorkItemResolutionServiceTests` | 6 | `ResolveAncestry` (all hierarchy patterns + circular reference safety) |

### Summary

Sprint Trends is fully functional. The feature reads activity events from `ActivityEventLedgerEntries` (populated by the sync pipeline), resolves work items into a product hierarchy via `ResolvedWorkItems`, and computes per-sprint per-product metrics stored in `SprintMetricsProjections`. The UI supports single-sprint and 10-sprint advanced mode with trend charts, feature/epic progress tables, approximate data warnings, and staleness detection with a recompute button.
