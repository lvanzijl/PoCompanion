# Post-Fix Stability Verification

## Summary

The two recently targeted corrections do improve the repository baseline, but they do not fully restore a clean unit-test baseline.

- **Portfolio-flow production bug:** fixed in code.
- **Raw persisted type handling:** production code now canonicalizes raw TFS work item types before authoritative PBI filtering.
- **Production-shaped regression coverage:** explicit for raw **`Product Backlog Item`**; indirect but not explicitly regression-tested for raw **`User Story`**.
- **Multi-product backlog-health tests:** corrected and passing.
- **Overall baseline:** improved, but still **partially unstable** because unrelated failures remain in the unit suite.

## Portfolio Verification

### State

**Fixed**

### What was verified

`PortfolioFlowProjectionService` now canonicalizes the persisted raw work item type before applying authoritative-PBI filtering:

- `PoTool.Api/Services/PortfolioFlowProjectionService.cs:261-264`
  - current code:
    - `workItemsByTfsId[workItemId].Type.ToCanonicalWorkItemType()`
    - then `CanonicalWorkItemTypes.IsAuthoritativePbi(...)`

This is the critical production fix for the disappearing-data bug. It means raw persisted values such as:

- `Product Backlog Item`
- `User Story`

are normalized before portfolio-flow scope selection.

The canonicalization behavior is implemented in:

- `PoTool.Api/Adapters/CanonicalWorkItemTypeMapper.cs:12-23`

That mapper currently resolves:

- `RawWorkItemType.Pbi` -> `CanonicalWorkItemTypes.Pbi`
- `RawWorkItemType.PbiShort` -> `CanonicalWorkItemTypes.Pbi`
- `RawWorkItemType.UserStory` -> `CanonicalWorkItemTypes.Pbi`

### Does it cover the real production-shaped scenario?

**Yes in code, partially in direct regression coverage.**

#### Explicitly covered

There is a current regression-style test that exercises the rebuilt portfolio-flow path with a raw persisted work item type of **`Product Backlog Item`**:

- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs:161-287`
  - test:
    - `ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionInTheSprintPipeline_WithRawProductBacklogItemType`
  - setup persists:
    - `Type = "Product Backlog Item"`
  - verification asserts a persisted `PortfolioFlowProjection` with non-zero:
    - `StockStoryPoints`
    - `InflowStoryPoints`
    - `ThroughputStoryPoints`

This is the strongest current proof that the original disappearing-data production scenario is fixed for raw persisted PBI-shaped input.

#### Not explicitly covered

I did **not** find a portfolio-flow regression test that explicitly persists raw **`User Story`** into the portfolio-flow path and verifies portfolio metrics.

So the current state is:

- **runtime logic covers both `Product Backlog Item` and `User Story`**
- **test coverage explicitly proves `Product Backlog Item`**
- **`User Story` is covered by the mapper, but not by a direct portfolio-flow regression test**

### Is the previous disappearing-data point gone?

**For the raw `Product Backlog Item` production-shaped path: yes.**

Evidence:

- the filtering code is now correctly canonicalized before authoritative-PBI filtering
- the targeted portfolio-flow / sprint projection cluster passed locally
- the explicit raw-`Product Backlog Item` sprint-pipeline regression test passes under current code

## Backlog Test Verification

### State

**Corrected and passing**

### What was verified

`GetMultiIterationBacklogHealthQueryHandlerMultiProductTests` is aligned with current loader and sprint-window behavior:

- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:139-140,169-170,194,218-219,239`
  - uses `GetProductsByIdsAsync(...)` via `SetupProductsByIds(...)`
- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:148,178,203,226`
  - selects the actual row with:
    - `IterationPath == "Sprint 1"`
  - does **not** use `First()`
- `PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs:245-254`
  - verifies work items are loaded through the mediator root-query path
  - verifies `AnalyzeAsync(...)` is actually invoked with the loaded sprint work items

Current production loader behavior also matches those corrected tests:

- `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:32-52`
  - selected products are resolved through `IProductRepository.GetProductsByIdsAsync(...)`
  - root work items are then loaded via `IMediator.Send(new GetWorkItemsByRootIdsQuery(...))`

### Current pass status

The targeted backlog-health test cluster passes under current code.

## Current Test Baseline

### Targeted verification runs

The following targeted cluster was run locally:

```text
dotnet restore PoTool.sln --nologo
dotnet build PoTool.sln --configuration Release --no-restore --nologo
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~PortfolioFlowProjectionServiceTests|FullyQualifiedName~GetPortfolioProgressTrendQueryHandlerTests|FullyQualifiedName~SprintTrendProjectionServiceSqliteTests|FullyQualifiedName~GetMultiIterationBacklogHealthQueryHandlerMultiProductTests" -v minimal
```

Result:

- **18 passed**
- **0 failed**

This confirms the previously targeted portfolio-flow and multi-product backlog-health areas are currently green.

### Full unit baseline

The broader unit suite was also run locally:

```text
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build -v minimal
```

Result:

- **Total:** 1680
- **Passed:** 1654
- **Failed:** 26
- **Skipped:** 0

### Did the baseline improve?

**Yes.**

The specific previously failing stabilization targets are now green:

- portfolio-flow / sprint projection verification cluster passes
- multi-product backlog-health tests pass

The remaining failures are outside the two just-fixed stabilization targets.

### Current remaining failure groups

| Group | Count | Classification | Notes |
| --- | ---: | --- | --- |
| Canonical-domain test drift | 9 | stale test | Tests still construct or expect raw `Product Backlog Item` where canonical-domain code now requires canonical values like `PBI` |
| Work item ancestor completion tests | 2 | infrastructure/test harness issue | Mocked HTTP response sequencing/shape appears out of sync with the current client hierarchy-fetch flow |
| Work item selection tests | 4 | stale test | Test `TreeNode` fixtures omit `JsonPayload`, but `WorkItemSelectionService` now requires it |
| Input sanitization test | 1 | stale test | Assertion arguments are reversed; test expects old behavior incorrectly |
| Area-path TFS handler test | 1 | stale test | Test expects null depth, but handler now deliberately calls `GetAreaPathsAsync(depth: 5, ...)` |
| Dependency graph handler tests | 5 | stale test | Tests still populate dependency data only in JSON payload strings; handler now reads `WorkItemDto.Relations` directly |
| Pipeline scatter-point ordering | 1 | real product regression | Observable output order is not start-time ascending under current code/test data |
| Audit/documentation expectation drift | 3 | documentation/audit expectation drift | Generated/report documents no longer match current source expectations |

## Remaining Risks

1. **The repository does not currently have a clean unit-test baseline.**
   - 26 unit failures remain.

2. **Some failures are clearly not related to the two verified fixes.**
   - This means the repository baseline is improved, but not yet trustworthy as a whole.

3. **There is at least one likely real regression still present.**
   - `GetPipelineInsightsScatterPointTests.Handle_ScatterPoints_OrderedByStartTimeAscending`
   - Under current code, the returned scatter points are not ordered by ascending `StartTime`.

4. **There is still explicit stale-test debt around canonical-domain inputs.**
   - Multiple tests are still passing raw `Product Backlog Item` into canonical-domain constructors that now enforce canonical types.

5. **CI signal is limited.**
   - GitHub Actions in this repository currently show only dynamic agent workflows (`Copilot coding agent`, `Copilot code review`, `OpenAI Codex`), not a conventional repository CI workflow.
   - Recent completed workflow runs inspected for this branch were successful, but they do not replace the local full unit baseline.

## Final Verdict

**Partially unstable**

### Why

- The two targeted stabilization fixes are verified as effective:
  - portfolio-flow bug: fixed
  - backlog-health multi-product tests: corrected and passing
- However, the repository still has **26 failing unit tests**, including:
  - many stale tests
  - documentation/audit drift
  - at least one likely real behavioral regression

So the baseline is improved, but not yet fully stable.

## Recommended Next Step

**Fix the remaining real behavioral failure first: `GetPipelineInsightsScatterPointTests.Handle_ScatterPoints_OrderedByStartTimeAscending`, then re-baseline the unit suite and clean up the stale canonical-domain and handler tests in a separate pass.**
