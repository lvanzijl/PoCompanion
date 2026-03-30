# Insight Null Semantics Fix

## Exact code changes

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Services/InsightService.cs`
  - Changed `IN-1` so it now emits only when `ProgressDelta == 0`.
  - Added `IN-8` so `ProgressDelta == null` emits a distinct warning for unknown progress.
  - Kept all other insight conditions, severities, and thresholds unchanged.

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Models/InsightModels.cs`
  - Added `InsightCodes.ProgressUnknown = "IN-8"`.

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/InsightServiceTests.cs`
  - Replaced the old null-progress expectation for `IN-1` with `IN-8`.
  - Added mutual-exclusivity assertions so `IN-1` and `IN-8` cannot appear together.
  - Added a combined scenario proving `IN-8` can coexist with planning quality warnings.

- Updated `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/insight-validation.md`
  - Corrected the published mapping so `null` is no longer documented as stalled progress.
  - Added `IN-8` to the validation report and verification list.

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/InsightNullSemanticsFixDocumentTests.cs`
  - Audits the presence and required content of this corrective report.

## Updated mapping logic

- `IN-1` — Progress stalled (`Warning`) when `ProgressDelta == 0`
- `IN-8` — Progress unknown (`Warning`) when `ProgressDelta == null`

These two mappings are explicitly separated in `InsightService`.

Semantic rule now preserved:

- `0` = measured, no change
- `null` = unknown / not measurable

No fallback, coercion, or defaulting is used.

All emitted insight context remains unchanged and still includes:

- `ProgressDelta`
- `ForecastRemainingDelta`
- `PlanningQualityScore`

## Verification results

### Functional checks

1. `ProgressDelta = 0`
   - Verified by `InsightServiceTests.Analyze_EmitsProgressStalledInsight_WhenProgressDeltaIsZero`
   - Result: emits `IN-1` only

2. `ProgressDelta = null`
   - Verified by `InsightServiceTests.Analyze_EmitsProgressUnknownInsight_WhenProgressDeltaIsNull`
   - Result: emits `IN-8` only

3. `ProgressDelta = -10`
   - Verified by `InsightServiceTests.Analyze_EmitsProgressReversedAndScopeIncreaseInsights_WhenDeliveryFallsBehind`
   - Result: emits `IN-2` (and `IN-3` when remaining increases), but not `IN-1` or `IN-8`

4. `ProgressDelta = null` with low planning quality
   - Verified by `InsightServiceTests.Analyze_EmitsProgressUnknownAlongsidePlanningQualityInsights_WhenProgressCannotBeMeasured`
   - Result: emits `IN-8`, `IN-5`, and `IN-6`

### Structural checks

- No logic path interprets `null` as `0`
  - Verified: `IN-1` requires `progressDelta.HasValue && progressDelta.Value == 0d`
- No fallback values introduced
  - Verified: `InsightContext.ProgressDelta` remains `null` for `IN-8`
- Centralized mapping preserved
  - Verified: all insight conditions remain in `InsightService`
- Mutual exclusivity preserved
  - Verified by explicit unit-test assertions that `IN-1` and `IN-8` never appear together

## Before vs after examples

### Before

- `ProgressDelta = null`, `ForecastRemainingDelta = 5`, `PlanningQualityScore = 100`
  - Emitted `IN-1`
  - Problem: unknown data was misrepresented as confirmed stalled progress

### After

- `ProgressDelta = null`, `ForecastRemainingDelta = 5`, `PlanningQualityScore = 100`
  - Emits `IN-8`
  - Meaning: progress cannot be determined due to missing or incomplete snapshot data

- `ProgressDelta = 0`, `ForecastRemainingDelta = 0`, `PlanningQualityScore = 100`
  - Emits `IN-1`
  - Meaning: progress was measured and confirmed unchanged
