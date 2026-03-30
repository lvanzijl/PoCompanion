# PR Batching Verification

## Summary

**Implemented, but only partially successful overall.**

Batch methods now exist on `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/IPullRequestReadProvider.cs`, and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs` uses them instead of calling per-PR enrichment methods directly.

That means the handler-level fan-out is gone.

However, the end-to-end result differs by provider:

- **Cached provider:** batching is implemented correctly and reduces the shape to **1 PR list query + 3 batched enrichment queries**.
- **Live provider:** batching is only a **provider-surface facade**. Under the hood it still performs **per-PR TFS calls**, and file-change loading re-fetches iterations, so the N+1/fan-out is **not fully removed** in live mode.

Overall conclusion: **partial success**.

---

## Query Pattern

### Handler shape

`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs` now does:

1. `GetByRepositoryNamesAsync(...)`
2. `GetIterationsForPullRequestsAsync(...)`
3. `GetCommentsForPullRequestsAsync(...)`
4. `GetFileChangesForPullRequestsAsync(...)`

Inside the final metrics loop it only reads from the returned dictionaries.
It no longer calls:

- `GetIterationsAsync(prId, ...)`
- `GetCommentsAsync(prId, ...)`
- `GetFileChangesAsync(prId, ...)`

So the **handler/provider-call shape** is now:

- **1 PR list call + 3 batch calls**

### Actual execution shape by provider

#### Cached provider

Actual DB shape is:

- **1 PR list query**
- **1 batched iterations query**
- **1 batched comments query**
- **1 batched file-changes query**

So cached mode is now:

- **1 + 3**

#### Live provider

Actual TFS shape is still hybrid/per-PR:

- `GetByRepositoryNamesAsync(...)` still loops repository names and calls TFS once per repository
- `GetIterationsForPullRequestsAsync(...)` performs **1 TFS iteration call per PR**
- `GetCommentsForPullRequestsAsync(...)` performs **1 TFS comment call per PR**
- `GetFileChangesForPullRequestsAsync(...)` first calls `GetIterationsForPullRequestsAsync(...)` again, then performs **1 TFS file-change call per PR**

So live mode is effectively:

- **repository-count PR-list calls**
- **2 × PR-count iteration calls**
- **1 × PR-count comment calls**
- **1 × PR-count file-change calls**

Relative to PR count, the live enrichment shape is still:

- **4 × PR count** remote calls under the hood

That means the original N+1/fan-out is **not removed end-to-end for live mode**.

---

## Cached Provider

**Correct.**

`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs` implements all three batch methods and uses the expected pattern:

- normalize/distinct PR ids once
- run a **single EF query per dataset**
- filter with `Contains(...)`
- order in SQL
- materialize with `ToListAsync(...)`
- group in memory with `GroupBy(...)`

Verified characteristics:

- `GetIterationsForPullRequestsAsync(...)`
  - `Where(iteration => normalizedPullRequestIds.Contains(iteration.PullRequestId))`
  - ordered by `PullRequestId`, then `IterationNumber`
- `GetCommentsForPullRequestsAsync(...)`
  - `Where(comment => normalizedPullRequestIds.Contains(comment.PullRequestId))`
  - ordered by `PullRequestId`, then `CreatedDateUtc`, then `InternalId`
- `GetFileChangesForPullRequestsAsync(...)`
  - `Where(fileChange => normalizedPullRequestIds.Contains(fileChange.PullRequestId))`
  - ordered by `PullRequestId`, then `IterationId`, then `FilePath`

Important observations:

- There are **no accidental per-PR EF queries** inside the batch methods.
- The only loop is the final in-memory `GroupBy(...)` after materialization.
- No hidden helper reintroduces per-PR database lookups.

This is the intended batched implementation.

---

## Live Provider

**Hybrid only; not truly batched.**

`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LivePullRequestReadProvider.cs` now exposes the batch methods, but they are not backed by bulk TFS APIs.

What they actually do:

- resolve repository names once from the handler-provided PR-id map when available
- use `Task.WhenAll(...)` over the requested PR ids
- call the existing per-PR TFS client methods for each PR

That means:

- `GetIterationsForPullRequestsAsync(...)` = **per-PR TFS iteration calls**
- `GetCommentsForPullRequestsAsync(...)` = **per-PR TFS comment calls**
- `GetFileChangesForPullRequestsAsync(...)` = **per-PR TFS file-change calls**, but only after calling the iteration batch method again to find each latest iteration

So the live provider is:

- **batch-shaped at the interface level**
- **still per-PR underneath**
- **not a regression from the prior live behavior**, but also **not the intended N+1 removal**

One positive detail:

- because the handler passes `repositoryNamesByPullRequestId`, `ResolveRepositoryNamesAsync(...)` usually avoids calling `GetByIdAsync(...)` per PR, so it does not add another extra lookup fan-out in the normal handler path.

One negative detail:

- file-change batching duplicates iteration retrieval, so live mode still pays for iteration calls twice.

---

## Functional Parity

**Maintained.**

The metrics calculations in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs` are unchanged in substance:

- `TotalTimeOpen` calculation is unchanged
- `EffectiveWorkTime` logic is unchanged apart from accepting `IReadOnlyList<PullRequestIterationDto>` instead of `List<PullRequestIterationDto>`
- `IterationCount`, `CommentCount`, `UnresolvedCommentCount`, `TotalFileCount`, `TotalLinesAdded`, `TotalLinesDeleted`, and `AverageLinesPerFile` are computed the same way

Behavioral parity checks:

- Missing batch entries fall back to `Array.Empty<...>()`, which matches prior behavior where per-PR methods could return empty sequences.
- The handler still emits one metric DTO per PR in `allPrs`.
- File totals still deduplicate by `FilePath` exactly as before.

Test evidence supports parity:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPullRequestMetricsQueryHandlerTests.cs` verifies the handler now uses the batch methods and no longer fans out to the per-PR enrichment methods.
- Existing PR metrics and related focused tests passed locally after verification.

No missing-data or duplication regression is evident from the implementation reviewed here.

---

## SQLite Safety

**Safe.**

The cached batch queries are SQLite-friendly because they use only simple EF-translatable patterns:

- `AsNoTracking()`
- `Where(...Contains(...))`
- scalar `OrderBy` / `ThenBy`
- `ToListAsync(...)`
- in-memory grouping after materialization

There are no new risks from:

- joins
- client-eval inside the EF query
- `DateTimeOffset` predicate ordering on SQLite
- returning `IQueryable` across boundaries

Additional evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CachedPullRequestReadProviderSqliteTests.cs` includes:
  - ordering verification for comments using `CreatedDateUtc` and stable tie-breaks
  - batch-method verification that grouping and ordering work under SQLite

So the cached batching path is SQLite-safe.

---

## Conclusion

**partial**

- **Handler implementation:** success
- **Interface extension:** success
- **Cached provider batching:** success
- **Live provider batching:** partial only
- **Functional parity:** maintained
- **SQLite safety:** safe

Final assessment:

- **N+1 is fully removed for cached mode.**
- **N+1 is not fully removed for live mode.**
- Therefore the overall verification result is **partial**, not full success.

## Verification notes

Local verification performed successfully with:

- `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~GetPullRequestMetricsQueryHandlerTests|FullyQualifiedName~CachedPullRequestReadProviderSqliteTests|FullyQualifiedName~GetPullRequestInsightsQueryHandlerTests|FullyQualifiedName~GetPrDeliveryInsightsQueryHandlerTests|FullyQualifiedName~PullRequestFilterResolutionServiceTests|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal`

Focused local result:

- **Build succeeded**
- **68 targeted tests passed**

Recent GitHub Actions inspection found a failed workflow run (`23695490187`), but MCP log retrieval for its failed job returned `HTTP 404`, so no reliable failure-cause analysis was available from the logs artifact.
