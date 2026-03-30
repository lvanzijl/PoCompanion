# Filter Integration Validation & Regression Audit

## 1. Summary

**Overall system status: UNSAFE**

Observed validation baseline:

- `dotnet build PoTool.sln --configuration Release` succeeded
- focused filter-validation coverage across PR, Pipeline, Delivery, Sprint, and portfolio/cross-slice paths passed **191 tests**

What is clearly working:

- migrated backend slices consistently resolve shared scope at controller boundaries before dispatching mediator queries:
  - PR: `PoTool.Api/Controllers/PullRequestsController.cs:114-126`, `163-180`, `354-409`
  - Pipeline: `PoTool.Api/Controllers/PipelinesController.cs:89-102`, `129-143`, `188-202`
  - Delivery/Sprint: `PoTool.Api/Controllers/MetricsController.cs:83-101`, `131-149`, `181-193`, `759-776`, `810-942`, `970-1023`
  - Build Quality delivery scope: `PoTool.Api/Controllers/BuildQualityController.cs:35-47`, `56-67`
- all migrated slices return envelope metadata (`RequestedFilter`, `EffectiveFilter`, `InvalidFields`, `ValidationMessages`) at the API boundary through `ToResponse(...)` helpers in:
  - `PoTool.Api/Services/PullRequestFilterResolutionService.cs:112-130`
  - `PoTool.Api/Services/PipelineFilterResolutionService.cs:104-122`
  - `PoTool.Api/Services/DeliveryFilterResolutionService.cs:83-101`
  - `PoTool.Api/Services/SprintFilterResolutionService.cs:104-122`

Why the status is **UNSAFE** instead of **SAFE**:

1. the UI/client layer mostly discards canonical filter metadata and therefore still allows **silent effective-filter substitution**
2. pipeline metrics/runs still have a **data-integrity risk** because cached runs are globally limited before final scope filtering
3. those two findings are significant enough to block a confident production rollout of the filtering feature in its current end-to-end form
4. some filter semantics are still duplicated and slightly divergent across slice-specific resolution services, which increases long-term drift risk

---

## 2. Findings

### Functional issues

#### F1. Canonical filter metadata is produced by the backend but not surfaced by the client/UI

**Evidence**

- Pages/services almost always reduce envelope responses to `response.Data` only:
  - `PoTool.Client/Pages/Home/PrOverview.razor:701-708`
  - `PoTool.Client/Pages/Home/PrDeliveryInsights.razor:823-830`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor:730-736`
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor:589-590`
  - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:778-783`
  - `PoTool.Client/Pages/Home/SprintExecution.razor:558-564`
  - `PoTool.Client/Pages/Home/SprintTrendActivity.razor:257-265`
  - `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor:106-112`
  - `PoTool.Client/Services/PipelineService.cs:34-35`, `71-72`
  - `PoTool.Client/Services/PullRequestService.cs:49-50`, `65-73`
  - `PoTool.Client/Services/SprintDeliveryMetricsService.cs:40-48`
  - `PoTool.Client/Services/WorkspaceSignalService.cs:426-443`, `467-488`
- direct search in `PoTool.Client` found no page/component usage of `ValidationMessages`, `InvalidFields`, `RequestedFilter`, or `EffectiveFilter`.

**Impact**

- invalid values can be normalized on the server without any user-visible warning
- users cannot tell when `RequestedFilter != EffectiveFilter`
- cross-slice comparisons can look inconsistent even when the backend behaved correctly, because the UI does not expose the applied scope

**Assessment**

- backend correctness: **good**
- end-to-end user transparency: **not yet complete**

#### F2. Default filter states still diverge across migrated slices

**Evidence**

- PR insights defaults to a **last 6 months** window:
  - `PoTool.Client/Pages/Home/PrOverview.razor:645-647`
  - also resets to 6 months when clearing team scope: `749-753`
- PR pages also fall back to a hardcoded **14-day sprint duration** when sprint metadata has no `EndUtc`:
  - `PoTool.Client/Pages/Home/PrOverview.razor:630-634`
  - used at `805-809` and `829-834`
- Pipeline insights auto-selects the **current or most recent past sprint**:
  - `PoTool.Client/Pages/Home/PipelineInsights.razor:653-676`
- Sprint execution defaults to a **single current sprint**:
  - `PoTool.Client/Pages/Home/SprintExecution.razor:533-545`
- Portfolio delivery defaults to a **range from current sprint back four sprints**:
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor:553-569`

**Impact**

- backend canonical semantics are consistent once a request is formed, but page entry defaults still differ significantly by slice
- users can compare PR/Pipeline/Delivery/Sprint views that appear related but are initialized with different time semantics

**Assessment**

- this is primarily a **UI/default-state consistency warning**, not a backend filtering bug

#### F3. UI loading behavior is inconsistent across migrated pages

**Evidence**

- PR and Pipeline pages render the header/filter shell immediately:
  - `PoTool.Client/Pages/Home/PrOverview.razor:19-120`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor:19-120`
- Portfolio delivery, Sprint execution, and Portfolio progress still gate the page body behind `_isLoading`:
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor:36-49`
  - `PoTool.Client/Pages/Home/SprintExecution.razor:112-130`
  - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:38-52`

**Impact**

- the migrated filter system is not presented with a consistent “render shell first, load progressively” UX
- this conflicts with `docs/rules/ui-loading-rules.md:5-39`, `91-102`

**Assessment**

- not a filtering correctness bug
- still a **real UX regression/compliance warning**

---

### Consistency issues

#### C1. Product-scope normalization logic is duplicated and already diverges by slice

**Evidence**

- Portfolio product normalization:
  - `PoTool.Api/Services/PortfolioFilterResolutionService.cs:156-183`
  - out-of-scope product selection is replaced with **ALL**
- Delivery product normalization:
  - `PoTool.Api/Services/DeliveryFilterResolutionService.cs:182-227`
  - out-of-scope selection is replaced with **all owner products**
  - explicitly tested in `PoTool.Tests.Unit/Services/DeliveryFilterResolutionServiceTests.cs:60-92`
- Sprint product normalization:
  - `PoTool.Api/Services/SprintFilterResolutionService.cs:211-256`
  - out-of-scope selection is replaced with **all owner products**
- Pipeline explicit product normalization:
  - `PoTool.Api/Services/PipelineFilterResolutionService.cs:176-199`
  - explicit product IDs are normalized only to positive/distinct values; there is no owner-scope equivalence rule in this helper

**Impact**

- there is no single repository-wide policy for “invalid/out-of-scope product filter”
- this is exactly the sort of duplication that can produce future semantic drift even if current endpoint behavior is mostly acceptable

**Assessment**

- **warning**
- currently more of a consistency/maintainability risk than a proven live-slice defect in every endpoint

#### C2. Cross-slice aggregations also discard filter metadata

**Evidence**

- `PoTool.Client/Services/WorkspaceSignalService.cs:426-443` reads Sprint envelopes and returns only `.Data`
- `PoTool.Client/Services/WorkspaceSignalService.cs:467-488` reads Sprint/PR trend envelopes and returns only `.Data`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor:681-688` consumes PR trend envelope data only

**Impact**

- cross-slice signals can no longer explain whether a “filtered” trend tile reflects requested scope or normalized effective scope
- this creates hidden coupling between backend canonicalization and UI assumptions

**Assessment**

- **warning**

#### C3. Filter control patterns differ notably across slices

**Evidence**

- PR/Pipeline use collapsible filter panels:
  - `PoTool.Client/Pages/Home/PrOverview.razor:40-120`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor:40-120`
- Delivery/Sprint/Portfolio trend pages use chip + popover scope controls:
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor:51-130`
  - `PoTool.Client/Pages/Home/SprintExecution.razor:39-108`
  - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:53-120`

**Impact**

- control semantics are understandable per page, but the cross-slice filtering experience is not uniform
- discoverability and expected defaults differ by workspace

**Assessment**

- **low-severity warning**

---

### Data issues

#### D1. Pipeline metrics/runs are vulnerable to undercounting because `Take(top)` is applied globally before final scoped filtering

**Evidence**

- cached provider fetches runs with a global top-N across all requested pipelines:
  - `PoTool.Api/Services/CachedPipelineReadProvider.cs:148-168`
- metrics handler requests runs first, then applies final scope filtering:
  - `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:34-43`
- runs handler does the same:
  - `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs:33-40`
- final branch/time/pipeline filtering is applied afterward in memory:
  - `PoTool.Api/Services/PipelineFiltering.cs:23-45`

**Impact**

- less-active pipelines can lose valid in-scope runs if busier pipelines occupy the latest 100 rows
- filtered result sets may be lower than reality
- aggregated metrics and comparisons can therefore be skewed

**Assessment**

- **critical**

#### D2. Pipeline metrics/runs and pipeline insights use different run-acquisition shapes

**Evidence**

- metrics/runs path uses provider + global top-N + `PipelineFiltering.ApplyRunScope(...)`:
  - `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:34-47`
  - `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs:33-40`
  - `PoTool.Api/Services/PipelineFiltering.cs:23-45`
- insights path queries cached runs directly for the sprint window, then filters default branch after materialization:
  - `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:243-290`

**Impact**

- even when the requested logical scope is compatible, the data acquisition path differs enough that metrics/runs and insights can disagree under higher volume
- this is amplified by the global-top limitation in the metrics/runs path

**Assessment**

- **warning**, elevated to **critical only when combined with D1**

#### D3. End-to-end “same filter, same visible explanation” is not yet true

**Evidence**

- backend metadata exists for all migrated slices
- client pages/services consistently discard it and render payload-only results:
  - PR: `PoTool.Client/Pages/Home/PrOverview.razor:701-708`
  - Pipeline: `PoTool.Client/Pages/Home/PipelineInsights.razor:730-736`
  - Delivery: `PoTool.Client/Pages/Home/PortfolioDelivery.razor:589-590`, `PortfolioProgressPage.razor:778-783`
  - Sprint: `PoTool.Client/Pages/Home/SprintExecution.razor:558-564`, `SprintTrendActivity.razor:257-265`

**Impact**

- the system may internally apply identical effective scope across slices, but users cannot validate that from the UI
- this weakens confidence in cross-slice metric alignment

**Assessment**

- **warning**

---

### Performance risks

#### P1. Sprint scoped work-item loading has an N+1 product lookup pattern and additional in-memory filtering

**Evidence**

- selected product scope loads products one-by-one:
  - `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:32-42`
- area-path filtering is applied in memory after work items are materialized:
  - `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:70-79`

**Impact**

- cost grows linearly with number of selected products
- for broad sprint views, filtering happens after large work-item sets are already loaded

**Assessment**

- **warning**

#### P2. Build-quality branch filtering is still partly in-memory

**Evidence**

- builds are selected from EF, then filtered to default branch after materialization:
  - `PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs:118-134`

**Impact**

- larger windows can pull more build rows than ultimately needed
- not an EF translation failure, but still a potentially expensive pattern

**Assessment**

- **warning**

#### P3. Pipeline insights also materializes then branch-filters runs in memory

**Evidence**

- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:257-287`

**Impact**

- likely acceptable for single-sprint windows today
- still a scale risk if sprint windows or run volume grow

**Assessment**

- **low-to-medium warning**

#### P4. No production `AsEnumerable()` client-eval bypasses were confirmed in the migrated production filter paths

**Evidence**

- direct search found `AsEnumerable()` only in mock/dev code and dev repositories, not in migrated production slice paths:
  - `PoTool.Api/Services/MockTfsClient.cs`
  - `PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs`
  - `PoTool.Api/Repositories/DevWorkItemRepository.cs`

**Impact**

- this is a **strength**, not a risk

---

### Architecture violations

#### A1. No confirmed backend boundary violation was found in the migrated slices

**Evidence**

- migrated controllers resolve scope first and pass effective filter into queries:
  - `PoTool.Api/Controllers/PullRequestsController.cs:114-126`, `163-180`, `354-409`
  - `PoTool.Api/Controllers/PipelinesController.cs:89-102`, `129-143`, `188-202`
  - `PoTool.Api/Controllers/MetricsController.cs:83-101`, `131-149`, `181-193`, `759-776`, `810-942`, `970-1023`
  - `PoTool.Api/Controllers/BuildQualityController.cs:35-47`, `56-67`

**Impact**

- the core architectural goal of the migration is largely met on the backend

#### A2. No CDC pollution or domain leakage into infrastructure was confirmed from the migrated filter work

**Evidence**

- filter resolution services live in API/services and shape query inputs; they do not mutate canonical CDC state
- focused validation for PR/Pipeline/Delivery/Sprint/Portfolio slices passed with build success and no failing regressions

**Impact**

- no blocker found here

#### A3. The main architectural gap is end-to-end observability, not backend layering

**Evidence**

- backend exposes canonical metadata
- UI orchestration layer discards it and continues to own divergent defaults:
  - `PoTool.Client/Services/PipelineService.cs:34-35`, `71-72`
  - `PoTool.Client/Services/PullRequestService.cs:49-50`, `65-73`
  - `PoTool.Client/Services/WorkspaceSignalService.cs:426-443`, `467-488`
  - `PoTool.Client/Pages/Home/PrOverview.razor:645-647`
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor:553-569`
  - `PoTool.Client/Pages/Home/SprintExecution.razor:533-545`

**Impact**

- the architecture is correct at the API boundary, but not yet fully visible/consistent at the UI boundary

**Assessment**

- **warning**

---

## 3. Critical Issues

### CI-1. Pipeline metrics/runs can return incomplete results because cached runs are globally limited before final scope filtering

**Why this must be fixed before merge**

- it is a direct data-integrity risk in a migrated slice
- it can make filtered results smaller than reality without any error or warning
- it undermines confidence in pipeline metrics and runs exactly where canonical filtering is supposed to improve trust

**Primary evidence**

- `PoTool.Api/Services/CachedPipelineReadProvider.cs:148-168`
- `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:34-43`
- `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs:33-40`
- `PoTool.Api/Services/PipelineFiltering.cs:23-45`

### CI-2. The UI/client layer drops canonical filter metadata, so invalid or normalized filters still fail silently from the user’s perspective

**Why this must be fixed before merge**

- it leaves the system vulnerable to “silent fallback” at the end-to-end UX level even when the backend does the right thing
- it prevents users from reconciling cross-slice differences via `RequestedFilter` vs `EffectiveFilter`
- it weakens one of the main reasons for introducing canonical filter envelopes

**Primary evidence**

- `PoTool.Client/Pages/Home/PrOverview.razor:701-708`
- `PoTool.Client/Pages/Home/PipelineInsights.razor:730-736`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor:589-590`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:778-783`
- `PoTool.Client/Pages/Home/SprintExecution.razor:558-564`
- `PoTool.Client/Services/PipelineService.cs:34-35`, `71-72`
- `PoTool.Client/Services/PullRequestService.cs:49-50`, `65-73`
- `PoTool.Client/Services/WorkspaceSignalService.cs:426-443`, `467-488`

---

## 4. Recommended Fixes

### For CI-1: pipeline run truncation before scope filtering

1. change cached pipeline run retrieval so limiting happens **after** final effective scope is known
2. preferred fix: fetch **top N per pipeline**, not global top N across the full pipeline set
3. add regression tests that prove:
   - one very active pipeline does not hide another pipeline’s valid runs
   - branch/default-branch filtering still behaves correctly after the change

### For CI-2: silent UI/client fallback

1. stop collapsing every envelope to `response.Data` at the first client hop
2. add a lightweight shared UI model for:
   - `RequestedFilter`
   - `EffectiveFilter`
   - `InvalidFields`
   - `ValidationMessages`
3. surface a visible warning/banner/chip when:
   - `InvalidFields.Count > 0`
   - or `RequestedFilter` differs materially from `EffectiveFilter`
4. update cross-slice signal consumers (`WorkspaceSignalService`, Trends tiles) so normalized scope is at least observable in diagnostics/logging or UI

### For duplicated/divergent resolver logic

1. extract shared owner/product normalization policy into a common helper/policy object
2. explicitly document any intentional slice-specific differences
3. add comparison tests across Portfolio/Delivery/Sprint/Pipeline where product-scope behavior is expected to match

### For UI default-state drift

1. define a shared client-side default filter policy for each workspace type
2. remove hardcoded fallback values where possible, especially:
   - PR `DefaultSprintDurationDays = 14` in `PoTool.Client/Pages/Home/PrOverview.razor:630-634`
3. make default scope visible in the page chrome so users understand the starting context

### For loading-state compliance

1. remove full-page `_isLoading` gates from migrated analytical pages that can render shell-first
2. align Delivery/Sprint/Portfolio pages with the PR/Pipeline pattern:
   - render header/filter shell immediately
   - keep loading indicators component-scoped

### For performance hardening

1. batch product lookups in `SprintScopedWorkItemLoader` instead of looping `GetProductByIdAsync(...)`
2. reduce in-memory post-filtering where possible in Sprint and BuildQuality loaders
3. add data-volume tests or diagnostics around:
   - pipeline run counts by scope
   - build-quality run selection
   - sprint work-item loading breadth

---

## 5. Confidence Assessment

**Confidence: Medium**

Why not Low:

- build succeeded
- 191 focused regression tests passed across all migrated slices in scope
- backend controller-boundary pattern is consistent and directly observable in code

Why not High:

- no full interactive end-to-end UI run was performed
- the most important remaining issues are system-level and data-volume-sensitive
- several warnings are about client/UI observability and performance characteristics that focused unit tests do not fully prove
