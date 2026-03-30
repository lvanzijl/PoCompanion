> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Delivery Slice Canonical Filter Migration

## Summary

The Delivery slice now resolves shared filter scope once at the API boundary and passes only a deterministic effective filter downstream for the migrated delivery endpoints.

Implemented changes:

- added `DeliveryFilterResolutionService` to normalize delivery product and time scope into a canonical `DeliveryEffectiveFilter`
- updated in-scope delivery endpoints to resolve requested versus effective scope before dispatching mediator queries
- wrapped migrated delivery responses in envelopes containing:
  - `RequestedFilter`
  - `EffectiveFilter`
  - `InvalidFields`
  - `ValidationMessages`
- removed handler-local owner/product intersection logic from:
  - portfolio progress trend
  - capacity calibration
  - portfolio delivery
  - home product bar
- moved sprint-to-window derivation for delivery build-quality sprint scope to the controller boundary resolver
- updated the delivery build-quality scope loader to consume effective product and time scope while keeping repository/pipeline/default-branch behavior slice-local
- updated delivery-related client calls to read the new response envelopes without changing existing page behavior

Why:

- delivery filtering semantics were previously split across controller parsing, handler-local owner scope resolution, and local sprint window derivation
- different delivery endpoints could silently interpret product scope differently
- the home product bar mixed owner-wide sprint progress with optionally product-filtered bug/change metrics
- build-quality delivery endpoints derived shared time semantics downstream instead of at the boundary

## Affected Files

### Controllers

- `PoTool.Api/Controllers/MetricsController.cs`
- `PoTool.Api/Controllers/BuildQualityController.cs`

### Filter resolution / shared delivery filtering

- `PoTool.Api/Services/DeliveryFilterResolutionService.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

### Delivery handlers / services

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/BuildQuality/GetBuildQualityRollingWindowQueryHandler.cs`
- `PoTool.Api/Handlers/BuildQuality/GetBuildQualitySprintQueryHandler.cs`
- `PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`

### Delivery query contracts / filter models

- `PoTool.Core/Delivery/Filters/DeliveryFilterModels.cs`
- `PoTool.Core/Metrics/Queries/GetPortfolioProgressTrendQuery.cs`
- `PoTool.Core/Metrics/Queries/GetCapacityCalibrationQuery.cs`
- `PoTool.Core/Metrics/Queries/GetPortfolioDeliveryQuery.cs`
- `PoTool.Core/Metrics/Queries/GetHomeProductBarMetricsQuery.cs`
- `PoTool.Core/BuildQuality/Queries/GetBuildQualityRollingWindowQuery.cs`
- `PoTool.Core/BuildQuality/Queries/GetBuildQualitySprintQuery.cs`
- `PoTool.Core/BuildQuality/Validators/GetBuildQualityRollingWindowQueryValidator.cs`
- `PoTool.Core/BuildQuality/Validators/GetBuildQualitySprintQueryValidator.cs`

### Shared/client DTO and API client updates

- `PoTool.Shared/Metrics/DeliveryFilterDtos.cs`
- `PoTool.Client/ApiClient/ApiClient.DeliveryFilters.cs`
- `PoTool.Client/ApiClient/ApiClient.BuildQualityDeliveryFilters.cs`
- `PoTool.Client/Services/BuildQualityService.cs`
- `PoTool.Client/Services/HomeProductBarMetricsService.cs`
- `PoTool.Client/Services/WorkspaceSignalService.cs`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
- `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`

### Tests

- `PoTool.Tests.Unit/Services/DeliveryFilterResolutionServiceTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerDeliveryCanonicalFilterTests.cs`
- `PoTool.Tests.Unit/Controllers/BuildQualityControllerDeliveryCanonicalFilterTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPortfolioProgressTrendQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPortfolioDeliveryQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetHomeProductBarMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/BuildQualityQueryHandlerTests.cs`
- `PoTool.Tests.Unit/TestSupport/DeliveryFilterTestFactory.cs`

## Before vs After

| Concern | Before | After |
| --- | --- | --- |
| Product scope | Controllers accepted owner/product inputs, then handlers re-derived owner products and re-applied local narrowing. | Controllers resolve owner/product scope once and handlers use only `DeliveryEffectiveFilter`. |
| Invalid product scope | Out-of-scope product selection could yield endpoint-specific empty results or owner-wide results depending on the handler. | Invalid product selections are normalized once at the boundary with validation metadata. |
| Time scope | Delivery handlers and build-quality sprint handlers derived sprint windows locally. | Sprint-to-window derivation happens once in `DeliveryFilterResolutionService`; downstream code consumes effective time values only. |
| Build-quality delivery scope | Delivery build-quality handlers and scope loader mixed owner scope and time derivation internally. | Shared product/time semantics come from `DeliveryEffectiveFilter`; repository/pipeline/default-branch logic remains slice-local inside build-quality services. |
| Response metadata | In-scope delivery endpoints returned raw payloads only. | Migrated delivery endpoints now return canonical filter metadata envelopes plus the payload. |
| Home product bar consistency | Selected product filtered bug/change counts, but sprint progress still stayed owner-wide. | Selected product now filters sprint progress, bug count, and change count consistently. |

## Validation

Correctness was ensured by:

- compiling the full solution in Release mode
- running focused delivery tests covering:
  - new delivery filter resolution behavior
  - new delivery controller envelope behavior
  - portfolio progress, capacity calibration, portfolio delivery, and home product bar handler behavior
  - delivery build-quality handler behavior
  - regression coverage for unaffected portfolio-read controller behavior

Validation commands:

```bash
dotnet build PoTool.sln --configuration Release
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeliveryFilterResolutionServiceTests|FullyQualifiedName~MetricsControllerDeliveryCanonicalFilterTests|FullyQualifiedName~BuildQualityControllerDeliveryCanonicalFilterTests|FullyQualifiedName~GetPortfolioProgressTrendQueryHandlerTests|FullyQualifiedName~GetCapacityCalibrationQueryHandlerTests|FullyQualifiedName~GetPortfolioDeliveryQueryHandlerTests|FullyQualifiedName~GetHomeProductBarMetricsQueryHandlerTests|FullyQualifiedName~BuildQualityQueryHandlerTests|FullyQualifiedName~MetricsControllerPortfolioReadTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal
```

## Known Limitations

- repository, pipeline-definition, and default-branch behavior inside delivery build-quality remains slice-local and is intentionally not modeled as shared canonical filter semantics
- the single-pipeline build-quality detail endpoint remains unchanged because it is not part of the delivery-scope endpoints targeted by this migration
- delivery pages still choose their existing UI-owned sprint ranges and selected products; this migration standardizes backend interpretation rather than redesigning page-level selection behavior
- a separate pre-existing SQLite projection test class still contains unrelated failing scenarios and was excluded from the focused delivery validation set

## Correctness Fixes

- selected product scope on `GET /api/metrics/home-product-bar` now applies consistently to sprint progress as well as bug and change metrics, fixing the previous mixed owner-wide versus product-filtered response semantics
- invalid delivery product selections are now normalized deterministically at the boundary instead of producing endpoint-specific silent differences
