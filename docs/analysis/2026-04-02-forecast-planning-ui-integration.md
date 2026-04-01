# Forecast Planning UI Integration Report

## Endpoint structure

Added product-scoped read endpoint:

- `GET /api/products/{productId}/planning-projections`

Response model:

- `PlanningEpicProjectionDto`
  - `EpicId`
  - `EpicTitle`
  - `RoadmapOrder`
  - `SprintsRemaining`
  - `EstimatedCompletionDate`
  - `Confidence`
  - `HasForecast`
  - `LastUpdated`

Implementation path:

- controller: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs`
- query: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Queries/GetProductPlanningProjectionsQuery.cs`
- handler: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs`
- shared DTO: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`

The handler:

- loads resolved Epic work items for the requested product
- filters to roadmap-tagged epics
- orders by backlog priority
- left-joins `ForecastProjectionEntity`
- maps persisted fields only

No forecast recalculation was added.

## UI before/after

### Before

`ProductRoadmaps.razor` loaded forecast data one epic at a time through the metrics endpoint and rendered:

- primary forecast signal: `~N sprints`
- optional warning: `Epic exceeds velocity`
- no visible projected completion date on the roadmap lane
- no lane-level forecast timeline

### After

`ProductRoadmaps.razor` now loads persisted projections once per product and renders:

- primary forecast signal: projected completion date
- secondary forecast signal: `~N sprints`
- per-lane visual sequence bars in roadmap order
- delayed highlighting when projected completion is in the past and the epic is not done
- forecast freshness via `LastUpdated`

The old per-epic forecast recomputation path is no longer used by the planning roadmap page.

## Example rendering of one product lane

Example lane shape after integration:

1. **Lane header**
   - product name
   - roadmap epic count
   - edit roadmap action

2. **Projected sequence panel**
   - `#1 Epic A ................................ 12 Apr 2026`
   - `#2 Epic B ................................ 03 May 2026`
   - `#3 Epic C ................................ 28 May 2026`

3. **Epic card**
   - `12 Apr 2026` (primary forecast chip)
   - `~2 sprints` (secondary chip)
   - `Delayed by 4 days` when applicable
   - `Forecast updated 01 Apr 2026`

## Exact files changed for this integration

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Queries/GetProductPlanningProjectionsQuery.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetProductPlanningProjectionsQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/UiSemanticLabelsTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## Limitations

- The lane timeline is a **visual sequential assumption only**.
- The first bar starts at today.
- Each later bar starts at the previous epic's projected end.
- This does **not** represent real dependency scheduling, team allocation, or cross-product planning.
- If a later epic has an earlier persisted completion date than the previous visual start, the UI clamps the bar visually so the sequence remains renderable while still showing the stored projected end date.

## Validation summary

- endpoint reads persisted `ForecastProjectionEntity` rows only
- planning roadmap UI no longer calls the forecast calculation endpoint for each epic
- no new forecast logic was introduced in the UI
