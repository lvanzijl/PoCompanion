# Delivery Trend Analytics CDC Summary

_Generated: 2026-03-15_

Reference documents:

- `docs/analysis/trend-delivery-analytics-exploration.md`
- `docs/architecture/domain-model.md`

## Scope re-audited

This re-audit covers only the narrow delivery trend analytics core:

- sprint delivery projection
- sprint trend metrics
- feature progress rollups
- epic progress rollups
- progression delta logic
- canonical delivery trend domain models

Excluded from this audit:

- forecasting
- capacity calibration
- effort distribution trend
- portfolio flow analytics
- PR/pipeline analytics

## Expected CDC ownership

The CDC is expected to own:

- sprint delivery projection formulas
- feature progress rollups
- epic progress rollups
- progression delta logic
- canonical delivery trend domain models

## What moved into the CDC

The audited slice is now primarily owned by `PoTool.Core.Domain/Domain/DeliveryTrends`.

Moved into the CDC:

- canonical delivery trend domain models in `PoTool.Core.Domain/Domain/DeliveryTrends/Models`
  - `SprintDeliveryProjection`
  - `SprintTrendMetrics`
  - `FeatureProgress`
  - `EpicProgress`
  - `ProgressionDelta`
  - `ProductDeliveryProgressSummary`
- sprint delivery projection formulas in `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- feature progress, epic progress, and progression delta rollups in `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
- product progress summary aggregation in `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressSummaryCalculator.cs`

Observed ownership after extraction:

- canonical formulas now execute in `PoTool.Core.Domain`
- API services prepare inputs, orchestrate persistence, and map CDC outputs to existing entities/DTOs
- DTOs remain transport-oriented and preserve legacy API field shapes where needed
- no local formula duplication remains in the audited handlers/services for this slice

## What remains outside the CDC

The following stay outside the audited CDC slice by design:

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
  - loads EF data
  - reconstructs request inputs from persistence models and activity ledgers
  - persists `SprintMetricsProjectionEntity`
  - maps CDC outputs back to API entities and DTOs
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
  - orchestrates cached vs recomputed retrieval
  - loads sprint/product metadata
  - shapes `GetSprintTrendMetricsResponse`
  - exposes staleness metadata
- excluded analytics families
  - forecasting
  - capacity calibration
  - effort distribution trend
  - portfolio flow analytics
  - PR/pipeline analytics

These remaining responsibilities are orchestration or adjacent analytics, not ownership of the audited delivery trend formulas.

## Implementation audit

### Formulas and rollups

Confirmed in `PoTool.Core.Domain`:

- sprint delivery projection formulas live in `SprintDeliveryProjectionService`
- feature progress rollups live in `DeliveryProgressRollupService`
- epic progress rollups live in `DeliveryProgressRollupService`
- progression delta logic lives in `DeliveryProgressRollupService`
- product-level progress summaries live in `DeliveryProgressSummaryCalculator`

### API orchestration boundary

Confirmed in `PoTool.Api`:

- `SprintTrendProjectionService` orchestrates EF queries, request assembly, and persistence
- `GetSprintTrendMetricsQueryHandler` orchestrates recompute/cached retrieval and DTO shaping
- API code maps CDC outputs but does not re-implement the audited formulas locally

### DTO boundary

Confirmed:

- `FeatureProgressDto`, `EpicProgressDto`, and sprint trend response DTOs remain transport-only
- legacy `*Effort` DTO names are preserved for API compatibility, while the CDC models use canonical story-point naming internally

## Remaining issues

Classification: **minor cleanup**

Remaining cleanup items:

1. The re-audit identified one API composition cleanup item in `PoTool.Api/Services/SprintTrendProjectionService.cs`: convenience constructors instantiated CDC services directly instead of relying exclusively on injected abstractions.
2. API adapters still translate between canonical CDC models and legacy DTO field names such as `*Effort`. This is acceptable transport compatibility, but it means the API boundary still carries some legacy naming debt outside the CDC.

Neither item blocks CDC ownership for the audited slice.

## Test validation

Existing focused tests cover the required areas:

- projection semantics
  - `PoTool.Tests.Unit/Services/SprintDeliveryProjectionServiceTests.cs`
- rollup semantics
  - `PoTool.Tests.Unit/Services/DeliveryProgressRollupServiceTests.cs`
- canonical delivery trend domain models
  - `PoTool.Tests.Unit/Services/DeliveryTrendDomainModelsTests.cs`
- handler orchestration and recompute path
  - `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs`

Validated locally during this re-audit:

- `dotnet build PoTool.sln --no-restore`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~DeliveryProgressRollupServiceTests|FullyQualifiedName~SprintDeliveryProjectionServiceTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~GetSprintTrendMetricsQueryHandlerTests|FullyQualifiedName~DeliveryTrendDomainModelsTests|FullyQualifiedName~TrendDeliveryAnalyticsExplorationDocumentTests" -v minimal`

These tests protect:

- projection semantics
- rollup semantics
- handler orchestration
- recompute path

## CDC Minor Cleanup Completed

- CDC services are now injected via DI in `PoTool.Api/Services/SprintTrendProjectionService.cs`; the API service no longer constructs `DeliveryProgressRollupService` or `SprintDeliveryProjectionService` via convenience constructors.
- API orchestration responsibilities remain unchanged: EF loading, request assembly, persistence, and DTO/entity mapping still stay in `PoTool.Api`.
- delivery trend formulas, rollups, and progression logic remain exclusively in `PoTool.Core.Domain`.

## Final verdict

**Delivery Trend Analytics CDC ready after minor cleanup**

The narrow delivery trend analytics core is now cleanly CDC-owned for formulas, rollups, and canonical models. The remaining issues are boundary/composition cleanup items on the API side and do not block the CDC readiness of the audited slice.
