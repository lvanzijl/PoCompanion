> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Bug Analysis Report

## Metadata
- Bug: Progress Trend shows unrealistic effort spike
- Area: Dashboard / Progress Trend
- Status: Analysis complete

## ROOT_CAUSE

- The spike is caused by PoCompanion's own Progress Trend metric contract, not by bad TFS data. The pipeline stores `PlannedEffort` as the effort of PBIs committed at the canonical commitment timestamp (`SprintStart + 1 day`), but stores `CompletedPbiEffort` as the effort of every PBI whose first transition to Done occurs inside the sprint window, including work added after commitment and carry-over finished during that sprint. `GetSprintTrendMetricsQueryHandler` aggregates those values, and `PoTool.Client/Pages/Home/DeliveryTrends.razor` plots `CompletedPbiEffort / TotalPlannedEffort * 100`. Mathematically, that ratio explodes whenever completed effort is much larger than committed effort; for example, 120 completed hours against 20 planned hours renders as 600%. Around sprint 2601, the steep line is therefore the direct result of comparing two different populations in tool logic, not a TFS ingestion, storage, CDC, or chart-rendering defect.

## CURRENT_BEHAVIOR
- TFS work item sync stores the current work item snapshot, including `Effort`, in `WorkItems` (`PoTool.Api/Services/Sync/WorkItemSyncStage.cs`), while revision ingestion stores whitelisted field changes such as `System.State`, `System.IterationPath`, and `Microsoft.VSTS.Scheduling.Effort` in `ActivityEventLedgerEntries` (`PoTool.Api/Services/ActivityEventIngestionService.cs`, `PoTool.Core/RevisionFieldWhitelist.cs`).
- Product ownership and hierarchy are resolved into `ResolvedWorkItems` (`PoTool.Api/Services/WorkItemResolutionService.cs`), and sprint trend projections are then recomputed in `PoTool.Api/Services/SprintTrendProjectionService.cs`.
- In that projection step, committed scope is reconstructed from iteration history at `CommitmentTimestamp = SprintStart + 1 day`, while delivery is reconstructed from the first canonical Done transition inside the sprint window (`PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs`, `PoTool.Core.Domain/Domain/Sprints/FirstDoneDeliveryLookup.cs`, `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`).
- The projection persists `PlannedEffort` and `CompletedPbiEffort` into `SprintMetricsProjectionEntity`, `GetSprintTrendMetricsQueryHandler` aggregates them into `TotalPlannedEffort` and `TotalCompletedPbiEffort`, and the Home Delivery Trends page renders Progress Trend as `completed / planned * 100` with no additional normalization or smoothing (`PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`, `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`, `PoTool.Shared/Metrics/SprintTrendDtos.cs`, `PoTool.Client/Pages/Home/DeliveryTrends.razor`).
- Because the chart directly plots those raw percentages, the UI only reflects the upstream ratio; it does not create or exaggerate the spike on its own.

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the full path from TFS to the graph and the spike is explainable without assuming corrupt or odd source data.

The important distinction is that the denominator is "what was committed" and the numerator is "what reached Done during the sprint." Those are not the same set of PBIs. If a sprint finishes a large amount of added scope or carry-over while having a relatively small committed effort baseline, the computed percentage can become far above 100% and the line will look unrealistic even though each underlying TFS event is ordinary.

So the defect is in our tool logic and contract for this chart: the Home Progress Trend currently mixes commitment-scope effort with done-in-sprint effort and labels the result as a simple effort progress signal. TFS is only providing the raw work item snapshots and history that the tool then combines this way. I did not find evidence that ingestion/storage corrupts the values, and the chart component itself just renders the percentage it is given.
</comments>
