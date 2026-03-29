# Pull Request Analytical Read Consolidation

## Summary

The pull-request analytical read path was consolidated so that `IPullRequestQueryStore` is now the single analytical persistence abstraction for the PR slice.

Before this change, the analytical read surface was split:
- some analytical handlers used `IPullRequestQueryStore`
- other analytical handlers still used `IPullRequestReadProvider`
- `CachedPullRequestReadProvider` still contained multi-PR analytical EF query logic

After this change:
- all multi-PR analytical handlers use `IPullRequestQueryStore`
- `EfPullRequestQueryStore` owns the cached analytical EF query composition for the PR slice
- `CachedPullRequestReadProvider` is reduced to generic provider-shaped reads only
- the provider/store boundary is explicit in code comments and DI comments

This eliminates the previous split architecture and makes the steady-state rule clear.

---

## Handler Migration Inventory

### Migrated to `IPullRequestQueryStore`

1. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs`**
   - **Class:** `GetFilteredPullRequestsQueryHandler`
   - **Why analytical:** repository-scoped filtered PR list for the analytical filter surface
   - **Migration decision:** moved to query store
   - **Current dependency shape:** `GetFilteredPullRequestsQueryHandler(IPullRequestQueryStore, ILogger<...>)`

2. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs`**
   - **Class:** `GetPullRequestMetricsQueryHandler`
   - **Why analytical:** aggregates multi-PR metrics and enriches results with iterations, comments, and file changes
   - **Migration decision:** moved to query store
   - **Current dependency shape:** `GetPullRequestMetricsQueryHandler(IPullRequestQueryStore, ILogger<...>)`

3. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPRReviewBottleneckQueryHandler.cs`**
   - **Class:** `GetPRReviewBottleneckQueryHandler`
   - **Why analytical:** computes reviewer/bottleneck summary over a recent PR cohort
   - **Migration decision:** moved to query store
   - **Current dependency shape:** `GetPRReviewBottleneckQueryHandler(IPullRequestQueryStore, ILogger<...>)`

### Already on `IPullRequestQueryStore`

1. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs`**
2. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs`**
3. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs`**
4. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestsByWorkItemIdQueryHandler.cs`**

### Intentionally still provider-based

These remain on `IPullRequestReadProvider` because they are generic/single-PR read operations rather than multi-PR analytical composition:

1. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetAllPullRequestsQueryHandler.cs`**
   - generic cache/live provider-shaped list retrieval

2. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestByIdQueryHandler.cs`**
   - single-PR generic retrieval

3. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestIterationsQueryHandler.cs`**
   - single-PR detail retrieval

4. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestCommentsQueryHandler.cs`**
   - single-PR detail retrieval

5. **`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestFileChangesQueryHandler.cs`**
   - single-PR detail retrieval

### Result
All remaining **analytical multi-PR handlers** in the PR slice now depend on `IPullRequestQueryStore`.

---

## Query Store Changes

### Interface expansion
`IPullRequestQueryStore` was expanded only for the migrated analytical use cases:

Added methods:
- `GetScopedPullRequestsAsync(PullRequestEffectiveFilter, CancellationToken)`
- `GetMetricsDataAsync(PullRequestEffectiveFilter, CancellationToken)`
- `GetReviewBottleneckPullRequestsAsync(DateTime cutoffDateUtc, int maxPullRequests, CancellationToken)`

Added data shape:
- `PullRequestMetricsQueryData`

### Why these additions are acceptable
- `GetScopedPullRequestsAsync(...)` supports the analytical filtered list handler without pushing EF into the handler.
- `GetMetricsDataAsync(...)` owns the multi-table cached composition needed for PR metrics.
- `GetReviewBottleneckPullRequestsAsync(...)` supports the recent-PR cohort used by review bottleneck analysis.

The interface was not expanded for speculative future cases.

### Store implementation changes
`EfPullRequestQueryStore` now additionally owns:
- repository/date-scoped analytical PR retrieval for filtered analytics
- grouped iteration/comment/file-change analytical enrichment for metrics
- recent PR cohort retrieval for review bottleneck analysis

The implementation materializes all EF results inside the store and returns DTOs / dictionaries rather than leaking `IQueryable` across the boundary.

---

## Provider Simplification

`CachedPullRequestReadProvider` no longer contains the duplicated multi-PR analytical EF methods that previously overlapped with the query store.

### Removed from provider path
- repository-scoped multi-PR analytical retrieval
- batched iteration lookup for multiple PRs
- batched comment lookup for multiple PRs
- batched file-change lookup for multiple PRs

### Kept in provider path on purpose
- `GetAllAsync(...)`
- `GetByProductIdsAsync(...)`
- `GetByIdAsync(...)`
- `GetIterationsAsync(...)`
- `GetCommentsAsync(...)`
- `GetFileChangesAsync(...)`
- repository-name single-PR overloads for the live provider

These remaining methods are provider-shaped generic reads or single-PR detail reads, not analytical multi-PR composition.

The same analytical method removals were applied to:
- `IPullRequestReadProvider`
- `LivePullRequestReadProvider`
- `CachedPullRequestReadProvider`

That keeps the provider contract aligned with the new architecture instead of leaving obsolete analytical APIs behind.

---

## Boundary Rule

### Final rule

#### `IPullRequestQueryStore`
Owns:
- analytical cached reads
- multi-PR analytical composition
- repository/date-scoped PR analytical retrieval
- analytical enrichment across PRs, iterations, comments, and file changes
- insights / delivery / trends / metrics / review-bottleneck style facts

#### `IPullRequestReadProvider`
Owns:
- generic pull request retrieval
- single-PR cache/live reads
- provider-shaped detail retrieval
- intentionally non-analytical cache/live access patterns

### Why this is now explicit
The rule is visible in:
- handler constructor dependencies
- `IPullRequestQueryStore` / `EfPullRequestQueryStore`
- reduced `IPullRequestReadProvider`
- `CachedPullRequestReadProvider` summary comment
- DI registration comments in `ApiServiceCollectionExtensions`

---

## Validation

### Build / test validation run
The following succeeded:

- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~GetFilteredPullRequestsQueryHandlerTests|FullyQualifiedName~GetPRReviewBottleneckQueryHandlerTests|FullyQualifiedName~GetPullRequestMetricsQueryHandlerTests|FullyQualifiedName~CachedPullRequestReadProviderSqliteTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~GetPullRequestInsightsQueryHandlerTests|FullyQualifiedName~GetPrDeliveryInsightsQueryHandlerTests|FullyQualifiedName~GetPrSprintTrendsQueryHandlerTests|FullyQualifiedName~GetPullRequestsByWorkItemIdQueryHandlerTests" -v minimal`

### Coverage added / adjusted
- updated `GetPullRequestMetricsQueryHandlerTests` to validate the query-store seam instead of the provider seam
- added `GetFilteredPullRequestsQueryHandlerTests`
- added `GetPRReviewBottleneckQueryHandlerTests`
- updated `CachedPullRequestReadProviderSqliteTests` to validate grouped analytical ordering through `EfPullRequestQueryStore.GetMetricsDataAsync(...)`
- existing query-store-backed PR analytical handler tests still pass
- DI/service registration tests still pass

### Additional validation results
- no PR analytical handler now injects `PoToolDbContext`
- no PR analytical handler composes EF directly
- no migrated analytical handler mixes query store and provider
- no new TFS/live/provider-mode coupling was introduced into the query store path

---

## Known Limitations

1. `GetAllPullRequestsQueryHandler` still uses `IPullRequestReadProvider`
   - intentional: generic provider-shaped list retrieval

2. single-PR detail handlers still use `IPullRequestReadProvider`
   - intentional: these are not multi-PR analytical composition handlers

3. The live provider still supports the remaining single-PR/generic provider methods
   - intentional: this preserves existing non-analytical provider behavior without reintroducing provider-as-primary for analytics

4. This change does not update unrelated historical documentation files that describe the previous split architecture
   - the new report in this file documents the current intended steady state

---

## Final Status

**The PR analytical read path is now fully consolidated for the analytical slice.**

There is now exactly one analytical persistence abstraction for pull-request analytics:
- `IPullRequestQueryStore` / `EfPullRequestQueryStore`

`CachedPullRequestReadProvider` no longer contains the duplicated multi-PR analytical EF query logic that previously split the architecture.
