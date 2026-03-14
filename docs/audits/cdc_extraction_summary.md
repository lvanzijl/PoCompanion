# CDC Extraction Summary

- **CDC package name:** `PoTool.Core.Domain`
- **Models moved:** `HistoricalSprintInputs.cs` (`WorkItemSnapshot`, `SprintDefinition`, `FieldChangeEvent`), `CanonicalWorkItem.cs`, and `StateClassificationModels.cs`
- **Services moved:** `SprintCommitmentLookup`, `FirstDoneDeliveryLookup`, `SprintSpilloverLookup`, `StateClassificationLookup`, `StateReconstructionLookup`, `CanonicalStoryPointResolutionService`, `SprintExecutionMetricsCalculator`, and `HierarchyRollupService`
- **API adapters retained:** `HistoricalSprintInputMapper`, `CanonicalMetricsInputMapper`, `StateClassificationInputMapper`, API handlers, `SprintTrendProjectionService`, and DI registration in `ApiServiceCollectionExtensions`
- **Architectural outcome:** the canonical sprint analytics domain is now isolated in the CDC package; `PoTool.Api` remains the EF/DTO orchestration boundary and `PoTool.Shared` remains transport-only
- **Recommended next step after CDC:** optionally rename or regroup the remaining API-side mappers under an explicit adapter namespace/folder to make the CDC boundary even clearer without changing behavior

## Final verdict

**CDC extracted with minor cleanup remaining**

## Adapter Boundary Cleanup

- adapter classes regrouped under `PoTool.Api/Adapters`
- namespaces updated to `PoTool.Api.Adapters`
- no semantic changes to CDC logic, handlers, or DTO contracts

## Final CDC Cleanup Verification

- adapter boundaries are now clean: `HistoricalSprintInputMapper`, `CanonicalMetricsInputMapper`, and `StateClassificationInputMapper` are explicitly grouped under `PoTool.Api/Adapters`, while handlers and projection services remain in API orchestration roles
- no structural CDC issues remain after the boundary cleanup: `PoTool.Core.Domain` has no project references and no direct dependencies on API, EF, shared DTOs, or persistence entities
- the shared DTO surface used at the CDC seam remains transport-only, and handlers continue to orchestrate EF reads plus CDC service calls rather than owning domain formulas
- final verdict: **CDC clean with optional naming cleanup only**
- prompt 6 is now the only remaining optional step, and it is the previously defined naming cleanup prompt rather than a required structural fix

## Post-Extraction Naming Cleanup

### Names clarified

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` now uses internal `*ScopeStoryPoints` locals and forecast helper parameters to make it explicit that the legacy DTO `*Effort` fields are populated from canonical story-point scope.
- `PoTool.Api/Services/SprintTrendProjectionService.cs` now uses internal `*ScopeStoryPoints` locals when computing feature and epic progress so the API-side orchestration matches the canonical domain vocabulary without changing the DTO contract.
- XML documentation now explicitly maps the following legacy transport names to their domain meaning:
  - `EpicCompletionForecastDto.TotalEffort` → total canonical scope
  - `EpicCompletionForecastDto.CompletedEffort` → delivered canonical scope
  - `EpicCompletionForecastDto.RemainingEffort` → remaining canonical scope
  - `FeatureProgressDto.TotalEffort` / `EpicProgressDto.TotalEffort` → total canonical scope
  - `FeatureProgressDto.DoneEffort` / `EpicProgressDto.DoneEffort` → delivered canonical scope
  - `FeatureProgressDto.SprintCompletedEffort` / `EpicProgressDto.SprintCompletedEffort` → sprint-delivered canonical scope
  - `PortfolioSprintProgressDto.RemainingEffort` → remaining scope effort in the portfolio stock/flow projection

### Names intentionally kept for compatibility

- `EpicCompletionForecastDto` keeps `TotalEffort`, `CompletedEffort`, `RemainingEffort`, `ExpectedCompletedEffort`, and `RemainingEffortAfterSprint` because they are part of the existing API transport surface.
- `FeatureProgressDto` and `EpicProgressDto` keep `TotalEffort`, `DoneEffort`, and `SprintCompletedEffort` for the same transport-compatibility reason.
- `PortfolioProgressTrendDto` keeps `RemainingEffort` and `RemainingEffortChangePts` because these names are already exposed to API consumers.

### Remaining legacy terminology

- Portfolio stock/flow DTOs still use `TotalScopeEffort`, `ThroughputEffort`, and `AddedEffort`; these names remain documented as compatibility debt until a deliberate API-contract revision is approved.
- Other effort-based analytics outside the canonical sprint-scope/story-point paths, such as effort distribution and capacity tooling, intentionally keep effort terminology because they still model implementation-hour effort rather than canonical story-point scope.
