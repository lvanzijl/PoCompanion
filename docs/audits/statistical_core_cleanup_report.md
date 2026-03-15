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
