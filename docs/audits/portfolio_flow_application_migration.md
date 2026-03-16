# PortfolioFlow Application Migration

## Legacy Path Removed

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` no longer reconstructs portfolio flow from `SprintMetricsProjectionEntity.PlannedEffort`, `CompletedPbiEffort`, or effort replay helpers.
- The portfolio progress / flow application path now reads persisted `PortfolioFlowProjectionEntity` rows instead of rebuilding an effort-based proxy model inside the handler.
- Residual effort semantics (`AddedEffort` as commitment proxy, residual `RemainingEffort`, and effort-based `PercentDone`) were removed from the application logic for this path.

## Canonical Projection Adopted

- The handler now aggregates canonical story-point metrics from `PortfolioFlowProjectionEntity` per sprint and product.
- The application path now exposes these canonical PortfolioFlow metrics:
  - `StockStoryPoints`
  - `RemainingScopeStoryPoints`
  - `InflowStoryPoints`
  - `ThroughputStoryPoints`
  - `CompletionPercent`
- `NetFlow` is now derived from canonical story-point throughput minus canonical story-point inflow.

## DTO Compatibility Decisions

- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs` now adds canonical story-point fields explicitly.
- Legacy transport properties were retained only as compatibility aliases:
  - `PercentDone` → `CompletionPercent`
  - `TotalScopeEffort` → `StockStoryPoints`
  - `RemainingEffort` → `RemainingScopeStoryPoints`
  - `ThroughputEffort` → `ThroughputStoryPoints`
  - `AddedEffort` → `InflowStoryPoints`
  - `NetFlow` → `NetFlowStoryPoints`
- Summary aliases were also retained temporarily:
  - `TotalScopeChangePts` → `TotalScopeChangeStoryPoints`
  - `RemainingEffortChangePts` → `RemainingScopeChangeStoryPoints`

## UI Changes

- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` now presents the view as portfolio flow backed by canonical story-point semantics.
- Labels and tooltips were updated to remove effort-proxy wording and describe:
  - portfolio stock
  - remaining scope
  - inflow
  - throughput
  - completion
- Charts now bind to canonical story-point fields rather than legacy effort-named properties.

## Tests Updated

- `PoTool.Tests.Unit/Handlers/GetPortfolioProgressTrendQueryHandlerTests.cs` now validates handler aggregation from `PortfolioFlowProjectionEntity` plus compatibility aliases.
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs` now verifies the migrated handler matches canonical projection values on the representative historical scenario.
- `PoTool.Tests.Unit/Audits/UiSemanticLabelsTests.cs` now checks that `PortfolioProgressPage.razor` uses story-point-correct portfolio flow labels.
- `PoTool.Tests.Unit/Audits/PortfolioFlowApplicationMigrationDocumentTests.cs` guards this migration report.

## Remaining Portfolio Work

- Portfolio delivery distribution remains out of scope.
- Ranking / top contributor portfolio work remains out of scope.
- Forecasting migration remains out of scope.
- Persistence abstraction and larger structural changes remain out of scope.
- Additional portfolio audit documents may still describe the pre-migration effort-based state as historical analysis and can be revised separately.
