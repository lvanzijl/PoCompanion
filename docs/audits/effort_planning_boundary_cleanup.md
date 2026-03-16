# EffortPlanning Boundary Cleanup

## CDC Text Formatting Removed

- Removed user-facing rationale string construction from `PoTool.Core.Domain/Domain/EffortPlanning/EffortEstimationSuggestionService.cs`.
- The CDC now returns suggestion facts only and no longer formats prose inside the EffortPlanning slice.

## Structured Suggestion Facts Added

- `EffortEstimationSuggestionResult` now exposes structured rationale inputs:
  - `HistoricalMatchCount`
  - `HistoricalEffortMin`
  - `HistoricalEffortMax`
- Existing CDC-owned suggestion behavior remains unchanged:
  - similarity scoring
  - top-match ranking
  - median effort selection
  - confidence scoring

## API Adapter Formatting Added

- Added `PoTool.Api/Adapters/EffortEstimationSuggestionMapper.cs`.
- The adapter maps `EffortEstimationSuggestionResult` to `EffortEstimationSuggestionDto`.
- DTO rationale text is now rebuilt in the API adapter layer, preserving the existing outward wording for:
  - configured-default fallback suggestions
  - fixed-point historical suggestions
  - ranged historical suggestions
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` remains limited to data loading, filtering, CDC invocation, and adapter mapping.

## Tests Updated

- `PoTool.Tests.Unit/Services/EffortPlanningCdcServicesTests.cs` now verifies structured suggestion facts instead of presentation text.
- `PoTool.Tests.Unit/Handlers/GetEffortEstimationSuggestionsQueryHandlerTests.cs` now verifies DTO rationale formatting through handler-level mapping.
- Added `PoTool.Tests.Unit/Audits/EffortPlanningBoundaryCleanupDocumentTests.cs` to guard this cleanup report.

## Final Boundary Status

- The EffortPlanning CDC suggestion service now returns canonical analytics facts only.
- Presentation-oriented rationale formatting now lives in the API adapter layer where DTO shaping belongs.
- Suggestion ranking, confidence, and median-selection formulas were not redesigned or changed.
