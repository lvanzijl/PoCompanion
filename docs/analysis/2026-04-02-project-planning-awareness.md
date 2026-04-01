# Project Planning Awareness

## Scope
This report documents the read-only project planning awareness layer added on top of the existing project alias foundation.

The change keeps all planning write paths product-scoped while exposing project-level planning signals through:
- `GET /api/projects/{alias}/planning-summary`
- `/planning/{projectAlias}/overview`
- summary bars on project-scoped Plan Board and Product Roadmaps pages

## Aggregation logic

### Read model
`PoTool.Shared/Planning/ProjectPlanningSummaryDtos.cs`
- `ProjectPlanningSummaryDto`
  - `ProjectAlias`
  - `ProductCount`
  - `TotalEpics`
  - `TotalPBIs`
  - `PlannedPBIs`
  - `UnplannedPBIs`
  - `TotalEffort`
  - `PlannedEffort`
  - `CapacityPerSprint`
  - `OvercommitIndicator`
  - `Products` (per-product read-only breakdown used for UI distribution and risk surfacing)
- `ProjectPlanningProductSummaryDto`
  - `ProductId`
  - `ProductName`
  - `EpicCount`
  - `TotalPBIs`
  - `PlannedPBIs`
  - `UnplannedPBIs`
  - `TotalEffort`
  - `PlannedEffort`
  - `CapacityPerSprint`

### Server aggregation service
`PoTool.Api/Services/ProjectPlanningSummaryService.cs`

The service aggregates existing persisted/cached product planning data without changing any existing handler.

#### Inputs reused
- project membership from `ProjectRepository`
- product backlog roots and team links from existing product data
- work item hierarchies from existing `IWorkItemReadProvider.GetByRootIdsAsync(...)`
- visible sprint windows from existing sprint entities
- historical sprint velocity from existing `SprintMetricsProjectionEntity` rows plus `IVelocityCalibrationService`
- done/removed semantics from `WorkItemStateClassificationEntity`

#### Per-product calculation
For each product in the project:
1. resolve all descendants under the configured backlog roots
2. count roadmap epics as `Epic + roadmap tag`
3. count PBIs using existing PBI type semantics while excluding Done/Removed items
4. determine planned PBIs by matching the product's PBIs to the same visible sprint window logic already used by Plan Board
5. sum effort from active PBIs for `TotalEffort`
6. sum effort from visible-sprint PBIs for `PlannedEffort`
7. compute capacity per sprint from the product's recent completed sprint projections using the existing velocity calibration service

#### Project rollup
The project summary is the sum of product summaries.

### Capacity aggregation
Project capacity per sprint is defined as:

`SUM(product median velocity)`

Implementation details:
- recent completed sprints come from each product's first linked team
- the last 6 completed sprints are used, matching the existing Plan Board calibration pattern
- calibration uses the existing `VelocityCalibrationService`
- `CapacityPerSprint` is the summed median velocity across the products in the project

### Overcommit calculation
`OvercommitIndicator` is calculated by comparing:
- total planned effort across the visible sprint window
- against the sum of each product's `capacityPerSprint * visibleSprintCount`

This preserves the existing read-only planning semantics already used on the product plan board, where effort and calibrated capacity are surfaced together as advisory planning signals.

## API

### Endpoint
- `GET /api/projects/{alias}/planning-summary`

### Controller and handler
- `PoTool.Api/Controllers/ProjectsController.cs`
- `PoTool.Api/Handlers/Settings/Projects/GetProjectPlanningSummaryQueryHandler.cs`
- `PoTool.Core/Settings/Queries/GetProjectPlanningSummaryQuery.cs`

### Route classification
The new endpoint is classified as cache-only analytical read in:
- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`

This prevents the project planning summary endpoint from inheriting the broader live-allowed `/api/projects` discovery behavior.

### Example response
```json
{
  "projectAlias": "project-alpha",
  "productCount": 2,
  "totalEpics": 2,
  "totalPBIs": 3,
  "plannedPBIs": 2,
  "unplannedPBIs": 1,
  "totalEffort": 60,
  "plannedEffort": 50,
  "capacityPerSprint": 16.0,
  "overcommitIndicator": true,
  "products": [
    {
      "productId": 1,
      "productName": "Alpha Product",
      "epicCount": 1,
      "totalPBIs": 2,
      "plannedPBIs": 1,
      "unplannedPBIs": 1,
      "totalEffort": 40,
      "plannedEffort": 30,
      "capacityPerSprint": 11.0
    },
    {
      "productId": 2,
      "productName": "Beta Product",
      "epicCount": 1,
      "totalPBIs": 1,
      "plannedPBIs": 1,
      "unplannedPBIs": 0,
      "totalEffort": 20,
      "plannedEffort": 20,
      "capacityPerSprint": 5.0
    }
  ]
}
```

## UI placement

### New overview route
- `/planning/{projectAlias}/overview`
- implemented in `PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`

The page shows:
- top-level project planning summary cards
- risk chips
- per-product distribution table
- links to:
  - roadmaps
  - plan board

### Planning hub integration
- `PoTool.Client/Pages/Home/PlanningWorkspace.razor`
- adds a conditional `Project Overview` navigation tile when a project alias is present in context

### Plan Board integration
- `PoTool.Client/Pages/Home/PlanBoard.razor`
- when a project is selected, a summary bar appears above the board
- the bar shows:
  - planned vs unplanned PBIs
  - planned effort vs total effort
  - capacity per sprint
  - imbalance/overcommit indicators
- no cross-product drag/drop or merged board behavior was added

### Roadmap integration
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- when a project is selected, a project roadmap summary bar appears above the product lanes
- the bar shows:
  - combined roadmap epic count
  - per-product epic distribution
  - roadmap dominance signal
- existing per-product roadmap behavior remains unchanged

## Detected risks surfaced

### 1. Product imbalance
Definition used for project overview and plan board:
- one product holds more than 60% of total project effort

### 2. Planning gap
Definition used for project overview and plan board:
- more than 50% of project PBIs are unplanned

### 3. Overcommit
Definition used for project overview and plan board:
- planned effort across the visible sprint window exceeds the aggregated calibrated capacity window

### 4. Route classification risk
A new analytical route under `/api/projects` would have incorrectly inherited the existing live-allowed prefix behavior.

Mitigation applied:
- `/api/projects/{alias}/planning-summary` is explicitly recognized as cache-only analytical read before the broader `/api/projects` live-allowed prefix check

## Constraints preserved
- zero write-path changes
- zero changes to existing handlers
- zero changes to iteration path logic
- no cross-product planning operations
- plan board still operates on a single selected product
- roadmap view still shows separate product lanes

## Validation performed
- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo -v minimal --filter "FullyQualifiedName~ProjectPlanningSummaryServiceTests|FullyQualifiedName~WorkspaceRoutesTests|FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~ReleaseNotesServiceTests"`
- manual API verification of `/api/projects/project-alpha/planning-summary`
- manual UI verification of:
  - `/planning/project-alpha/overview`
  - `/planning/project-alpha/plan-board`
  - `/planning/project-alpha/product-roadmaps`

## Screenshot
Suitable overview screenshot for PR description:
- https://github.com/user-attachments/assets/9d11058b-8d4d-4cc5-9c8c-2bba8ccc7d9c
