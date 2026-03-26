# Epic Aggregation Null Semantics Fix

## Exact places corrected

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicProgress.cs`
  - `ProgressPercent` changed from non-nullable `int` to nullable `int?`.
  - Validation now checks the compatibility field only when a value is present.

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
  - Removed the `null -> 0` coercion when mapping aggregated epic progress into the compatibility field.
  - `ProgressPercent` is now set to `null` when `aggregation.EpicProgress` is `null`.

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Metrics/SprintTrendDtos.cs`
  - `EpicProgressDto.ProgressPercent` changed from non-nullable `int` to nullable `int?`.
  - DTO documentation now explicitly states that unknown epic progress remains `null`.

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor`
  - The epic progress column now renders `Unknown` when `ProgressPercent` is `null` instead of trying to render a `0%` progress bar.

- Updated tests covering domain models, rollups, mapping, serialization, and projections:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/DeliveryProgressRollupServiceTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/DeliveryTrendDomainModelsTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Adapters/DeliveryTrendProgressRollupMapperTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/DtoContractCleanupTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/EpicAggregationNullSemanticsFixDocumentTests.cs`

## Null semantics preservation

- `AggregatedProgress = null` now remains `ProgressPercent = null`.
- True zero progress remains explicitly representable as:
  - `AggregatedProgress = 0`
  - `ProgressPercent = 0`
- Unknown / not computable epic progress is no longer collapsed into the same value as explicit zero progress.
- Mapper and DTO layers preserve the nullable compatibility field instead of substituting a fallback value.

## Verification results

### Functional checks

1. All features excluded
   - Verified by:
     - `DeliveryProgressRollupServiceTests.ComputeEpicProgress_PreservesNullProgress_WhenAggregationIsUnknown`
     - `SprintTrendProjectionServiceTests.ComputeEpicProgress_ZeroTotalStoryPoints_SprintDeltaIsZero`
     - `DeliveryTrendProgressRollupMapperTests.ToEpicProgressDto_PreservesNullProgressPercent`
     - `DtoContractCleanupTests.EpicProgressDto_SerializesNullProgressPercent_WhenEpicProgressIsUnknown`
   - Result:
     - `AggregatedProgress = null`
     - `ProgressPercent = null`
     - no `0` fallback remains in the tested paths

2. Zero actual progress
   - Verified by existing feature/epic rollup tests where progress is explicitly zero and not unknown.
   - Result:
     - explicit zero progress remains distinguishable from `null`

3. Standard non-null case
   - Verified by:
     - `EpicAggregationServiceTests.Compute_UsesWeightedAverageAcrossIncludedFeatures`
     - `DeliveryProgressRollupServiceTests.ComputeEpicProgress_DelegatesCanonicalAggregationToEpicAggregationService`
     - `SprintTrendProjectionServiceTests.ComputeEpicProgress_AggregatesFromChildFeatures`
   - Result:
     - weighted rollup behavior remains unchanged for computable progress

### Structural checks

Searched for epic-progress null-to-zero risks including:

- `?? 0`
- `GetValueOrDefault()`
- nullable-to-zero conversions
- rounding/fallback logic around epic `ProgressPercent`

Confirmed correction:

- `DeliveryProgressRollupService` no longer converts `aggregation.EpicProgress` from `null` to `0`.
- No remaining epic compatibility mapping intentionally substitutes unknown progress with zero in the corrected domain/model/adapter/DTO paths.

## Legacy compatibility constraints

- The legacy `ProgressPercent` field is still retained for compatibility, but it is now nullable.
- Existing non-null epic cases continue to expose the same rounded integer compatibility value.
- This fix intentionally does not change:
  - forecast rollup behavior
  - feature forecast logic
  - snapshot behavior
  - broader UI design beyond displaying unknown epic progress without a false `0%`
