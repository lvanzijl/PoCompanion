# Sprint Commitment Handler Simplification

## Removed Handler Calculations

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
  - removed handler-owned `ResolveSprintStoryPoints(...)` summation
  - handler now maps planned and completed sprint totals from `ISprintFactService.BuildSprintFactResult(...)`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
  - removed handler-owned `SumStoryPoints(...)`
  - removed handler-owned `SumDeliveredStoryPoints(...)`
  - handler now maps `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `DeliveredFromAddedSP`, `SpilloverSP`, and `RemainingStoryPoints` from `SprintFactResult`

## New CDC SprintFactResult

- added `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintFactResult.cs`
- added `ISprintFactService` and `SprintFactService` in `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`
- `SprintFactService` composes:
  - `ISprintCommitmentService`
  - `ISprintScopeChangeService`
  - `ISprintCompletionService`
  - `ISprintSpilloverService`
  - `ICanonicalStoryPointResolutionService`
- `RemainingStoryPoints` is produced inside the CDC as:
  - `Committed + Added - Removed - Delivered`

## Updated Handlers

- `GetSprintMetricsQueryHandler`
  - still loads work items, sprint history, and state classifications
  - still uses CDC reconstruction for membership and first-Done attribution
  - now consumes `SprintFactResult` for committed and delivered story points
- `GetSprintExecutionQueryHandler`
  - still loads sprint evolution inputs, current scope lists, and starved-work heuristics
  - now consumes `SprintFactResult` for all sprint story-point totals before mapping the DTO

## DTO Simplification

- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
  - `RemainingStoryPoints` changed from an inline formula to a mapped field
  - DTO no longer owns SprintCommitment arithmetic

## Test Adjustments

- updated handler tests to verify `ISprintFactService` is invoked and mapped correctly
- added CDC slice coverage for `SprintFactService.BuildSprintFactResult(...)`
- updated DI coverage so `ISprintFactService` registration is validated
- updated DTO serialization coverage for mapped `RemainingStoryPoints`
- updated audit documents and audit tests to reflect the new CDC-owned sprint fact seam

## Lines of Code Removed

- removed the local sprint story-point summation helpers from `GetSprintExecutionQueryHandler`
- removed the local sprint story-point resolver from `GetSprintMetricsQueryHandler`
- net effect: sprint total formulas now live only in the SprintCommitment CDC slice
