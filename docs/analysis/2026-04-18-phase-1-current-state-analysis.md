# Phase 1 Current State Analysis

## 1. Summary

- **VERIFIED:** The current user-facing planning workspace is the `/home/planning` hub and exposes **Project Overview**, **Product Roadmaps**, **Plan Board**, and **Multi-Product Planning**.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanningWorkspace.razor`, page route `/home/planning`, methods `NavigateToProjectOverview`, `NavigateToProductRoadmaps`, `NavigateToPlanBoard`, `NavigateToMultiProductPlanning`.

- **VERIFIED:** There are **two separate planning concepts** in the repository:
  1. the current TFS-backed planning flow used by `ProductRoadmaps`, `ProductRoadmapEditor`, `PlanBoard`, and `MultiProductPlanning`;  
  2. a separate persisted **Release Planning Board** model with lanes, placements, milestone lines, and iteration lines.  
  **Evidence:** current pages in `PoTool.Client/Pages/Home/*.razor`; separate board contracts in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`; controller `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ReleasePlanningController.cs`; repository `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ReleasePlanningRepository.cs`.

- **INFERRED:** The Release Planning Board is **not part of the current planning workspace flow**.  
  **Evidence:** `PlanningWorkspace.razor` does not navigate to it; `ReleasePlanningBoard.razor` is a component, not a routed page (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor` has no `@page`); no routed planning page referencing that component was found in `PoTool.Client`.

- **VERIFIED:** A **timeline-based planning concept already exists**, but it is **read-only forecast visualization**, not persisted editable scheduling.  
  **Evidence:** `ProductRoadmaps.razor` method `BuildTimeline`; `MultiProductPlanning.razor` methods `BuildUnalignedLane`, `BuildAlignedLane`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs` derives timeline bars from `EstimatedCompletionDate`, `SprintsRemaining`, and sprint cadence.

- **VERIFIED:** Current roadmap ordering is driven by **TFS work item backlog priority** plus the explicit `roadmap` tag.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapEpicDiscoveryAnalysis.cs` sorts roadmap epics by `BacklogPriority`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapWorkItemRules.cs` method `HasRoadmapTag`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs` endpoint `POST api/WorkItems/{tfsId}/backlog-priority`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs` method `UpdateWorkItemBacklogPriorityAsync`.

- **VERIFIED:** Sprint handling is backed by persisted sprint cache, not live-only lookups during page rendering.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/TeamSprintSyncStage.cs`.

## 2. Frontend findings

### 2.1 Planning hub

- **VERIFIED:** The planning hub is navigation-only and contains no planning data logic.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanningWorkspace.razor`, page route `/home/planning`, UI cards only.

### 2.2 Product Roadmaps (`/planning/product-roadmaps`, `/planning/{project}/product-roadmaps`)

- **VERIFIED:** The page is presented as **read-only overview**, but each product lane links to an editor.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`, `Read-only` chip in header; button `Edit roadmap`; method `NavigateToRoadmapEditor`.

- **VERIFIED:** The page uses **route/global filter state**, not local selector controls. No `MudSelect`/`MudAutocomplete` is present on the page.  
  **Evidence:** `ProductRoadmaps.razor` injects `GlobalFilterStore` and `GlobalFilterRouteService`; methods `ResolveInitialProjectSelection`, `ApplyProjectFilter`, `HandleGlobalFilterChangedAsync`; no selector markup found in that file.

- **VERIFIED:** Roadmap lanes are built product-by-product from cached work item hierarchies retrieved by backlog roots.  
  **Evidence:** `ProductRoadmaps.razor`, method `BuildProductLanesAsync`; `WorkItemService.GetByRootIdsAsync(product.BacklogRootWorkItemIds.ToArray())`.

- **VERIFIED:** Roadmap membership is **Epic type + `roadmap` tag**.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapEpicDiscoveryAnalysis.cs`, `RoadmapWorkItemRules.IsEpic`, `RoadmapWorkItemRules.HasRoadmapTag`.

- **VERIFIED:** Per-epic cards include local analytics and persisted forecast signals.  
  **Evidence:** `ProductRoadmaps.razor`, method `BuildProductLanesAsync`; `RoadmapAnalyticsService.ComputeLocalAnalytics`; `AnalyticsService.LoadBacklogHealthAsync`; `AnalyticsService.LoadDependencySignalsAsync`; `ProductService.GetPlanningProjectionsAsync`.

- **VERIFIED:** Product lane order is based on the **Objective work item backlog priority**, not a separate product-order store.  
  **Evidence:** `ProductRoadmaps.razor`, method `BuildProductLanesAsync`; it resolves `objectiveWorkItem` and sorts `_productLanes` by `ObjectiveBacklogPriority`, then `ObjectiveTfsId`.

- **VERIFIED:** Product lane reordering mutates TFS backlog priority on the Objective work items.  
  **Evidence:** `ProductRoadmaps.razor`, method `SwapProductOrder`; `WorkItemService.UpdateBacklogPriorityAsync`; `WorkItemsController.UpdateBacklogPriority`.

- **VERIFIED:** Timeline rendering is inline in this page and uses `RoadmapTimelineLayout`, not a shared roadmap-specific UI component.  
  **Evidence:** `ProductRoadmaps.razor`, method `BuildTimeline`, CSS classes `projection-timeline-*`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`.

- **VERIFIED:** Snapshot/reporting behavior already exists on this page.  
  **Evidence:** `ProductRoadmaps.razor` menus `Reporting` and `Snapshots`; methods `GenerateVisualRoadmap`, `ExportStructuredData`, `CreateSnapshot`, `CompareSnapshot`; client service `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapSnapshotService.cs`.

### 2.3 Product Roadmap Editor (`/planning/product-roadmaps/{productId}`)

- **VERIFIED:** The editor supports drag/drop between **Roadmap Epics** and **Available Epics**, plus button-based add/remove/reorder/edit actions.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`; `MudDropContainer`, `MudDropZone`, methods `OnItemDropped`, `AddToRoadmap`, `RemoveFromRoadmap`, `SwapEpicOrder`, `OpenDrawer`.

- **VERIFIED:** Adding/removing roadmap membership mutates **tags** in TFS and then adjusts **backlog priority**.  
  **Evidence:** `ProductRoadmapEditor.razor`, methods `AddToRoadmapAtPosition`, `AddToRoadmap`, `RemoveFromRoadmap`, `RemoveFromRoadmapByDrag`; calls `WorkItemService.UpdateTagsAsync` and `WorkItemService.UpdateBacklogPriorityAsync`.

- **VERIFIED:** Reordering is implemented as **priority rewrites**, not a separate roadmap-order entity.  
  **Evidence:** `ProductRoadmapEditor.razor`, methods `ApplyRoadmapReorderAsync`, `TryCreateRoadmapReorderPlan`, `NormalizeRoadmapPrioritiesAsync`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapEpicPriorityReorderPlanner.cs`.

- **VERIFIED:** The editor also supports title/description edits through work item mutation APIs.  
  **Evidence:** `ProductRoadmapEditor.razor`, method `SaveDrawerChanges`; `WorkItemService.UpdateTitleDescriptionAsync`; backend endpoint `POST api/WorkItems/{tfsId}/title-description`.

- **VERIFIED:** The editor respects global product scope and route context, but it is still a single-product page.  
  **Evidence:** `ProductRoadmapEditor.razor`, `_selectedProductId => GlobalProductSelectionHelper.ResolveSingleProductId(...)`, `BootstrapRouteProductAsync`, `_projectRouteKey`.

### 2.4 Plan Board (`/planning/plan-board`, `/planning/{project}/plan-board`)

- **VERIFIED:** The Plan Board is **single-product operational sprint planning**. It requires exactly one selected product from global filter state.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`, `_selectedProductId => GlobalProductSelectionHelper.ResolveSingleProductId(...)`, `BoardRenderState` returns `NotRequested` when no single product is selected.

- **VERIFIED:** The page has no local product/project selector UI; product scope comes from route/global filters.  
  **Evidence:** `PlanBoard.razor` injects `GlobalFilterStore` and `GlobalFilterRouteService`; methods `ResolveInitialProjectSelection`, `ApplyProjectFilter`, `HandleGlobalFilterChangedAsync`; no selector markup found in the file.

- **VERIFIED:** The left column is a **hierarchical backlog candidate tree** built from Epic → Feature → PBI/Bug relationships.  
  **Evidence:** `PlanBoard.razor`, `RenderCandidateNode`, `BuildBoard`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/PlanBoardWorkItemRules.cs`, method `BuildCandidateTree`.

- **VERIFIED:** Candidate leaves are only **PBIs and Bugs** that are not Done/Removed and not already assigned to visible sprint columns.  
  **Evidence:** `PlanBoardWorkItemRules.cs`, methods `IsPlanBoardItem`, `IsDoneOrRemoved`, `IsAssignedToSprint`.

- **VERIFIED:** Sprint columns are populated from persisted team sprints, filtered to `current`/`future`, limited to four columns, with fallback to the last four if no current/future sprints exist.  
  **Evidence:** `PlanBoard.razor`, method `LoadBoardAsync`, lines that build `allTeamSprints`, filter by `TimeFrame`, and `Take(4)`.

- **VERIFIED:** Planning mutations are implemented by updating **System.IterationPath** in TFS.  
  **Evidence:** `PlanBoard.razor`, methods `DropCandidateNodeOnSprint` and `MoveSprintItemToSprint`; `WorkItemService.UpdateIterationPathAsync`; backend endpoint `POST api/WorkItems/{tfsId}/iteration-path`.

- **VERIFIED:** The page can force a hierarchy refresh from TFS before rebuilding the board.  
  **Evidence:** `PlanBoard.razor`, method `RefreshBoardFromTfsAsync`; `WorkItemService.RefreshWorkItemsByRootIdsFromTfsAsync`; backend endpoint `POST api/WorkItems/by-root-ids/refresh-from-tfs`.

- **VERIFIED:** Capacity indicators are shown per sprint column and are loaded from delivery/capacity data, not entered manually on the board.  
  **Evidence:** `PlanBoard.razor`, `GetSprintCapacityState`, `LoadCapacityCalibrationAsync`, UI `Capacity: ... Assigned: ...`.

### 2.5 Multi-Product Planning (`/planning/multi-product`)

- **VERIFIED:** This page is a **cross-product read-only forecast view** aligned to a shared time axis.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor`, caption `All products aligned to a shared time axis. End dates are authoritative; start dates are forecast-derived.`

- **VERIFIED:** This page does **not** use `GlobalFilterStore`; it loads all owner products and provides its own local multi-select filter.  
  **Evidence:** `MultiProductPlanning.razor` injects `ProductService` and `SprintService`, but not `GlobalFilterStore`; page markup contains `MudSelect` with `MultiSelection="true"` and `_selectedProductIds`.

- **VERIFIED:** It loads persisted planning projections for every product and computes sprint cadence from completed team sprints.  
  **Evidence:** `MultiProductPlanning.razor`, methods `LoadAsync`, `ResolveSprintCadence`, `BuildAlignedLane`; calls `ProductService.GetPlanningProjectionsAsync(product.Id)` and `SprintService.GetSprintsForTeamAsync(teamId)`.

- **VERIFIED:** It overlays **pressure zones** and **capacity collision hints** on top of the shared axis.  
  **Evidence:** `MultiProductPlanning.razor`, UI blocks for `_pressureZones` and `_capacityCollisionWindows`; methods `ComputePressureZones`, `CapacityCollisionHintDetector.Build`, `BuildEpicCollisionHints`.

- **VERIFIED:** Current interaction is limited to filtering/toggles; there are no planning mutation calls on this page.  
  **Evidence:** `MultiProductPlanning.razor` only changes `_selectedProductIds`, `_showClusters`, `_showCapacityCollisions`; no `WorkItemService` or mutation endpoint usage appears in the file.

## 3. Backend findings

### 3.1 User-facing planning/roadmap/sprint endpoints

- **VERIFIED:** Product planning projections are exposed by `GET /api/Products/{productId}/planning-projections`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs`, method `GetPlanningProjections`; query `GetProductPlanningProjectionsQuery`.

- **VERIFIED:** Project planning summary is exposed by `GET /api/Projects/{alias}/planning-summary`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProjectsController.cs`, method `GetPlanningSummary`; service `ProjectPlanningSummaryService`.

- **VERIFIED:** Sprint lookup endpoints are `GET /api/Sprints?teamId=` and `GET /api/Sprints/current?teamId=`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/SprintsController.cs`, methods `GetSprintsForTeam`, `GetCurrentSprintForTeam`.

- **VERIFIED:** Current roadmap/plan-board mutation endpoints are:
  - `POST /api/WorkItems/by-root-ids/refresh-from-tfs`
  - `POST /api/WorkItems/{tfsId}/tags`
  - `POST /api/WorkItems/{tfsId}/backlog-priority`
  - `POST /api/WorkItems/{tfsId}/iteration-path`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs`, methods `RefreshByRootIdsFromTfs`, `UpdateTags`, `UpdateBacklogPriority`, `UpdateIterationPath`.

- **VERIFIED:** Roadmap snapshot endpoints are:
  - `GET /api/RoadmapSnapshots`
  - `GET /api/RoadmapSnapshots/{id}`
  - `POST /api/RoadmapSnapshots`
  - `DELETE /api/RoadmapSnapshots/{id}`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/RoadmapSnapshotsController.cs`.

- **VERIFIED:** A separate Release Planning API surface exists with board/placement/lane/line/split/export endpoints under `api/ReleasePlanning`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ReleasePlanningController.cs`, endpoints `board`, `unplanned-epics`, `lanes`, `placements`, `milestone-lines`, `iteration-lines`, `validation/refresh`, `epics/{epicId}/split`, `export`.

### 3.2 Planning logic and forecast/trend services

- **VERIFIED:** `GetProductPlanningProjectionsQueryHandler` reads product-scoped roadmap epics from resolved work items and joins persisted forecast projections.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs`, `from resolved in _context.ResolvedWorkItems ... join projection in _context.ForecastProjections`.

- **VERIFIED:** Returned roadmap order in projections is **recomputed as `index + 1` after sorting by backlog priority**, not stored separately in the projections table.  
  **Evidence:** `GetProductPlanningProjectionsQueryHandler.cs`, `OrderBy(candidate => candidate.BacklogPriority ?? double.MaxValue)`, then `Select((candidate, index) => new PlanningEpicProjectionDto(..., index + 1, ...))`.

- **VERIFIED:** `ProjectPlanningSummaryService` calculates:
  - roadmap epic count,
  - planned/unplanned active PBIs,
  - planned/total effort,
  - capacity per sprint,
  - overcommit indicator.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ProjectPlanningSummaryService.cs`, methods `GetSummaryAsync`, `CalibrateCapacityPerSprint`, `IsRoadmapEpic`, `IsActivePbi`.

- **VERIFIED:** Capacity in the planning summary is derived from historical sprint projections, not a dedicated planning-capacity table.  
  **Evidence:** `ProjectPlanningSummaryService.cs`, queries `_context.SprintMetricsProjections`; method `CalibrateCapacityPerSprint`.

- **VERIFIED:** Forecast projections are materialized and persisted by `ForecastProjectionMaterializationService`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ForecastProjectionMaterializationService.cs`, method `ComputeProjectionsAsync`; sync stage `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/ForecastProjectionSyncStage.cs`.

- **VERIFIED:** The persisted forecast model stores `SprintsRemaining`, `EstimatedCompletionDate`, `Confidence`, and serialized variants.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ForecastProjectionEntity.cs`.

- **VERIFIED:** The read-only metrics forecast endpoint reads those persisted forecast variants; it does not recompute them on request.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`, method `Handle`; it reads `_context.ForecastProjections`.

### 3.3 Mutation handlers relevant to planning

- **VERIFIED:** Backlog priority, iteration path, and tag mutations follow the pattern **write to TFS, then refresh/update local cache**.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/UpdateWorkItemBacklogPriorityCommandHandler.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/UpdateWorkItemIterationPathCommandHandler.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/UpdateWorkItemTagsCommandHandler.cs`.

- **VERIFIED:** Product hierarchy refresh also updates only the local cache after a TFS re-read.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/RefreshWorkItemsByRootIdsFromTfsCommandHandler.cs`.

## 4. Domain model findings

- **VERIFIED:** `WorkItemDto` already carries the fields needed by current roadmap/sprint planning: `IterationPath`, `Effort`, `Tags`, `BacklogPriority`, `CreatedDate`, `ChangedDate`, `ParentTfsId`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs`.

- **VERIFIED:** Sprint identity is represented with both internal and TFS-facing identifiers: `Id`, `TeamId`, `TfsIterationId`, `Path`, `Name`, `StartUtc`, `EndUtc`, `TimeFrame`, `LastSyncedUtc`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/SprintDto.cs`.

- **VERIFIED:** Raw TFS iteration payloads are captured as `TeamIterationDto` with `Id`, `Name`, `Path`, `StartDate`, `FinishDate`, `TimeFrame`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TeamIterationDto.cs`.

- **VERIFIED:** Product roadmap timeline data is represented only as:
  - `PlanningEpicProjectionDto` (`RoadmapOrder`, `SprintsRemaining`, `EstimatedCompletionDate`, `Confidence`, `HasForecast`, `LastUpdated`)  
  - client-side `RoadmapTimelineModel`/`RoadmapTimelineRow`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`.

- **VERIFIED:** There is **no shared DTO that stores an editable planned start date, duration, or assigned timeline band for roadmap epics** in the current product-roadmap flow.  
  **Evidence:** `PlanningEpicProjectionDto` has no start/duration fields; `RoadmapTimelineLayout.BuildRow` derives `StartDate` from `EstimatedCompletionDate` and `SprintsRemaining`.

- **VERIFIED:** A separate release-planning domain model exists with `LaneDto`, `EpicPlacementDto`, `MilestoneLineDto`, `IterationLineDto`, and `ReleasePlanningBoardDto`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`.

- **VERIFIED:** That separate release-planning model is row/placement-based, not date/sprint based.  
  **Evidence:** `EpicPlacementDto` uses `LaneId`, `RowIndex`, `OrderInRow`; `IterationLineDto` and `MilestoneLineDto` use `VerticalPosition`; no date fields exist in those contracts.

## 5. TFS / data integration findings

- **VERIFIED:** The real TFS client explicitly requests these work-item fields relevant to planning:
  - `System.Id`
  - `System.WorkItemType`
  - `System.Title`
  - `System.State`
  - `System.AreaPath`
  - `System.IterationPath`
  - `System.Description`
  - `System.CreatedDate`
  - `System.ChangedDate`
  - `Microsoft.VSTS.Common.ClosedDate`
  - `Microsoft.VSTS.Common.Severity`
  - `System.Tags`
  - `Microsoft.VSTS.Common.BusinessValue`
  - `Microsoft.VSTS.Scheduling.Effort`
  - `Microsoft.VSTS.Scheduling.StoryPoints`
  - `Microsoft.VSTS.Common.BacklogPriority`
  - `Microsoft.VSTS.Common.TimeCriticality`
  - `Rhodium.Funding.ProjectNumber`
  - `Rhodium.Funding.ProjectElement`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`, `RequiredWorkItemFields`.

- **VERIFIED:** The real TFS client maps roadmap/sprint-relevant fields directly into `WorkItemDto`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`, field extraction around `System.IterationPath`, `System.Tags`, `Microsoft.VSTS.Common.BacklogPriority`.

- **VERIFIED:** Team sprint data is retrieved from the TFS team iterations REST API and persisted locally.  
  **Evidence:** `RealTfsClient.Teams.cs`, method `GetTeamIterationsAsync`, URL `.../_apis/work/teamsettings/iterations?api-version=...`; `TeamSprintSyncStage.cs`; `SprintRepository.UpsertSprintsForTeamAsync`.

- **VERIFIED:** Roadmap membership/order is **not stored in a dedicated roadmap table** for the current roadmap flow; it is reconstructed from TFS work items (`roadmap` tag + backlog priority).  
  **Evidence:** `RoadmapEpicDiscoveryAnalysis.cs`; `RoadmapWorkItemRules.cs`; `GetProductPlanningProjectionsQueryHandler.cs`; `ProductRoadmapEditor.razor`.

- **VERIFIED:** App-side persistence exists for:
  - sprint cache (`Sprints`)
  - forecast cache (`ForecastProjections`)
  - roadmap snapshots (`RoadmapSnapshots`, `RoadmapSnapshotItems`)
  - separate release-planning board tables (`Lanes`, `EpicPlacements`, `MilestoneLines`, `IterationLines`)
  - newer planning-board tables (`BoardRows`, `PlanningEpicPlacements`, `PlanningBoardSettings`).  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260126215105_AddPlanningBoardTables.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260307094353_AddRoadmapSnapshots.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260401170553_AddForecastProjections.cs`.

- **VERIFIED:** The current roadmap pages use only snapshots as application-side persisted roadmap artifacts.  
  **Evidence:** `ProductRoadmaps.razor` uses `IRoadmapSnapshotsClient`; no current roadmap page calls `ReleasePlanningService`.

- **UNKNOWN:** Whether production currently runs the mock or real TFS client cannot be established from repository inspection alone.  
  **Evidence:** both `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockTfsClient.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.*.cs` exist; runtime selection is environment/config dependent.

## 6. Current behavior description (end-to-end)

1. **VERIFIED:** Sprint data is synced from TFS team iterations into `SprintEntity`, and forecast data is materialized into `ForecastProjectionEntity`.  
   **Evidence:** `TeamSprintSyncStage.ExecuteAsync`; `SprintRepository.UpsertSprintsForTeamAsync`; `ForecastProjectionSyncStage.ExecuteAsync`; `ForecastProjectionMaterializationService.ComputeProjectionsAsync`.

2. **VERIFIED:** `ProductRoadmaps` loads product scope, reads cached work-item hierarchies by product backlog roots, identifies roadmap epics by `roadmap` tag, and enriches them with backlog health/dependency/forecast data.  
   **Evidence:** `ProductRoadmaps.razor`, method `BuildProductLanesAsync`.

3. **VERIFIED:** The roadmap timeline shown on `ProductRoadmaps` is computed from persisted forecast end dates and derived sprint cadence; start dates are estimated client-side.  
   **Evidence:** `ProductRoadmaps.razor`, `BuildTimeline`; `RoadmapTimelineLayout.BuildRow`.

4. **VERIFIED:** `ProductRoadmapEditor` is the page where roadmap membership and order are changed. It writes to TFS through work-item mutation endpoints and relies on cache refresh afterward.  
   **Evidence:** `ProductRoadmapEditor.razor`, methods `AddToRoadmapAtPosition`, `ReorderRoadmapByDrag`, `SaveDrawerChanges`; work-item mutation handlers/controllers.

5. **VERIFIED:** `PlanBoard` loads a single product hierarchy, resolves visible sprints for the product teams, shows unplanned PBIs/Bugs on the left, and sprint-assigned PBIs/Bugs on the right.  
   **Evidence:** `PlanBoard.razor`, methods `LoadBoardAsync`, `BuildBoard`; `PlanBoardWorkItemRules.BuildCandidateTree`.

6. **VERIFIED:** Moving items in `PlanBoard` updates `System.IterationPath` in TFS and then reloads the board.  
   **Evidence:** `PlanBoard.razor`, `DropCandidateNodeOnSprint`, `MoveSprintItemToSprint`; `UpdateWorkItemIterationPathCommandHandler`.

7. **VERIFIED:** `MultiProductPlanning` does not plan work; it aligns already-computed product projections on a shared axis and highlights clustering/collision risk.  
   **Evidence:** `MultiProductPlanning.razor`, `LoadAsync`, `RebuildVisibleLanes`, `BuildAlignedLane`; no work-item mutation usage.

8. **VERIFIED:** Roadmap snapshots are stored in the app database and compared against the current roadmap state for drift reporting.  
   **Evidence:** `RoadmapSnapshotsController`; API `RoadmapSnapshotService`; client `RoadmapSnapshotService.Compare`.

## 7. Gaps relative to desired planning feature

- **VERIFIED:** There is no persisted editable **timeline schedule** for roadmap epics in the current roadmap flow; only forecast end dates plus derived bars exist.  
  **Evidence:** `PlanningEpicProjectionDto`; `RoadmapTimelineLayout.BuildRow`.

- **VERIFIED:** There is no current backend endpoint that writes **timeline start/end/duration** for roadmap epics.  
  **Evidence:** roadmap-related active mutations are `tags`, `backlog-priority`, and `iteration-path` in `WorkItemsController`; snapshot endpoints are read/store-only and never mutate TFS.

- **VERIFIED:** Multi-product planning is read-only and cannot currently change roadmap order, sprint assignment, or forecast inputs.  
  **Evidence:** `MultiProductPlanning.razor` uses only `ProductService` and `SprintService`; no `WorkItemService` or `ReleasePlanningService`.

- **VERIFIED:** Current roadmap ordering depends on TFS backlog priority, so roadmap order and general backlog order are coupled.  
  **Evidence:** `RoadmapEpicDiscoveryAnalysis.cs`; `UpdateWorkItemBacklogPriorityCommandHandler.cs`; `RealTfsClient.WorkItemsUpdate.cs`.

- **VERIFIED:** The separate Release Planning Board has persistence for placements/lines, but it is row-based and separate from the current roadmap/sprint pages.  
  **Evidence:** `ReleasePlanningDtos.cs`; `ReleasePlanningController.cs`; `ReleasePlanningBoard.razor`; `PlanningWorkspace.razor`.

- **UNKNOWN:** Whether the next planning feature is intended to extend the current TFS-backed roadmap pages or the separate Release Planning Board cannot be determined from repository state alone.  
  **Evidence:** both implementation tracks exist; current workspace nav exposes only the TFS-backed pages.

## 8. Risks in current implementation

- **VERIFIED:** The repository contains **multiple overlapping planning models** (`ProductRoadmaps`/`PlanBoard`, `ReleasePlanningBoard`, and newer `PlanningEpicPlacements`/`BoardRows` tables).  
  **Evidence:** `PlanningWorkspace.razor`; `ReleasePlanningDtos.cs`; `PoToolDbContext.cs`; migration `AddPlanningBoardTables.cs`.

- **VERIFIED:** Roadmap timeline rendering is duplicated across at least two pages instead of being encapsulated in one shared roadmap timeline component.  
  **Evidence:** inline timeline markup in `ProductRoadmaps.razor` and `MultiProductPlanning.razor`; shared logic exists only in `RoadmapTimelineLayout.cs`.

- **VERIFIED:** `MultiProductPlanning` uses `.Result` on completed tasks after `Task.WhenAll`, which is an async-style inconsistency in `PoTool.Client`.  
  **Evidence:** `MultiProductPlanning.razor`, `LoadAsync`, line pattern `entry.Value.Result` after `await Task.WhenAll(teamSprintTasks.Values)`.

- **VERIFIED:** Timeline visibility depends on having completed sprint history; products without usable completed sprint data cannot resolve sprint cadence cleanly.  
  **Evidence:** `SprintCadenceResolver.Resolve`; `ProductRoadmaps.BuildTimeline`; `MultiProductPlanning.BuildAlignedLane`.

- **VERIFIED:** `PlanBoard` limits visible sprint columns to four and groups team sprints by normalized iteration path, which can hide additional sprint detail.  
  **Evidence:** `PlanBoard.razor`, `LoadBoardAsync`, `GroupBy(sprint => NormalizeIterationPath(sprint.Path))`, `Take(4)`.

- **VERIFIED:** Product lane order in `ProductRoadmaps` depends on Objective backlog priority and two-work-item swap writes, so missing/duplicate objective priorities can affect stability.  
  **Evidence:** `ProductRoadmaps.razor`, `BuildProductLanesAsync`, `SwapProductOrder`; `WorkItemsController.UpdateBacklogPriority`.

## 9. UNKNOWN areas

- **UNKNOWN:** No routed page or workspace entry for the Release Planning Board was found, but it is not proven that no non-planning page hosts it elsewhere at runtime.  
  **Evidence:** no `@page` on `ReleasePlanningBoard.razor`; no planning hub link; only component/service references found in `PoTool.Client`.

- **UNKNOWN:** The repository does not prove which runtime mode (mock vs real TFS) is active in the target environment.  
  **Evidence:** both `MockTfsClient` and `RealTfsClient` implementations exist.

- **UNKNOWN:** No repository evidence was found for user-facing persistence of manually entered epic duration or manual epic start dates in the active planning pages.  
  **Evidence:** current planning DTOs/pages/handlers inspected above do not expose them.

## 10. Recommendation for next phase

- **VERIFIED:** Phase 2 should not start implementation until the target planning surface is locked to **one** of these existing tracks:
  - extend the current TFS-backed `ProductRoadmaps` / `ProductRoadmapEditor` / `PlanBoard` / `MultiProductPlanning` flow, or
  - reactivate and integrate the separate `ReleasePlanningBoard` model.  
  **Evidence:** both tracks exist today and are structurally different (`WorkItemsController` mutation flow vs `ReleasePlanningController` persistence flow).

- **NO-GO for unconstrained Phase 2:** the repository currently has enough implementation to build on, but not enough alignment to safely proceed without first deciding which planning model is authoritative.

---

## Direct verification answers

### A. Is there already a timeline-based planning concept?

- **VERIFIED:** Yes, in `ProductRoadmaps` and `MultiProductPlanning`. It is forecast-driven and read-only.  
  **Evidence:** `ProductRoadmaps.razor` `BuildTimeline`; `MultiProductPlanning.razor` `BuildAlignedLane`; `RoadmapTimelineLayout.cs`.

### B. How is roadmap ordering stored and retrieved?

- **VERIFIED:** Stored in TFS `Microsoft.VSTS.Common.BacklogPriority`; roadmap inclusion is controlled by the `roadmap` tag. Retrieved through work item sync/read models and re-ranked into `RoadmapOrder`.  
  **Evidence:** `RealTfsClient.Core.cs`; `RealTfsClient.WorkItems.cs`; `RoadmapEpicDiscoveryAnalysis.cs`; `GetProductPlanningProjectionsQueryHandler.cs`; `UpdateWorkItemBacklogPriorityCommandHandler.cs`.

### C. How are sprints represented (IDs, dates, iteration paths)?

- **VERIFIED:** Via `SprintDto` and `SprintEntity` using internal `Id`, `TeamId`, optional `TfsIterationId`, `Path`, `Name`, `StartUtc`, `EndUtc`, `TimeFrame`, `LastSyncedUtc`.  
  **Evidence:** `SprintDto.cs`; `SprintEntity.cs`; `SprintRepository.cs`.

### D. Is there any existing concept of duration or scheduling?

- **VERIFIED:** Yes, but only indirectly:
  - forecast schedule: `EstimatedCompletionDate` + `SprintsRemaining`
  - derived timeline duration: `sprintsRemaining * sprint cadence`
  - separate release-planning board row positions: `RowIndex`, `VerticalPosition`  
  **Evidence:** `PlanningEpicProjectionDto`; `RoadmapTimelineLayout.BuildRow`; `ReleasePlanningDtos.cs`.

- **VERIFIED:** No persisted editable start-date/duration model was found for the active product-roadmap flow.  
  **Evidence:** `PlanningEpicProjectionDto`; `WorkItemsController` active planning mutations.

### E. What does the multi-product planning page currently do?

- **VERIFIED:** It loads all products for the active owner, fetches each product’s persisted planning projections, derives sprint cadence from team sprint history, aligns timelines on a shared axis, and overlays pressure/collision hints.  
  **Evidence:** `MultiProductPlanning.razor`, methods `LoadAsync`, `RebuildVisibleLanes`, `BuildAlignedLane`.

### F. Are there existing mutation endpoints relevant to planning?

- **VERIFIED:** Yes:
  - roadmap membership: `POST /api/WorkItems/{tfsId}/tags`
  - roadmap ordering: `POST /api/WorkItems/{tfsId}/backlog-priority`
  - sprint assignment: `POST /api/WorkItems/{tfsId}/iteration-path`
  - product hierarchy refresh: `POST /api/WorkItems/by-root-ids/refresh-from-tfs`
  - separate release-planning mutations: `api/ReleasePlanning/...`  
  **Evidence:** `WorkItemsController.cs`; `ReleasePlanningController.cs`.

---

## Final section (mandatory)

### Findings

1. **VERIFIED:** Current user-facing planning already covers roadmap overview, roadmap editing, sprint assignment, and multi-product forecast visualization.
2. **VERIFIED:** Roadmap order is currently a TFS concern (`BacklogPriority` + `roadmap` tag), not an app-owned ordering model.
3. **VERIFIED:** Sprint planning currently means assigning PBIs/Bugs to real team iteration paths.
4. **VERIFIED:** Timeline visualization already exists, but it is derived from forecast data and sprint cadence, not directly planned by users.
5. **VERIFIED:** A second persisted planning system (`ReleasePlanningBoard`) exists beside the active TFS-backed flow.

### Evidence

- Frontend pages:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanningWorkspace.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor`
- Frontend services/components:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapEpicDiscoveryAnalysis.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapEpicPriorityReorderPlanner.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/PlanBoardWorkItemRules.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`
- Backend endpoints/services:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProjectsController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/SprintsController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/RoadmapSnapshotsController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ReleasePlanningController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ProjectPlanningSummaryService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ForecastProjectionMaterializationService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`
- Shared/domain/persistence:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProjectPlanningSummaryDtos.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/SprintDto.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ForecastProjectionEntity.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PlanningEpicPlacementEntity.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PlanningBoardSettingsEntity.cs`

### Risks

- Competing planning models may cause Phase 2 to extend the wrong stack.
- Timeline UI is already duplicated across pages.
- Forecast timelines depend on sprint-history availability.
- Roadmap order is coupled to TFS backlog priority.
- Plan Board currently compresses sprint visibility to four columns.

### Open Questions

1. Should Phase 2 extend the active TFS-backed roadmap/plan-board flow or the separate Release Planning Board?
2. Should roadmap order remain coupled to TFS backlog priority?
3. Should multi-product planning remain read-only, or become a mutation surface?
4. Is a persisted editable timeline/date model required, or should forecast-derived scheduling remain authoritative?

### Go/No-Go for Phase 2 (Decision Lock)

- **Decision Lock:** **NO-GO** until the authoritative planning model is chosen.
- **Reason:** the repository already contains both an active TFS-backed planning flow and a separate persisted release-planning model; proceeding without choosing one would create overlap rather than extend a single current-state implementation.
