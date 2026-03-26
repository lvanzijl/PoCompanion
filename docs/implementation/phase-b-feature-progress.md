# Phase B Feature Progress & Override Aggregation

## Summary

Implemented the Phase B feature-progress model in the domain layer as a deterministic effort-based calculation.

Feature progress now:

- includes only Feature children of type `Product Backlog Item`, `PBI`, `User Story`, and `Bug`
- ignores `Task`
- uses canonical `StateClassification`
- calculates base progress from child effort only
- applies `Microsoft.VSTS.Common.TimeCriticality` as a strict post-calculation override

The implementation keeps existing API/UI percentage rendering intact by converting the internal domain ratio back to percentage values at the delivery-rollup boundary.

## Implementation details

### Domain service

Updated `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureProgressService.cs` to compute progress from canonical work items instead of count/story-point request primitives.

The service now accepts:

- one canonical `Feature`
- normalized child work items with canonical `StateClassification`

The service rejects non-Feature inputs and deterministically returns:

- calculated progress ratio
- raw override percentage
- effective progress ratio
- completed effort
- total effort

### Canonical work item support

Extended `PoTool.Core.Domain/Models/CanonicalWorkItem.cs` with `Effort` so normalized canonical work items can carry the effort field required by the Phase B formula.

Updated canonical mappings used by delivery-trend calculations so effort is available in-domain without introducing DTO-side or UI-side progress logic.

### Delivery integration

Updated `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs` to:

- include `Bug` in progress contributors
- keep `Task` ignored
- pass canonical child/state inputs into `FeatureProgressService`
- use the returned total effort as the feature aggregation weight
- convert the domain ratio to 0–100 percentage fields for existing read models

### EstimationMode integration

No multi-mode branching was added to the Phase B domain calculation.

The existing runtime warning in `PoTool.Api/Services/SprintTrendProjectionService.cs` remains the guard for products configured with non-default `EstimationMode`.

Current behavior:

- if `EstimationMode != StoryPoints`, runtime still uses the current effort field
- no alternate field switching was introduced
- no dual logic was introduced

## Formula validation

Base formula implemented:

`Progress = CompletedEffort / TotalEffort`

Where:

- `CompletedEffort` = sum of included child effort where `StateClassification == Done`
- `TotalEffort` = sum of included child effort where `StateClassification != Removed`

Rules enforced:

- null effort is treated as `0`
- removed items are excluded from total effort
- tasks do not contribute
- if `TotalEffort == 0`, progress is `0`

## Override behavior validation

Override rule implemented exactly as:

- `TimeCriticality == null` → use base progress
- `TimeCriticality == 0` → final progress `0`
- `TimeCriticality == 50` → final progress `0.5`
- `TimeCriticality == 100` → final progress `1`

The override is applied after base calculation.

No blending, weighting, heuristics, or fallback override behavior is used.

## Test coverage

Added or updated tests covering:

- all contributors done → 100%
- no contributors done → 0%
- mixed contributor ratios
- no children → 0
- all effort null → 0
- removed items excluded
- tasks ignored
- bugs included
- strict overrides at 0, 50, and 100
- override replacing base progress
- non-Feature rejection
- deterministic output independent of child ordering
- delivery-rollup integration
- feature forecast consuming the new 0–1 domain ratio

## Files changed

- `PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgressEngineModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureForecastService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureProgressService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`
- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`
- `PoTool.Tests.Unit/Services/DeliveryProgressRollupServiceTests.cs`
- `PoTool.Tests.Unit/Services/FeatureForecastServiceTests.cs`
- `PoTool.Tests.Unit/Services/FeatureProgressServiceTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

## Build/test results

Required checks executed:

- `dotnet build PoTool.sln --configuration Release`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~FeatureProgressServiceTests|FullyQualifiedName~FeatureForecastServiceTests|FullyQualifiedName~DeliveryProgressRollupServiceTests|FullyQualifiedName~SprintTrendProjectionServiceTests" -v minimal`

Status at implementation time:

- targeted unit tests passed
- final full build/test validation pending completion of the change set

## Risks for Phase C

- Epic and product rollups now consume feature weights derived from total contributor effort; later phases should confirm this remains the intended aggregation basis.
- Existing UI/API raw progress fields still expose percentages for compatibility, while the domain engine now computes canonical ratios internally.
- If invalid `TimeCriticality` values exist in persisted data, the domain engine now rejects them instead of heuristically correcting them.
