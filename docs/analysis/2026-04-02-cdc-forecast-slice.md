# CDC Forecast Slice Centralization Report

## Scope

This change centralizes Epic/Feature delivery forecast projection logic inside the existing `Forecasting` slice without renaming the slice. It adds persisted forecast projections, refreshes them during the sync pipeline, and changes the Epic forecast handler to read the persisted projection instead of recomputing forecast math on demand.

## Exact file moves and additions

### New Forecasting slice component/module

- Added `PoTool.Core.Domain/Domain/Forecasting/DeliveryForecast/DeliveryForecastProjector.cs`
- Added `PoTool.Core.Domain/Domain/Forecasting/DeliveryForecast/ForecastProjection.cs`

### Existing Forecasting adapter updated

- Updated `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
  - previously contained the completion-projection math directly
  - now delegates to `DeliveryForecastProjector`

### New persisted projection storage

- Added `PoTool.Api/Persistence/Entities/ForecastProjectionEntity.cs`
- Updated `PoTool.Api/Persistence/PoToolDbContext.cs`
- Updated `PoTool.Api/Persistence/Entities/ProductOwnerCacheStateEntity.cs`
- Added migration `PoTool.Api/Migrations/20260401170553_AddForecastProjections.cs`
- Added migration designer `PoTool.Api/Migrations/20260401170553_AddForecastProjections.Designer.cs`

### New projection materialization path

- Added `PoTool.Api/Services/ForecastProjectionMaterializationService.cs`
- Added `PoTool.Api/Services/ForecastProjectionStoredModels.cs`
- Added `PoTool.Api/Services/Sync/ForecastProjectionSyncStage.cs`
- Updated `PoTool.Api/Services/Sync/SyncPipelineRunner.cs`
- Updated `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

### Handler and test updates

- Updated `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- Updated `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
- Added `PoTool.Tests.Unit/Services/ForecastProjectionMaterializationServiceTests.cs`
- Updated `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs`
- Updated `docs/analysis/forecasting-cdc-summary.md`
- Updated `PoTool.Tests.Unit/Audits/ForecastingCdcSummaryDocumentTests.cs`

## Old vs new data flow

### Old flow

1. `GetEpicCompletionForecastQueryHandler` loaded work items.
2. The handler rebuilt done-state lookups and canonical scope rollups.
3. The handler rebuilt historical velocity samples by calling sprint metrics queries.
4. The handler called `ICompletionForecastService` directly.
5. The handler mapped the in-memory forecast result to `EpicCompletionForecastDto`.

### New flow

1. Sync pipeline computes sprint trend projections.
2. `ForecastProjectionSyncStage` runs after sprint trend projection refresh.
3. `ForecastProjectionMaterializationService` loads canonical work item snapshots, hierarchy inputs, state classifications, and persisted sprint history.
4. `ForecastProjectionMaterializationService` calls `DeliveryForecastProjector` inside the `Forecasting` slice.
5. `ForecastProjectionMaterializationService` persists `ForecastProjectionEntity` rows containing velocity-window variants.
6. `GetEpicCompletionForecastQueryHandler` reads `ForecastProjectionEntity`, selects the requested velocity-window variant, and maps it to `EpicCompletionForecastDto`.
7. UI continues consuming the existing DTO contract and does not trigger recomputation.

## Where logic previously lived vs now

### Previously

- Completion projection math lived in `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
- Forecast orchestration plus on-demand recomputation lived in `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`

### Now

- Canonical delivery-forecast projection math lives in `PoTool.Core.Domain/Domain/Forecasting/DeliveryForecast/DeliveryForecastProjector.cs`
- The compatibility adapter remains in `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
- Canonical persisted read model lives in `PoTool.Core.Domain/Domain/Forecasting/DeliveryForecast/ForecastProjection.cs`
- Persistence/materialization lives in `PoTool.Api/Services/ForecastProjectionMaterializationService.cs`
- Durable storage lives in `PoTool.Api/Persistence/Entities/ForecastProjectionEntity.cs`
- Handler behavior is read-only in `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`

## Persistence model

`ForecastProjectionEntity` stores:

- `WorkItemId`
- `WorkItemType`
- `SprintsRemaining`
- `EstimatedCompletionDate`
- `Confidence`
- `LastUpdated`
- `ProjectionVariantsJson`

`ProjectionVariantsJson` preserves the existing velocity-window behavior by storing variants for velocity windows 1 through 20 under the same `WorkItemId` key.

## Verification that no handler recomputes forecasts

Verified in `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`:

- no `ICompletionForecastService` dependency remains
- no `IHierarchyRollupService` dependency remains
- no state-classification lookup remains
- no historical sprint query/rebuild remains
- handler logic is limited to:
  - loading the work item row
  - loading the persisted projection row
  - selecting the stored velocity-window variant
  - mapping persisted values to `EpicCompletionForecastDto`

## Risk verification

### Stale projections

- Addressed by adding `ForecastProjectionSyncStage` into the sync pipeline
- `ProductOwnerCacheStateEntity.ForecastProjectionAsOfUtc` now records last forecast materialization time

### Historical velocity vs stored projection mismatch

- Materialization now depends on persisted sprint trend projections
- Forecast projections are refreshed after sprint trend projections in the sync pipeline to keep inputs aligned

### Partial migration

- Verified handler path now reads persisted projections only
- Focused tests cover:
  - projector semantics
  - persisted projection materialization
  - handler read/mapping behavior

## Validation

- `dotnet build PoTool.Api/PoTool.Api.csproj --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~GetEpicCompletionForecastQueryHandlerTests|FullyQualifiedName~ForecastProjectionMaterializationServiceTests|FullyQualifiedName~ForecastingDomainServicesTests|FullyQualifiedName~ForecastingCdcSummaryDocumentTests"`
