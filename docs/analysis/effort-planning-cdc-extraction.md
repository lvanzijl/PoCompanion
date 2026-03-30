# EffortPlanning CDC Extraction

## Removed Handler Calculations

- `GetEffortDistributionQueryHandler`
  - removed handler-owned area-path totals
  - removed handler-owned iteration totals and utilization percentages
  - removed handler-owned heat-map cell calculations
- `GetEffortEstimationQualityQueryHandler`
  - removed handler-owned variance and coefficient-of-variation rollups
  - removed handler-owned quality-by-type and trend calculations
  - removed handler-owned weighted overall-accuracy calculation
- `GetEffortEstimationSuggestionsQueryHandler`
  - removed handler-owned similarity scoring
  - removed handler-owned median selection
  - removed handler-owned confidence and rationale heuristics

## New CDC EffortPlanning Slice

- Added `PoTool.Core.Domain/Domain/EffortPlanning/EffortPlanningModels.cs`
- Added `PoTool.Core.Domain/Domain/EffortPlanning/EffortDistributionService.cs`
- Added `PoTool.Core.Domain/Domain/EffortPlanning/EffortEstimationQualityService.cs`
- Added `PoTool.Core.Domain/Domain/EffortPlanning/EffortEstimationSuggestionService.cs`
- Added canonical results:
  - `EffortDistributionResult`
  - `EffortAreaDistributionResult`
  - `EffortIterationDistributionResult`
  - `EffortHeatMapCellResult`
  - `EffortEstimationQualityResult`
  - `EffortQualityTrendResult`
  - `EffortEstimationSuggestionResult`

## Updated Handlers

- `PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs`
  - loads work items
  - filters by area path and presence of effort
  - calls `IEffortDistributionService.Analyze(...)`
  - maps canonical results to `EffortDistributionDto`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs`
  - loads work items
  - filters to completed work items via `IWorkItemStateClassificationService`
  - calls `IEffortEstimationQualityService.Analyze(...)`
  - maps canonical results to `EffortEstimationQualityDto`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs`
  - loads work items
  - filters candidate items and historical completed samples
  - calls `IEffortEstimationSuggestionService.GenerateSuggestion(...)`
  - maps canonical results to `EffortEstimationSuggestionDto`

## Statistical Helper Usage

- `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs` is the single shared statistics helper used by the new slice for:
  - variance
  - median
  - standard deviation through `Math.Sqrt(Variance(...))`
- Handler-local statistical formulas were removed from the extracted effort-planning handlers.

## Test Adjustments

- Added `PoTool.Tests.Unit/Services/EffortPlanningCdcServicesTests.cs`
  - verifies effort distribution totals
  - verifies heat-map correctness
  - verifies estimation accuracy calculations
  - verifies suggestion median selection
  - verifies similarity ranking
- Updated handler tests so they verify:
  - service invocation
  - adapter-level filtering
  - DTO mapping
- Added `PoTool.Tests.Unit/Audits/EffortPlanningCdcExtractionAuditTests.cs`

## Lines of Code Removed

- Removed approximately 180 lines of local analytics formulas from the three handlers.
- Added CDC-owned services and result records so the analytical logic is centralized and testable without changing DTO contracts.
