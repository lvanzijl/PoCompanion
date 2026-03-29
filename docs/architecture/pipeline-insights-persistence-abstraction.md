# Pipeline Insights Persistence Abstraction Slice

## Summary

This slice introduces a dedicated persistence abstraction for Pipeline Insights reads:

- `IPipelineInsightsReadStore`
- `EfPipelineInsightsReadStore`

The change removes direct `PoToolDbContext` usage from `GetPipelineInsightsQueryHandler` and keeps the existing Pipeline Insights orchestration logic intact. The refactor is intentionally narrow: no Build Quality changes, no new shared abstraction layer, no domain model changes, and no performance tuning.

## Design

### Interface

`IPipelineInsightsReadStore` is shaped only around the current Pipeline Insights read path. It exposes five fully materialized operations:

1. `GetSprintWindowAsync(int sprintId, CancellationToken cancellationToken)`
2. `GetPreviousSprintWindowAsync(int teamId, DateTime sprintStartUtc, CancellationToken cancellationToken)`
3. `GetProductsAsync(PipelineEffectiveFilter filter, CancellationToken cancellationToken)`
4. `GetPipelineDefinitionsAsync(IReadOnlyList<int> productIds, IReadOnlyCollection<string> repositories, CancellationToken cancellationToken)`
5. `GetRunsAsync(IReadOnlyList<PipelineInsightsDefinitionSelection> pipelineDefinitions, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken)`

### Read models

The store returns explicit slice-level read models:

- `PipelineInsightsSprintWindow`
- `PipelineInsightsProductSelection`
- `PipelineInsightsDefinitionSelection`
- `PipelineInsightsRun`

These models are fully materialized and carry only the fields the handler needs for:

- sprint window selection
- product scoping
- pipeline-definition selection
- scatter plot construction
- failure-rate analysis
- duration metrics
- previous-sprint delta calculations

### Boundary rules enforced

The abstraction does **not** expose:

- `IQueryable`
- EF entities
- navigation properties
- generic repository methods

EF stays contained inside the read-store implementation.

## Implementation

### EF-backed store

`EfPipelineInsightsReadStore` encapsulates the EF query surface that previously lived in `GetPipelineInsightsQueryHandler`.

It now owns reads from:

- `Sprints`
- `Products`
- `PipelineDefinitions`
- `CachedPipelineRuns`

### Query behavior preserved

The store keeps the prior query semantics intact:

- selected sprint lookup still uses the requested sprint ID
- previous sprint lookup still uses the same-team, immediately preceding `StartDateUtc` rule
- products remain ordered by product name
- pipeline definitions remain filtered by scoped product IDs and repository scope
- pipeline runs remain filtered by `FinishedDateUtc` inside the selected window
- default-branch filtering still includes all runs when a definition has no stored default branch
- run facts still retain the fields required for scatter, duration, breakdown, and delta computation

### SQLite-safe handling

The implementation continues to use UTC `DateTime` bounds in predicates against `FinishedDateUtc`, which preserves the existing SQLite-safe pattern already used by the handler. `DateTimeOffset` values are projected only as materialized read-model fields and are not used in server-side predicate or ordering logic.

## Refactored Components

The following components were updated:

- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`
- `PoTool.Api/Services/IPipelineInsightsReadStore.cs`
- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`

`GetPipelineInsightsQueryHandler` now depends on `IPipelineInsightsReadStore` and performs only orchestration and in-memory analytics over store-returned read models.

## Behavior Verification

### Handler leakage checks

Post-change verification confirmed:

- no `PoToolDbContext` usage remains in `GetPipelineInsightsQueryHandler`
- no `IQueryable` is returned from `IPipelineInsightsReadStore`
- no EF entities escape the Pipeline Insights read store

### Observed parity evidence

Existing behavior-focused handler tests remained green for:

- empty and unknown sprint handling
- failure-rate and warning-rate calculations
- canceled and partially succeeded toggles
- sprint window filtering
- global top-3 ordering
- previous-sprint delta calculations
- scatter-point projection and ordering
- per-pipeline breakdown ordering and trend classification
- sprint boundary propagation in the response DTO

## Validation

### Focused validation

Succeeded:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsScatterPointTests|FullyQualifiedName~GetPipelineInsightsBreakdownTests|FullyQualifiedName~ServiceCollectionTests" --logger "console;verbosity=normal"`

Result:

- `36 / 36` focused tests passed

### Full validation

Succeeded:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Results:

- solution build passed with `0` warnings and `0` errors
- unit test suite passed: `1682 / 1682`
- domain test suite passed: `1 / 1`
- total validated tests passed: `1683 / 1683`

## Architectural Impact

### What improved

This slice now has an explicit analytical persistence boundary:

- the handler no longer knows about EF or the DbContext
- Pipeline Insights persistence logic is centralized in one EF-backed implementation
- the handler is easier to test and reason about because its remaining logic is in-memory orchestration
- the slice now follows the same persistence-abstraction pattern already established for Build Quality and pull-request analytics

### What was intentionally not changed

This slice did **not**:

- change Build Quality abstractions
- introduce a generic cross-slice query-store layer
- change DTO shapes or domain models
- optimize query strategy or performance

## Risks

Residual risks are limited to the normal parity risks of moving query code:

- future changes to Pipeline Insights filtering could drift if the handler and store evolve independently
- case-sensitivity semantics for repository scope still follow the existing EF translation behavior
- no dedicated golden-data parity harness exists yet to compare pre-refactor and post-refactor outputs on the same fixture set

No new security-sensitive write path or dependency was introduced.

## Next Candidates

The next logical abstraction targets are:

1. other analytical pipeline read paths that still compose EF queries directly
2. cache-backed analytical handlers in adjacent slices that have not yet been moved behind a usage-shaped store
3. any helper service that still couples handler orchestration directly to EF query composition

## Final Status

Slice complete: yes

The requested Pipeline Insights persistence abstraction is implemented for the targeted scope, the handler is free of direct DbContext usage, and the validated behavior remains green.

## Security Summary

- `code_review` was run for this slice during finalization and reported no review comments.
- `codeql_checker` reported `0` alerts, but C# database analysis was skipped in this environment because the analysis database is too large.
- No new dependencies were introduced.
- No persistence write behavior was changed.
