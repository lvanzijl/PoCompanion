# Effort Planning Boundary Audit

## CDC Service Responsibilities

- `PoTool.Core.Domain/Domain/EffortPlanning/EffortDistributionService.cs`
  - owns reusable effort-distribution calculations
  - computes area totals, iteration totals, utilization percentages, and heat-map capacity states
  - does not shape API DTOs or apply page-specific ordering beyond slice-owned analytic selection limits
- `PoTool.Core.Domain/Domain/EffortPlanning/EffortEstimationQualityService.cs`
  - owns reusable quality scoring and trend calculations for completed work with effort
  - computes type-level accuracy, trend accuracy, and weighted overall accuracy
  - does not load data, classify states, or shape API DTOs
- `PoTool.Core.Domain/Domain/EffortPlanning/EffortEstimationSuggestionService.cs`
  - owns reusable similarity scoring, median selection, and confidence scoring for effort suggestions
  - returns canonical historical examples ranked by similarity
  - still contains `BuildRationale(...)`, which formats user-facing recommendation text inside the CDC slice

## Handler Responsibilities

- `PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs`
  - loads work items from product roots or the repository
  - applies request-scoped area filtering and effort presence filtering before CDC invocation
  - maps `EffortDistributionResult` to `EffortDistributionDto`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs`
  - loads work items from product roots or the repository
  - resolves completed-state membership through `IWorkItemStateClassificationService`
  - maps `EffortEstimationQualityResult` to `EffortEstimationQualityDto`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs`
  - loads work items and estimation settings
  - filters candidate items and historical completed samples before CDC invocation
  - maps `EffortEstimationSuggestionResult` to `EffortEstimationSuggestionDto`

## Statistical Helper Consistency

- `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs` is the shared statistical helper used by the audited EffortPlanning slice.
- `EffortEstimationQualityService` uses `StatisticsMath.Variance(...)` for coefficient-of-variation based accuracy scoring.
- `EffortEstimationSuggestionService` uses:
  - `StatisticsMath.Median(...)` for median effort selection
  - `StatisticsMath.Variance(...)` for confidence scoring input
- `EffortDistributionService` does not perform variance, median, percentile, or standard-deviation work; its remaining math is deterministic rollup and utilization arithmetic.

## Boundary Compliance

- Distribution analytics and estimation-quality analytics are CDC-compliant: the services contain reusable domain calculations and the handlers remain orchestration/mapping code.
- Suggestion ranking and confidence analytics are also CDC-compliant.
- The EffortPlanning slice is **not fully boundary-clean yet** because `EffortEstimationSuggestionService` still formats recommendation text through `BuildRationale(...)`.
- That rationale string is presentation-oriented output rather than reusable domain analytics, so the audit cannot confirm full compliance with the “no recommendation text formatting in CDC services” requirement.

## Remaining Adapter Logic

- Handlers still own adapter-level retrieval and filtering that depends on request scope or external state services:
  - product-root loading
  - area and iteration request filters
  - candidate selection for unestimated items
  - completed-state checks through `IWorkItemStateClassificationService`
  - DTO materialization for API contracts
- Test placement is mostly aligned with the intended boundary:
  - `PoTool.Tests.Unit/Services/EffortPlanningCdcServicesTests.cs` verifies distribution correctness, quality scoring correctness, configured-default fallback, median selection, and suggestion ranking
  - `PoTool.Tests.Unit/Handlers/GetEffortDistributionQueryHandlerTests.cs`, `GetEffortEstimationQualityQueryHandlerTests.cs`, and `GetEffortEstimationSuggestionsQueryHandlerTests.cs` verify CDC service invocation and DTO mapping while also asserting adapter-side filtering inputs
- Final audit verdict:
  - CDC analytics ownership is established for distribution, quality scoring, and suggestion ranking
  - adapter orchestration remains in handlers where expected
  - recommendation text formatting remains the one confirmed boundary leak inside the EffortPlanning CDC slice
