# PR Provider Cleanup

## Summary

After cache-only analytical guardrails blocked pull request workspace reads from falling back to live mode, the default PR read path no longer needed runtime provider switching. This cleanup makes the analytical PR read path explicit and deterministic by binding the default `IPullRequestReadProvider` registration directly to `CachedPullRequestReadProvider`, removing the PR-specific lazy wrapper, and deleting the unused PR branch from the runtime provider factory.

## Removed / Simplified Components

- Removed `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LazyPullRequestReadProvider.cs`
- Removed `GetPullRequestReadProvider()` from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
- Rewrote misleading comments in PR handlers and `IPullRequestReadProvider` so they no longer describe runtime live/cache selection for analytical reads
- Left `LivePullRequestReadProvider` in place for explicit live resolution, but it is no longer the default application read path

## DI Changes

### Before

- Keyed live registration: `IPullRequestReadProvider["Live"] -> LivePullRequestReadProvider`
- Keyed cached registration: `IPullRequestReadProvider["Cached"] -> CachedPullRequestReadProvider`
- Default registration: `IPullRequestReadProvider -> LazyPullRequestReadProvider`
- Runtime path: `LazyPullRequestReadProvider -> DataSourceAwareReadProviderFactory -> Live/Cached by request mode`

### After

- Keyed live registration remains: `IPullRequestReadProvider["Live"] -> LivePullRequestReadProvider`
- Keyed cached registration remains: `IPullRequestReadProvider["Cached"] -> CachedPullRequestReadProvider`
- Default registration is now direct: `IPullRequestReadProvider -> CachedPullRequestReadProvider`
- `DataSourceAwareReadProviderFactory` still exists for work item and pipeline reads that legitimately switch by request mode, but not for PR analytical reads

## Handler Changes

The analytical PR handlers still depend on `IPullRequestReadProvider`, but they no longer depend on runtime provider selection:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetAllPullRequestsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestByIdQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestIterationsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestCommentsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestFileChangesQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPRReviewBottleneckQueryHandler.cs`

They now resolve directly to the cached implementation through DI, which keeps test seams stable without retaining misleading lazy/factory indirection.

## Live Path Preservation

Live onboarding/configuration/discovery flows are preserved because:

- startup/team/repository discovery uses `ITfsClient` directly rather than the analytical PR provider path
- PR sync ingestion still uses `ITfsClient` directly in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PullRequestSyncStage.cs`
- explicit live PR resolution remains possible through the keyed `"Live"` registration of `IPullRequestReadProvider`

This cleanup does not alter the cache-only middleware guardrails or any sync/write path behavior.

## Validation

Confirmed with focused validation:

- `dotnet build PoTool.sln --configuration Release --no-restore`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~WorkspaceGuardMiddlewareTests|FullyQualifiedName~GetPullRequestMetricsQueryHandlerTests|FullyQualifiedName~GetPullRequestInsightsQueryHandlerTests|FullyQualifiedName~GetPrDeliveryInsightsQueryHandlerTests|FullyQualifiedName~PullRequestFilterResolutionServiceTests|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~DataSourceAwareReadProviderFactoryTests|FullyQualifiedName~LazyReadProviderTests" -v minimal`

Confirmed by inspection that:

- analytical PR handlers no longer rely on runtime provider selection
- no PR code path resolves through `LazyPullRequestReadProvider`
- no PR branch remains in `DataSourceAwareReadProviderFactory`
- explicit live PR provider registration still exists

## Known Limitations

- The PR handlers still depend on the shared `IPullRequestReadProvider` abstraction rather than `CachedPullRequestReadProvider` directly to keep the refactor small and avoid broad test churn
- Work item and pipeline reads still use the lazy/factory runtime-switching pattern because this cleanup intentionally targets the PR slice only
