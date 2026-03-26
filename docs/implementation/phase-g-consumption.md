# Phase G Consumption Validation

## Summary

Phase G now exposes the CDC delivery model through explicit read models, application query services, read-only API endpoints, and an initial UI panel embedded in the existing Portfolio Progress workspace.

The implementation keeps mutation out of scope and reuses existing CDC domain services for snapshot creation, comparison, and portfolio aggregation. Filtering, sorting, and grouping are applied only after the underlying snapshot and comparison data is produced.

## DTO design

Added read-only transport models under `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs`:

- `PortfolioProgressDto`
- `PortfolioSnapshotDto`
- `PortfolioSnapshotItemDto`
- `PortfolioComparisonDto`
- `PortfolioComparisonItemDto`

Supporting read-only transport enums/options were also added:

- `PortfolioLifecycleState`
- `PortfolioReadSortBy`
- `PortfolioReadSortDirection`
- `PortfolioReadGroupBy`
- `PortfolioReadQueryOptions`

Design constraints followed:

- DTOs are projections only
- no recalculation lives inside DTOs
- no lifecycle decisions live inside DTOs
- snapshot item progress remains in the canonical unit interval `[0, 1]`

## Query services

Application query services were added under `PoTool.Api/Services`:

- `PortfolioProgressQueryService`
- `PortfolioSnapshotQueryService`
- `PortfolioComparisonQueryService`

Supporting services:

- `PortfolioReadModelStateService`
- `PortfolioReadModelMapper`
- `PortfolioReadModelFiltering`

Read-only Mediator queries and handlers were added:

- `GetPortfolioProgressQuery`
- `GetPortfolioSnapshotsQuery`
- `GetPortfolioComparisonQuery`
- matching handlers in `PoTool.Api/Handlers/Metrics`

Service responsibilities:

- load the latest available snapshot state
- call canonical snapshot/comparison/product aggregation services
- map domain outputs explicitly to DTOs
- apply output filtering, sorting, and grouping hints

No query service mutates state or reimplements domain aggregation formulas.

## Filtering behavior

Supported output filters:

- `ProductId`
- `ProjectNumber`
- `WorkPackage`
- `LifecycleState`

Supported presentational options:

- sorting by `Progress`, `Weight`, or `Delta`
- grouping hint by `Product`, `Project`, or `WorkPackage`

Filtering is applied after the latest snapshot and comparison outputs are built. Portfolio progress remains the unfiltered portfolio value, while the item lists are filtered for consumption.

## API endpoints

Read-only endpoints were added in `PoTool.Api/Controllers/MetricsController.cs` with absolute routes:

- `GET /api/portfolio/progress`
- `GET /api/portfolio/snapshots`
- `GET /api/portfolio/comparison`

These endpoints only dispatch queries through Mediator. No POST or mutation endpoint was added.

## UI integration details

The initial UI integration is a new read-only component:

- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`

It is hosted inside the existing page:

- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`

The UI consumes only the new read models through handcrafted `IMetricsClient` extensions:

- `GetPortfolioProgressAsync`
- `GetPortfolioSnapshotsAsync`
- `GetPortfolioComparisonAsync`

The UI displays:

- latest portfolio progress
- current snapshot rows
- snapshot comparison rows
- lifecycle state per row

No UI aggregation or portfolio calculation was introduced.

## Test coverage

Added focused tests for:

- explicit mapping:
  - `PoTool.Tests.Unit/Services/PortfolioReadModelMapperTests.cs`
- query service filtering and read-only behavior:
  - `PoTool.Tests.Unit/Services/PortfolioQueryServicesTests.cs`
- API endpoint query mapping:
  - `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`
- UI consumption/source audit:
  - `PoTool.Tests.Unit/Audits/PortfolioCdcUiAuditTests.cs`
- documentation audit:
  - `PoTool.Tests.Unit/Audits/PhaseGConsumptionDocumentTests.cs`
- DI registration:
  - `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`

## Files changed

- `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs`
- `PoTool.Core/Metrics/Queries/GetPortfolioProgressQuery.cs`
- `PoTool.Core/Metrics/Queries/GetPortfolioSnapshotsQuery.cs`
- `PoTool.Core/Metrics/Queries/GetPortfolioComparisonQuery.cs`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs`
- `PoTool.Api/Services/PortfolioReadModelMapper.cs`
- `PoTool.Api/Services/PortfolioReadModelFiltering.cs`
- `PoTool.Api/Services/PortfolioProgressQueryService.cs`
- `PoTool.Api/Services/PortfolioSnapshotQueryService.cs`
- `PoTool.Api/Services/PortfolioComparisonQueryService.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioSnapshotsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioComparisonQueryHandler.cs`
- `PoTool.Api/Controllers/MetricsController.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Client/ApiClient/ApiClient.PortfolioConsumption.cs`
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Tests.Unit/Services/PortfolioReadModelMapperTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioQueryServicesTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`
- `PoTool.Tests.Unit/Audits/PhaseGConsumptionDocumentTests.cs`
- `PoTool.Tests.Unit/Audits/PortfolioCdcUiAuditTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `docs/implementation/phase-g-consumption.md`
- `docs/release-notes.json`

## Build/test results

Validated with:

- `dotnet build PoTool.sln --configuration Release`
- targeted Phase G unit/audit tests covering mapper, query services, controller routes, DI, UI source audit, and the report audit

## Remaining risks

- The read-only snapshot state is derived from the latest available sprint-backed CDC data; it does not introduce new snapshot persistence.
- Snapshot rows require canonical project-number data on epics. Rows missing that business key are skipped rather than mutated into a synthetic key.
- Grouping is exposed as a presentational hint and ordered output; the UI still renders the grouped rows without re-running domain calculations.
