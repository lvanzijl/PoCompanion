# Insight Validation

## Implementation summary

- Added `IInsightService` and `InsightService` under `PoTool.Core.Domain/Domain/DeliveryTrends/Services`.
- Added `InsightRequest`, `InsightResult`, `Insight`, `InsightContext`, `InsightSeverity`, and `InsightCodes` under `PoTool.Core.Domain/Domain/DeliveryTrends/Models`.
- Registered the centralized insight engine in `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` so downstream consumers resolve one explainable decision-signal service.
- Added focused tests in:
  - `PoTool.Tests.Unit/Services/InsightServiceTests.cs`
  - `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
  - `PoTool.Tests.Unit/Audits/InsightValidationDocumentTests.cs`

## Mapping logic

`InsightService` is deterministic, read-only, and consumes only:

- `ProductAggregationResult`
- `SnapshotComparisonResult`
- `PlanningQualityResult`

Implemented mappings:

- `IN-1` — Progress stalled (`Warning`) when `ProgressDelta == null` or `ProgressDelta == 0`
- `IN-2` — Progress reversed (`Critical`) when `ProgressDelta < 0`
- `IN-3` — Scope increased faster than delivery (`Critical`) when `ForecastRemainingDelta > 0` and `ProgressDelta <= 0`
- `IN-4` — Healthy progress (`Info`) when `ProgressDelta > 0` and `ForecastRemainingDelta <= 0`
- `IN-5` — Low planning quality (`Warning`) when `PlanningQualityScore < 70`
- `IN-6` — Very low planning quality (`Critical`) when `PlanningQualityScore < 50`
- `IN-7` — Forecast unreliable (`Warning`) when Planning Quality contains `PQ-1`, `PQ-2`, or `PQ-7`

Each `Insight` carries an `InsightContext` with:

- `ProgressDelta`
- `ForecastRemainingDelta`
- `PlanningQualityScore`

This keeps every message explainable without introducing new calculations or hidden state.

## Verification results

### Functional checks

1. Progress = `0` emits `IN-1`
   - Verified by `InsightServiceTests.Analyze_EmitsProgressStalledInsight_WhenProgressDeltaIsZero`
2. Progress = `null` emits `IN-1`
   - Verified by `InsightServiceTests.Analyze_EmitsProgressStalledInsight_WhenProgressDeltaIsNull`
3. Progress `< 0` emits `IN-2`
   - Verified by `InsightServiceTests.Analyze_EmitsProgressReversedAndScopeIncreaseInsights_WhenDeliveryFallsBehind`
4. Remaining increases while progress is non-positive emits `IN-3`
   - Verified by `InsightServiceTests.Analyze_EmitsProgressReversedAndScopeIncreaseInsights_WhenDeliveryFallsBehind`
5. Progress `> 0` and remaining decreases emits `IN-4`
   - Verified by `InsightServiceTests.Analyze_EmitsHealthyProgressInsight_WhenProgressImprovesAndRemainingDrops`
6. Score `< 70` emits `IN-5`
   - Verified by `InsightServiceTests.Analyze_EmitsPlanningQualityInsights_AndForecastUnreliable_WhenThresholdsAndSignalsMatch`
7. Score `< 50` emits `IN-6`
   - Verified by `InsightServiceTests.Analyze_EmitsPlanningQualityInsights_AndForecastUnreliable_WhenThresholdsAndSignalsMatch`
8. `PQ-1`, `PQ-2`, or `PQ-7` emits `IN-7`
   - Verified by `InsightServiceTests.Analyze_EmitsPlanningQualityInsights_AndForecastUnreliable_WhenThresholdsAndSignalsMatch`

### Structural checks

- Single service
  - Verified: `InsightService` is the only service that maps insight codes and messages.
- No duplicated insight logic
  - Verified: all rule combinations are centralized in `InsightService`.
- No UI dependency
  - Verified: all new code lives in Core domain models/services, with DI registration in API only.
- No mutation of inputs
  - Verified: `InsightService` only reads already-computed result objects and emits new immutable records.
- No new calculations
  - Verified: the service does not compute progress, forecast totals, deltas, or planning quality score.

Note: `IN-5` and `IN-6` are intentionally cumulative. When `PlanningQualityScore < 50`, both explicit conditions are true, so the service emits both a warning and a critical insight rather than inventing a suppression rule.

## Examples

- `ProgressDelta = 0`, `ForecastRemainingDelta = 5`, `PlanningQualityScore = 100`
  - Emits `IN-1`
- `ProgressDelta = -10`, `ForecastRemainingDelta = 12`, `PlanningQualityScore = 100`
  - Emits `IN-2`
  - Emits `IN-3`
- `ProgressDelta = 8`, `ForecastRemainingDelta = -6`, `PlanningQualityScore = 100`
  - Emits `IN-4`
- `ProgressDelta = 4`, `ForecastRemainingDelta = 2`, `PlanningQualityScore = 40`, Planning Quality signals include `PQ-1` and `PQ-7`
  - Emits `IN-5`
  - Emits `IN-6`
  - Emits `IN-7`
