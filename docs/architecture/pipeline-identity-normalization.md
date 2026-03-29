# Pipeline Identity Normalization — Build Quality vs Pipeline Insights

## Summary

Public pipeline identity is now normalized across Build Quality and Pipeline Insights.

The normalization rule is:

- **public pipeline identity = external TFS pipeline definition ID**

This change resolves the highest-priority semantic blocker identified in cross-slice validation:

- Build Quality already exposed external TFS pipeline definition IDs
- Pipeline Insights had been exposing internal `PipelineDefinitionEntity.Id` database keys in public DTOs

After this change:

- Build Quality continues to expose external TFS pipeline definition IDs
- Pipeline Insights now also exposes external TFS pipeline definition IDs in all public/API-facing pipeline identifier fields
- internal DB pipeline-definition IDs remain internal-only and are still used for joins and cached-run correlation

This preserves behavior while removing a structural contradiction that affected:

- cross-slice alignment
- drilldown consistency
- UI navigation
- future shared contract design

## Public Identity Inventory

### Before normalization

| Surface | Location | Before | Notes |
| --- | --- | --- | --- |
| Build Quality product breakdown | `PoTool.Shared/BuildQuality/BuildQualityProductDto.cs` → `PipelineDefinitionIds` | external TFS pipeline definition ID | Already correct |
| Build Quality pipeline detail | `PoTool.Shared/BuildQuality/PipelineBuildQualityDto.cs` → `PipelineDefinitionId` | external TFS pipeline definition ID | Already correct |
| Pipeline Insights trouble entries | `PoTool.Shared/Pipelines/PipelineInsightsDto.cs` → `PipelineTroubleEntryDto.PipelineDefinitionId` | **internal DB pipeline-definition ID** | Incorrect public identity |
| Pipeline Insights scatter points | `PoTool.Shared/Pipelines/PipelineInsightsDto.cs` → `PipelineScatterPointDto.PipelineDefinitionId` | **internal DB pipeline-definition ID** | Incorrect public identity |
| Pipeline Insights breakdown entries | `PoTool.Shared/Pipelines/PipelineInsightsDto.cs` → `PipelineBreakdownEntryDto.PipelineDefinitionId` | **internal DB pipeline-definition ID** | Incorrect public identity |
| Pipeline Insights client drilldown/highlight usage | `PoTool.Client/Pages/Home/PipelineInsights.razor` | consumed `PipelineDefinitionId` as if it were public | Behavior depended on the incorrect internal ID exposure |

### After normalization

| Surface | Location | After | Notes |
| --- | --- | --- | --- |
| Build Quality product breakdown | `PoTool.Shared/BuildQuality/BuildQualityProductDto.cs` → `PipelineDefinitionIds` | external TFS pipeline definition ID | Unchanged |
| Build Quality pipeline detail | `PoTool.Shared/BuildQuality/PipelineBuildQualityDto.cs` → `PipelineDefinitionId` | external TFS pipeline definition ID | Unchanged |
| Pipeline Insights trouble entries | `PoTool.Shared/Pipelines/PipelineInsightsDto.cs` → `PipelineTroubleEntryDto.PipelineDefinitionId` | external TFS pipeline definition ID | Normalized |
| Pipeline Insights scatter points | `PoTool.Shared/Pipelines/PipelineInsightsDto.cs` → `PipelineScatterPointDto.PipelineDefinitionId` | external TFS pipeline definition ID | Normalized |
| Pipeline Insights breakdown entries | `PoTool.Shared/Pipelines/PipelineInsightsDto.cs` → `PipelineBreakdownEntryDto.PipelineDefinitionId` | external TFS pipeline definition ID | Normalized |
| Pipeline Insights client drilldown/highlight usage | `PoTool.Client/Pages/Home/PipelineInsights.razor` | now consumes normalized external ID | No client contract change required |

### Naming assessment

The field name `PipelineDefinitionId` remains valid after normalization because:

- the public contract is now explicitly defined as the **external TFS pipeline definition ID**
- Build Quality already used the same public meaning
- changing the field name would have widened scope unnecessarily

To avoid ambiguity, the DTO comments were updated so the contract is explicit.

## Normalization Decision

The canonical public pipeline identity is now:

- **external TFS pipeline definition ID**

Why this decision is correct:

1. Build Quality already used it successfully in public contracts and drilldown behavior.
2. It is the stable cross-slice identity that API and UI consumers can use without knowledge of persistence internals.
3. It avoids leaking internal EF/database keys into public analytics contracts.
4. It supports future cross-slice linking and shared contracts without binding those contracts to database implementation details.

The alternative—using DB IDs as the public contract—was explicitly rejected by the prompt and would have increased coupling between API/UI consumers and persistence internals.

## Code Changes

### 1. Pipeline Insights read-store selection now carries both identities

Updated:

- `PoTool.Api/Services/IPipelineInsightsReadStore.cs`
- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`

`PipelineInsightsDefinitionSelection` now carries:

- `Id` = internal DB pipeline-definition ID
- `ExternalPipelineDefinitionId` = external TFS pipeline definition ID

This makes the internal/public identity boundary explicit at the read-store layer.

### 2. Pipeline Insights handler now maps public DTOs to external IDs

Updated:

- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

The handler still:

- groups runs by internal DB definition ID
- uses internal DB definition ID dictionaries for joins and lookups

But when constructing public DTOs, it now maps to:

- `definition.ExternalPipelineDefinitionId`

This applies to:

- `PipelineTroubleEntryDto`
- `PipelineScatterPointDto`
- `PipelineBreakdownEntryDto`

A small helper was added in the handler to make the boundary explicit:

- internal lookups remain internal
- public output uses external IDs only

### 3. Pipeline Insights DTO comments were corrected

Updated:

- `PoTool.Shared/Pipelines/PipelineInsightsDto.cs`

Comments previously described `PipelineDefinitionId` as:

- database PK of `PipelineDefinitionEntity`

They now describe it correctly as:

- external TFS pipeline definition ID

This change was applied to:

- `PipelineTroubleEntryDto.PipelineDefinitionId`
- `PipelineScatterPointDto.PipelineDefinitionId`
- `PipelineBreakdownEntryDto.PipelineDefinitionId`

### 4. Build Quality remained unchanged in behavior

Verified:

- `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`
- `PoTool.Shared/BuildQuality/BuildQualityProductDto.cs`
- `PoTool.Shared/BuildQuality/PipelineBuildQualityDto.cs`

Build Quality already exposed external TFS pipeline definition IDs in its public contracts, so no production logic change was required there.

## Internal vs Public Identity Boundary

The identity boundary is now explicit:

### Internal identity

Used only for persistence and joins:

- `PipelineDefinitionEntity.Id`
- `CachedPipelineRunEntity.PipelineDefinitionId` (FK to internal definition row)
- in-memory lookup keys such as `defByDbId`

### Public identity

Used only at API/UI boundaries:

- `PipelineDefinitionEntity.PipelineDefinitionId`

### Boundary implementation

The normalized flow in Pipeline Insights is now:

1. read store loads definition rows with both IDs
2. handler performs joins/grouping on internal DB IDs
3. handler maps DTO output to external TFS pipeline definition IDs
4. API/UI consumers see only the external identity

This preserves internal joins safely while preventing DB IDs from leaking into public contracts.

## Validation

### Commands run

Baseline:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Focused identity-validation tests:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsBreakdownTests|FullyQualifiedName~BuildQualityQueryHandlerTests|FullyQualifiedName~ServiceCollectionTests" -v minimal`

Full validation:

- `dotnet test PoTool.sln --configuration Release --no-build --nologo -v minimal`

### Test coverage updated

Updated tests verify that:

- Build Quality product pipeline IDs remain external TFS pipeline definition IDs
- Pipeline Insights top-3 trouble entries expose external TFS pipeline definition IDs
- Pipeline Insights scatter points expose external TFS pipeline definition IDs
- Pipeline Insights breakdown entries expose external TFS pipeline definition IDs

### Result

The identity normalization preserves behavior while changing the public meaning of the affected Pipeline Insights fields from internal DB IDs to external TFS IDs.

## Remaining Semantic Gaps

This prompt resolves only the highest-priority public pipeline identity contradiction.

The larger cross-slice alignment work still has unresolved gaps, including:

- repository identity mismatch
  - Build Quality uses repository ID
  - Pipeline Insights uses repository name
- failure-classification mismatch
  - Build Quality fixed semantics
  - Pipeline Insights toggle-driven semantics
- product-scope validation differences
- shared sensitivity to incremental sync completeness and start-time vs finish-time semantics

Those gaps remain intentionally out of scope for this change.

## Final Status

**Public pipeline identity is now normalized across Build Quality and Pipeline Insights: yes**

Canonical public pipeline identity is now consistently:

- **external TFS pipeline definition ID**

Internal DB pipeline-definition IDs remain internal-only and no longer leak through Pipeline Insights public analytical contracts.
