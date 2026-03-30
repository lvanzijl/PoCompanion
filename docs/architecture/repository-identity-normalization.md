# Repository Identity Normalization — Build Quality vs Pipeline Insights

## Summary

Repository identity is now normalized across Build Quality and Pipeline Insights to use the external repository ID as the canonical analytical identity.

The normalization rule is:

- analytical and API-facing pipeline contracts use repository ID for identity
- repository name remains display-only metadata
- name-based repository matching no longer drives Pipeline Insights grouping, filtering, or read-store selection

This resolves the next structural inconsistency identified in cross-slice validation:

- Build Quality already scoped repository-related analytics by repository ID
- Pipeline Insights previously resolved and propagated repository scope by repository name

That mismatch was unsafe under:

- repository renames
- duplicate names across projects
- cross-product reuse

After this change:

- Build Quality remains repository-ID based
- Pipeline Insights repository filter context now uses repository IDs
- Pipeline Insights read-store selection now filters pipeline definitions by repository ID
- repository names no longer participate in Pipeline Insights analytical identity

## Repository Identity Inventory

### Before normalization

| Location | Identity form before | Classification | Notes |
| --- | --- | --- | --- |
| `PoTool.Shared/BuildQuality/BuildQualityProductDto.cs` | `RepositoryIds` | Repository ID | Already canonical for Build Quality public product-level identity |
| `PoTool.Shared/BuildQuality/PipelineBuildQualityDto.cs` | `RepositoryId` + `RepositoryName` | Mixed, but ID is identity | Repository ID already used for scope selection; repository name already display metadata |
| `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs` | `RepositoryId` in read-store selection | Repository ID | Build Quality read store already grouped and selected by repository ID |
| `PoTool.Core/Pipelines/Filters/PipelineFilterModels.cs` | `RepositoryNames`, `RepositoryScope` | Repository name | Pipeline filter context and effective scope used repository names as identity |
| `PoTool.Shared/Pipelines/PipelineFilterDtos.cs` | `RepositoryNames` | Repository name | Pipeline API-facing filter metadata exposed repository names as canonical pipeline repository filter identity |
| `PoTool.Api/Services/PipelineFilterResolutionService.cs` | repository universe and scope built from names | Mixed/derived, effectively name-based | Used repository names from `Repositories.Name` and `PipelineDefinitions.RepoName`; resolved analytical scope by name |
| `PoTool.Api/Services/IPipelineInsightsReadStore.cs` | repository scope parameter by name | Repository name | `GetPipelineDefinitionsAsync` accepted repository names |
| `PoTool.Api/Services/EfPipelineInsightsReadStore.cs` | `definition.RepoName` filtering | Repository name | Pipeline definition selection depended on repository name matching |
| `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | `filter.RepositoryScope` as name set | Repository name | Handler passed name-based repository scope into read-store selection |
| `PoTool.Client/Helpers/CanonicalClientResponseFactory.cs` | pipeline filter notice rendered `RepositoryNames` | Repository name | API-facing canonical filter metadata implied repository-name identity for pipeline responses |

### Build Quality status before normalization

Build Quality was already aligned with the required repository identity contract:

- repository identity used repository ID
- repository name was supplemental metadata only

### Pipeline Insights status before normalization

Pipeline Insights still treated repository name as the canonical repository scope:

- filter models used `RepositoryNames`
- effective scope used repository name lists
- read-store selection filtered by `RepoName`
- canonical filter response metadata surfaced repository names

That meant repository renames could alter analytical scoping even when the stable repository ID had not changed.

## Normalization Decision

### Canonical repository identity

The canonical repository identity is the external stable repository ID.

Rules applied:

- repository identity in pipeline analytical contracts uses repository ID
- repository name is display-only metadata
- no Pipeline Insights analytical logic depends on repository name

### Why repository ID is canonical

Repository ID is the correct analytical identity because it is:

- stable across repository renames
- unambiguous across products and projects
- consistent with existing Build Quality contracts
- safe for future cross-slice joins with pipelines, pull requests, and work items

Repository name is not acceptable as identity because it is:

- mutable
- display-oriented
- vulnerable to ambiguity and drift

## Code Changes

### 1. Pipeline filter models now use repository IDs

Updated:

- `PoTool.Core/Pipelines/Filters/PipelineFilterModels.cs`
- `PoTool.Shared/Pipelines/PipelineFilterDtos.cs`

Changes:

- `PipelineFilterContext.RepositoryNames` → `RepositoryIds`
- `PipelineEffectiveFilter.RepositoryScope` now carries `IReadOnlyList<int>`
- pipeline filter response DTO metadata now exposes `RepositoryIds`

This removes the ambiguous name-based contract from the pipeline analytical filter model.

### 2. Pipeline filter resolution now resolves repository scope by ID

Updated:

- `PoTool.Api/Services/PipelineFilterResolutionService.cs`

Changes:

- `PipelineFilterBoundaryRequest` now supports optional `RepositoryIds`
- repository universe is loaded as IDs from configured repositories and cached pipeline definitions
- requested repository filters are validated and resolved as positive IDs
- effective pipeline filter context now carries repository IDs only
- pipeline definition scope selection is filtered by `RepositoryId`

Important boundary behavior:

- if no repository IDs are explicitly requested, the boundary still resolves the effective pipeline repository scope from the repository-ID universe
- repository name is no longer propagated into analytical scope

### 3. Pipeline Insights read store now filters by repository ID

Updated:

- `PoTool.Api/Services/IPipelineInsightsReadStore.cs`
- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

Changes:

- `GetPipelineDefinitionsAsync` now accepts repository IDs
- EF selection filters `PipelineDefinitions` with `definition.RepositoryId`
- Pipeline Insights handler passes repository ID scope through to the read store

This is the critical analytical change that removes repository-name identity from Pipeline Insights grouping and selection.

### 4. Pipeline filter metadata now reflects repository IDs

Updated:

- `PoTool.Client/Helpers/CanonicalClientResponseFactory.cs`

Changes:

- pipeline canonical filter notices now compare `RepositoryIds` instead of `RepositoryNames`

This keeps client-facing pipeline filter metadata aligned with the new canonical identity contract.

### 5. Tests updated

Updated or added:

- `PoTool.Tests.Unit/Services/PipelineFilterResolutionServiceTests.cs`
- `PoTool.Tests.Unit/Controllers/PipelinesControllerCanonicalFilterTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineRunsForProductsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Helpers/CanonicalClientResponseFactoryTests.cs`
- `PoTool.Tests.Unit/Services/PipelineServiceTests.cs`

These tests now verify:

- pipeline repository scope resolves to repository IDs
- pipeline controller response metadata exposes repository IDs
- Pipeline Insights still groups correctly when repository names drift
- repository rename scenarios do not change repository-scoped Pipeline Insights grouping
- pipeline metrics and run handlers accept ID-based repository scope without semantic change

## Internal vs Public Identity

### Internal identity

Pipeline Insights now enforces repository identity internally with repository IDs in:

- pipeline filter context
- effective repository scope
- pipeline filter resolution
- read-store definition selection

### Public/API-facing identity

Pipeline analytical filter metadata now exposes repository IDs in:

- `PipelineFilterContextDto.RepositoryIds`

Repository name is no longer the public pipeline filter identity contract.

### Display metadata

Repository names still exist in repository entities and configuration-oriented surfaces, but they are no longer used as pipeline analytical identity.

Build Quality already followed this pattern:

- `RepositoryId` / `RepositoryIds` = identity
- `RepositoryName` = display metadata

Pipeline Insights now matches that contract.

## Validation

### Workflow/build status

Checked recent GitHub Actions runs for branch `copilot/introduce-persistence-abstraction`.

Observed status at validation time:

- latest Copilot run in progress
- recent completed branch runs successful
- no recent failed branch run required failure-log inspection

### Commands run

Baseline:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Focused repository-identity tests:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineMetricsQueryHandlerTests|FullyQualifiedName~GetPipelineRunsForProductsQueryHandlerTests|FullyQualifiedName~CanonicalClientResponseFactoryTests|FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~BuildQualityQueryHandlerTests|FullyQualifiedName~BuildQualityProviderTests" -v minimal`

Full validation:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

### Result

Focused repository-identity tests passed after the repository-ID normalization changes.

Full validated test projects also passed after the change.

## Remaining Semantic Gaps

This prompt resolves repository identity normalization between Build Quality and Pipeline Insights.

Still out of scope:

- pull request slices still use repository-name filtering contracts
- product scoping rules remain unchanged
- pipeline identity remains separate from repository identity
- broader cross-slice joins to PRs and work items still need follow-up normalization work

Those are separate architectural tasks.

## Final Status

**Repository identity is now normalized across Build Quality and Pipeline Insights: yes**

Both slices now use repository ID as the canonical analytical identity, and repository name is no longer used as Pipeline Insights analytical identity.
