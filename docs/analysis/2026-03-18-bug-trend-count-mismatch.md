# Bug Analysis Report

## Metadata
- Bug: Bug Trend graph count does not match TFS sprint backlog
- Area: Dashboard / Bug Trend
- Status: Analysis complete

## ROOT_CAUSE

- The mismatch comes from aggregation/query logic using a different metric contract than the TFS sprint backlog. The Bug Trend graph does **not** count "bugs currently shown as solved in the sprint backlog". It counts only bugs that are already resolved into the selected product hierarchy and whose **first canonical Done transition** happened inside the sprint date window. TFS sprint backlog counts are based on sprint membership/state in the backlog view. Because those are different scopes, the graph can legitimately show `3` while the sprint backlog shows about `10`.

## CURRENT_BEHAVIOR
- TFS work item snapshots are fetched through `ITfsClient` during `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/WorkItemSyncStage.cs` and stored in `WorkItems` with fields such as `Type`, `State`, `IterationPath`, and `CreatedDate`.
- TFS update history is fetched through `ITfsClient.GetWorkItemUpdatesAsync(...)` during `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ActivityEventIngestionService.cs` and stored in `ActivityEventLedgerEntries` as field-level CDC events, including `System.State` and `System.IterationPath`.
- Current product ownership and current sprint membership are resolved from cached work item hierarchy and current iteration path in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemResolutionService.cs` and stored in `ResolvedWorkItems`.
- The Bug Trend graph is loaded from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor`, which calls `GET /api/Metrics/sprint-trend` through `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/SprintDeliveryMetricsService.cs`.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/MetricsController.cs` forwards that request to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`, which reads cached `SprintMetricsProjections` or recomputes them through `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs`.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs` computes one projection per **product + sprint**. It loads `ResolvedWorkItems`, `WorkItems`, and `ActivityEventLedgerEntries`, builds `firstDoneByWorkItem` from state-change history, and passes product-scoped inputs into `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`.
- In `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`, bug metrics are calculated only from `productResolved` bugs. `BugsCreatedCount` is incremented when `WorkItem.CreatedDate` falls inside the sprint window. `BugsClosedCount` is incremented only when `firstDoneByWorkItem` contains the bug and that first canonical Done timestamp falls inside the sprint window.
- `BugsClosedCount` does **not** filter bugs by current sprint backlog membership. Unlike planned scope, it does not use `CommittedWorkItemIds` or `ResolvedSprintId` to decide whether a closed bug belongs to the sprint backlog being viewed.
- The computed values are stored in `SprintMetricsProjectionEntity.BugsClosedCount` and then returned unchanged through `GetSprintTrendMetricsResponse` to the client.
- The UI does not add a hidden bug filter. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor` either shows the sprint total or the selected product's `BugsClosedCount`; it does not transform the metric into a sprint-backlog count.
- Therefore the mismatch is **not primarily caused by TFS data, ingestion/storage loss, or UI filtering**. It is caused by the graph using a product-scoped sprint-window closure metric while the compared TFS view uses sprint-backlog membership/state.

## Comments on the Issue (you are @copilot in this section)

<comments>
@copilot The end-to-end path is: TFS `ITfsClient` -> `WorkItemSyncStage` -> `WorkItems`; TFS update history -> `ActivityEventIngestionService` -> `ActivityEventLedgerEntries`; hierarchy resolution -> `WorkItemResolutionService` -> `ResolvedWorkItems`; projection rebuild -> `SprintTrendProjectionService` + `SprintDeliveryProjectionService`; API response -> `GetSprintTrendMetricsQueryHandler`; chart render -> `DeliveryTrends.razor`.

@copilot The strict current contract of the graph is: "count product-resolved bugs whose first canonical Done transition occurred inside the sprint window." That is a time-window delivery metric, not a sprint-backlog solved-count metric.

@copilot If the product owner wants the chart to match the TFS sprint backlog for the same sprint, the metric contract must be changed explicitly to a sprint-membership-based rule. If the current behavior is intended, the UI/help text must say clearly that Bug Trend is a product-scoped time-window metric and is not expected to equal the TFS sprint backlog solved count.
</comments>
