# PR Pipeline Linkage Analysis

Date: 2026-03-17

## Direct Linkage Fields Found

### Conclusion

No direct persisted Pull Request ↔ Pipeline Run foreign key was found in the solution.

The two persisted roots are modeled independently:

- `PullRequestEntity` stores PR identity, repository, branch, lifecycle dates, and an optional `ProductId`, but no pipeline/run identifier or navigation property to pipeline data (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestEntity.cs:8-103`).
- `CachedPipelineRunEntity` stores pipeline definition scope, TFS run ID, timing, branch, and URL, but no `PullRequestId`, PR number, or navigation property to PR data (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs:8-105`).
- EF configuration defines PR foreign keys only to `ProductEntity`, and pipeline-run foreign keys only to `ProfileEntity` and `PipelineDefinitionEntity`; there is no PR ↔ pipeline relationship or join table in `OnModelCreating` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs:251-272`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs:541-565`).

### Fields that may look related but are not PR ↔ pipeline linkage

Several `PullRequestId` fields do exist, but they only link PRs to PR-owned detail rows or PR-linked work items:

- `PullRequestIterationEntity.PullRequestId` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestIterationEntity.cs:8-51`)
- `PullRequestCommentEntity.PullRequestId` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestCommentEntity.cs:8-80`)
- `PullRequestFileChangeEntity.PullRequestId` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestFileChangeEntity.cs:8-59`)
- `PullRequestWorkItemLinkEntity.PullRequestId` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestWorkItemLinkEntity.cs:9-27`)

These are direct persisted FKs, but only inside the PR subsystem. They do not connect PRs to pipeline runs.

No `PipelineRunId` field was found on PR models, DTOs, entities, handlers, or client models.

## Indirect Linkage Logic Found

### Repository-scoped discovery

Pipeline definition discovery is repository-scoped, not PR-scoped:

- `SyncPipelineRunner` discovers pipeline definitions by repository name and persists them with `RepositoryId`, `RepoId`, `RepoName`, and `DefaultBranch` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/SyncPipelineRunner.cs:525-585`).
- `PipelineDefinitionEntity` itself is rooted on repository metadata, not PR metadata (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PipelineDefinitionEntity.cs:9-99`).
- `PipelineDefinitionDto` exposes repository identity and default branch, but no PR linkage fields (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Pipelines/PipelineDefinitionDto.cs:7-65`).

Classification: **Indirect inferred linkage** at repository scope.

### Branch-based matching/filtering

Both PRs and pipeline runs carry source-branch data:

- PRs persist `RepositoryName`, `SourceBranch`, and `TargetBranch` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestEntity.cs:22-91`).
- Cached pipeline runs persist `SourceBranch` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs:72-82`).
- Pipeline run sync stores only `dto.Branch` into `entity.SourceBranch`; it does not map any PR identifier or parse `TriggerInfo` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PipelineSyncStage.cs:189-219`).
- Cached pipeline reads support branch filtering for runs (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs:122-176`).

Classification: **Indirect inferred linkage** capability exists through branch semantics, but no concrete PR ↔ run matching logic was found.

### Trigger-based metadata

`PipelineRunDto` includes `Trigger` and `TriggerInfo`, which can indicate a PR-triggered build at the transport level (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Pipelines/PipelineRunDto.cs:6-19`).

The real TFS client maps raw build/release reasons into `PipelineRunTrigger.PullRequest` and preserves the raw reason string as `TriggerInfo`:

- Build parsing (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs:468-495`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs:576-603`)
- Release parsing (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs:498-548`)

However, the cached read path explicitly discards this linkage hint:

- Cached runs are returned as `Trigger = PipelineRunTrigger.Unknown` and `TriggerInfo = null` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs:208-225`).

Classification:

- **Display-only / non-functional metadata** in the live transport DTO
- **Dead or unused field/path** for cache-backed analytics, because the cached model does not retain it

### Time-window usage

Pipeline analytics query runs by sprint time windows using `FinishedDateUtc` and optionally `CreatedDateUtc`; PR analytics query PRs by PR creation dates. These are independent time filters, not a PR ↔ run correlation algorithm:

- Pipeline insights load cached runs inside sprint windows (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:226-274`)
- PR insights load PRs by PR creation range (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs:94-129`)
- PR delivery insights load PRs by date range and then resolve work item links, not pipeline runs (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:150-205`)

Classification: **Indirect inferred linkage ingredients** only; no actual PR/pipeline temporal matching implementation was found.

## Real vs Mock Path Comparison

### Real TFS / Azure DevOps path

The real path can observe PR-like trigger reasons on pipeline runs, but it still does not join runs back to persisted PRs:

- PR ingestion is repository-scoped and stores PR detail/work-item detail keyed by PR ID and repository name (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PullRequestSyncStage.cs:41-120`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PullRequestSyncStage.cs:192-210`)
- Pipeline ingestion fetches runs by pipeline definition IDs and only persists run identity, result, timing, and branch (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PipelineSyncStage.cs:32-109`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PipelineSyncStage.cs:189-219`)
- The live TFS client parses `pullrequest` reasons, but no later stage resolves that to a cached PR row (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs:471-495`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs:576-603`)

Result: **the real path lacks direct persisted PR ↔ pipeline linkage too**.

### Cache persistence path

The cache is the strongest evidence that the system-wide model is repository-scoped rather than PR-linked:

- `CachedPipelineRunEntity` has no PR field (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs:8-105`)
- `CachedPipelineReadProvider` drops trigger metadata and only returns branch/run/result/timing data (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs:208-225`)
- `GetPipelineInsightsQueryHandler` further narrows analytics to each pipeline definition’s default branch, explicitly excluding feature-branch and PR builds when `DefaultBranch` is populated (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:255-270`)

Result: **cache-backed analytics are not only missing a direct PR FK; they intentionally operate on branch-filtered repository/pipeline aggregates**.

### Mock path

The mock path mirrors the same repository-scoped shape:

- Mock PRs are generated with repository and branch values, but no pipeline linkage (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPullRequestGenerator.cs:37-78`)
- Mock pipeline definitions are assigned per repository through `MockDevOpsSeedCatalog.GetPipelineDefinitionsForRepository` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/MockDevOpsSeedCatalog.cs:38-71`)
- Mock pipeline runs are generated with random trigger info and random branch selection, not matched to generated PR IDs/branches (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPipelineGenerator.cs:197-215`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPipelineGenerator.cs:337-368`)
- `MockTfsClientTests` validate repository-scoped pipeline definitions and the presence of PR-triggered runs, but do not assert any PR ↔ run pairing (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/MockTfsClientTests.cs:24-42`)

Result: **mock data also lacks a direct PR FK and does not provide deterministic PR ↔ pipeline correlation**.

## Actual Usage in the Codebase

### 1. Direct persisted FK usage

Present only inside subsystems:

- PR → iterations/comments/file changes/work item links (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestIterationEntity.cs:8-51`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestCommentEntity.cs:8-80`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestFileChangeEntity.cs:8-59`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestWorkItemLinkEntity.cs:9-27`)
- PipelineRun → PipelineDefinition/Profile (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs:541-565`)

No direct persisted FK usage exists for PR ↔ pipeline.

### 2. Indirect inferred linkage

Observed forms:

- Repository-scoped discovery of pipeline definitions (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/SyncPipelineRunner.cs:531-585`)
- Repository-scoped PR filtering in PR insights (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs:54-123`)
- Branch filter support for pipeline runs (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs:122-176`)
- Default-branch-only pipeline analytics (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:255-270`)

Important limit: none of these paths actually join one PR to one pipeline run.

### 3. Display-only / non-functional metadata

- `PipelineRunDto.Trigger` and `TriggerInfo` exist on the transport contract (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Pipelines/PipelineRunDto.cs:6-19`)
- The client’s pipeline page exposes branch metadata in the run drawer, but no PR identity, validation status, or PR correlation (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:451-469`)

### 4. Dead or unused field/path

- `CachedPipelineRunEntity.SourceVersion` exists in the persistence model, but runtime sync code never populates it (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs:78-82`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PipelineSyncStage.cs:189-219`)
- Cached reads intentionally erase trigger metadata, making `Trigger`/`TriggerInfo` unusable after sync (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs:208-225`)

### What the current codebase actually depends on

Based on the analyzed handlers and DTO flows, the codebase currently depends on:

- **Repository-level pipeline stats**, not exact PR → pipeline traceability (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:81-106`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:255-270`)
- **PR counts and PR quality metrics without pipeline correlation** (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs:133-209`)
- **PR ↔ work item ↔ hierarchy correlation**, but not PR ↔ pipeline correlation (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:186-257`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestsByWorkItemIdQueryHandler.cs:23-54`)

No feature was found that currently depends on inferred PR validation status from pipeline data.

## Impact of Missing Direct PR Foreign Key

If no direct PR foreign key exists, the affected surface is:

- **PR detail and insight pages** cannot show exact validation-build status per PR; current PR views are repository/work-item oriented only (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:1-220`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor:1-220`)
- **Pipeline analytics** remain limited to per-product/per-pipeline health and default-branch runs; they cannot answer which specific PR caused or passed a given run (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:81-106`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:255-270`)
- **Work item ↔ PR ↔ pipeline traceability** stops at the PR stage because work item linkage ends at `PullRequestWorkItemLinkEntity` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PullRequestWorkItemLinkEntity.cs:5-27`)
- **Sync consistency checks** cannot verify whether a cached PR-triggered build actually belongs to a specific cached PR because the cache drops trigger details and stores no PR key (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PipelineSyncStage.cs:189-219`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs:208-225`)

Practical examples of questions the current cache model cannot answer exactly:

- Which pipeline runs validated PR `1234`?
- Did PR `1234` pass before merge?
- Which failed runs belong to PRs linked to work item `5678`?
- Which PRs in a sprint had failed validation builds?

## Dead / Unused Linkage Paths

- `SourceVersion` on cached pipeline runs appears to be an unrealized commit-based correlation hook. It is persisted in schema/configuration but not assigned by sync code (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs:78-82`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs:541-565`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PipelineSyncStage.cs:189-219`).
- `TriggerInfo` can carry a PR-like reason in the live client and in mock generation, but it is not retained in cache-backed reads (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs:471-495`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPipelineGenerator.cs:202-212`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs:208-225`).
- Mock PR-triggered runs are present for realism, but the generator uses random PR numbers and random branches rather than linking to the generated PR dataset (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPipelineGenerator.cs:202-212`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPipelineGenerator.cs:357-368`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPullRequestGenerator.cs:42-78`).

## Conclusion

The codebase does **not** currently rely on a direct persisted Pull Request → Pipeline Run foreign key anywhere.

Across the real TFS path, cache model, handlers, UI, and mock path, PR and pipeline data are modeled as separate repository-scoped datasets:

- PRs are correlated to repositories, branches, work items, and hierarchy.
- Pipelines are correlated to repositories, pipeline definitions, default branches, and time windows.
- PR-trigger hints exist only as transient transport/mock metadata and are not preserved in the cached analytics model.

System-wide, this is a consistent modeling limitation rather than a single missing field in one layer.
