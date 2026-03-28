# Cross-Slice Canonical Filter Migration

Status: design and impact analysis only  
Scope: expand canonical filter enforcement beyond the portfolio read slice without changing API contracts yet

## 0. Baseline

The current canonical filter path is implemented only for the portfolio read family:

- `PoTool.Core/Filters/FilterContext.cs`
- `PoTool.Api/Services/PortfolioFilterResolutionService.cs`
- `PoTool.Api/Services/PortfolioReadModelFiltering.cs`

That path resolves request input once at the API boundary, produces an `EffectiveFilter`, and applies only the effective filter downstream.

All other relevant query families still interpret request filters locally in controllers, handlers, or slice services.

This report covers those non-portfolio families.

## 1. Affected Query Families

Included in scope:

- shared analytics scope filters: product, team, time, iteration/sprint selection, owner-scoped product resolution
- adjacent slice-local scope filters that currently influence result sets: repository, repository/pipeline selection, area path, validation drill-down filters

Excluded from scope:

- portfolio read endpoints already using `EffectiveFilter`
- endpoints with no filter input or only identity lookup with no scope semantics
- purely local UI-only filters such as client-side search text or tag matching that do not hit these API query families

### Affected family inventory

| Area | Endpoint / Service | Current Filter Handling | Uses EffectiveFilter |
| --- | --- | --- | --- |
| PR | `GET /api/pullrequests/metrics` → `GetPullRequestMetricsQueryHandler` | `PullRequestsController` parses `productIds` CSV; handler reads `query.ProductIds` directly and enforces its own 6-month default when `FromDate` is null. | No |
| PR | `GET /api/pullrequests/filter` → `GetFilteredPullRequestsQueryHandler` | Controller parses `productIds`; handler applies `IterationPath`, `CreatedBy`, `ToDate`, and `Status` inline against the returned PR list. | No |
| PR | `GET /api/pullrequests/sprint-trends` → `GetPrSprintTrendsQueryHandler` | Controller parses `productIds`; handler uses `ProductIds` first, otherwise resolves `TeamId` through `ProductTeamLinks`, then maps PRs into sprint windows locally. | No |
| PR | `GET /api/pullrequests/insights` → `GetPullRequestInsightsQueryHandler` | Controller assigns default `fromDate`/`toDate`; handler resolves `TeamId` to linked products, filters by date range and optional `RepositoryName`, then aggregates locally. | No |
| PR | `GET /api/pullrequests/delivery-insights` → `GetPrDeliveryInsightsQueryHandler` | Controller assigns default dates; handler optionally overrides them from `SprintId`, resolves `TeamId` to products and then to repository names, then filters PRs locally. | No |
| Pipeline | `GET /api/pipelines/metrics` → `GetPipelineMetricsQueryHandler` | Controller parses `productIds` CSV; handler loops `query.ProductIds`, resolves pipeline definitions product by product, then hard-codes a 6-month `refs/heads/main` run filter. | No |
| Pipeline | `GET /api/pipelines/runs` → `GetPipelineRunsForProductsQueryHandler` | Controller parses `productIds` CSV; handler repeats the same per-product pipeline-definition resolution and hard-coded 6-month `refs/heads/main` filter. | No |
| Pipeline | `GET /api/pipelines/definitions` → `GetPipelineDefinitionsQueryHandler` | Query accepts `productId` or `repositoryId`; handler branches directly on those request fields and throws if neither is present. | No |
| Pipeline | `GET /api/pipelines/insights` → `GetPipelineInsightsQueryHandler` | Query uses `productOwnerId`, `sprintId`, and inclusion toggles; handler resolves owner products, derives the sprint window, and applies branch filtering per default branch inside the handler/service path. | No |
| Delivery | `GET /api/metrics/portfolio-progress-trend` → `GetPortfolioProgressTrendQueryHandler` | Query accepts `productOwnerId`, `sprintIds`, optional `productIds`; handler loads owner products and intersects requested `productIds` locally before projection lookup. | No |
| Delivery | `GET /api/metrics/capacity-calibration` → `GetCapacityCalibrationQueryHandler` | Same owner + sprint-range + optional product filter pattern as portfolio progress trend, resolved directly in the handler. | No |
| Delivery | `GET /api/metrics/portfolio-delivery` → `GetPortfolioDeliveryQueryHandler` | Query accepts `productOwnerId` and `sprintIds`; handler resolves owner products and sprint range directly, with no shared canonical filter object. | No |
| Delivery | `GET /api/buildquality/rolling` → `GetBuildQualityRollingWindowQueryHandler` + `BuildQualityScopeLoader` | Query accepts `productOwnerId` and explicit window; scope loader resolves products, repository/pipeline constraints, default branches, and time window locally. | No |
| Delivery | `GET /api/buildquality/sprint` → `GetBuildQualitySprintQueryHandler` + `BuildQualityScopeLoader` | Query accepts `productOwnerId` and `sprintId`; handler turns the sprint into a window and delegates all scope resolution to the local build-quality scope loader. | No |
| Pipeline | `GET /api/buildquality/pipeline` → `GetBuildQualityPipelineDetailQueryHandler` + `BuildQualityScopeLoader` | Query accepts `productOwnerId`, `sprintId`, optional `pipelineDefinitionId`, optional `repositoryId`; handler derives the sprint window and loader resolves product/pipeline/repository/default-branch scope locally. | No |
| Sprint | `GET /api/metrics/sprint-trend` → `GetSprintTrendMetricsQueryHandler` | Query accepts `productOwnerId` and `sprintIds`; handler uses request sprint IDs directly for projection selection and current/previous sprint window derivation. | No |
| Sprint | `GET /api/metrics/sprint-execution` → `GetSprintExecutionQueryHandler` | Query accepts `productOwnerId`, `sprintId`, optional `productId`; handler resolves owner products, optionally narrows to one product, and reconstructs scope locally. | No |
| Sprint | `GET /api/metrics/sprint` → `GetSprintMetricsQueryHandler` | Query accepts `iterationPath`; the sprint identifier is interpreted directly in the legacy sprint metrics path instead of through canonical time resolution. | No |
| Sprint | `GET /api/metrics/backlog-health` → `GetBacklogHealthQueryHandler` | Query accepts `iterationPath`; handler/service family uses the raw path as its filter. | No |
| Sprint / Health | `GET /api/metrics/multi-iteration-health` → `GetMultiIterationBacklogHealthQueryHandler` | Query accepts `productIds`, `areaPath`, and `maxIterations`; handler prioritizes product-root loading when `productIds` are present and otherwise falls back to area-path filtering. | No |
| Sprint / Planning | `GET /api/metrics/capacity-plan` → `GetSprintCapacityPlanQueryHandler` | Query accepts `iterationPath` and optional capacity override; iteration selection remains a raw request field. | No |
| Sprint | `GET /api/metrics/work-item-activity/{workItemId}` → `GetWorkItemActivityDetailsQueryHandler` | Query accepts explicit period bounds and applies them directly in the query path. | No |
| Delivery / Home | `GET /api/metrics/home-product-bar` → `GetHomeProductBarMetricsQueryHandler` | Query accepts `productOwnerId` and optional `productId`; handler resolves owner scope locally, but only partially applies `productId` across the returned metrics. | No |
| Health | `GET /api/workitems/validated` → `GetAllWorkItemsWithValidationQueryHandler` | Controller parses `productIds` CSV; handler branches between selected products, all products, or profile-area fallback and then validates the loaded set. | No |
| Health | `GET /api/workitems/validated/{tfsId}` → `GetWorkItemByIdWithValidationQueryHandler` | Controller parses `productIds`, but handler does not enforce them; the parameter is accepted yet ignored downstream. | No |
| Health | `GET /api/workitems/validation-triage` → `GetValidationTriageSummaryQueryHandler` | Controller parses `productIds`; handler passes them to `GetAllWorkItemsWithValidationQuery` and builds category aggregates locally. | No |
| Health | `GET /api/workitems/validation-queue` → `GetValidationQueueQueryHandler` | Controller parses `productIds`; handler combines the raw category key with locally loaded product-scoped work items. | No |
| Health | `GET /api/workitems/validation-fix` → `GetValidationFixSessionQueryHandler` | Controller parses `productIds`; handler combines raw `ruleId`/`category` with locally filtered work items. | No |
| Health | `GET /api/workitems/validation-impact-analysis` → `GetValidationImpactAnalysisQueryHandler` | Query accepts `areaPathFilter` and `iterationPathFilter`; handler loads product-scoped work items first and then applies both filters inline. | No |
| Health | `GET /api/workitems/advanced-filter` → `GetFilteredWorkItemsAdvancedQueryHandler` | Query exposes type, state, iteration, area, effort, blocked, title, and validation flags; handler applies every filter directly over the loaded work-item set. | No |
| Health | `GET /api/workitems/backlog-state/{productId}` → `GetProductBacklogStateQueryHandler` | Route `productId` is used directly to load the product backlog graph; product scope is resolved locally in the handler. | No |
| Health | `GET /api/workitems/health-summary/{productId}` → `GetHealthWorkspaceProductSummaryQueryHandler` | Route `productId` is used directly to load one product backlog tree and compute dashboard summary metrics. | No |

## 2. Filter Logic Duplication and Semantic Drift

| Location | Problem | Risk |
| --- | --- | --- |
| `PoTool.Api/Controllers/PullRequestsController.cs:96-148,227-248,372-394`; `PoTool.Api/Controllers/PipelinesController.cs:70-119`; `PoTool.Api/Controllers/WorkItemsController.cs:71-81,131-213,927-959` | Repeated parsing of comma-separated `productIds` at multiple controller boundaries. Portfolio already centralizes request mapping in `PortfolioFilterResolutionService`. | Multiple parser variants create different invalid-input behavior and guarantee drift whenever canonical product parsing changes. |
| `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs:75-110`; `GetPullRequestInsightsQueryHandler.cs:54-85`; `GetPrDeliveryInsightsQueryHandler.cs:107-142` | `TeamId` is interpreted three different ways: direct linked product scope, linked product scope plus repository filter, and product scope only for sprint aggregation. | Team-based results can disagree across PR pages for the same selected team, which is exactly the kind of cross-slice divergence the canonical model is meant to prevent. |
| `PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs:33-40`; `PullRequestsController.cs:317-358`; `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:58-65`; `GetPipelineRunsForProductsQueryHandler.cs:57-63` | Rolling time windows are encoded locally and differently: PR metrics and insights default to 6 months, pipeline metrics and runs also force main-branch filtering, and delivery insights can replace dates with sprint boundaries. | Equivalent “time filtered” screens can silently analyze different windows and branch populations, producing mismatched trend narratives. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs:52-81`; `GetCapacityCalibrationQueryHandler.cs:56-84`; `GetSprintExecutionQueryHandler.cs:85-105`; `GetHomeProductBarMetricsQueryHandler.cs:30-53` | Owner-scope plus optional `productId`/`productIds` resolution is duplicated in four delivery/sprint handlers. Each performs its own intersection or narrowing. | Invalid or out-of-scope product selection does not behave consistently. Some paths return empty results, others silently narrow, and none produce canonical invalid-field metadata. |
| `PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:41-53` vs `119-164` | `productId` only narrows bug and change counts. Sprint progress is still computed from all owner-linked teams/products. | A single response mixes filtered and unfiltered semantics, which can break UI assumptions once global product scope becomes canonical elsewhere. |
| `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:27-66`; `GetPipelineRunsForProductsQueryHandler.cs:26-64`; `PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs:26-150`; `GetPipelineInsightsQueryHandler.cs:97-117,255-270` | Product-to-pipeline and branch filtering is implemented in several separate paths. Some use hard-coded `refs/heads/main`, others use repository default branch lookup, and build quality additionally supports repository/pipeline narrowing. | Pipeline/build metrics can disagree on which runs are “in scope”, especially for repos whose default branch is not `main` or where definitions were synced before branch data was available. |
| `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsWithValidationQueryHandler.cs:50-110`; `GetMultiIterationBacklogHealthQueryHandler.cs:57-139`; `GetFilteredWorkItemsAdvancedQueryHandler.cs:39-63`; `GetValidationImpactAnalysisQueryHandler.cs:47-71`; `GetProductBacklogStateQueryHandler.cs:47-66`; `GetHealthWorkspaceProductSummaryQueryHandler.cs:47-67` | Product-scoped backlog-root loading is duplicated across the health and validation family. Some paths fall back to profile area paths, some to all products, and some require a route product ID. | The same product selection can yield different work-item universes depending on the endpoint, especially when product root IDs are missing or only partially configured. |
| `PoTool.Api/Handlers/WorkItems/GetWorkItemByIdWithValidationQueryHandler.cs:49-52` | The query accepts `productIds` but explicitly does not enforce them. | UI code can believe it is reading product-scoped data while the server returns any cached work item with that TFS ID. |
| `PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs:33-56`; `PoTool.Api/Handlers/WorkItems/GetFilteredWorkItemsAdvancedQueryHandler.cs:65-133`; `PoTool.Api/Handlers/WorkItems/GetValidationImpactAnalysisQueryHandler.cs:75-87` | Request DTO fields are consumed directly inside handlers and turned into inline LINQ conditions. | Filter semantics are spread across many handlers, so future changes will almost certainly miss one path and reintroduce drift. |
| `PoTool.Api/Services/PortfolioFilterResolutionService.cs:156-183` vs non-portfolio handlers above | Portfolio invalid product selections are normalized to `ALL` with validation metadata. Non-portfolio handlers either intersect silently, ignore invalid filters, or return empty results. | The same user filter can be treated as invalid-but-accepted in one slice and as empty/no-data in another, undermining trust in analytics consistency. |

## 3. Target Architecture

### 3.1 Unified model

The target architecture is:

1. **Filter resolution happens only at the API boundary.**
2. **Shared analytical scope is represented only as `EffectiveFilter`.**
3. **Handlers and services never reinterpret raw request DTO fields for shared scope.**
4. **Slice-specific non-scope options remain explicit, but cannot override or re-resolve shared scope.**

### 3.2 FilterContext lifecycle

1. **Request binding**
   - Controllers bind existing query/route DTOs exactly as they do today.
   - No breaking transport changes are required.

2. **Boundary mapping**
   - A slice boundary mapper converts transport-specific inputs into:
     - `RequestedFilter` for canonical scope dimensions
     - explicit slice-local options for non-canonical toggles or drill-down selectors
   - Examples of non-canonical options:
     - PR repository name
     - pipeline include-canceled/include-partially-succeeded toggles
     - validation category/rule
     - title search and other local finder inputs

3. **Resolution**
   - A slice filter resolver validates and normalizes `RequestedFilter` against:
     - owner product scope
     - team-to-product mappings
     - sprint/date-range derivation rules
     - slice capability rules
   - The output is a single `EffectiveFilter` plus validation metadata.

4. **Propagation**
   - The mediator request sent downstream contains `EffectiveFilter` for scope semantics.
   - Downstream services receive only `EffectiveFilter` for shared scope decisions.
   - Slice-local options may travel separately, but only for non-canonical behavior.

5. **Application**
   - Data loaders, aggregators, and summary services use `EffectiveFilter` only.
   - Any necessary derived scope expansion already happened at the boundary.

6. **Response**
   - Migrated endpoints may echo canonical filter metadata just as portfolio endpoints already do.
   - For legacy transport compatibility, response shapes can stay stable while metadata is added where safe.

### 3.3 EffectiveFilter propagation rules

- `ProductIds`, `TeamIds`, `Time`, `ProjectNumbers`, `WorkPackages`, and `LifecycleStates` are canonical shared scope.
- If a slice supports only a subset of dimensions, unsupported requested dimensions are normalized at the boundary, not downstream.
- If a slice needs derived scope:
  - `TeamId` → product set
  - `SprintId` / `SprintIds` → time window
  - owner scope → permitted product universe
  this derivation must happen once during resolution.

### 3.4 Prohibited patterns

The following patterns should be considered migration blockers:

- Parsing `productIds` or other shared scope values in more than one controller.
- Handlers reading raw request DTO fields to decide shared scope.
- `if ProductIds else if TeamId` precedence logic inside handlers.
- Handlers silently intersecting, ignoring, or replacing invalid scope without validation metadata.
- Slice services deriving sprint windows, product scope, repository scope, or branch scope from raw request DTOs after the boundary step.
- Mixing filtered and unfiltered metrics in the same response, as in the current home-product-bar behavior.

### 3.5 Allowed extension points

- **Boundary mappers** per slice family (`PullRequest`, `Pipeline`, `Delivery`, `Health`).
- **Resolver policies** that define which canonical dimensions a slice supports.
- **Slice-local options objects** for non-canonical toggles and drill-down selectors.
- **Temporary compatibility adapters** that allow legacy query contracts to coexist while a slice is still migrating.
- **Comparison logging** that records legacy-vs-effective filter results during rollout.

## 4. Migration Strategy

### Phase A — Detection and Logging

Goal: observe current semantics without changing results.

- Add boundary-level canonical mapping for one slice at a time.
- Produce `RequestedFilter`, `EffectiveFilter`, and invalid-field metadata in logs.
- Keep legacy handlers fully in control of the returned data.
- Compare:
  - resolved canonical scope
  - legacy scope inferred by the handler
  - key result counts where practical

Exit criteria:

- request-to-effective mapping is understood for the slice
- no ambiguous precedence rules remain undocumented
- legacy/canonical differences are visible in logs before any switch

### Phase B — Dual Path (Old + EffectiveFilter)

Goal: run the canonical path in shadow mode while preserving existing behavior.

- Controllers resolve `EffectiveFilter`.
- Legacy request DTOs continue to be passed for compatibility.
- New adapter code produces the downstream scope from `EffectiveFilter`.
- The legacy and effective-filter paths are compared for:
  - selected entity IDs
  - item counts
  - per-product aggregates
  - time-window selection

Constraints:

- no endpoint contract changes
- no UI changes required
- coexistence is temporary but intentional

Exit criteria:

- slice-specific mismatches are understood
- comparison noise is low enough to trust the effective path

### Phase C — Switch Over

Goal: make `EffectiveFilter` authoritative for the slice.

- Downstream handlers stop reading raw shared-scope fields.
- Shared-scope filtering moves entirely to `EffectiveFilter`.
- Legacy DTO fields remain only as transport inputs at the controller boundary.
- Response metadata is enabled where it helps debugging or future UI behavior.

Exit criteria:

- results match agreed semantics
- no downstream code reinterprets shared request filters
- targeted regression tests cover the migrated slice

### Phase D — Cleanup

Goal: remove duplication and temporary compatibility code.

- Delete controller-local CSV parsing helpers replaced by shared boundary mapping.
- Remove legacy scope branches from handlers and loaders.
- Consolidate repeated owner/team/product/sprint resolution helpers.
- Keep only one authoritative implementation per shared filter concept.

Exit criteria:

- no duplicated shared filter logic remains
- temporary comparison logging is removed or reduced to normal diagnostics
- documentation and tests reflect the canonical path

### Recommended migration order

Safest order based on current coupling:

1. PR family (`metrics`, `insights`, `delivery-insights`, `sprint-trends`, `filter`)
2. Pipeline family (`metrics`, `runs`, `definitions`, `insights`, build-quality pipeline scope)
3. Delivery family (`portfolio-progress-trend`, `capacity-calibration`, `portfolio-delivery`, build-quality rolling/sprint)
4. Sprint family (`sprint-trend`, `sprint-execution`, legacy iteration-path endpoints)
5. Health family (`validated`, `validation-*`, `advanced-filter`, `backlog-state`, `health-summary`, `multi-iteration-health`)

Rationale:

- PR and Pipeline have the most obvious duplicated product/team/time logic.
- Delivery and Sprint share owner/sprint/product semantics and should converge together.
- Health has the most local, legacy-specific filtering behavior and should move after the shared scope rules are stable.

## 5. Risk Analysis

| Area | Risk | Severity | Mitigation |
| --- | --- | --- | --- |
| Delivery | Incorrect filtering risk: owner/product scope is resolved separately in portfolio progress, capacity calibration, portfolio delivery, home product bar, and build-quality flows. Performance impact: dual-path comparison may temporarily duplicate projection or aggregate work. Snapshot/history inconsistency risk: delivery projections, build-quality facts, and home-product rollups can disagree if product scope is normalized differently. UI breakage risk: cards that currently assume owner-wide data, especially home product bar, may change visibly when product scope becomes consistent. | High | Migrate delivery endpoints as a coordinated family; compare product IDs and aggregate totals before cutover; explicitly separate canonical scope from page-local display defaults. |
| Sprint | Incorrect filtering risk: sprint selection is represented as `iterationPath`, `sprintId`, or `sprintIds` depending on the endpoint. Performance impact: resolving sprint windows centrally may add extra lookup work during dual path. Snapshot/history inconsistency risk: some paths are projection-based while others reconstruct history from activity events, so a changed time window can shift both counts and chronology. UI breakage risk: sprint pages may lose or gain items if their legacy iteration matching does not align with the canonical time model. | High | Standardize sprint/time mapping at the boundary first, then compare selected sprint IDs, window boundaries, and work-item counts per page before switching each endpoint. |
| Pipeline | Incorrect filtering risk: pipeline metrics and runs use hard-coded `refs/heads/main`, while pipeline insights and build quality use default-branch-aware filtering. Performance impact: central resolution may widen or narrow candidate definition sets, affecting query volume. Snapshot/history inconsistency risk: the same sprint or time range may include different run populations across endpoints. UI breakage risk: top-trouble pipelines and failure rates can move noticeably once branch scope is unified. | High | Treat branch/default-branch selection as an explicit slice-local option; compare selected run IDs and definition IDs during dual path; migrate pipeline insights and build-quality scope together. |
| PR | Incorrect filtering risk: `TeamId` and date-range semantics differ across metrics, insights, sprint trends, and delivery insights. Performance impact: central resolution may add one shared scope lookup but should reduce repeated handler work over time. Snapshot/history inconsistency risk: sprint-derived PR windows and free date ranges can classify the same PR differently today. UI breakage risk: PR overview and delivery insights may show changed totals or repository breakdowns when team/product scope is normalized. | High | Start with boundary comparison logging for team/product/date resolution; verify PR ID sets, repository buckets, and per-sprint counts before enabling the effective path. |
| Health | Incorrect filtering risk: product-root loading, profile-area fallback, area-path filters, and validation drill-downs all define scope locally. Performance impact: dual-path work can be expensive because some handlers currently load broad work-item sets and then filter in memory. Snapshot/history inconsistency risk: health and validation endpoints can analyze different work-item universes for the same product selection. UI breakage risk: triage counts, validation queues, backlog-state trees, and health summary cards may change once hidden fallback behavior is removed. | High | Migrate health last; first document and compare work-item ID sets per endpoint; enforce product/profile defaults at the boundary; add regression tests around validation queue counts and health summary totals before switch-over. |

## 6. Success Criteria

Migration is complete only when all of the following are true:

- all relevant query families resolve shared filter semantics at the API boundary
- all downstream query handlers and services consume only `EffectiveFilter` for shared scope
- no controller or handler parses or reinterprets shared request filters locally
- product, team, and time semantics are consistent across PR, pipeline, delivery, sprint, and health slices
- invalid shared filter input yields explicit validation metadata instead of silent per-slice behavior changes
- duplicated shared filter logic has been removed
- result comparisons across migrated slices show no unintended regressions

## 7. Implementation Notes for the Future Migration

This report intentionally does **not** implement anything.

The next implementation step should be to choose one slice family and add:

1. a boundary mapper
2. a resolver policy
3. comparison logging
4. a dual-path adapter

without changing public request contracts.

