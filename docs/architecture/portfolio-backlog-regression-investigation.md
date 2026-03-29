# Portfolio & Backlog Regression Investigation

## Summary

I re-verified the current codebase and did **not** trust the earlier investigation.

Current local verification shows a split result:

- The **named portfolio-flow / sprint projection tests currently pass**.
- The **current reproducible zero-value failures are the five multi-product backlog-health tests** in `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs`.

The current likely root causes are therefore **not a single shared regression**:

1. **Portfolio flow:** there is still a **real product regression in projection input selection/data filtering**. Raw work item types are persisted unchanged, but `PortfolioFlowProjectionService` filters candidate PBIs using a canonical-only predicate. That is the first point where expected portfolio data can disappear in production.
2. **Backlog health multi-product:** the current failing tests are **stale after loader/window refactors**. They do not configure the batch product lookup that `SprintScopedWorkItemLoader` now uses, so no work items are loaded and analysis never runs. Several assertions also assume the first returned slot is `Sprint 1`, which is no longer true for the current chronological issue-comparison window.

I also checked recent GitHub Actions runs. The latest completed failed run was an unrelated Copilot workflow on another branch, and job log download returned 404, so this report relies primarily on current source inspection and local focused test execution.

### Local verification performed

```text
cd /home/runner/work/PoCompanion/PoCompanion && \
  dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj \
    --configuration Release --no-build \
    --filter "FullyQualifiedName~PortfolioFlowProjectionServiceTests|FullyQualifiedName~SprintTrendProjectionServiceSqliteTests|FullyQualifiedName~GetPortfolioProgressTrendQueryHandlerTests|FullyQualifiedName~GetPortfolioDeliveryQueryHandlerTests|FullyQualifiedName~GetMultiIterationBacklogHealthQueryHandlerMultiProductTests|FullyQualifiedName~GetBacklogHealthQueryHandlerTests|FullyQualifiedName~SprintScopedWorkItemLoaderTests" -v minimal
```

Observed result:

- **30 total**
- **25 passed**
- **5 failed**
- All 5 failures were in `GetMultiIterationBacklogHealthQueryHandlerMultiProductTests`

---

## Portfolio Flow Investigation

### Verified status of the named portfolio/sprint cluster

The following named tests currently pass in the focused run:

- `ComputeProductSprintProjection_UsesHistoricalEstimateAtFirstDone_AndSprintEndEstimateForStock`
- `ComputeProductSprintProjection_ComputesCompletionPercentFromStockAndRemainingScope`
- `ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionInTheSprintPipeline`

That means the previously reported failing portfolio test cluster is **not reproducible as-is in the current tree**.

### Why the named portfolio tests pass now

The current portfolio-flow tests seed **canonical** PBI types directly:

- `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs:253-260`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs:204-208`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs:218-223`

Those fixtures use `CanonicalWorkItemTypes.Pbi`, which matches the current portfolio-flow filter.

### Current production data path

#### Source data selection

1. TFS/raw work items are retrieved with their raw type names.
   - `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs:361-385`
2. Sync persists the raw type unchanged into `WorkItemEntity.Type`.
   - `PoTool.Api/Services/Sync/WorkItemSyncStage.cs:170-205`
3. `SprintTrendProjectionSyncStage` computes sprint projections during sync.
   - `PoTool.Api/Services/Sync/SprintTrendProjectionSyncStage.cs:41-67`
4. `SprintTrendProjectionService.ComputeProjectionsAsync(...)` optionally calls portfolio-flow recomputation after sprint metrics are saved.
   - `PoTool.Api/Services/SprintTrendProjectionService.cs:267-272`

So portfolio-flow rows are **not never-computed**; they are recomputed through the sprint-trend sync pipeline when that pipeline runs.

#### Projection computation

`PortfolioFlowProjectionService.ComputeProjectionsAsync(...)` loads:

- products for the owner
- valid sprints
- resolved work items for those products
- membership/state/story-point/business-value activity events
- then calls `ComputeProductSprintProjection(...)` per sprint/product

Relevant code:

- `PoTool.Api/Services/PortfolioFlowProjectionService.cs:48-234`

### First point where expected data disappears

**File:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioFlowProjectionService.cs`  
**Method:** `ComputeProductSprintProjection(...)`  
**Condition:** `CanonicalWorkItemTypes.IsAuthoritativePbi(workItemsByTfsId[workItemId].Type)`

```csharp
var candidatePbiIds = candidateWorkItemIds
    .Where(workItemsByTfsId.ContainsKey)
    .Where(workItemId => CanonicalWorkItemTypes.IsAuthoritativePbi(workItemsByTfsId[workItemId].Type))
    .ToList();
```

- `CanonicalWorkItemTypes.IsAuthoritativePbi(...)` only accepts canonical `"PBI"`.
- Persisted work items keep raw TFS names such as `"Product Backlog Item"` or `"User Story"`.
- Therefore candidate work items can already be present in `candidateWorkItemIds`, but then disappear at `candidatePbiIds` before stock / remaining / inflow / throughput are computed.

### Expected vs actual intermediate data

If a raw persisted work item has:

- `Type = "Product Backlog Item"`
- or `Type = "User Story"`

Expected behavior:

- It should be treated as a canonical PBI.
- It should survive into `candidatePbiIds`.
- It should contribute to stock / throughput / inflow metrics.

Actual behavior:

- It is dropped before the loop.
- `candidatePbiIds` can become empty.
- `StockStoryPoints`, `RemainingScopeStoryPoints`, `InflowStoryPoints`, and `ThroughputStoryPoints` stay zero.
- `GetPortfolioProgressTrendQueryHandler` then reads already-zeroed projection rows and can produce the wrong trajectory.

### Output shaping / consumers

The portfolio trend handler itself is a passive consumer:

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs:74-95`

It simply reads `PortfolioFlowProjections` and hands them to `IPortfolioFlowSummaryService`. So if the stored projection rows are already zero, the wrong status is downstream, not introduced by the handler.

`GetPortfolioDeliveryQueryHandler` is also a passive read-path over `SprintMetricsProjections` plus feature progress:

- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs:66-119`

I did **not** reproduce a current failing delivery-handler cluster in the focused run.

### Classification

- **Root cause category:** **projection input selection / data filtering**
- **Current portfolio test failures from the old audit:** **stale / no longer reproducible as currently written**
- **Current runtime portfolio risk:** **real product regression** in `PortfolioFlowProjectionService`

---

## Backlog Health Investigation

### Verified status of the multi-product backlog cluster

The following tests currently fail locally:

- `Handle_WithTwoDisjointProducts_ReturnsCumulativeTotals`
- `Handle_WithOverlappingProducts_DeduplicatesWorkItems`
- `Handle_WithSingleProduct_BehavesLikeOriginal`
- `Handle_WithNonExistentProduct_SkipsInvalidProducts`
- `Handle_UsesBacklogQualityAnalysisServiceForRealSprintSlots`

All failures are zero-count or "analysis was never called" failures.

### Current multi-product data path

1. `GetMultiIterationBacklogHealthQueryHandler.Handle(...)` calls:
   - `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs:65`
2. `SprintScopedWorkItemLoader.LoadAsync(...)` resolves product scope:
   - `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:32-52`
3. For selected products it now uses **batch lookup**:

```csharp
var selectedProductIds = effectiveFilter.Context.ProductIds.Values
    .Distinct()
    .ToArray();
var productsById = (await _productRepository.GetProductsByIdsAsync(selectedProductIds, cancellationToken))
    .ToDictionary(product => product.Id);
```

4. It derives combined root IDs and loads the hierarchy with:

```csharp
await _mediator.Send(new GetWorkItemsByRootIdsQuery(rootIds), cancellationToken)
```

5. The handler extracts distinct iteration paths, builds sprint slots, and calls `CalculateIterationHealth(...)` only for real slots with work items.
   - `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs:67-192`
6. `CalculateIterationHealth(...)` invokes the analysis service only if filtered iteration work items are present.
   - `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs:213-236`

### First point where expected data disappears

**File:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintScopedWorkItemLoader.cs`  
**Method:** `LoadAsync(...)`  
**Condition:** test fixture does not set up `GetProductsByIdsAsync(...)`

```csharp
var productsById = (await _productRepository.GetProductsByIdsAsync(selectedProductIds, cancellationToken))
    .ToDictionary(product => product.Id);
var products = selectedProductIds
    .Where(productsById.ContainsKey)
    .Select(productId => productsById[productId])
    .ToList();

var rootIds = products
    .SelectMany(product => product.BacklogRootWorkItemIds)
    .Distinct()
    .ToArray();

workItems = rootIds.Length == 0
    ? Array.Empty<WorkItemDto>()
    : await _mediator.Send(new GetWorkItemsByRootIdsQuery(rootIds), cancellationToken);
```

The failing tests still mock `GetProductByIdAsync(...)` instead of `GetProductsByIdsAsync(...)`:

- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:138-141`
- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:171-174`
- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:199-200`
- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:224-227`
- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:248-249`

Because the batch lookup is never configured in those tests:

- `productsById` is empty
- `products` is empty
- `rootIds` is empty
- `LoadAsync(...)` returns an empty work-item list
- `GetMultiIterationBacklogHealthQueryHandler` sees no real iteration work items
- `AnalyzeAsync(...)` is never called

That exactly matches the observed failures.

### Expected vs actual intermediate data

Expected in the stale tests:

- Product IDs 1 and/or 2 resolve to products with backlog roots.
- `rootIds` contains the configured backlog roots.
- Mediator returns the supplied work items.
- `distinctIterationPaths` contains `"Sprint 1"`.
- `CalculateIterationHealth(...)` runs analysis once for the real sprint.

Actual in the current tests:

- `GetProductsByIdsAsync(...)` is not configured.
- `rootIds` becomes empty.
- `allWorkItems` becomes empty immediately.
- `distinctIterationPaths` becomes empty.
- Handler builds placeholder/empty slots and total counts remain zero.
- `AnalyzeAsync(...)` is never invoked.

### Additional stale-test mismatch: slot ordering

The multi-product tests also assume that `result.IterationHealth.First()` is the `Sprint 1` slot.

That assumption no longer matches the current window selector for `MaxIterations > 3`:

- `GetMultiIterationBacklogHealthQueryHandler` uses `GetIssueComparisonWindow(...)` when `MaxIterations > 3`
  - `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs:130-135`
- `SprintWindowSelector.GetIssueComparisonWindow(...)` returns **3 past + current + 2 future**, in **chronological order**
  - `PoTool.Core/Metrics/Services/SprintWindowSelector.cs:124-233`

So even after fixing the loader setup in the tests, `result.IterationHealth.First()` would usually be a past sprint or placeholder, not the current `Sprint 1` slot.

### Classification

- **Root cause category:** **stale tests caused by loader behavior change**, with a secondary **stale ordering assumption**
- **Real product regression?** Not verified from these failures. The reproduced failures are explained completely by outdated test setup and assertions.

---

## Shared Root Causes

There is **not one shared runtime root cause** across both clusters.

### Shared theme: boundary contracts changed

Both clusters were affected by contract shifts at boundaries:

1. **Portfolio path:** raw persisted work-item types are now more clearly distinct from canonical domain work-item types, but `PortfolioFlowProjectionService` still mixes the two.
2. **Backlog multi-product tests:** product loading changed from per-product lookup assumptions to batched `GetProductsByIdsAsync(...)`, and iteration window ordering is now explicit and chronological.

### Important distinction

- **Portfolio issue:** real runtime logic bug in current projection filtering.
- **Backlog multi-product issue:** stale tests after refactors.

So the overlap is **refactor fallout / boundary drift**, not a single shared production defect.

---

## Classification

### Portfolio / sprint projection cluster

| Item | Current classification | Reason |
|---|---|---|
| `ComputeProductSprintProjection_UsesHistoricalEstimateAtFirstDone_AndSprintEndEstimateForStock` | Stale prior failure / currently passing | Current test uses canonical `PBI` fixtures and passes. |
| `ComputeProductSprintProjection_ComputesCompletionPercentFromStockAndRemainingScope` | Stale prior failure / currently passing | Same as above. |
| `ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionInTheSprintPipeline` | Stale prior failure / currently passing | Current sqlite fixture seeds canonical types and passes. |
| Portfolio zero / wrong trajectory in product runtime | **Real product regression** | Data first disappears at `PortfolioFlowProjectionService.ComputeProductSprintProjection(...)` candidate PBI filter on raw persisted types. |
| `GetPortfolioProgressTrendQueryHandler` | Downstream effect, not first break | It reads projection rows after the compute path has already zeroed/excluded data. |
| `GetPortfolioDeliveryQueryHandler` | Not implicated by current reproduced failures | Focused handler tests pass; no local failing cluster reproduced here. |

### Backlog health multi-product cluster

| Item | Current classification | Reason |
|---|---|---|
| `Handle_WithTwoDisjointProducts_ReturnsCumulativeTotals` | **Stale test** | Uses `GetProductByIdAsync(...)` setup, but loader now requires `GetProductsByIdsAsync(...)`. |
| `Handle_WithOverlappingProducts_DeduplicatesWorkItems` | **Stale test** | Same loader mismatch. |
| `Handle_WithSingleProduct_BehavesLikeOriginal` | **Stale test** | Same loader mismatch. |
| `Handle_WithNonExistentProduct_SkipsInvalidProducts` | **Stale test** | Same loader mismatch. |
| `Handle_UsesBacklogQualityAnalysisServiceForRealSprintSlots` | **Stale test** | Analysis is skipped because the stale loader setup yields zero work items. |
| Assertions using `IterationHealth.First()` with `MaxIterations: 5` | **Stale test** | Current issue-comparison window is chronological, not "current sprint first". |

---

## Recommended Next Step

**Do one narrow follow-up focused on the actual runtime defect first:**

- Fix `PortfolioFlowProjectionService.ComputeProductSprintProjection(...)` so candidate PBIs are selected from **canonicalized** work-item types (`ToCanonicalWorkItemType()`), and add one projection test that persists a raw `"Product Backlog Item"` or `"User Story"` through the real persistence path.

After that, update `GetMultiIterationBacklogHealthQueryHandlerMultiProductTests` separately so they:

- mock `GetProductsByIdsAsync(...)`
- assert against the `Sprint 1` slot by `IterationPath`, not `First()`

The single best immediate action is the **portfolio-flow fix**, because that is the only verified current **product** regression.

---

## Confidence

**Medium**

Reason:

- Confidence is **high** for the backlog-health multi-product diagnosis because it was reproduced locally and the first disappearing-data point is explicit in current code.
- Confidence is **medium** for the portfolio runtime diagnosis because the current named tests pass, but the raw-type persistence path and canonical-only PBI filter still form a concrete, current code-level defect that would zero portfolio-flow inputs in production data shaped like TFS raw types.
