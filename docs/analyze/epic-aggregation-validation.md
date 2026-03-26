# Epic Aggregation Validation

## What was added

- Added `IEpicAggregationService` and `EpicAggregationService` under `PoTool.Core.Domain/Domain/DeliveryTrends/Services`.
- Added `EpicAggregationRequest` and `EpicAggregationResult` under `PoTool.Core.Domain/Domain/DeliveryTrends/Models`.
- Extended canonical feature rollups to carry:
  - `Weight`
  - `IsExcluded`
- Extended epic rollups to carry:
  - `AggregatedProgress`
  - `ForecastConsumedEffort`
  - `ForecastRemainingEffort`
  - `ExcludedFeaturesCount`
  - `IncludedFeaturesCount`
  - `TotalWeight`
- Wired the new aggregation outputs through:
  - `PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicProgress.cs`
  - `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs`
  - `PoTool.Shared/Metrics/SprintTrendDtos.cs`
- Added focused tests in:
  - `PoTool.Tests.Unit/Services/EpicAggregationServiceTests.cs`
  - `PoTool.Tests.Unit/Services/DeliveryProgressRollupServiceTests.cs`
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - `PoTool.Tests.Unit/Adapters/DeliveryTrendProgressRollupMapperTests.cs`
  - `PoTool.Tests.Unit/Audits/EpicAggregationValidationDocumentTests.cs`

## Single source of truth

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicAggregationService.cs`

`EpicAggregationService` is now the only service that calculates weighted epic progress and epic forecast totals.
`DeliveryProgressRollupService.ComputeEpicProgress` only groups features by epic, delegates the canonical aggregation to `IEpicAggregationService`, and maps the returned values into the existing `EpicProgress` transport shape.

## Verification results

### Functional checks

1. Standard weighted case
   - Verified by `EpicAggregationServiceTests.Compute_UsesWeightedAverageAcrossIncludedFeatures`.
   - Input: Feature A = 50 @ weight 2, Feature B = 100 @ weight 1
   - Result: `EpicProgress = 66.67`

2. Excluded feature ignored
   - Verified by `EpicAggregationServiceTests.Compute_IgnoresExcludedFeaturesForWeightedProgress` and `SprintTrendProjectionServiceTests.ComputeEpicProgress_ExcludedFeaturesDoNotAffectProgress`.
   - Result: excluded feature does not affect weighted progress and `ExcludedFeaturesCount = 1`.

3. All features excluded
   - Verified by `EpicAggregationServiceTests.Compute_ReturnsNullProgress_WhenAllFeaturesAreExcluded` and `SprintTrendProjectionServiceTests.ComputeEpicProgress_ZeroTotalStoryPoints_SprintDeltaIsZero`.
   - Result: `EpicProgress = null`, compatibility fields preserve `null`, and `ExcludedFeaturesCount = N`.

4. Forecast sum
   - Verified by `EpicAggregationServiceTests.Compute_SumsForecastsAcrossFeatures`.
   - Result: `EpicForecastConsumed = 50`, `EpicForecastRemaining = 150`.

5. Null forecast handling
   - Verified by `EpicAggregationServiceTests.Compute_SkipsNullForecastsInSums`.
   - Result: only non-null feature forecasts contribute to totals.

6. No feature forecast available
   - Verified by `EpicAggregationServiceTests.Compute_ReturnsNullForecastTotals_WhenNoFeatureForecastExists`.
   - Result: both forecast totals remain `null`.

### Structural checks

- Exactly one service calculates epic rollup
  - Verified: `EpicAggregationService` owns the weighted epic progress and forecast formulas.
- No handlers, projections, or UI recalculate epic progress directly
  - Verified for this slice: `DeliveryProgressRollupServiceTests.ComputeEpicProgress_DelegatesCanonicalAggregationToEpicAggregationService` proves the delivery rollup delegates canonical aggregation instead of recomputing those values inline.
- No use of epic effort in forecast rollup
  - Verified: `EpicAggregationService` only inspects feature-level `ForecastConsumedEffort` and `ForecastRemainingEffort`.
- No raw PBI traversal inside this service
  - Verified: `EpicAggregationService` consumes `FeatureProgress` objects only and does not access work-item hierarchy inputs.
- Mixed mode safety preserved
  - Verified: the epic aggregation service only consumes upstream-normalized `Weight`, `EffectiveProgress`, `ForecastConsumedEffort`, `ForecastRemainingEffort`, and `IsExcluded`.

## Follow-up work

- `ProgressPercent` remains as a compatibility alias rounded from the nullable aggregated progress so existing consumers continue to compile without UI changes.
- Existing legacy story-point and sprint diagnostic fields on `EpicProgress` were intentionally left intact because this prompt is limited to centralizing epic progress/forecast rollup, not removing downstream compatibility fields.
