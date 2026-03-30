# Test Cleanup Step 1

_Generated: 2026-03-17_

Reference documents:

- `docs/analysis/test-ownership-audit.md`
- `docs/analysis/cdc-completion-summary.md`
- `docs/architecture/cdc-reference.md`

## Files Updated

- `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`
  - removed after its remaining sprint-history semantics were folded into the CDC sprint suite
- `PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs`
  - strengthened with the boundary and first-Done assertions previously held outside the CDC suite
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
  - reduced to null handling, CDC-service orchestration, and empty-sprint projection behavior
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
  - reduced to CDC-service orchestration coverage
- `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
  - replaced percentile and ratio re-checks with sample-filtering and DTO-mapping assertions
- `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - replaced forecast-math assertions with product-root loading and forecast DTO mapping coverage
- `PoTool.Tests.Unit/Handlers/GetEffortDistributionTrendQueryHandlerTests.cs`
  - replaced trend-formula assertions with area filtering, product-root loading, and DTO mapping coverage
- `PoTool.Tests.Unit/Handlers/GetEffortImbalanceQueryHandlerTests.cs`
  - removed direct risk-band scenarios and kept empty handling, mapping, filtering, and recommendation coverage
- `PoTool.Tests.Unit/Audits/TestCleanupStep1DocumentTests.cs`
  - added report coverage for this cleanup artifact

## Semantic Assertions Removed

Removed duplicate non-CDC semantic coverage for:

- sprint commitment boundary semantics previously asserted in:
  - `HistoricalSprintLookupTests.cs`
  - `GetSprintMetricsQueryHandlerTests.cs`
  - `GetSprintExecutionQueryHandlerTests.cs`
- first-Done and reopen semantics previously asserted in:
  - `HistoricalSprintLookupTests.cs`
  - `GetSprintMetricsQueryHandlerTests.cs`
  - `GetSprintExecutionQueryHandlerTests.cs`
- spillover boundary semantics previously asserted in:
  - `HistoricalSprintLookupTests.cs`
  - `GetSprintExecutionQueryHandlerTests.cs`
- canonical story-point source rules previously asserted in:
  - `GetSprintMetricsQueryHandlerTests.cs`
  - `GetSprintExecutionQueryHandlerTests.cs`
  - `GetEpicCompletionForecastQueryHandlerTests.cs`
- velocity percentile, predictability, and outlier formulas previously asserted in:
  - `GetCapacityCalibrationQueryHandlerTests.cs`
- completion-forecast math and confidence threshold formulas previously asserted in:
  - `GetEpicCompletionForecastQueryHandlerTests.cs`
- effort trend direction and forecast-generation formulas previously asserted in:
  - `GetEffortDistributionTrendQueryHandlerTests.cs`
- imbalance score and risk-band formulas previously asserted in:
  - `GetEffortImbalanceQueryHandlerTests.cs`

## Orchestration Tests Retained

The following higher-layer checks remain because they verify application behavior rather than CDC formulas:

- `GetSprintMetricsQueryHandlerTests.cs`
  - missing sprint metadata returns `null`
  - CDC sprint services are invoked and their results are surfaced through the DTO
  - known sprint without historical scope returns zeroed metrics with preserved sprint dates
- `GetSprintExecutionQueryHandlerTests.cs`
  - CDC sprint services are invoked for execution reconstruction and the handler still maps persisted work items into the execution DTO
- `GetCapacityCalibrationQueryHandlerTests.cs`
  - no-product and no-sprint empty handling
  - owner/product filtering
  - projection samples passed into `IVelocityCalibrationService`
  - DTO mapping from `VelocityCalibration`
- `GetEpicCompletionForecastQueryHandlerTests.cs`
  - missing epic returns `null`
  - product-root hierarchy loading via `GetWorkItemsByRootIdsQuery`
  - DTO mapping from `IHierarchyRollupService` + `ICompletionForecastService`
- `GetEffortDistributionTrendQueryHandlerTests.cs`
  - area-path filtering before CDC analysis
  - product-root hierarchy loading
  - DTO mapping from `IEffortTrendForecastService`
- `GetEffortImbalanceQueryHandlerTests.cs`
  - no-work-item empty handling
  - handler mapping from analyzer buckets into team/sprint DTOs
  - area filtering
  - recommendation generation
  - capacity-utilization description behavior

## CDC Tests Strengthened

Strengthened CDC ownership in `SprintCommitmentCdcServicesTests.cs` by adding direct CDC assertions for:

- commitment membership at the exact commitment timestamp boundary
- first-Done detection using canonical mappings case-insensitively
- reopen behavior preserving the first Done transition
- spillover detection when the iteration move happens exactly at sprint end

No additional CDC strengthening was required for forecasting or effort diagnostics because the canonical suites already cover the removed semantics:

- `ForecastingDomainServicesTests.cs`
- `CanonicalStoryPointResolutionServiceTests.cs`
- `EffortDiagnosticsDomainModelsTests.cs`
- `EffortDiagnosticsAnalyzerTests.cs`

## Remaining Duplicate Risk

Remaining duplicate risk is low.

- `GetEffortImbalanceQueryHandlerTests.cs` still compares handler output with analyzer-produced bucket facts, but it now does so to verify mapping rather than to re-prove the analyzer formulas with independent literals.
- `GetSprintMetricsQueryHandlerTests.cs` and `GetSprintExecutionQueryHandlerTests.cs` keep only CDC-interface orchestration coverage and no longer re-state sprint semantics through persisted-input scenario math.
- If future handlers need richer regression coverage, add assertions to the CDC/domain suite first and keep handler tests focused on loading, filtering, invocation, and DTO shape.
