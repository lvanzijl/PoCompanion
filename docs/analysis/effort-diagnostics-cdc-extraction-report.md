# Effort Diagnostics CDC Extraction Report

## Summary of extraction

- Audited the stable EffortDiagnostics CDC slice centered on:
  - `/PoTool.Core/Metrics/EffortDiagnostics`
  - `/PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs`
  - `/PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs`
- Confirmed the stable handlers already delegate the analytical formulas to `EffortDiagnosticsAnalyzer` and keep API responsibilities limited to:
  - product-scoped loading
  - filtering/grouping
  - DTO mapping
  - recommendation text composition
- Removed the remaining legacy forwarding wrapper at `/PoTool.Core/Metrics/EffortDiagnosticsStatistics.cs` so the stable production helper implementation is no longer duplicated outside the CDC slice.
- A later statistical core cleanup pass then removed `/PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs` as a second production owner and consolidated primitive helper ownership into `/PoTool.Core.Domain/Domain/EffortDiagnostics`.

## Files changed

- Deleted `/PoTool.Core/Metrics/EffortDiagnosticsStatistics.cs`
- Deleted `/PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs`
- Added `/PoTool.Tests.Unit/Audits/EffortDiagnosticsCdcExtractionAuditTests.cs`
- Added `/docs/analysis/effort-diagnostics-cdc-extraction-report.md`

## Math removed from handlers

- No new handler extraction was required in this audit pass.
- `GetEffortImbalanceQueryHandler` already delegates imbalance math to `Analyzer.AnalyzeImbalance(...)`.
- `GetEffortConcentrationRiskQueryHandler` already delegates concentration math to `Analyzer.AnalyzeConcentration(...)`.
- The audit test added in this change locks in that neither handler directly calls:
  - `DeviationFromMean`
  - `ShareOfTotal`
  - `HHI`
  - `CoefficientOfVariation`
  - `Math.*`

## Tests updated

- Added `PoTool.Tests.Unit/Audits/EffortDiagnosticsCdcExtractionAuditTests.cs`
- New audit coverage verifies:
  - the legacy wrapper file outside the CDC slice is gone
  - the duplicate stable helper file under `/PoTool.Core/Metrics/EffortDiagnostics` is gone
  - the canonical stable helper file exists under `/PoTool.Core.Domain/Domain/EffortDiagnostics`
  - the two stable handlers remain orchestration/mapping-only and do not perform direct math helper calls

## Confirmation of CDC boundary

- **Stable production helper ownership:** after the later statistical core cleanup, the stable production implementation of the audited math helpers is centralized in `/PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsStatistics.cs`.
- **Handler boundary:** both audited handlers remain adapter/orchestration code and call the analyzer instead of implementing formulas inline.
- **Unstable families untouched:** no code changes were made to:
  - `EstimationQuality`
  - `EstimationSuggestions`
  - `SprintCapacityPlanning`
  - `CapacityCalibration`
- **Search result interpretation:** a raw repository-wide text search for `DeviationFromMean`, `ShareOfTotal`, `HHI`, and `CoefficientOfVariation` still returns test references, DTO/domain model property names, and canonical `PoTool.Core.Domain.EffortDiagnostics` abstractions. Those are not alternate handler-side math implementations. Within the audited stable production helper path, the executable CDC math remains isolated to `/PoTool.Core/Metrics/EffortDiagnostics`.

## Final assessment

- The EffortDiagnostics stable CDC slice is clean at the audited adapter boundary:
  - stable math implementation is centralized in the domain-owned surface
  - stable handlers are orchestration-only
  - unstable effort families were left unchanged
- Remaining non-handler search hits are documentation/tests/domain-abstraction references rather than duplicated stable helper implementations in the API handlers.
