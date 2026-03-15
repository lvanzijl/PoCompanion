# Statistical Core Cleanup Report

## EffortDiagnostics Statistics Ownership Consolidation

- Previous duplicate owners:
  - `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs`
  - `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsStatistics.cs`
- Chosen canonical owner:
  - `PoTool.Core.Domain/Domain/EffortDiagnostics`
- Files changed:
  - `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsStatistics.cs`
  - `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsCanonicalRules.cs`
  - `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs`
  - `PoTool.Tests.Unit/Domain/EffortDiagnosticsStatisticsTests.cs`
  - `PoTool.Tests.Unit/Services/EffortDiagnosticsDomainModelsTests.cs`
  - `PoTool.Tests.Unit/Audits/EffortDiagnosticsCdcExtractionAuditTests.cs`
  - `docs/audits/statistical_helper_audit.md`
  - `docs/audits/effort_diagnostics_cdc_extraction_report.md`
  - `docs/audits/statistical_core_cleanup_report.md`
- Tests updated:
  - `PoTool.Tests.Unit/Domain/EffortDiagnosticsStatisticsTests.cs`
  - `PoTool.Tests.Unit/Services/EffortDiagnosticsDomainModelsTests.cs`
  - `PoTool.Tests.Unit/Audits/EffortDiagnosticsCdcExtractionAuditTests.cs`

## Shared Pure-Math Statistics Core Introduced

- Helper location:
  - `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`
- Helper contracts:
  - `Mean(IEnumerable<double>)` accepts unsorted input, returns `double`, and returns `0` for empty samples.
  - `Variance(IEnumerable<double>)` calculates population variance for unsorted input, returns `double`, and returns `0` for empty samples.
  - `StandardDeviation(IEnumerable<double>)` calculates population standard deviation for unsorted input, returns `double`, and returns `0` for empty samples.
  - `Median(IEnumerable<double>)` accepts unsorted input, sorts deterministically, returns `double`, returns `0` for empty samples, and averages the two middle values for even-sized samples.
- Tests added:
  - `PoTool.Tests.Unit/Domain/StatisticsMathTests.cs`
  - `PoTool.Tests.Unit/Audits/StatisticalHelperAuditDocumentTests.cs`

## Variance Duplication Removed from Estimation Handlers

- handlers updated:
  - `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs`
- local helpers removed:
  - removed each handler-local `CalculateVariance` wrapper and routed the existing variance call sites directly to `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`
- behavior preserved:
  - the handlers still calculate the same exact population variance values for quality accuracy and suggestion confidence
  - focused handler and audit coverage now verifies preserved outputs plus the absence of local variance helpers

## Percentile Semantics Standardized

- previous percentile variants found:
  - linear interpolation in `GetCapacityCalibrationQueryHandler`, `GetPipelineInsightsQueryHandler`, `GetPrSprintTrendsQueryHandler`, and the client calculators
  - nearest-rank in `GetPullRequestInsightsQueryHandler` and `GetPrDeliveryInsightsQueryHandler`
- chosen canonical algorithm:
  - `PoTool.Shared/Statistics/PercentileMath.cs` now defines repository-default percentile semantics as Linear interpolation on pre-sorted ascending samples
  - contract:
    - sorted vs unsorted input: callers must provide values already sorted ascending
    - empty input: returns `0`
    - one-sample input: returns that only sample value
    - percentile range: inclusive `[0, 100]`, otherwise throws `ArgumentOutOfRangeException`
- files migrated:
  - `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
  - `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`
  - `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs`
  - `PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs`
  - `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs`
  - `PoTool.Client/Services/PullRequestInsightsCalculator.cs`
  - `PoTool.Client/Services/PipelineInsightsCalculator.cs`
  - `PoTool.Client/Services/BugInsightsCalculator.cs`
  - `PoTool.Tests.Unit/Shared/PercentileMathTests.cs`
  - `PoTool.Tests.Unit/Handlers/GetPullRequestInsightsQueryHandlerTests.cs`
  - `PoTool.Tests.Unit/Handlers/GetPrDeliveryInsightsQueryHandlerTests.cs`
  - `PoTool.Tests.Unit/Audits/StatisticalHelperAuditDocumentTests.cs`
- intentionally local exceptions:
  - none
