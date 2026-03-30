# Test Ownership Audit

_Generated: 2026-03-16_

Scope note:

- The issue names historical test projects (`PoTool.Core.Tests`, `PoTool.Application.Tests`, `PoTool.Api.Tests`, `PoTool.Client.Tests`, `PoTool.Core.Domain.Tests`).
- The active repository does not contain those projects as separate `.csproj` files.
- The audited tests are consolidated under `PoTool.Tests.Unit`, with ownership expressed by folders such as `Services`, `Handlers`, `Domain`, `Shared`, and `Audits`.

Semantic areas reviewed:

- sprint commitment
- scope added / removed
- spillover detection
- delivery trends
- velocity calculations
- forecast projections
- stock / inflow / throughput
- effort imbalance
- readiness scoring
- percentile / statistics behavior

## CDC Semantic Tests Already Correct

These tests already sit at the CDC/domain slice level and validate the canonical semantics directly rather than through handler orchestration.

| Test file | Semantic area | Ownership classification | Notes |
| --- | --- | --- | --- |
| `PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs` | commitment, scope added/removed, completion, spillover | CDC slice semantic test | Canonical sprint CDC service coverage. |
| `PoTool.Tests.Unit/Services/SprintDeliveryProjectionServiceTests.cs` | delivery trends, spillover totals, derived/unestimated delivery | CDC slice semantic test | Verifies delivery projection semantics against direct domain inputs. |
| `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs` | velocity, forecast projections, effort trend forecasting | CDC slice semantic test | Keeps forecasting formulas inside domain services. |
| `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs` | stock / inflow / throughput | CDC slice semantic test | Tests portfolio flow reconstruction directly from canonical signals. |
| `PoTool.Tests.Unit/Services/EffortDiagnosticsDomainModelsTests.cs` | effort imbalance / concentration rules | CDC slice semantic test | Verifies canonical risk bands and weighted score behavior. |
| `PoTool.Tests.Unit/Services/BacklogReadinessServiceTests.cs` | readiness scoring | CDC slice semantic test | Readiness scoring is already covered as a direct domain service. |
| `PoTool.Tests.Unit/Domain/EffortDiagnosticsStatisticsTests.cs` | statistics primitives | CDC slice semantic test | Canonical effort-diagnostics math primitives. |
| `PoTool.Tests.Unit/Domain/StatisticsMathTests.cs` | statistics behavior | CDC slice semantic test | Generic domain math helpers. |
| `PoTool.Tests.Unit/Shared/PercentileMathTests.cs` | percentile behavior | CDC slice semantic test | Shared percentile interpolation behavior. |

## Tests That Belong in CDC

These tests currently live outside the CDC-focused semantic suites but still verify CDC/domain rules rather than only application orchestration.

| Test file | Specific tests | Semantic area | Ownership classification | Why it should move |
| --- | --- | --- | --- | --- |
| `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs` | whole file | sprint commitment, first-Done delivery, spillover, state reconstruction | CDC slice semantic test (should move) | Directly exercises legacy lookup helpers whose semantics are already wrapped by CDC services. |
| `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs` | `Handle_UsesHistoricalCommitmentAndFirstDoneSemantics`, `Handle_DoesNotCountSecondDoneTransition_WhenFirstDoneWasBeforeSprint`, `Handle_DoesNotUseRawDoneFallback_WhenCanonicalMappingIsMissing` | commitment timestamp, first-Done behavior, canonical done mapping | CDC slice semantic test (should move) | These assertions re-prove sprint CDC rules instead of only checking handler wiring. |
| `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` | `Handle_UsesCanonicalDoneMapping_ForCompletedItems`, `Handle_DoesNotUseRawFallbackDoneStates_WhenClassificationMissing`, `Handle_DoesNotCountSecondDone_WhenFirstDoneWasBeforeSprint`, `Handle_CountsFirstDoneWithinSprint_WhenItemIsNoLongerInSprintIteration`, `Handle_TreatsItemAddedAfterCommitment_AsAddedScope`, `Handle_KeepsCommittedItemInInitialScope_WhenMovedAwayAfterCommitment` | completion, scope-added, commitment retention, canonical done mapping | CDC slice semantic test (should move) | These methods assert CDC semantics with concrete formula/timeline expectations in the handler suite. |
| `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs` | `Handle_ThreeSprints_ComputesMedianVelocity`, `Handle_FiveSprints_ComputesP25P75Band`, `Handle_WithPredictabilityData_ComputesCorrectRatio`, `Handle_WithOutliers_FlagsOutlierSprints` | velocity calculations, percentile/statistics behavior | CDC slice semantic test (should move) | The handler tests restate `VelocityCalibrationService` outputs instead of limiting themselves to transport/integration behavior. |
| `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs` | `Handle_WithEpicAndChildren_CalculatesCorrectly`, `Handle_WithNestedChildren_IncludesAllDescendants`, `Handle_WithZeroVelocity_ReturnsZeroSprintsRemaining`, `Handle_WithChildrenWithoutEffort_IgnoresThemInCalculation` | forecast projections, derived estimate behavior | CDC slice semantic test (should move) | These methods verify forecast math and rollup semantics that belong with the forecasting domain slice. |
| `PoTool.Tests.Unit/Handlers/GetEffortDistributionTrendQueryHandlerTests.cs` | `Handle_WithIncreasingEffort_DetectsIncreasingTrend`, `Handle_WithDecreasingEffort_DetectsDecreasingTrend`, `Handle_WithStableEffort_DetectsStableTrend`, `Handle_WithVolatileEffort_DetectsVolatileTrend`, `Handle_WithSufficientHistory_GeneratesForecasts`, `Handle_CalculatesChangeFromPrevious` | effort trend forecasting, statistics behavior | CDC slice semantic test (should move) | Trend classification and forecast semantics are being revalidated through the handler. |
| `PoTool.Tests.Unit/Handlers/GetEffortImbalanceQueryHandlerTests.cs` | `Handle_UsesAnalyzerDerivedBucketValues_ForImbalanceOutput`, `Handle_WithBalancedDistribution_ReturnsLowRisk`, `Handle_WithImbalancedTeams_DetectsHighRisk`, `Handle_WithImbalancedSprints_DetectsHighRisk` | effort imbalance scoring and risk bands | CDC slice semantic test (should move) | These methods assert canonical score/risk outcomes already owned by effort-diagnostics domain rules. |

## Duplicate Semantic Tests

The following duplicated semantic coverage exists today.

| Canonical CDC/domain suite | Duplicate outside CDC | Duplicate formula/semantic |
| --- | --- | --- |
| `PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs` | `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs` | commitment timestamp, first-Done reconstruction, spillover boundary behavior |
| `PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs` | `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs` | first-Done counting, commitment membership, canonical done fallback |
| `PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs` | `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` | first-Done reuse, scope added after commitment, canonical done classification |
| `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs` | `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs` | median velocity, P25/P75 bands, predictability ratios, outlier detection |
| `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs` | `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs` | forecast remaining scope, estimated velocity, sprint count projection |
| `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs` | `PoTool.Tests.Unit/Handlers/GetEffortDistributionTrendQueryHandlerTests.cs` | trend direction, forecast generation, change-from-previous behavior |
| `PoTool.Tests.Unit/Services/EffortDiagnosticsDomainModelsTests.cs` and `PoTool.Tests.Unit/Domain/EffortDiagnosticsStatisticsTests.cs` | `PoTool.Tests.Unit/Handlers/GetEffortImbalanceQueryHandlerTests.cs` | imbalance score, bucket risk bands, statistics-derived outputs |

No ownership duplication was found for:

- `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs`
- `PoTool.Tests.Unit/Services/BacklogReadinessServiceTests.cs`
- `PoTool.Tests.Unit/Shared/PercentileMathTests.cs`

## Valid Application Tests

These tests remain in the correct location because they verify orchestration, DTO aggregation, or projection persistence rather than replaying CDC formulas.

| Test file | Ownership classification | Why it is valid here |
| --- | --- | --- |
| `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs` (`Handle_UsesCdcSprintServices_ForScopeAndCompletionReconstruction`) | handler integration test | Verifies the handler calls CDC interfaces and maps the result shape. |
| `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` (`Handle_UsesCdcSprintServices_ForExecutionReconstruction`) | handler integration test | Confirms execution reconstruction flows through injected CDC services. |
| `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs` | handler integration test | Focused on cache/recompute behavior and aggregation of stored projections. |
| `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs` | projection persistence test | Verifies projection rebuilds and SQLite translation/persistence behavior. |
| `PoTool.Tests.Unit/Services/DeliveryTrendDomainModelsTests.cs` | transport / projection model test | Checks aggregate DTO/model construction rather than re-deriving CDC event semantics. |

No dedicated `PoTool.Client` semantic test ownership findings were found in the current repository snapshot because there is no separate client test project in this clone.

## Migration Candidates

Recommended future moves into CDC slice suites:

1. Move `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs` into the sprint CDC semantic area, or retire it once the CDC service suite fully subsumes the legacy helper coverage.
2. Split the semantic methods out of `GetSprintMetricsQueryHandlerTests.cs` and `GetSprintExecutionQueryHandlerTests.cs`; keep only the CDC-interface orchestration tests in the handler folder.
3. Split the formula assertions out of `GetCapacityCalibrationQueryHandlerTests.cs`, `GetEpicCompletionForecastQueryHandlerTests.cs`, and `GetEffortDistributionTrendQueryHandlerTests.cs`; keep query wiring, filtering, null/empty handling, and ownership/security checks in the handler folder.
4. Split the canonical score/risk assertions out of `GetEffortImbalanceQueryHandlerTests.cs`; keep request filtering and recommendation plumbing in the handler folder.
5. Leave `SprintTrendProjectionServiceSqliteTests.cs` and `GetSprintTrendMetricsQueryHandlerTests.cs` where they are; they are validating persistence/orchestration rather than CDC formula ownership.
