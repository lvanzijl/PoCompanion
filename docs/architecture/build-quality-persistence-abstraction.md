# Build Quality Persistence Abstraction

## Summary

This slice introduces a dedicated persistence abstraction for Build Quality reads:

- `IBuildQualityReadStore`
- `EfBuildQualityReadStore`

The change removes direct `PoToolDbContext` usage from the Build Quality handlers and keeps all EF-backed query composition inside a single Build Quality read store. The goal was architectural cleanup only: no domain changes, no behavior changes, no caching changes, and no performance tuning.

## Design

### Interface definition

`IBuildQualityReadStore` is intentionally small and shaped around current Build Quality usage only.

It exposes three fully materialized read operations:

1. `GetScopeSelectionAsync(int productOwnerId, DateTime windowStartUtc, DateTime windowEndUtc, int? repositoryId, int? pipelineDefinitionId, CancellationToken cancellationToken)`
2. `GetScopeSelectionAsync(IReadOnlyList<int> productIds, DateTime? windowStartUtc, DateTime? windowEndUtc, int? repositoryId, int? pipelineDefinitionId, CancellationToken cancellationToken)`
3. `GetSprintWindowAsync(int sprintId, CancellationToken cancellationToken)`

The store returns slice-level read models such as `BuildQualityScopeSelection`, `BuildQualityProductSelection`, and `BuildQualityPipelineDefinitionSelection`. It does not expose implementation-specific nested types.

### Responsibilities

The interface owns:

- sprint metadata reads needed by Build Quality handlers
- product / pipeline / repository scope resolution for Build Quality
- build run, test run, and coverage fact loading
- ordering and materialization of Build Quality read models

The interface does **not** expose:

- `IQueryable`
- EF entities
- generic repository-style CRUD methods
- speculative methods for other slices

## Implementation

### EF-backed store

`EfBuildQualityReadStore` now encapsulates the EF queries that previously lived in `BuildQualityScopeLoader` plus the sprint lookup logic that previously lived in the handlers.

It reads from:

- `Sprints`
- `Products`
- `PipelineDefinitions`
- `CachedPipelineRuns`
- `TestRuns`
- `Coverages`

### Query behavior preserved

The implementation keeps the existing Build Quality semantics intact:

- product IDs are normalized with distinct ordering
- products remain ordered by product name
- pipeline definitions remain filtered by product, repository, and pipeline definition scope
- build runs remain filtered by the same sprint/window boundaries
- product-owner filtering on cached runs is preserved for the pipeline-detail path
- default-branch filtering remains per definition and still includes all runs when no default branch is configured
- test-run and coverage facts are still loaded only for the selected build IDs
- Build Quality result computation remains in `IBuildQualityProvider`

### SQLite safety

The store continues to use UTC `DateTime` boundaries in predicates against `FinishedDateUtc` and performs materialization inside the persistence layer. No `DateTimeOffset` predicates were introduced, and no `IQueryable` crosses the abstraction boundary.

## Refactored Components

The following components now depend on `IBuildQualityReadStore` instead of querying `PoToolDbContext` directly:

- `PoTool.Api/Handlers/BuildQuality/GetBuildQualityRollingWindowQueryHandler.cs`
- `PoTool.Api/Handlers/BuildQuality/GetBuildQualitySprintQueryHandler.cs`
- `PoTool.Api/Handlers/BuildQuality/GetBuildQualityPipelineDetailQueryHandler.cs`

Supporting changes:

- `PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs` was replaced by `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`
- DI now registers `IBuildQualityReadStore` to `EfBuildQualityReadStore`
- unit tests were updated to construct handlers with the new abstraction
- DI tests now assert Build Quality read-store registration

## Behavior Verification

### No behavior change

Observed behavior remains the same:

- rolling-window Build Quality queries still return the same summary and per-product results
- sprint Build Quality queries still return the same sprint metadata and metrics
- pipeline-detail Build Quality queries still preserve repository/pipeline scoping and selected names
- missing-default-branch handling still includes all builds for that definition

### Leakage checks

Post-change verification confirmed:

- no `PoToolDbContext` usage remains in `PoTool.Api/Handlers/BuildQuality`
- no `IQueryable` is returned from the Build Quality read store
- no EF entities are exposed from the Build Quality read store

## Validation

### Focused validation

Succeeded:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BuildQualityQueryHandlerTests|FullyQualifiedName~ServiceCollectionTests" -v minimal`

Result:

- `18 / 18` tests passed

### Full validation

Succeeded:

- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Results:

- solution build passed with `0` warnings and `0` errors
- unit test suite passed: `1681 / 1681`
- domain test suite passed: `1 / 1`
- total validated tests passed: `1682 / 1682`

## Architectural Impact

### What improved

This slice now has a clear analytical read boundary:

- handlers depend on a Build Quality-specific abstraction
- EF query composition is centralized in one implementation
- Build Quality persistence concerns are separated from Build Quality metric computation
- the slice now matches the persistence-abstraction pattern already introduced for pull-request analytics

### What is still missing

This change is intentionally narrow. It does **not** yet introduce:

- a broader pipeline analytics store
- refactoring of `GetPipelineInsightsQueryHandler`
- persistence abstractions for other analytical slices
- parity tests that compare pre-refactor and post-refactor outputs through a dedicated golden-data harness

## Next Candidates

The next logical candidates for the same pattern are:

1. pipeline analytics reads, especially `GetPipelineInsightsQueryHandler`
2. additional cache-backed analytical slices that still compose EF queries in handlers
3. larger hierarchy- or event-driven analytics only after their fact contracts are narrowed enough to avoid over-generalization

## Security Summary

- `code_review` was run and one encapsulation issue was reported; it was fixed by replacing the leaked nested pipeline-definition type with the slice-level `BuildQualityPipelineDefinitionSelection` read model.
- `codeql_checker` reported `0` alerts, but the C# analysis database was skipped because the database size was too large in this environment.
- No new dependencies were introduced.
- No security-sensitive behavior or persistence write paths were changed.

## Final Status

The Build Quality persistence abstraction slice is **complete and stable** for the requested scope.

It now has:

- a minimal `IBuildQualityReadStore`
- an EF-backed `EfBuildQualityReadStore`
- Build Quality handlers refactored to depend on the abstraction
- preserved query semantics and output behavior
- passing focused and full validation

No user-visible behavior changes were introduced.
