# Pull Request Persistence Abstraction Validation

> **Historical note:** This document reflects the architecture state **before** the analytical read-path consolidation implemented afterward. For the current steady-state architecture, see `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/pull-request-analytical-read-consolidation.md`.

## Summary

**Overall validation result: Partially correct**

The pull-request persistence abstraction was implemented for the handlers that previously performed direct EF/Core cache reads through `PoToolDbContext`, and those handlers are now cleanly decoupled from the DbContext through `IPullRequestQueryStore` and `EfPullRequestQueryStore`.

However, the implementation is only **partially** correct against the broader validation goal because `EfPullRequestQueryStore` is **not** the only remaining home for pull-request analytical EF queries. `CachedPullRequestReadProvider` still contains EF/Core cache query logic that is used by several pull-request analytical handlers. That means the refactor successfully removed direct handler-level persistence coupling for the targeted handlers, but it did **not** consolidate the full pull-request analytical query surface into a single persistence abstraction.

---

## Abstraction Detection

### Status
**Implemented, but only partially centralizing the pull-request analytical read surface.**

### What exists
- `IPullRequestQueryStore` exists at:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/IPullRequestQueryStore.cs`
- `EfPullRequestQueryStore` exists at:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/EfPullRequestQueryStore.cs`

### Interface surface
`IPullRequestQueryStore` exposes exactly four methods:
- `GetInsightsDataAsync(PullRequestEffectiveFilter, CancellationToken)`
- `GetDeliveryInsightsDataAsync(PullRequestEffectiveFilter, CancellationToken)`
- `GetSprintTrendDataAsync(PullRequestEffectiveFilter, CancellationToken)`
- `GetByWorkItemIdAsync(int, CancellationToken)`

### Assessment
- **Minimal method surface:** yes. The interface methods are usage-shaped around current consumers and do not obviously anticipate future slices.
- **Some shape bloat in support types:** yes. The same file also defines several transport records (`PullRequestInsightsQueryData`, `PrDeliveryInsightsQueryData`, `PrSprintTrendQueryData`, etc.). That is not necessarily wrong, but it does make the abstraction file heavier than a strictly minimal interface-only contract.

### Evidence
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/IPullRequestQueryStore.cs:6-83`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/EfPullRequestQueryStore.cs:11-302`

---

## Handler Inventory

Below is the current pull-request handler inventory, classified by dependency shape.

### Uses `IPullRequestQueryStore`

1. **File:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs`
   - **Class:** `GetPullRequestInsightsQueryHandler`
   - **Constructor dependency shape:** `GetPullRequestInsightsQueryHandler(IPullRequestQueryStore queryStore, ILogger<...> logger)`
   - **Method:** `Handle`
   - **Assessment:** refactored to query store

2. **File:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs`
   - **Class:** `GetPrDeliveryInsightsQueryHandler`
   - **Constructor dependency shape:** `GetPrDeliveryInsightsQueryHandler(IPullRequestQueryStore queryStore, ILogger<...> logger)`
   - **Method:** `Handle`
   - **Assessment:** refactored to query store

3. **File:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs`
   - **Class:** `GetPrSprintTrendsQueryHandler`
   - **Constructor dependency shape:** `GetPrSprintTrendsQueryHandler(IPullRequestQueryStore queryStore, ILogger<...> logger)`
   - **Method:** `Handle`
   - **Assessment:** refactored to query store

4. **File:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestsByWorkItemIdQueryHandler.cs`
   - **Class:** `GetPullRequestsByWorkItemIdQueryHandler`
   - **Constructor dependency shape:** `GetPullRequestsByWorkItemIdQueryHandler(IPullRequestQueryStore queryStore)`
   - **Method:** `Handle`
   - **Assessment:** refactored to query store

### Uses `IPullRequestReadProvider`

These handlers do **not** use `PoToolDbContext` directly. They remain on the existing provider abstraction.

1. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetAllPullRequestsQueryHandler.cs`
   - `GetAllPullRequestsQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

2. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestByIdQueryHandler.cs`
   - `GetPullRequestByIdQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

3. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs`
   - `GetFilteredPullRequestsQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

4. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs`
   - `GetPullRequestMetricsQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

5. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestIterationsQueryHandler.cs`
   - `GetPullRequestIterationsQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

6. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestCommentsQueryHandler.cs`
   - `GetPullRequestCommentsQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

7. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestFileChangesQueryHandler.cs`
   - `GetPullRequestFileChangesQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

8. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPRReviewBottleneckQueryHandler.cs`
   - `GetPRReviewBottleneckQueryHandler(IPullRequestReadProvider, ILogger<...>)`
   - `Handle`
   - **Classification:** unaffected / out of this slice

### Still uses `PoToolDbContext` directly
- **No current pull-request handlers** under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests` directly inject `PoToolDbContext`.

### Uses both `IPullRequestQueryStore` and `PoToolDbContext`
- None found.

### Evidence
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs:26-54`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:34-80`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs:29-67`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestsByWorkItemIdQueryHandler.cs:11-25`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetAllPullRequestsQueryHandler.cs:13-32`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestByIdQueryHandler.cs:13-32`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs:13-42`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs:14-109`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestIterationsQueryHandler.cs:13-32`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestCommentsQueryHandler.cs:13-32`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestFileChangesQueryHandler.cs:13-32`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPRReviewBottleneckQueryHandler.cs:14-151`

---

## DbContext Leakage

### Result
**Handlers that were directly refactored are now clean.**

### What was checked
Searched the pull-request handler slice for:
- direct `PoToolDbContext`
- direct DbSet access
- EF query composition in handlers

### Findings
- No handler in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests` currently injects `PoToolDbContext`.
- The four refactored handlers now depend on `IPullRequestQueryStore` only.
- Remaining LINQ inside those handlers is in-memory transformation over store-returned DTOs, not EF query composition.

### Important nuance
DbContext leakage **does still exist in the broader pull-request analytical path** via:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs`

That is **not handler leakage**, but it does mean the pull-request analytical slice as a whole still has two persistence access paths:
- `EfPullRequestQueryStore`
- `CachedPullRequestReadProvider`

### Evidence
- No `PoToolDbContext` references found under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs:14-260`

---

## Duplication Check

### Result
**Mixed outcome: handler-level query duplication was removed, but repository-level PR EF query duplication still exists.**

### Cleanly moved
The direct EF queries previously embedded in these handlers appear to have been moved out cleanly:
- `GetPullRequestInsightsQueryHandler`
- `GetPrDeliveryInsightsQueryHandler`
- `GetPrSprintTrendsQueryHandler`
- `GetPullRequestsByWorkItemIdQueryHandler`

Those handlers no longer retain old EF query code.

### Duplication that remains
There is still overlap between:
- `EfPullRequestQueryStore`
- `CachedPullRequestReadProvider`

Examples of overlap:
- PR projection from `PullRequestEntity` to `PullRequestDto`
- PR lookup by repository/date ranges
- iteration/comment/file-change loading from PR-related DbSets

This means the refactor did **not** produce a single consolidated PR cache-read location across the entire analytical surface.

There is also a small duplication introduced in:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemResolutionService.cs`

The new overload of `ResolveAncestry` for `PullRequestWorkItemNode` repeats the same traversal logic already present for `WorkItemEntity`, rather than extracting a shared generic helper.

### Assessment
- **Old handler EF logic left behind:** no
- **Broader PR analytical query duplication still present elsewhere:** yes
- **Verdict:** partial duplication remains

---

## Behavioral Parity Assessment

### Confidence level
**Moderate to strong confidence, but not absolute proof of parity.**

### Strong evidence of parity
1. **The refactored handlers preserve their logging in the handlers themselves**
   - `GetPullRequestInsightsQueryHandler`
   - `GetPrDeliveryInsightsQueryHandler`
   - `GetPrSprintTrendsQueryHandler`
   - `GetPullRequestsByWorkItemIdQueryHandler`

2. **The new store appears to preserve important query characteristics**
   - filtering via `PullRequestFiltering.ApplyScope(...)`
   - `GetByWorkItemIdAsync` ordering preserved as:
     - `OrderByDescending(CreatedDateUtc)`
     - `ThenByDescending(Id)`
   - DTO projection shape preserved
   - empty-list guards remain present in store methods for count lookups and linked-item loading

3. **Focused tests pass against the new wiring**
   - PR insights handler tests
   - PR delivery insights handler tests
   - PR sprint trends handler tests
   - PRs-by-work-item handler tests
   - DI wiring tests

### Risks / caveats
1. **No explicit pre/post parity test exists**
   - There is no test that compares old query results versus new store results for the same seeded database.

2. **Store composition is not a pure mechanical extraction in every case**
   - The store introduces new carrier types (`PullRequestInsightsQueryData`, `PrDeliveryInsightsQueryData`, `PrSprintTrendQueryData`) and shared helper methods.
   - This is reasonable, but it means behavior preservation depends on correct reshaping rather than just moving code verbatim.

3. **Not all PR analytical reads were unified**
   - Some analytical handlers still go through `IPullRequestReadProvider`/`CachedPullRequestReadProvider`, so parity validation is fragmented across two abstractions.

### Specific parity observations
- **Filtering:** appears preserved for the refactored handlers because `PullRequestFiltering.ApplyScope(...)` is still used in the store.
- **Ordering:** explicitly preserved for `GetByWorkItemIdAsync`; other refactored queries mostly materialize then process in-memory similar to previous handler logic.
- **Joins / related-data shaping:** preserved in intent, but now composed through store-returned dictionaries/lists rather than direct handler EF queries.
- **Null / empty handling:** still present and generally explicit.
- **Logging:** preserved in handlers; no new logging pattern introduced in the store.

### Verdict
Behavior looks preserved for the targeted refactor, but the evidence is **confidence-building**, not definitive proof.

---

## Forbidden Coupling Check

### Result
**No forbidden new live/TFS/provider-mode coupling found in the new store.**

### Checked against
- `TfsAccessGateway`
- `ITfsClient`
- provider mode
- live providers
- sync logic

### Findings
`EfPullRequestQueryStore` references:
- `PoToolDbContext`
- PR/WorkItem persistence entities
- `PullRequestFiltering`
- `WorkItemType`
- shared PR DTOs

It does **not** reference:
- `TfsAccessGateway`
- `ITfsClient`
- `LivePullRequestReadProvider`
- `DataSourceModeProvider`
- sync stages

### Evidence
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/EfPullRequestQueryStore.cs:1-302`

### Verdict
No accidental live/TFS/provider-mode coupling was introduced in the new persistence abstraction.

---

## DI Validation

### Result
**Correct and deterministic.**

### Findings
DI registration exists:
- `services.AddScoped<IPullRequestQueryStore, EfPullRequestQueryStore>();`

Characteristics:
- scoped lifetime: **yes**
- single implementation: **yes**
- conditional wiring: **no evidence found**
- competing registrations: **none found for `IPullRequestQueryStore`**

### Important nuance
The repository still also registers:
- `IPullRequestReadProvider -> CachedPullRequestReadProvider`
- keyed live/cached `IPullRequestReadProvider` registrations

That does not conflict with `IPullRequestQueryStore`, but it reinforces that the PR analytical read surface remains split across two abstractions.

### Evidence
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:206-230`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs:263-296`

---

## Test Coverage

### Result
**Partial, but meaningful.**

### What exists
Updated / added tests validate handlers through the new store seam:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPullRequestInsightsQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPrDeliveryInsightsQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPrSprintTrendsQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPullRequestsByWorkItemIdQueryHandlerTests.cs`

DI wiring is validated in:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`

### What is missing
- No dedicated unit tests for `EfPullRequestQueryStore` in isolation
- No parity/regression tests that compare pre-refactor and post-refactor outputs for the same seeded data
- No explicit tests asserting that handlers no longer reference `PoToolDbContext` (this was verified by inspection/search instead)

### Validation commands run
After `dotnet restore`, the following succeeded:
- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~GetPullRequestInsightsQueryHandlerTests|FullyQualifiedName~GetPrDeliveryInsightsQueryHandlerTests|FullyQualifiedName~GetPrSprintTrendsQueryHandlerTests|FullyQualifiedName~GetPullRequestsByWorkItemIdQueryHandlerTests|FullyQualifiedName~ServiceCollectionTests" -v minimal`

### Verdict
- **Handler behavior confidence:** good
- **Store-specific confidence:** moderate
- **Overall coverage rating:** partial

---

## Conclusion

**Verdict: Partially correct.**

The refactor succeeded at its narrowest objective: the handlers that previously performed direct pull-request EF reads through `PoToolDbContext` now use `IPullRequestQueryStore`, and no forbidden TFS/live coupling was introduced. DI wiring is correct, focused tests pass, and handler-level persistence leakage was removed.

The most important limitation is that the pull-request analytical query surface is still split. `CachedPullRequestReadProvider` continues to host PR-related EF queries used by other analytical handlers, so `EfPullRequestQueryStore` is **not** the single place where PR analytical EF reads live.

### Single most important next step
If the architectural goal is a **complete** pull-request analytical persistence abstraction, the next step should be to decide whether the remaining analytical handlers currently using `IPullRequestReadProvider` should also be migrated onto `IPullRequestQueryStore` (or whether the provider/store split should be explicitly documented as the intended steady state).
