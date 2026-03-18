# Bug Trend TFS vs Dashboard Mismatch

_Generated: 2026-03-18_

## ROOT_CAUSE

### 1. Which service/query builds the Bug Trend dataset

The sprint-based Bug Trend graph shown in `PoTool.Client/Pages/Home/DeliveryTrends.razor` is populated from `_trendMetrics`, and the chart uses `BugsCreatedCount` / `BugsClosedCount` from `SprintTrendMetricsDto` in `BuildTrendCharts()` (`PoTool.Client/Pages/Home/DeliveryTrends.razor:423-525`).

The request path is:

1. `PoTool.Client/Pages/Home/DeliveryTrends.razor:423-458`
   - calls `MetricsClient.GetSprintTrendMetricsAsync(...)`
2. `PoTool.Api/Controllers/MetricsController.cs:457-482`
   - handles `GET api/Metrics/sprint-trend`
3. `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs:34-249`
   - loads cached `SprintMetricsProjectionEntity` rows or recomputes them
4. `PoTool.Api/Services/SprintTrendProjectionService.cs:71-263`
   - computes projections when needed
5. `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs:63-300`
   - owns the actual bug-created / bug-closed formulas

### 2. Which storage is used

The dashboard does **not** query TFS directly at chart-render time.

It uses cached local data:

- `WorkItemEntity` for current snapshot fields such as `Type`, `IterationPath`, `State`, `CreatedDate`, and `ClosedDate`
  - sync source: `PoTool.Api/Services/Sync/WorkItemSyncStage.cs:53-91, 162-210`
- `ActivityEventLedgerEntryEntity` for field-level update history, including `System.State` changes
  - ingestion source: `PoTool.Api/Services/ActivityEventIngestionService.cs:34-200`
- `ResolvedWorkItemEntity` for current product/sprint resolution
  - resolution source: `PoTool.Api/Services/WorkItemResolutionService.cs:111-130`
- `SprintMetricsProjectionEntity` for precomputed per-sprint per-product metrics
  - compute source: `PoTool.Api/Services/Sync/SprintTrendProjectionSyncStage.cs:32-79`
  - entity: `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs:1-173`

So the query path is:

TFS work item snapshot / updates  
→ local `WorkItems` + `ActivityEventLedgerEntries`  
→ local `ResolvedWorkItems`  
→ local `SprintMetricsProjections`  
→ `GetSprintTrendMetricsQueryHandler`  
→ `SprintTrendMetricsDto`  
→ Delivery Trends Bug Trend chart.

### 3. Filters currently applied in the sprint Bug Trend calculation

The projection logic for bug trend is in `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs:196-242`.

#### Work item type

Only `Bug` items are counted:

- `bugResolved = productResolved.Where(resolvedItem => string.Equals(resolvedItem.WorkItemType, BacklogWorkItemTypes.Bug, ...))`

#### Product scope

Bug trend is scoped to the selected **product owner/product set**, because `productResolved` is built from `ResolvedWorkItems` where `ResolvedProductId == request.ProductId` (`SprintDeliveryProjectionService.cs:89-99`).

#### Iteration / sprint scope

This is the first key mismatch:

- `BugsCreatedCount` and `BugsClosedCount` are computed from **all bugs resolved to the product**
- they are **not filtered by `ResolvedSprintId`**
- they are **not filtered by `IterationPath == sprint.Path`**
- they are **not filtered by committed sprint scope**

In other words, for a given sprint row the chart answers:

- “how many product bugs were created during this sprint window?”
- “how many product bugs first reached Done during this sprint window?”

It does **not** answer:

- “how many bugs in this sprint backlog are currently closed/resolved?”

Current sprint resolution itself is an exact snapshot path match in `WorkItemResolutionService`:

- `ResolvedSprintId` is assigned only when `wi.IterationPath` exactly matches a synced sprint path (`PoTool.Api/Services/WorkItemResolutionService.cs:111-117`)

But that sprint resolution is not used by the bug created/closed counters.

#### State mapping

Bug closure uses canonical Done classification via `firstDoneByWorkItem`:

- `SprintDeliveryProjectionService.cs:215-222`
- `PoTool.Api/Services/SprintTrendProjectionService.cs:177-183`

The default fallback state map for bugs is:

- `Bug/New -> New`
- `Bug/Approved -> New`
- `Bug/Committed -> InProgress`
- `Bug/Done -> Done`
- `Bug/Removed -> Removed`

Source: `PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs:50-55`

Important consequence:

- `Resolved` and `Closed` are **not** default Done states for `Bug`
- Resolved and Closed are not default Done states for Bug
- `WorkItemStateClassificationService.GetClassificationAsync(...)` defaults unknown states to `New`
  - `PoTool.Api/Services/WorkItemStateClassificationService.cs:187-199`

So if the TFS project uses bug states such as `Resolved` or `Closed` and no custom state classification exists in `WorkItemStateClassifications`, PoTool will undercount bug closures.

#### Date fields used

- `BugsCreatedCount`
  - uses snapshot `CreatedDate` from `WorkItemEntity`
  - source check: `SprintDeliveryProjectionService.cs:208-213`
- `BugsClosedCount`
  - uses the **first timestamp** where the bug transitioned to canonical Done
  - source check: `SprintDeliveryProjectionService.cs:215-222`
  - history source: `ActivityEventLedgerEntries` with `FieldRefName == "System.State"` in `SprintTrendProjectionService.cs:146-152, 177-183`

Even though `ClosedDate` is synced into `WorkItemEntity` (`PoTool.Api/Services/Sync/WorkItemSyncStage.cs:174-175, 201-202`), the sprint trend projection input drops it:

- `PoTool.Api/Adapters/DeliveryTrendProjectionInputMapper.cs:8-21`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs:8-18`

So `ClosedDate` is available in cache but is **not used** by the Bug Trend chart.

### 4. Comparison with TFS sprint backlog semantics

The closest code-level analogue to a TFS sprint backlog view in this repository is the sprint execution query, which starts from current sprint membership:

- `currentSprintItems = relevantWorkItems.Where(w => w.IterationPath == sprint.Path)`  
  `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs:128-130`

That is much closer to how a sprint backlog is typically understood:

- current sprint scope comes from the sprint iteration path
- closed/resolved status comes from the workflow column/state now visible on that backlog

By contrast, the Delivery Trends bug chart uses:

- product-level bug set
- sprint **date window**
- first canonical Done transition timestamp

So the TFS sprint backlog and the Delivery Trends bug chart are currently measuring different things.

### 5. Most likely mismatch cause for “TFS backlog ≈ 10 resolved, dashboard shows 3”

The discrepancy is best explained as an **aggregation/query semantics mismatch**, not a UI rendering problem.

Primary causes:

1. **Wrong sprint attribution logic for bug closures**
   - The chart does not restrict closed bugs to the sprint backlog.
   - It counts product bugs whose first Done transition happened during the sprint window.

2. **Wrong completion semantics relative to a sprint backlog**
   - The chart counts only the **first** canonical Done transition during the sprint.
   - A sprint backlog count usually reflects bugs that are currently in the sprint backlog and currently in a terminal/resolved state, even if they were resolved earlier.

3. **Potential state-mapping undercount**
   - Default bug mappings only treat `Done` as Done.
   - If TFS backlog shows `Resolved` bugs as completed, PoTool will miss them unless custom state classification rows exist.

Contributing detail:

- `ClosedDate` is already ingested from TFS, but the sprint trend pipeline does not use it.
- ClosedDate is already ingested from TFS, but the sprint trend pipeline does not use it.
- Because `DeliveryTrendWorkItem` excludes `ClosedDate`, the projection must reconstruct closure from activity ledger state events only.

### 6. Data-lineage classification of the issue

Based on the current implementation, the issue is:

- **Not primarily a direct TFS-call issue**
  - query-time reads are from local cache/projections only
- **Not primarily a UI filtering issue**
  - the UI simply renders `TotalBugsCreatedCount` / `TotalBugsClosedCount`
- **Not primarily a raw ingestion failure**
  - `CreatedDate`, `ClosedDate`, and activity updates are all ingested and stored

The issue is primarily:

- **an aggregation/query problem**
  - wrong business definition for “closed bugs per sprint” versus sprint backlog expectation

And secondarily:

- **a CDC interpretation / state-classification problem**
  - bug `Resolved` / `Closed` states are not default Done states for `Bug`

## Comments on the Issue (you are @copilot in this section)

- The Delivery Trends Bug Trend chart is built from cached `SprintMetricsProjectionEntity` rows, not from direct TFS reads.
- The chart currently counts product-level bug events in the sprint time window, not “bugs currently closed on this sprint backlog”.
- The most actionable code hotspot is `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`, where bug closure ignores sprint membership and depends on first canonical Done transition timing.
- A second likely undercount source is the default bug state map in `PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs`, which does not classify `Resolved` or `Closed` bug states as Done unless overridden in project settings.
