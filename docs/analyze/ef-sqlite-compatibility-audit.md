# EF Core SQLite Compatibility Audit

## 1. Executive summary

- **Overall SQLite query risk:** **FAIL**
- **Confidence level:** **Medium-High**

This audit focused on EF Core query usage in repositories, handlers, services, sync/background stages, projection/read-model flows, and provider-backed unit tests.

The codebase already contains two strong SQLite guardrails:

- `PoTool.Api/Persistence/PoToolDbContext.cs` blocks indexed `DateTimeOffset` properties when the active provider is SQLite.
- `PoTool.Tests.Unit/Repositories/PullRequestRepositorySqliteTests.cs` documents and tests a real prior SQLite translation failure, and `PoTool.Api/Repositories/PullRequestRepository.cs` now uses the SQLite-safe `CreatedDateUtc` + `InternalId` ordering pattern for pull-request comments.

However, the audit still found a small number of concrete EF/query-path patterns that can distort SQLite-based testing confidence:

- client-side materialization used to avoid server-side `DateTimeOffset` aggregation/ordering
- cached-provider query paths that do not use the repository's existing SQLite-safe ordering pattern
- CDC-related query paths that may scale or behave differently on SQLite because work is intentionally moved client-side
- test suites for several EF-backed handlers/services that still rely on `UseInMemoryDatabase(...)`, so SQLite translation issues would not be caught there

---

## 2. Critical findings

### Finding C1 — InMemory-only tests leave key SQLite query paths unverified

- **Files / methods:**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPortfolioDeliveryQueryHandlerTests.cs` → `CreateContext()`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs` → `Setup()`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs` → multiple `UseInMemoryDatabase(...)` setups
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/BuildQualityQueryHandlerTests.cs` → `Setup()`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CachedReadProviderDiagnosticsTests.cs` → all test setups
- **Query pattern:** EF-backed handler/provider tests use `UseInMemoryDatabase(...)` instead of SQLite.
- **Why risky on SQLite:** EF InMemory does not validate SQL translation, null semantics, or provider-specific ordering behavior. That means query regressions can pass unit tests and still fail or behave differently when the application uses SQLite. This is especially important because the repository already has evidence of a prior SQLite-only failure in pull-request comment ordering.
- **Category:** test reliability risk
- **Severity:** **Critical**
- **Recommended fix direction:** **test adjustment only** — add SQLite-backed coverage for the highest-risk query paths before relying on SQLite-based test confidence.

### Finding C2 — CDC backfill start lookup materializes `DateTimeOffset` values before taking `Min()`

- **File / method:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemBackfillStartProvider.cs` → `GetEarliestChangedDateUtcAsync(...)`
- **Query pattern:**
  ```csharp
  var earliest = await context.WorkItems
      .Where(w => workItemIds.Contains(w.TfsId) && w.CreatedDate != null)
      .Select(w => w.CreatedDate)
      .ToListAsync(cancellationToken);

  return earliest.Count > 0 ? earliest.Min() : null;
  ```
- **Why risky on SQLite:** this is an explicit client-side fallback around a `DateTimeOffset` aggregate. It avoids server-side translation by materializing all candidate values and computing `Min()` in memory. On SQLite this can hide provider limitations during tests while also scaling worse than a server-side scalar aggregate. Because this value influences backfill window selection, it touches CDC/history loading behavior directly.
- **Category:** performance/materialization risk
- **Severity:** **Critical**
- **Recommended fix direction:** **store alternate scalar** or **rewrite query** — use a queryable UTC scalar for the boundary if the domain requires database-side min selection.

---

## 3. High-risk findings

### Finding H1 — Cached pull-request comment ordering still relies on client-side `DateTimeOffset` ordering

- **File / method:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs` → `GetCommentsAsync(...)`
- **Query pattern:**
  ```csharp
  var entities = await _dbContext.PullRequestComments
      .AsNoTracking()
      .Where(c => c.PullRequestId == pullRequestId)
      .ToListAsync(cancellationToken);

  return entities.OrderBy(c => c.CreatedDate).Select(MapCommentToDto);
  ```
- **Why risky on SQLite:** the repository already contains the SQLite-safe server-side pattern for the same domain query:
  `OrderBy(c => c.CreatedDateUtc).ThenBy(c => c.InternalId)`.
  The cached provider sidesteps provider translation by loading all rows first and then sorting by the `DateTimeOffset` property in memory. That has two consequences:
  1. large comment sets are materialized before ordering; and
  2. equal timestamps have no stable tie-breaker here, unlike the repository implementation.
- **Category:** semantic difference
- **Severity:** **High**
- **Recommended fix direction:** **rewrite query** — reuse the repository's SQLite-safe server-side ordering pattern.

### Finding H2 — Snapshot/source lookup coverage exists on SQLite, but not for the source-normalization query path

- **Files / methods:**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs` → `GetBySourceAsync(...)`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioSnapshotSelectionService.cs` → `GetPortfolioSnapshotBySourceAsync(...)`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs`
- **Query pattern:** both query paths filter by `(TimestampUtc, Source)` for persisted snapshot reloading / grouped snapshot selection, but the SQLite-backed tests exercise latest/previous/range behavior rather than source-normalization/lookup edge cases.
- **Why risky on SQLite:** these are CDC-critical snapshot selection paths. The general ordering logic is already deterministic and covered on SQLite, but there is no direct SQLite test covering the specific source-based lookup path used after persistence and during grouped portfolio snapshot selection. That leaves a confidence gap in one of the most important query families touched by the new snapshot feature stack.
- **Category:** test reliability risk
- **Severity:** **High**
- **Recommended fix direction:** **test adjustment only** — add SQLite-backed tests that exercise exact source/timestamp lookups, including duplicate timestamps and grouped portfolio snapshot retrieval.

### Finding H3 — Cached-provider diagnostics are only exercised against InMemory, not SQLite

- **Files / methods:**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs` → `LogEmptyResultDiagnosticsAsync(...)`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs` → `LogEmptyRunsDiagnosticsAsync(...)`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/SyncPipelineRunner.cs` → `LogPersistenceDiagnosticsAsync(...)`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CachedReadProviderDiagnosticsTests.cs`
- **Query pattern:**
  ```csharp
  .GroupBy(_ => 1)
  .Select(g => new { Min = g.Min(...Utc), Max = g.Max(...Utc) })
  .FirstOrDefaultAsync(cancellationToken)
  ```
- **Why risky on SQLite:** this aggregate shape is diagnostic-only and uses UTC `DateTime` columns, so it is not the highest-risk query in the codebase. But it is still more translation-sensitive than necessary, and the existing tests run only against InMemory, so SQLite behavior is never validated.
- **Category:** test reliability risk
- **Severity:** **High**
- **Recommended fix direction:** **test adjustment only** or **rewrite query** — either add SQLite coverage for these diagnostics or simplify them to separate scalar aggregates.

---

## 4. Medium/low findings

### Finding M1 — Diagnostics use `GroupBy(_ => 1)` instead of simpler scalar aggregates

- **Files / methods:**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs` → `LogEmptyResultDiagnosticsAsync(...)`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs` → `LogEmptyRunsDiagnosticsAsync(...)`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/SyncPipelineRunner.cs` → `LogPersistenceDiagnosticsAsync(...)`
- **Query pattern:** grouped constant-key aggregate to compute min/max date range.
- **Why risky on SQLite:** SQLite can usually handle this, and these queries are not on the hot business path. The risk is mainly maintainability and provider sensitivity: if translation changes, an empty-result diagnostic path starts throwing instead of logging. A pair of scalar aggregates would be simpler and less provider-shaped.
- **Category:** translation failure
- **Severity:** **Medium**
- **Recommended fix direction:** **rewrite query**

### Finding M2 — Pull-request comment ordering safety is inconsistent across data-access paths

- **Files / methods:**
  - Safe path: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/PullRequestRepository.cs` → `GetCommentsAsync(...)`
  - Risky path: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs` → `GetCommentsAsync(...)`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Repositories/PullRequestRepositorySqliteTests.cs`
- **Query pattern:** one path uses SQLite-safe `CreatedDateUtc` + `InternalId`; another path materializes first and orders by `CreatedDate` in memory.
- **Why risky on SQLite:** the repository already codifies the provider-safe pattern because `DateTimeOffset` ordering previously failed translation on SQLite. Keeping two different strategies for the same conceptual query increases regression risk and makes test outcomes dependent on which access path a feature happens to use.
- **Category:** semantic difference
- **Severity:** **Medium**
- **Recommended fix direction:** **rewrite query**

---

## 5. CDC-critical path findings

### Snapshot selection / persistence lookup

- **Observed guardrail:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioSnapshotSelectionService.cs` consistently orders by `TimestampUtc` and `SnapshotId`, and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs` validates deterministic latest/previous selection on SQLite.
- **Risk that remains:** source-based snapshot lookup (`GetBySourceAsync`, `GetPortfolioSnapshotBySourceAsync`) does not have explicit SQLite test coverage for the exact lookup path.

### Snapshot persistence reload

- **Observed pattern:** persistence immediately reloads the snapshot by `(productId, source, timestamp)` after save.
- **Risk that remains:** because the reload path is CDC-critical, lack of direct SQLite test coverage for this exact path weakens confidence even though nearby snapshot-ordering tests already run on SQLite.

### Latest/previous snapshot retrieval

- **Observed guardrail:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioSnapshotSelectionService.cs` uses stable ordering with `ThenByDescending(snapshot => snapshot.SnapshotId)`.
- **Assessment:** no concrete SQLite issue found here.

### Trend analysis / decision signals

- **Observed guardrail:** `PortfolioTrendAnalysisService` and `GetPipelineInsightsQueryHandler` mostly operate on materialized DTO/domain data rather than pushing risky grouped/date-offset logic into EF.
- **Assessment:** no direct SQLite translation failure found in those in-memory analysis steps.

### Validation queues / delivery rollups / planning quality

- **Observed risk:** handler/service tests for several of these query paths still use InMemory rather than SQLite (`GetPortfolioDeliveryQueryHandlerTests`, `GetSprintTrendMetricsQueryHandlerTests`, `GetPipelineInsightsQueryHandlerTests`, `BuildQualityQueryHandlerTests`).
- **Assessment:** the main risk is misleading test confidence, not an already-identified runtime translation failure.

### Historical backfill boundary selection

- **Observed risk:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemBackfillStartProvider.cs` computes its boundary by materializing `DateTimeOffset` values and taking `Min()` in memory.
- **Assessment:** this is the most important CDC-path query concern found in production code.

---

## 6. Test reliability findings

### Existing positive SQLite coverage

The following tests are good provider-specific guardrails already present in the repo:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Repositories/PullRequestRepositorySqliteTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/TfsConfigurationServiceSqliteTests.cs`

These materially improve confidence for some high-value paths.

### Misleading or incomplete coverage

1. **Handler query tests using InMemory only**
   - Examples:
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPortfolioDeliveryQueryHandlerTests.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/BuildQualityQueryHandlerTests.cs`
   - **Risk:** these tests validate business outcomes, but not SQLite translation or query-shape behavior.

2. **Cached read-provider diagnostics tested only with InMemory**
   - Example: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CachedReadProviderDiagnosticsTests.cs`
   - **Risk:** none of the diagnostic aggregate queries are exercised on the actual default provider.

3. **No direct SQLite test for the cached-provider comment ordering path**
   - Risk path: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs` → `GetCommentsAsync(...)`
   - Existing nearby evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Repositories/PullRequestRepositorySqliteTests.cs` proves this domain already had a SQLite-only ordering translation problem.

4. **No direct SQLite test for work-item backfill minimum-boundary lookup**
   - Risk path: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemBackfillStartProvider.cs`
   - **Risk:** the current implementation intentionally materializes before aggregation, so tests do not demonstrate how the boundary would behave if the query were refactored or scaled.

---

## 7. Prioritized remediation list

1. **Add SQLite-backed tests for the currently InMemory-only critical query handlers/providers**
   - Start with `GetPortfolioDeliveryQueryHandlerTests`, `GetSprintTrendMetricsQueryHandlerTests`, `GetPipelineInsightsQueryHandlerTests`, `BuildQualityQueryHandlerTests`, and `CachedReadProviderDiagnosticsTests`.
   - **Why first:** biggest impact on testing confidence with the smallest semantic change.

2. **Refactor `CachedPullRequestReadProvider.GetCommentsAsync(...)` to match the repository's SQLite-safe ordering**
   - Target pattern: server-side `OrderBy(c => c.CreatedDateUtc).ThenBy(c => c.InternalId)`.
   - **Why second:** directly aligns a risky path with an already-proven safe pattern.

3. **Refactor `WorkItemBackfillStartProvider.GetEarliestChangedDateUtcAsync(...)` away from client-side `DateTimeOffset` aggregation**
   - Options:
     - add/query an alternate UTC scalar and aggregate server-side; or
     - explicitly split a narrow server/client boundary with documented intent.
   - **Why third:** CDC-critical path with both performance and provider-confidence implications.

4. **Simplify diagnostic min/max queries that currently use `GroupBy(_ => 1)`**
   - **Why fourth:** lower user-facing risk, but easy hardening once SQLite coverage exists.

5. **Add source-based SQLite tests for portfolio snapshot lookup/reload paths**
   - Specifically exercise `GetBySourceAsync(...)` and `GetPortfolioSnapshotBySourceAsync(...)`.
   - **Why fifth:** these are already close to safe, but still deserve direct provider-backed proof because they sit on the snapshot lifecycle.
