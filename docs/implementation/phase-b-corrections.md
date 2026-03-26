# Phase B Corrections

## Summary

Applied a targeted hardening pass to the Phase B feature-progress implementation to reduce contract leakage, enforce canonical typing on the feature-progress path, normalize invalid `TimeCriticality` values deterministically, and keep forecasting logic decoupled from the feature-progress engine.

No new functionality, UI changes, or API contract expansion were introduced.

## Corrections applied per section

### 1. Remove forecasting coupling

Applied:

- Kept `FeatureForecastService` independent of `FeatureProgressService`.
- `FeatureForecastService` continues to consume only `EffectiveProgress` and `Effort`.
- Removed rollup dependence on internal `FeatureProgressService` result details by moving internal calculation details behind an internal-only helper.

Verification:

- Forecasting services do not directly reference `FeatureProgressService`.
- The solution builds cleanly without circular dependency changes.

### 2. Enforce canonical work item typing

Applied:

- Added API-side canonical mapping through `PoTool.Api/Adapters/CanonicalWorkItemTypeMapper.cs`.
- Normalized the feature-progress path to canonical domain types before domain models are created.
- Hardened `CanonicalWorkItem`, `DeliveryTrendWorkItem`, and `DeliveryTrendResolvedWorkItem` to reject non-canonical work item types.
- Replaced feature-progress-path work item type checks with canonical type usage.

Canonical types used in this corrected path:

- `Feature`
- `PBI`
- `Bug`
- supporting hierarchy-only types: `Epic`, `Task`, `Goal`, `Objective`, `Other`

Verification:

- Raw `Product Backlog Item` / `User Story` values are mapped to canonical `PBI` before entering the feature-progress domain path.
- Focused search of the corrected feature-progress path no longer relies on raw TFS work item type comparisons.

### 3. Define and enforce TimeCriticality validation

Applied:

- `WorkItemFieldSemantics.NormalizeTimeCriticality(...)` now applies explicit rules:
  - `null` → no override
  - `0..100` → valid
  - `< 0` → invalid, normalized to `null`
  - `> 100` → invalid, normalized to `null`
- `SprintTrendProjectionService` now logs a warning with:
  - `workItemId`
  - invalid value

Result:

- Invalid values are not thrown
- Invalid values are not accepted
- Invalid values are treated as missing override deterministically

### 4. Narrow FeatureProgressService contract

Applied:

- Simplified the public `FeatureProgressService` output to:
  - `BaseProgress`
  - `EffectiveProgress`
- Moved override raw value and effort totals into an internal-only calculation details structure used inside the domain assembly.

Result:

- External consumers no longer depend on internal calculation details.
- Public service contract is limited to progress semantics only.

### 5. Preserve determinism and ordering independence

Applied:

- Retained explicit deterministic accumulation logic over child contributors.
- Preserved ordering independence in the progress calculation.
- Kept the explicit ordering-independence unit test for reversed child ordering.

## Files changed

- `PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs`
- `PoTool.Api/Adapters/DeliveryTrendProjectionInputMapper.cs`
- `PoTool.Api/Adapters/HistoricalSprintInputMapper.cs`
- `PoTool.Api/Adapters/StateClassificationInputMapper.cs`
- `PoTool.Api/Adapters/CanonicalWorkItemTypeMapper.cs`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgressEngineModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureProgressService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- `PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs`
- `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`
- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`
- `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs`
- `PoTool.Tests.Unit/Adapters/StateClassificationInputMapperTests.cs`
- `PoTool.Tests.Unit/DomainWorkItemFieldSemanticsTests.cs`
- `PoTool.Tests.Unit/Services/FeatureProgressServiceTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

## Test coverage updates

Added or updated coverage for:

- invalid `TimeCriticality` normalization to `null`
- canonical type enforcement for direct domain work item construction
- API-side canonical state classification mapping
- minimal `FeatureProgressService` contract behavior
- ordering independence
- sprint projection behavior after canonical type hardening

## Build/test results

### Build

- `dotnet build PoTool.sln --configuration Release` — passed before and after the correction pass

### Relevant tests

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~FeatureProgressServiceTests|FullyQualifiedName~DomainWorkItemFieldSemanticsTests|FullyQualifiedName~StateClassificationInputMapperTests|FullyQualifiedName~DeliveryProgressRollupServiceTests|FullyQualifiedName~SprintTrendProjectionServiceTests" -v minimal` — passed

## Remaining risks (if any)

- This correction pass hardens the feature-progress path specifically. Other domain slices still retain older work-item-type handling patterns outside the scope of this issue.
- `FeatureProgress` read models still expose compatibility fields such as `Override` and `ValidationSignals`; the public service contract is narrow, but downstream compatibility models were intentionally not removed in this correction pass.
