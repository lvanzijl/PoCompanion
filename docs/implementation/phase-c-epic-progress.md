# Phase C Epic Progress Aggregation

## Summary

Implemented the Phase C epic-progress model in the domain layer as a deterministic weighted aggregation of Feature progress only.

Epic progress now:

- includes only child work items of type `Feature`
- uses Feature `EffectiveProgress` as the only progress input
- uses Feature `TotalEffort` / weight produced by the Phase B feature-progress path
- ignores non-Feature children entirely
- ignores epic-level `TimeCriticality`
- returns `0` when no included Feature weight is available

The implementation keeps the internal domain engine ratio-based (`0–1`) and continues converting compatibility fields back to `0–100` percentages at the delivery-rollup boundary.

## Implementation details

### Domain service

Added `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicProgressService.cs`.

The new service accepts:

- one canonical `Epic`
- canonical child `Feature` inputs
- per-Feature `EffectiveProgress` ratio
- per-Feature `TotalEffort` weight

The service rejects non-Epic inputs, ignores non-Feature child inputs, and deterministically returns:

- epic progress ratio
- included/excluded Feature counts
- total aggregation weight

### Reuse of Phase B weight logic

Phase C does not recalculate Feature weight independently.

`DeliveryProgressRollupService` already obtains Feature weight from the same internal Phase B computation used by `FeatureProgressService`:

- `FeatureProgressComputation.ComputeDetails(...)`
- `details.TotalEffort`

That same `TotalEffort` value is passed forward into epic aggregation as the Feature weight.

No alternate formula or duplicate weight logic was introduced.

### Delivery integration

Updated `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicAggregationService.cs` to delegate weighted progress to `EpicProgressService` while preserving forecast summation responsibilities.

Updated `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs` to:

- require canonical epic/feature work items for epic aggregation
- ignore parent items that are not canonical `Epic`
- pass Feature `EffectiveProgress` as a ratio (`0–1`)
- pass Phase B `TotalEffort` as the epic aggregation weight
- keep forecast totals and compatibility percentage fields mapped from the canonical domain result

### Override behavior

Epic aggregation does not read or apply epic `TimeCriticality`.

Any epic-level override value remains ignored at this layer, matching the Phase A normalization rule and the Phase C requirement that Epic progress is a pure aggregation of Feature progress.

## Aggregation formula validation

Implemented formula:

`EpicProgress = Sum(FeatureProgress * FeatureWeight) / Sum(FeatureWeight)`

Where:

- `FeatureProgress` = canonical Feature `EffectiveProgress` from the Phase B engine
- `FeatureWeight` = canonical Feature `TotalEffort`

Rules enforced:

- only `Feature` children are considered
- if `Sum(FeatureWeight) == 0`, epic progress is `0`
- no division by zero occurs
- ordering of Features does not affect the result
- no heuristic weighting or override blending is applied

## Test coverage

Added or updated tests covering:

- all Features 100% → Epic 100%
- all Features 0% → Epic 0%
- mixed weighted aggregation
- no Features → 0
- all weights 0 → 0
- single Feature → Epic equals Feature
- deterministic output regardless of Feature ordering
- non-Epic rejection
- non-Feature child exclusion
- epic `TimeCriticality` ignored
- delivery-rollup integration
- sprint projection integration for zero-weight and weighted epic cases

## Files changed

- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicAggregationModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicAggregationService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicProgressService.cs`
- `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`
- `PoTool.Tests.Unit/Audits/PhaseCEpicProgressDocumentTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `PoTool.Tests.Unit/Services/DeliveryProgressRollupServiceTests.cs`
- `PoTool.Tests.Unit/Services/EpicAggregationServiceTests.cs`
- `PoTool.Tests.Unit/Services/EpicProgressServiceTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

## Build/test results

Required checks executed:

- `dotnet build PoTool.sln --configuration Release`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~EpicProgressServiceTests|FullyQualifiedName~EpicAggregationServiceTests|FullyQualifiedName~DeliveryProgressRollupServiceTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~PhaseCEpicProgressDocumentTests" -v minimal`

Status at implementation time:

- `dotnet build PoTool.sln --configuration Release` — passed
- targeted Phase C unit/doc tests — passed (106/106)

## Risks for Phase D

- Current compatibility models still expose epic progress as percentages even though the canonical epic engine now works on ratios internally.
- Epic aggregation now depends on canonical Feature work item lookups during rollup integration; future phases should preserve that canonical-input contract when extending roadmap or portfolio paths.
- Historical documentation describing null epic progress semantics is now superseded by this deterministic zero-weight behavior and should not be used as the current Phase C source of truth.
