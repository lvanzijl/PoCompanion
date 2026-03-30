# Cross-Slice Validation & Alignment — Build Quality vs Pipeline Insights

## Summary

Build Quality and Pipeline Insights now both have persistence abstractions, but they are **not yet semantically aligned enough for shared abstraction work**.

High-level findings:

- both slices depend on the same cached pipeline-run anchor table: `CachedPipelineRuns`
- both slices use half-open UTC windows over `FinishedDateUtc` for analytical inclusion
- both slices apply default-branch filtering from `PipelineDefinitionEntity.DefaultBranch`
- Build Quality is **child-fact based** (`CachedPipelineRuns` + `TestRuns` + `Coverages`) and explicitly models unknown/partial data
- Pipeline Insights is **run-centric** and computes failure, warning, duration, scatter, and trend metrics directly from cached runs
- product scoping, repository identity, pipeline identity, and failure classification are implemented differently enough that any cross-slice “shared analytical abstraction” would currently risk semantic drift

Current local validation is green:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Recent GitHub Actions inspection for the current branch showed:

- one in-progress agent run
- one completed successful agent run
- no recent failed branch run in the inspected branch-specific sample

## Product Scoping Comparison

### Build Quality

Primary files:

- `PoTool.Api/Controllers/BuildQualityController.cs`
- `PoTool.Api/Services/DeliveryFilterResolutionService.cs`
- `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`

Observed semantics:

- rolling and sprint endpoints resolve scope through `DeliveryFilterResolutionService`
- requested product IDs are validated against the Product Owner scope in `ResolveProductIds(...)`
- when explicit product IDs fall outside the owner scope, Build Quality replaces them with all owner products and records a validation issue
- the rolling and sprint handlers then pass **effective product IDs** into `IBuildQualityReadStore.GetScopeSelectionAsync(IReadOnlyList<int> productIds, ...)`
- the pipeline-detail handler is stricter and uses the overload that also passes `productOwnerId`
- repository filtering is by **repository ID** (`RepositoryId`), not by repository name
- no team-based or area-path-based product scoping is used by the analytical handlers

### Pipeline Insights

Primary files:

- `PoTool.Api/Controllers/PipelinesController.cs`
- `PoTool.Api/Services/PipelineFilterResolutionService.cs`
- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`

Observed semantics:

- the current `/api/pipelines/insights` endpoint resolves scope through `PipelineFilterResolutionService`
- when requested product IDs are `All`, the service derives product IDs from `productOwnerId`
- when explicit product IDs are supplied, `ResolveProductIdsAsync(...)` **does not** validate them against the Product Owner scope; it only normalizes positive distinct IDs
- repository filtering is by **repository name** (`RepoName` / `RepositoryScope`), not by repository ID
- `PipelineFilterContext` contains `TeamIds`, but the current resolution path sets `TeamIds = All()` and the handler does not use them for product scoping
- no area-path-based analytical scoping exists in this slice either

### Side-by-side differences

| Dimension | Build Quality | Pipeline Insights | Risk |
| --- | --- | --- | --- |
| Product-owner scope validation | Explicit owner validation for rolling/sprint filter resolution; hard owner parameter in pipeline detail | Owner-derived only for `All`; explicit product IDs are not owner-validated | Medium |
| Repository identity | Repository ID | Repository name | High |
| Team scope | No team filter dimension in effective read path | TeamIds exist in filter model but are not materially used | Medium |
| Area path usage | None | None | Low |

### Assessment

The slices are **not product-scope equivalent**. They are close for the current endpoints, but they diverge in how strongly Product Owner scope is enforced and in how repository scope is identified.

## Time Window Analysis

### Shared behavior

Primary files:

- `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`
- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`
- `PoTool.Api/Services/PipelineFilterResolutionService.cs`
- `PoTool.Api/Services/DeliveryFilterResolutionService.cs`

Verified commonality:

- both slices predicate on `CachedPipelineRunEntity.FinishedDateUtc`
- both use half-open boundaries: `>= start` and `< end`
- both derive sprint windows from persisted sprint UTC date columns
- both avoid `DateTimeOffset` in SQL predicates and convert to UTC `DateTime` before querying

### Build Quality specifics

- rolling endpoint accepts explicit `windowStartUtc` / `windowEndUtc`
- sprint endpoint resolves a sprint to `RangeStartUtc` / `RangeEndUtc` via `DeliveryFilterResolutionService`
- the pipeline-detail handler always uses the stored sprint boundaries directly
- output DTOs expose `DateTime` sprint/window values, not `DateTimeOffset`

### Pipeline Insights specifics

- the insights endpoint is sprint-based in the controller, but the query model also supports arbitrary `RangeStartUtc` / `RangeEndUtc`
- `GetPipelineInsightsQueryHandler` coalesces the effective window:
  - `filter.RangeStartUtc?.UtcDateTime ?? sprint.StartDateUtc`
  - `filter.RangeEndUtc?.UtcDateTime ?? sprint.EndDateUtc`
- output DTOs expose `SprintStart` / `SprintEnd` as `DateTimeOffset?`
- previous-sprint comparison is based on the immediately preceding sprint for the same `TeamId`, ordered by `StartDateUtc`

### Consistency and risks

1. **Analytical inclusion uses finish time; sync ingestion watermark uses start time**
   - sync stage fetches runs from TFS using `minStartTime: context.PipelineWatermark`
   - both analytical slices later include runs by `FinishedDateUtc`
   - a long-running or delayed build can therefore be relevant to a sprint window while still being sensitive to start-time-based incremental retrieval

2. **Pipeline Insights has a coalesced time model; Build Quality uses clearer endpoint-specific windows**
   - this is not a correctness bug today, but it increases the chance of future divergence when adding new endpoints

3. **Output time types differ**
   - Build Quality returns `DateTime`
   - Pipeline Insights returns `DateTimeOffset?`
   - the predicate logic is aligned, but the transport semantics are not identical

## Identity & Linking

### Pipeline identity

Primary files:

- `PoTool.Api/Persistence/Entities/PipelineDefinitionEntity.cs`
- `PoTool.Api/Persistence/PoToolDbContext.cs`
- `PoTool.Shared/BuildQuality/BuildQualityProductDto.cs`
- `PoTool.Shared/Pipelines/PipelineInsightsDto.cs`

Verified facts:

- `PipelineDefinitionEntity` has:
  - internal database key: `Id`
  - external TFS key: `PipelineDefinitionId`
- uniqueness is enforced on `(ProductId, PipelineDefinitionId)`, not globally on `PipelineDefinitionId`
- `CachedPipelineRuns` are unique on `(ProductOwnerId, PipelineDefinitionId, TfsRunId)` where `PipelineDefinitionId` is the **internal** database FK

### Cross-slice identity mismatch

Build Quality:

- exposes external pipeline definition IDs in `BuildQualityProductDto.PipelineDefinitionIds`
- pipeline-detail lookup also treats `PipelineDefinitionId` as the **external** pipeline definition ID

Pipeline Insights:

- `PipelineTroubleEntryDto.PipelineDefinitionId` is documented as the **database PK**
- `PipelineScatterPointDto.PipelineDefinitionId` is also the **database PK**
- the read model and handler operate entirely on internal definition IDs

### Run identity

Build Quality:

- child facts (`TestRuns`, `Coverages`) link to internal cached build anchor ID (`CachedPipelineRunEntity.Id`)
- aggregation ultimately reduces to build counts plus summed child facts

Pipeline Insights:

- carries both `DbId` and `TfsRunId` in `PipelineInsightsRun`
- DTOs expose both cached DB ID (`Id`) and TFS run ID (`TfsRunId`) for scatter/drilldown use

### Repository identity

Build Quality:

- repository scoping is by `RepositoryId`

Pipeline Insights:

- repository scoping is by `RepoName`
- repository universe is composed from both `Repositories.Name` and `PipelineDefinitions.RepoName`

### Work item linking

No pipeline-to-work-item analytical link exists in either slice.

That means:

- Build Quality has no work-item dimension
- Pipeline Insights has no work-item dimension
- any future cross-slice “pipeline to delivery-unit” abstraction still lacks a shared linking contract

### Gaps and assumptions

1. **Public pipeline identity is not normalized**
   - Build Quality exposes external IDs
   - Pipeline Insights exposes internal IDs

2. **Repository identity is not normalized**
   - ID-based vs name-based scope can drift on rename or name collision scenarios

3. **Sync stage assumes external pipeline IDs are globally unique enough for dictionary materialization**
   - `PipelineSyncStage` builds a dictionary keyed only by external `PipelineDefinitionId`
   - that is stronger than the database uniqueness rule and should be treated as an implicit global-uniqueness assumption

## Aggregation Differences

### Build Quality

Primary files:

- `PoTool.Api/Services/BuildQuality/BuildQualityProvider.cs`
- `PoTool.Tests.Unit/Services/BuildQualityProviderTests.cs`

Aggregation model:

- build counts are derived from `BuildQualityBuildFact`
- test pass rate is computed from **summed totals**, not averaged percentages
- coverage is computed from **summed covered/total lines**
- confidence is a threshold-derived signal:
  - build threshold met if eligible builds >= 3
  - test threshold met if test volume >= 20
- unknown-state semantics are explicit per metric dimension

### Pipeline Insights

Primary files:

- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`
- `PoTool.Shared/Pipelines/PipelineInsightsDto.cs`

Aggregation model:

- counts completed / failed / warning / succeeded runs directly from cached runs
- computes failure and warning rates per product and globally
- computes median and P90 durations from run durations
- computes top-3 ranking by:
  - failure rate desc
  - completed builds desc
  - pipeline name asc
- computes half-sprint trend using first-half vs second-half failure rates with a 10pp threshold

### Conflicts in logic

These are not accidental code duplicates; they are **different analytical definitions**:

| Topic | Build Quality | Pipeline Insights |
| --- | --- | --- |
| Primary unit | build + child facts | run |
| Success metric | `Succeeded / EligibleBuilds` | `Succeeded / CompletedBuilds` |
| Duration metrics | none | median, P90, scatter |
| Confidence / uncertainty | explicit | implicit / absent |
| Trending | none | previous sprint delta + half-sprint trend |
| Summation behavior | sums test and coverage facts | counts classified runs |

### Assessment

There is **no safe shared aggregation layer yet**. The slices operate on the same anchors but answer different questions and use different result semantics.

## Failure Classification

### Build Quality classification

Source:

- `PoTool.Api/Services/BuildQuality/BuildQualityProvider.cs`
- `PoTool.Tests.Unit/Services/BuildQualityProviderTests.cs`

Verified behavior:

- eligible builds:
  - `Succeeded`
  - `Failed`
  - `PartiallySucceeded`
- excluded from eligible denominator:
  - `Canceled`
- `PartiallySucceeded` reduces success rate by remaining in the denominator without increasing succeeded count
- `Unknown` and missing-result cases do not contribute to eligible builds
- classification is fixed; there are no caller toggles

### Pipeline Insights classification

Source:

- `PoTool.Core/Pipelines/Queries/GetPipelineInsightsQuery.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

Verified behavior:

- always counted:
  - `Succeeded`
  - `Failed`
- conditionally counted:
  - `PartiallySucceeded` when `IncludePartiallySucceeded = true`
  - `Canceled` when `IncludeCanceled = true`
- ignored:
  - `Unknown`
  - `None`
- `PartiallySucceeded` goes into a separate warning bucket
- classification is intentionally toggle-driven

### Mismatch analysis

1. **PartiallySucceeded semantics differ**
   - Build Quality always includes it in eligible builds
   - Pipeline Insights can exclude it entirely

2. **Canceled semantics differ**
   - Build Quality always excludes canceled from the denominator
   - Pipeline Insights can include canceled in completed count

3. **Unknown-state modeling differs**
   - Build Quality reports explicit unknown reasons
   - Pipeline Insights falls back to zero or null outputs without an explicit unknown-reason contract

4. **Retry logic is absent in both slices**
   - neither slice deduplicates “retries” or groups reruns of the same logical build
   - every cached run is treated as a separate analytical event

### Assessment

Failure classification is **not aligned** across slices. This is acceptable for isolated product behavior, but it is a hard blocker for any planned shared failure-classification abstraction until a canonical classification matrix is explicitly chosen.

## Read Model Evaluation

### Build Quality read models

Source:

- `PoTool.Api/Services/BuildQuality/IBuildQualityReadStore.cs`
- `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`

Strengths:

- strongly intent-driven
- scoped fact bundle is explicit (`BuildQualityScopeSelection`)
- downstream provider receives normalized fact types (`BuildQualityBuildFact`, `BuildQualityTestRunFact`, `BuildQualityCoverageFact`)
- domain-facing outputs model confidence and unknown states explicitly

Weaknesses:

- one scope selection aggregates many concerns into a single large transport object
- the product/read-store contract is still partly shaped by storage tables rather than a higher-level “quality evidence” concept

### Pipeline Insights read models

Source:

- `PoTool.Api/Services/IPipelineInsightsReadStore.cs`
- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`

Strengths:

- fully materialized
- no `IQueryable`
- narrow to current handler needs

Weaknesses:

- more storage-shaped than Build Quality
- carries both UTC and offset timestamps for the same run
- exposes internal DB identifiers (`DbId`, internal pipeline definition ID) directly into downstream DTOs
- no normalization layer separates “raw run fact” from “presentation-ready insight fact”

### Coupling comparison

Build Quality is more intent-driven.

Pipeline Insights is improved relative to direct EF usage, but its read model still looks closer to persisted shape than semantic shape.

## CDC Alignment

### Shared dependency

Both slices depend on the same ingestion anchor path:

- `PoTool.Api/Services/Sync/PipelineSyncStage.cs`

Shared assumptions:

- pipeline definitions exist and can be resolved from TFS IDs
- cached run anchors are present in `CachedPipelineRuns`
- `CreatedDateUtc` / `FinishedDateUtc` are populated correctly during sync
- `DefaultBranch` is populated on definitions when branch filtering should be strict

### Build Quality-specific dependency

Build Quality additionally depends on:

- `TestRuns`
- `Coverages`
- successful child-fact retrieval from TFS
- correct linkage from external build ID to cached build anchor ID

It also has explicit missing-data handling:

- logs `BUILDQUALITY_TESTRUN_MISSING_DATA`
- logs `BUILDQUALITY_COVERAGE_MISSING_DATA`
- backfills missing child facts from cached scoped runs up to `MaxBuildQualityBuildBatchSize`

### Pipeline Insights-specific dependency

Pipeline Insights depends only on anchored cached runs plus sprint metadata and definitions.

That makes it:

- less sensitive to missing child facts
- more directly sensitive to any missing or stale run anchors

### Dependency and risks

1. **Both slices are exposed to anchor-loss or anchor-staleness**
   - if a run never lands in `CachedPipelineRuns`, both slices miss it

2. **Build Quality is more resilient to missing child-fact rows**
   - current sync stage can backfill missing `TestRuns` / `Coverages`
   - Pipeline Insights has no analogous corrective path because it has no child-fact layer

3. **Neither slice carries a freshness/completeness marker in its analytical DTOs**
   - consumers cannot tell whether “zero” means “none happened” or “cache may be incomplete”

## Incremental Sync Risks

### Shared failure scenarios

1. **Run-anchor omission due to incremental retrieval shape**
   - sync fetches pipeline runs with `minStartTime = PipelineWatermark` and `top: 100`
   - analytics later query by `FinishedDateUtc`
   - this start-time vs finish-time mismatch is a cross-slice sensitivity

2. **Silent analytical loss when runs are missing from cache**
   - neither slice exposes a “cache completeness” indicator
   - both can therefore produce plausible but incomplete results after a partial refresh

3. **Implicit dependence on default-branch completeness**
   - both slices include all runs when `DefaultBranch` is null/empty
   - branch filtering therefore changes meaning based on sync completeness of pipeline definition metadata

### Build Quality-specific risks

- missing child facts can still produce partially known results
- the provider explicitly surfaces unknown reasons, which is good
- `EfBuildQualityReadStore.LoadBuildsAsync(...)` executes one run query per pipeline definition, which increases sensitivity to scope size and partial failure patterns

### Pipeline Insights-specific risks

- no backfill or explicit incomplete-data signal exists for missing run anchors
- no child-fact layer means fewer ingestion points, but also fewer observability signals
- trend, top-3, and duration calculations can all drift silently if the cache is partially refreshed

## Performance Risks

Observed risk indicators only; no optimization recommendations are being implemented here.

### Build Quality

Source:

- `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`

Risks:

- `LoadBuildsAsync(...)` performs an EF query inside a loop over pipeline definitions
- this is an N+1 pattern for run selection
- product-level regrouping then performs additional in-memory filtering over the accumulated build set

### Pipeline Insights

Source:

- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

Risks:

- default-branch filtering happens client-side after materializing all scoped runs for the window
- the handler repeatedly filters the same in-memory run lists by product and definition
- global top-3 accumulation does repeated per-definition scans inside the product loop

### Shared observation

Neither slice currently looks unsafe for small-to-moderate datasets, but both contain scale-sensitive patterns that should be addressed before unifying them behind heavier shared analytical contracts.

## Critical Inconsistencies

The following items are the must-fix or must-decide blockers before further **cross-slice** abstraction:

1. **Pipeline identity is inconsistent**
   - Build Quality exposes external pipeline definition IDs
   - Pipeline Insights exposes internal DB pipeline-definition IDs

2. **Repository identity is inconsistent**
   - Build Quality scopes by repository ID
   - Pipeline Insights scopes by repository name

3. **Failure classification is inconsistent**
   - fixed Build Quality semantics vs toggle-driven Pipeline Insights semantics
   - especially for `PartiallySucceeded` and `Canceled`

4. **Product-owner validation behavior is inconsistent**
   - Build Quality delivery filter resolution validates explicit requested product IDs against owner scope
   - Pipeline filter resolution does not do equivalent validation for explicit product IDs

5. **Incremental ingestion assumptions are not explicitly aligned with analytical time semantics**
   - sync watermarking is start-time-based
   - both analytical slices are finish-time-based

## Recommended Fix Order

### 1. Normalize public pipeline identity first

Why first:

- every downstream shared abstraction would otherwise be built on ambiguous keys
- Build Quality and Pipeline Insights cannot safely share ranking, drilldown, or navigation contracts until this is explicit

Recommended direction:

- choose one public pipeline identifier contract:
  - external TFS pipeline ID
  - or internal DB ID
- document and enforce it consistently across DTOs, read models, and handlers

### 2. Normalize repository identity second

Why:

- ID-based and name-based repository scoping will drift under rename, duplication, or future cross-product reuse
- repository identity is foundational for product scoping and pipeline linking

Recommended direction:

- choose a canonical repository identity for analytical scope
- keep names as display values only

### 3. Decide a canonical cross-slice result classification matrix

Why:

- this is the biggest blocker for any shared aggregation or “run health” abstraction

Recommended decision points:

- should `PartiallySucceeded` always count?
- should `Canceled` ever affect denominators?
- is warning a first-class category or only a Build Quality penalty?

### 4. Align explicit product-owner validation behavior

Why:

- current endpoint behavior is mostly safe, but the reusable filter semantics are different
- future expansion of Pipeline Insights filters would otherwise inherit weaker validation than Build Quality

### 5. Document and, later, align incremental sync assumptions with analytical finish-time windows

Why:

- this is the main shared data-completeness risk
- it affects both slices regardless of abstraction shape

### 6. Only after the above, reconsider shared analytical abstractions

Why:

- current similarities are infrastructural, not semantic
- premature unification would encode the wrong common denominator

## Final Assessment

**Is system aligned:** no

**Is it safe to continue abstraction:** no

More specifically:

- it is safe to continue **slice-local** cleanup and documentation
- it is **not** safe to continue with shared cross-slice analytical abstractions yet

The immediate blocker is not EF coupling anymore; it is **semantic divergence**:

- different public identifiers
- different repository identities
- different failure classification rules
- different product-scope validation behavior
- a shared dependency on incremental sync assumptions that is not yet explicitly normalized

Until those items are resolved or explicitly codified as intentionally different, further cross-slice abstraction work would carry a high risk of encoding incorrect shared behavior.
