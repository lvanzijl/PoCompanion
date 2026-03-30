# Snapshot Comparison Validation

## Implementation summary

- Added `ISnapshotComparisonService` and `SnapshotComparisonService` under `PoTool.Core.Domain/Domain/DeliveryTrends/Services`.
- Added `ProductSnapshot`, `SnapshotComparisonRequest`, and `SnapshotComparisonResult` under `PoTool.Core.Domain/Domain/DeliveryTrends/Models`.
- Registered the comparison engine in `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` so downstream handlers can resolve a single canonical delta service.
- Added focused tests in:
  - `PoTool.Tests.Unit/Services/SnapshotComparisonServiceTests.cs`
  - `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
  - `PoTool.Tests.Unit/Audits/SnapshotComparisonValidationDocumentTests.cs`

## Single source confirmation

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SnapshotComparisonService.cs`

`SnapshotComparisonService` is now the only service that calculates `ProgressDelta`, `ForecastConsumedDelta`, and `ForecastRemainingDelta` between two canonical product snapshots.
It consumes only already-aggregated `ProductSnapshot` values and does not access epics, features, PBIs, persistence, projections, or UI models.

## Verification results

### Functional checks

1. Standard delta
   - Verified by `SnapshotComparisonServiceTests.Compare_ReturnsStandardProgressDelta_WhenBothProgressValuesExist`.
   - Input: previous progress = 50, current progress = 70
   - Result: `ProgressDelta = 20`

2. Negative delta
   - Verified by `SnapshotComparisonServiceTests.Compare_PreservesNegativeProgressDelta_WhenCurrentDecreases`.
   - Input: previous progress = 70, current progress = 50
   - Result: `ProgressDelta = -20`

3. Null previous snapshot
   - Verified by `SnapshotComparisonServiceTests.Compare_ReturnsNullDeltas_WhenPreviousSnapshotIsMissing`.
   - Result: all deltas remain `null`.

4. Null progress value
   - Verified by `SnapshotComparisonServiceTests.Compare_ReturnsNullProgressDelta_WhenEitherProgressValueIsNull`.
   - Result: `ProgressDelta = null` when either snapshot lacks progress.

5. Forecast consumed delta
   - Verified by `SnapshotComparisonServiceTests.Compare_ReturnsForecastConsumedDelta_WhenBothConsumedValuesExist`.
   - Input: previous consumed = 40, current consumed = 60
   - Result: `ForecastConsumedDelta = 20`

6. Forecast remaining drop
   - Verified by `SnapshotComparisonServiceTests.Compare_ReturnsNegativeForecastRemainingDelta_WhenRemainingDrops`.
   - Input: previous remaining = 80, current remaining = 50
   - Result: `ForecastRemainingDelta = -30`

### Structural checks

- Exactly one service handles snapshot comparison
  - Verified: `SnapshotComparisonService` owns all snapshot delta logic.
- No null to zero conversion
  - Verified: `SnapshotComparisonService` returns `null` when either compared value is missing.
- No clamping or normalization
  - Verified: negative deltas are preserved as-is.
- No aggregation logic here
  - Verified: the service only subtracts canonical snapshot values and does not recalculate product, epic, or feature metrics.
- Single DI registration
  - Verified by `ServiceCollectionTests.AddPoToolApiServices_RegistersCanonicalMetricsServices_ForDiConsumers`.

## Edge cases observed

- A missing previous snapshot yields `null` for all three deltas instead of substituting a baseline snapshot.
- A missing value on either side yields `null` for that delta only; other populated fields can still produce their own delta in the same comparison.
- Negative deltas are valid signals and remain unchanged.
