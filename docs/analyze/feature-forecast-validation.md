# Feature Forecast Validation

## What was added

- Added `IFeatureForecastService` and `FeatureForecastService` under `PoTool.Core.Domain/Domain/DeliveryTrends/Services`.
- Added `FeatureForecastCalculationRequest` and `FeatureForecastResult` under `PoTool.Core.Domain/Domain/DeliveryTrends/Models`.
- Extended feature-progress rollups to carry:
  - `ForecastConsumedEffort`
  - `ForecastRemainingEffort`
- Mapped the forecast fields through:
  - `PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgress.cs`
  - `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs`
  - `PoTool.Shared/Metrics/SprintTrendDtos.cs`
- Added focused tests in:
  - `PoTool.Tests.Unit/Services/FeatureForecastServiceTests.cs`
  - `PoTool.Tests.Unit/Services/DeliveryProgressRollupServiceTests.cs`
  - `PoTool.Tests.Unit/Adapters/DeliveryTrendProgressRollupMapperTests.cs`

## Single source of truth

The single source of truth for feature forecast math is:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureForecastService.cs`

That service is the only place that calculates:

- `ForecastConsumedEffort`
- `ForecastRemainingEffort`

`DeliveryProgressRollupService` does not recalculate forecast math. It computes canonical feature progress through `IFeatureProgressService`, then delegates forecast translation to `IFeatureForecastService`.

## Verification results

### Functional checks

1. Standard case
   - Input: `Effort = 100`, `EffectiveProgress = 50`
   - Result: `ForecastConsumedEffort = 50`, `ForecastRemainingEffort = 50`
   - Covered by: `FeatureForecastServiceTests.Compute_ReturnsExpectedForecast_ForStandardCase`

2. Full completion
   - Input: `Effort = 100`, `EffectiveProgress = 100`
   - Result: `ForecastConsumedEffort = 100`, `ForecastRemainingEffort = 0`
   - Covered by: `FeatureForecastServiceTests.Compute_ReturnsZeroRemaining_ForFullCompletion`

3. Zero progress
   - Input: `Effort = 100`, `EffectiveProgress = 0`
   - Result: `ForecastConsumedEffort = 0`, `ForecastRemainingEffort = 100`
   - Covered by: `FeatureForecastServiceTests.Compute_ReturnsFullRemaining_ForZeroProgress`

4. Missing effort
   - Input: `Effort = null`, `EffectiveProgress = 70`
   - Result: `ForecastConsumedEffort = null`, `ForecastRemainingEffort = null`
   - Covered by: `FeatureForecastServiceTests.Compute_ReturnsNullForecast_WhenEffortIsMissing`

5. Decimal progress
   - Input: `Effort = 80`, `EffectiveProgress = 37.5`
   - Result: `ForecastConsumedEffort = 30`, `ForecastRemainingEffort = 50`
   - Covered by: `FeatureForecastServiceTests.Compute_HandlesDecimalProgressDeterministically`

### Structural checks

- Exactly one service calculates feature forecast
  - Verified: `FeatureForecastService` owns the formula.
- No feature forecast math duplicated in handlers, projections, or UI
  - Verified for this slice: `DeliveryProgressRollupServiceTests.ComputeFeatureProgress_DelegatesForecastCalculationToFeatureForecastService` proves the rollup consumes `IFeatureForecastService` output instead of recomputing values inline.
- Service does not infer or calculate progress itself
  - Verified: `FeatureForecastService` takes `EffectiveProgress` as input and does not inspect PBIs, state, or overrides.
- Service does not use epic-level effort
  - Verified: the request accepts only feature-level `Effort` and `EffectiveProgress`.

### Forecast-only boundary

- The implementation is forecast only.
- It does not use timesheets.
- It does not use budget snapshots.
- It does not use external systems.
- It does not claim to compute actual consumed hours.

### Mathematical safety

- The forecast engine clamps the progress ratio to `0..1` before multiplication.
- Remaining effort is computed deterministically and clamped to avoid negative values from floating-point rounding artifacts.

## Follow-up work

- Epic forecast rollups remain intentionally out of scope for this prompt.
- Snapshot persistence remains intentionally out of scope for this prompt.
- Budget comparison remains intentionally out of scope for this prompt.
- UI rendering remains intentionally out of scope for this prompt.
