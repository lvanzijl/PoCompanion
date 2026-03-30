# Product Aggregation Validation

## What was added

- Added `IProductAggregationService` and `ProductAggregationService` under `PoTool.Core.Domain/Domain/DeliveryTrends/Services`.
- Added `ProductAggregationRequest`, `ProductAggregationEpicInput`, and `ProductAggregationResult` under `PoTool.Core.Domain/Domain/DeliveryTrends/Models`.
- Registered the new aggregation engine in `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` so downstream handlers and projections can consume a single canonical service.
- Added focused tests in:
  - `PoTool.Tests.Unit/Services/ProductAggregationServiceTests.cs`
  - `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
  - `PoTool.Tests.Unit/Audits/ProductAggregationValidationDocumentTests.cs`

## Single source of truth

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/ProductAggregationService.cs`

`ProductAggregationService` is now the only service that calculates weighted product progress and product forecast totals from epic-level inputs.
It consumes only prepared epic outputs (`EpicProgress`, forecast totals, weight, and exclusion state) and does not traverse features, PBIs, persistence, or UI models.

## Verification results

### Functional checks

1. Standard weighted case
   - Verified by `ProductAggregationServiceTests.Compute_UsesWeightedAverageAcrossIncludedEpics`.
   - Input: Epic A = 50 @ weight 2, Epic B = 100 @ weight 1
   - Result: `ProductProgress = 66.67`

2. Null epic excluded
   - Verified by `ProductAggregationServiceTests.Compute_ExcludesNullProgressEpicsFromWeightedProgressAndCountsThem`.
   - Result: `ProductProgress = 100` and `ExcludedEpicsCount = 1`.

3. All epics invalid
   - Verified by `ProductAggregationServiceTests.Compute_ReturnsNullProgress_WhenAllEpicsAreInvalid`.
   - Result: `ProductProgress = null`.

4. Forecast sum
   - Verified by `ProductAggregationServiceTests.Compute_SumsForecastsAcrossEpics`.
   - Result: `ProductForecastConsumed = 50`, `ProductForecastRemaining = 150`.

5. Null forecast handling
   - Verified by `ProductAggregationServiceTests.Compute_SkipsNullForecastsInSums`.
   - Result: only non-null epic forecasts contribute to totals.

6. No forecast data
   - Verified by `ProductAggregationServiceTests.Compute_ReturnsNullForecastTotals_WhenNoEpicForecastExists`.
   - Result: both forecast totals remain `null`.

### Structural checks

- Exactly one service calculates product rollup
  - Verified: `ProductAggregationService` owns the weighted product progress and forecast formulas.
- No epic reinterpretation
  - Verified: the service consumes only prepared epic inputs and never accesses features, PBIs, or product-level effort.
- Null propagation preserved
  - Verified: unknown `EpicProgress` remains `null` and is counted as excluded instead of being coerced to `0`.
- Mixed mode safety preserved
  - Verified: the service uses only upstream `Weight` and does not infer or normalize estimation modes.
- Single DI registration
  - Verified by `ServiceCollectionTests.AddPoToolApiServices_RegistersCanonicalMetricsServices_ForDiConsumers`.

## Follow-up work

- Existing product-level sprint diagnostics (`DeliveryProgressSummaryCalculator` and `ProductDeliveryProgressSummary`) were intentionally left unchanged because they track sprint scope-change and completion counts, not canonical product progress/forecast.
- No UI, API contract, or portfolio reporting consumer was changed in this prompt; future prompts can wire `IProductAggregationService` into those layers without duplicating the aggregation formulas.
