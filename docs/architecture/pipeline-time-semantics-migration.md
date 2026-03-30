# Pipeline Time Semantics Migration — Finish-Time Canonical Anchor

## Summary

Pipeline ingestion and pipeline analytics are now aligned on **finish time** as the canonical analytical boundary.

Before this migration:

- incremental pipeline ingestion fetched runs from TFS using a **start-time watermark**
- Pipeline Insights and Build Quality analytics already evaluated cached runs by **finish time**

That mismatch allowed a long-running pipeline to be:

- ingested according to its `StartTime`
- analyzed according to its `FinishTime`

which could shift runs across windows and create inconsistent analytical inclusion.

This prompt implemented a controlled migration by:

- validating available timestamps
- auditing analytical consumers
- introducing dual-watermark state with overlap-compatible ingestion
- adding migration diagnostics
- switching canonical incremental progression to **finish time**
- retaining start time only as metadata and compatibility input for overlap/reconciliation

## Current Temporal Model

### Before the migration

#### Ingestion

Pipeline sync used:

- `SyncContext.PipelineWatermark`
- `ITfsClient.GetPipelineRunsAsync(..., minStartTime: context.PipelineWatermark, ...)`
- `CachedPipelineRunEntity.CreatedDateUtc` / `StartTime`

This meant the incremental fetch boundary was start-time anchored.

Primary location:

- `PoTool.Api/Services/Sync/PipelineSyncStage.cs`

#### Analytics

Pipeline Insights already used:

- `CachedPipelineRunEntity.FinishedDateUtc`
- sprint membership based on `FinishedDateUtc`

Primary locations:

- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

Build Quality already used:

- `CachedPipelineRunEntity.FinishedDateUtc`

Primary location:

- `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`

#### Remaining inconsistent analytical paths found during the audit

The audit found older cached pipeline read/query paths still using start time:

- `PoTool.Api/Services/CachedPipelineReadProvider.cs`
- `PoTool.Api/Services/PipelineFiltering.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs`

These have now been aligned to finish-time filtering.

## Dual Timestamp Validation

### Data model and ingestion path audited

Audited locations:

- `PoTool.Shared/Pipelines/PipelineRunDto.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs`
- `PoTool.Api/Services/Sync/PipelineSyncStage.cs`
- `PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs`

### What was verified

#### Build runs

`RealTfsClient.ParseBuildRun(...)` reads:

- `startTime` → `PipelineRunDto.StartTime`
- `finishTime` → `PipelineRunDto.FinishTime`

#### Release runs

`RealTfsClient.ParseReleaseRun(...)` reads:

- `createdOn` → `PipelineRunDto.StartTime`
- `modifiedOn` → `PipelineRunDto.FinishTime`

### Completed runs

Completed runs are persisted as:

- `State = "completed"` when `FinishTime` exists
- `FinishedDateUtc = dto.FinishTime?.UtcDateTime`

This means the cache already distinguishes completed vs in-progress based on finish-time availability.

### Failed runs

Failed runs with valid `FinishTime` are fully compatible with finish-time analytics and canonical finish watermark progression.

### Canceled runs

Canceled runs with valid `FinishTime` are also finish-time compatible and remain analytically classified by the existing outcome logic.

### Running / in-progress runs

Runs without `FinishTime` remain persisted with:

- `State = "running"`
- `FinishedDateUtc = null`

These runs are **not** analytically included in finish-time windows, but they are retained in cache and now explicitly participate in migration overlap reconciliation.

### Completed run without finish time

No normal cache path marks a run as completed without a finish time:

- `PipelineSyncStage` derives `"completed"` exclusively from `dto.FinishTime.HasValue`

Migration diagnostics now count any fetched run that appears completed by result shape but lacks `FinishTime` as suspicious input.

## Analytics Audit

### Locations inspected

#### Pipeline Insights

- `PoTool.Api/Services/EfPipelineInsightsReadStore.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

Status after audit:

- finish-time filtering already canonical
- no start-time inclusion/grouping correction required
- start time remains only for scatter X-axis rendering and duration computation metadata

#### Build Quality

- `PoTool.Api/Services/BuildQuality/EfBuildQualityReadStore.cs`
- `PoTool.Api/Services/BuildQuality/BuildQualityProvider.cs`

Status after audit:

- build selection already finish-time anchored
- no start-time analytical inclusion logic remained

#### Pipeline metrics endpoints

- `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs`
- `PoTool.Api/Services/CachedPipelineReadProvider.cs`
- `PoTool.Api/Services/PipelineFiltering.cs`

Corrections applied:

- range filtering now uses `FinishTime` / `FinishedDateUtc`
- ordering for “last run” now prefers `FinishTime`
- `LastRunTime` now represents the most recent finish time, not start time

#### Pipeline runs endpoints

- `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs`
- `PoTool.Api/Services/CachedPipelineReadProvider.cs`
- `PoTool.Api/Services/LivePipelineReadProvider.cs`

Corrections applied:

- finish-time range semantics are now used for cached analytical reads
- live provider parameters were renamed to finish-time semantics and locally finish-filtered after TFS retrieval

### Remaining retained start-time usage

The following start-time usage remains intentionally:

- duration calculations (`FinishTime - StartTime`)
- scatter-chart X-axis positioning in Pipeline Insights
- persisted metadata (`CreatedDate`, `CreatedDateUtc`)
- TFS compatibility fetch parameter in sync (`minStartTime`) because the upstream client contract is still start-time based

These retained usages are informational or compatibility-related, not analytical inclusion anchors.

## Dual-Window Ingestion

### Watermark model introduced

Cache state now maintains:

- `PipelineWatermark` = compatibility **start-time** watermark
- `PipelineFinishWatermark` = canonical **finish-time** watermark

Updated locations:

- `PoTool.Api/Persistence/Entities/ProductOwnerCacheStateEntity.cs`
- `PoTool.Core/Contracts/ICacheStateRepository.cs`
- `PoTool.Api/Repositories/CacheStateRepository.cs`
- `PoTool.Core/Contracts/ISyncStage.cs`
- `PoTool.Api/Services/Sync/SyncPipelineRunner.cs`
- `PoTool.Api/Services/Sync/FinalizeCacheStage.cs`

### Compatibility overlap model

`PipelineSyncStage` now computes a compatibility fetch boundary from:

- the start-time watermark
- the finish-time watermark
- the earliest cached still-running pipeline start time

This produces a temporary overlap window that keeps previously tracked in-progress runs eligible for refresh until they complete.

### Inclusion contract during migration

Fetched runs are included for persistence when any of the following is true:

- `StartTime >= start watermark`
- `FinishTime >= finish watermark`
- the run already exists in cache as a tracked in-progress run
- full sync mode is active

This prevents silent exclusion when:

- a run started earlier
- but finished after the canonical finish watermark

### Why this is safe

- completed long-running runs previously cached as in-progress are re-fetched through the tracked-running overlap
- duplicate ingestion remains safe because cached pipeline runs are upserted by `(ProductOwnerId, PipelineDefinitionId, TfsRunId)`
- build-quality child ingestion now operates on the migration-filtered effective run batch instead of every overlap-fetched run

## Diagnostics

### Added diagnostics

`PipelineSyncStage` now logs:

- `PIPELINE_TIME_SEMANTICS_DIAGNOSTICS`

Reported fields include:

- fetched run count
- included run count
- compatibility fetch start
- tracked running run count
- crossing-boundary count
- start-only inclusion count
- finish-only inclusion count
- duplicate run count
- in-progress without finish count
- completed-without-finish count

### What the diagnostics validate

- runs crossing time boundaries
- runs included by start but not finish
- runs included by finish but not start
- possible duplicate ingestion
- suspicious timestamp-shape anomalies from upstream data

### How “suspiciously missing analytics runs” was checked

This prompt used targeted validation tests rather than adding a new persistent report surface.

Added/updated tests verify:

- a run starting before a sprint but finishing inside the sprint is counted analytically
- cached analytical reads use finish-time windows
- long-running in-progress cached runs are reconciled when they finish later
- in-progress runs without finish time do not advance the canonical finish watermark

## Canonical Anchor Switch

### Canonical ingestion boundary

Canonical pipeline incremental progression is now:

- **`PipelineFinishWatermark`**

`PipelineSyncStage` returns the new finish watermark as the stage result watermark.

### Start-time role after the switch

`PipelineWatermark` is now retained only as:

- compatibility state for overlap fetching
- migration/backward-compatibility input
- non-analytical metadata

Start time no longer defines analytical inclusion.

### Compatibility path still retained

One compatibility path remains intentionally:

- TFS bulk pipeline retrieval still accepts only the existing start-time-based filter contract (`minStartTime`)

Because of that upstream limitation, the sync stage still computes a compatibility fetch window and then applies canonical finish-time inclusion locally.

That compatibility path is documented and deliberate.

## Cleanup

### Removed / corrected

- cached analytical pipeline queries no longer filter windows by start time
- pipeline metrics no longer use start time for last-run ordering
- shared pipeline analytical filtering no longer uses `StartTime` for range inclusion

### Retained temporarily or intentionally

- upstream TFS fetch uses `minStartTime` as a compatibility retrieval input
- start time remains persisted for duration and display metadata
- start watermark remains stored for overlap reconciliation safety

This is the minimum safe cleanup within the prompt scope.

## Validation

### Workflow/build status

Checked recent GitHub Actions runs for branch `copilot/introduce-persistence-abstraction`.

Observed at validation time:

- latest Copilot run in progress
- recent branch runs successful
- no recent failed branch run needed deeper failure-log analysis

### Commands run

Baseline:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Focused validation:

- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PipelineSyncStageBuildQualityTests|FullyQualifiedName~CachedPipelineReadProviderSqliteTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineMetricsQueryHandlerTests|FullyQualifiedName~CacheStateRepositoryTests|FullyQualifiedName~CachedReadProviderDiagnosticsTests|FullyQualifiedName~GetPipelineRunsForProductsQueryHandlerTests" -v minimal`

Migration validation:

- `dotnet tool restore`
- `dotnet ef migrations add AddPipelineFinishWatermark --no-build --configuration Release --project PoTool.Api/PoTool.Api.csproj --startup-project PoTool.Api/PoTool.Api.csproj --context PoToolDbContext --output-dir Migrations`
- `dotnet ef database update --configuration Release --project PoTool.Api/PoTool.Api.csproj --startup-project PoTool.Api/PoTool.Api.csproj --context PoToolDbContext --connection "Data Source=/tmp/prompt59-migration.db"`
- `dotnet ef database update 20260327090926_AddWorkItemCreatedDateUtcForSqliteBackfill --configuration Release --project PoTool.Api/PoTool.Api.csproj --startup-project PoTool.Api/PoTool.Api.csproj --context PoToolDbContext --connection "Data Source=/tmp/prompt59-migration.db"`
- `dotnet ef database update --configuration Release --project PoTool.Api/PoTool.Api.csproj --startup-project PoTool.Api/PoTool.Api.csproj --context PoToolDbContext --connection "Data Source=/tmp/prompt59-migration.db"`

Full validation:

- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

### Results

- baseline build succeeded
- focused pipeline migration tests passed
- migration upgrade, rollback, and reapply succeeded
- full validated suite passed:
  - `PoTool.Tests.Unit`: 1699 passed
  - `PoTool.Core.Domain.Tests`: 1 passed

## Risks and Mitigations

### Long-running pipelines spanning windows

Risk:

- start before the old boundary
- finish after the new finish boundary

Mitigation:

- tracked in-progress runs now participate in compatibility overlap fetching
- local inclusion uses either timestamp or tracked-running status

### Failed and canceled runs

Risk:

- non-success runs could be excluded if finish-time handling only assumed success paths

Mitigation:

- finish-time watermarking is result-agnostic
- failed/canceled runs with `FinishTime` are still included and persisted
- analytics classification remains delegated to the existing normalized outcome rules

### In-progress runs without finish time

Risk:

- they could incorrectly advance the canonical boundary or disappear before completion

Mitigation:

- they do not advance `PipelineFinishWatermark`
- they remain cached as running
- tracked-running overlap keeps them eligible for reconciliation

### Duplicate ingestion

Risk:

- overlap fetches can re-fetch already cached runs

Mitigation:

- upsert identity remains stable on cached run keys
- diagnostics count duplicate batch keys
- existing duplicate-handling tests still pass

## Final Status

**Pipeline ingestion and analytics are now end-time consistent for analytical inclusion: yes**

Finish time is now the canonical analytical and incremental progression anchor, while start time remains only as metadata plus a documented compatibility retrieval aid needed to safely bridge upstream TFS fetch behavior during the migration.
