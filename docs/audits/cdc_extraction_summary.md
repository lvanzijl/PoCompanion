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
