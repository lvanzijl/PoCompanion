# PoTool Global Filtering — Decision-Grade Analysis

This document upgrades the existing filter inventory into a decision-grade system design baseline for a unified filtering system.
It is based on verified current code paths, current API contracts, and existing repository analysis documents.

Primary verified sources:

- `PoTool.Client/Pages/Home/WorkspaceBase.cs:13-106`
- `PoTool.Client/Pages/Home/HomePage.razor:43-93,599-740`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor:41-123,528-588,760-795`
- `PoTool.Client/Pages/Home/DeliveryTrends.razor:68-129,423-621`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:53-210,694-820`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor:51-165,571-665`
- `PoTool.Client/Pages/Home/SprintExecution.razor:40-109,548-617`
- `PoTool.Client/Pages/Home/PrOverview.razor:70-126,689-849`
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor:71-109,811-942`
- `PoTool.Client/Pages/Home/PipelineInsights.razor:40-152,642-758`
- `PoTool.Client/Pages/Home/BugOverview.razor:65-105,317-437`
- `PoTool.Client/Pages/BugsTriage.razor:81-121,193-220`
- `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor:43-59,88-120,135-162`
- `PoTool.Api/Controllers/MetricsController.cs:28-53,247-445,691-888`
- `PoTool.Api/Controllers/PullRequestsController.cs:298-360`
- `PoTool.Api/Controllers/PipelinesController.cs:151-176`
- `PoTool.Api/Controllers/WorkItemsController.cs:125-220,863-910`
- `PoTool.Api/Controllers/BuildQualityController.cs:22-62`
- `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs:1-119`
- `PoTool.Api/Services/PortfolioReadModelFiltering.cs:6-45`
- `docs/analyze/filtering.md:1-200`
- `docs/domain/rules/hierarchy_rules.md:38-46`

---

## 1. Canonical Filter Definitions

### 1.1 Canonical model

The current codebase does **not** have one canonical client filter model. It has:

1. **Route/query-based workspace context** via `WorkspaceBase` for `productId` and `teamId` only (`WorkspaceBase.cs:24-80`)
2. **Local component state** for sprint, sprint range, date range, repository, toggles, and search fields (`docs/analyze/filtering.md:47-68`)
3. **Portfolio read-model filters** already defined in backend DTO/query contracts via `PortfolioReadQueryOptions` (`PortfolioConsumptionDtos.cs:103-119`)

The canonical target model implied by current backend contracts is:

```text
FilterSystem
├── GlobalContext
│   ├── ProductId?
│   └── TeamId?
├── WorkspaceContext
│   └── TimeContext
│       ├── SprintId?
│       ├── SprintRange? (FromSprintId, ToSprintId)
│       └── DateRange? (FromDate, ToDate)
├── PortfolioContext
│   ├── ProjectNumber?
│   ├── WorkPackage?
│   └── LifecycleState?
└── PageContext
    ├── RepositoryName?
    ├── ValidationCategory?
    ├── ValidationRuleId?
    ├── IncludePartiallySucceeded?
    ├── IncludeCanceled?
    ├── SloDurationMinutes?
    ├── TagFilters[]
    └── SearchText?
```

### 1.2 Canonical filter table

| Normalized filter | Domain meaning | Data source | Cardinality | Scope | Persistence | Backend support | Current usage |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Product** | Primary analytics boundary; one product can aggregate multiple backlog roots | `ProductDto`, `HomeProductBarMetricsDto`, `PortfolioReadQueryOptions.ProductId`; product-scoped queries and work-item/product services | Single-select | Global | Conditional | Partial | Home, Health Overview, Backlog Health, Validation pages, Delivery Trends, Portfolio Progress, Sprint Execution, Plan Board |
| **Team** | Team scope used either directly for PR/pipeline queries or indirectly to load sprint lists | `TeamDto`; `WorkspaceBase.TeamId`; `GetPullRequestInsightsQuery.TeamId`; team-owned sprint loading via `SprintService` | Single-select | Global candidate / workspace practical | Conditional | Partial | Trends workspace, Delivery Trends, Sprint Execution, Portfolio Delivery, PR Overview, PR Delivery Insights, Pipeline Insights, Bug Insights |
| **Sprint** | Single sprint time context for one delivery/trends view | `SprintDto`; `GetSprintExecutionQuery.SprintId`; `GetPipelineInsightsQuery.SprintId`; `GetPrDeliveryInsightsQuery.SprintId` | Single-select | Workspace | Conditional | Partial | Sprint Delivery, Sprint Execution, PR Overview, PR Delivery Insights, Pipeline Insights |
| **Sprint Range** | Ordered multi-sprint horizon for aggregate/trend analysis | `SprintDto`; `GetPortfolioDeliveryQuery.SprintIds`; `GetPortfolioProgressTrendQuery.SprintIds`; `GetSprintTrendMetricsQuery.SprintIds` | Multi-select (ordered contiguous range) | Workspace | Conditional | Partial | Delivery Trends, Portfolio Delivery, Portfolio Progress, Trends workspace |
| **Date Range** | Arbitrary time horizon independent of sprint identity | `GetPullRequestInsightsQuery.FromDate/ToDate`; `GetPrDeliveryInsightsQuery.FromDate/ToDate`; `BuildQualityController.GetRolling(...)` | Single range pair | Workspace / page | Conditional | Full for PR/build-quality pages; none for bug insights | PR Overview, PR Delivery Insights, Health Overview (fixed), Bug Insights (fixed local) |
| **Project** | Portfolio project identifier for persisted portfolio read models | `PortfolioReadQueryOptions.ProjectNumber`; `PortfolioSnapshotItemDto.ProjectNumber`; `PortfolioComparisonItemDto.ProjectNumber` | Single-select | Workspace (portfolio) | Conditional | Full in `/api/portfolio/*`; none in sprint-driven endpoints | No current UI page uses it; backend already supports it |
| **Package** | Portfolio work-package identifier for persisted portfolio read models | `PortfolioReadQueryOptions.WorkPackage`; `PortfolioSnapshotItemDto.WorkPackage` | Single-select | Workspace (portfolio) | Conditional | Full in `/api/portfolio/*`; none in sprint-driven endpoints | No current UI page uses it; backend already supports it |
| **State** | Portfolio lifecycle slice today; broader work-item state filtering is not standardized | `PortfolioReadQueryOptions.LifecycleState`; `PortfolioLifecycleState` enum | Single-select | Workspace (portfolio) / page-local elsewhere | Conditional | Partial | No current UI page uses portfolio lifecycle state; Validation Fix and Bug Detail only display item state |
| **Repository** | Repository scope for pull-request analysis | `PullRequestInsightsDto`; `GetPullRequestInsightsQuery.RepositoryName` | Single-select | Page | Local | Full on PR Overview; none elsewhere | PR Overview |
| **Validation Category** | Structural/refinement category drill-down key | `ValidationQueueDto`, `ValidationFixSessionDto`, `GetValidationQueueQuery(category)` | Single-select | Workspace (Health) | Local | Full | Validation Queue, Validation Fix |
| **Validation Rule** | Specific validation rule within a category | `ValidationFixSessionDto`, `GetValidationFixSessionQuery(ruleId, category, ...)` | Single-select | Page | Local | Full | Validation Fix |
| **Include Partially Succeeded** | Include partially-succeeded builds in pipeline metrics | `GetPipelineInsightsQuery.IncludePartiallySucceeded` | Single toggle | Page | Local | Full | Pipeline Insights |
| **Include Canceled** | Include canceled builds in pipeline metrics | `GetPipelineInsightsQuery.IncludeCanceled` | Single toggle | Page | Local | Full | Pipeline Insights |
| **SLO Duration Minutes** | Pure visualization threshold line for pipeline scatter | Local field `_sloDurationMinutes` only | Single numeric | Page | Local | None | Pipeline Insights |
| **Tag Filter** | Triage-tag subset for bug triage workflow | `TriageTagDto`, client-side bug set in `BugsTriage` | Multi-select | Page | Local | None | Bugs Triage |
| **Tag Match Mode** | Whether selected tags are matched using Any vs All semantics | Local enum `TagMatchMode` in `BugsTriage` | Single-select | Page | Local | None | Bugs Triage |
| **Search Text** | Client-side narrowing text for local collections | `ProductRoadmapEditor._searchText`; `WorkItemExplorer._filterText` | Single text | Page | Local | None | Product Roadmap Editor, Work Item Explorer |

### 1.3 Canonical decisions

1. **Product is the strongest verified global filter.** The domain model explicitly defines product as the primary analytics boundary (`hierarchy_rules.md:38-46`).
2. **Team is the strongest secondary shared filter, but not yet truly global.** It is reused heavily, yet not consistently persisted by `WorkspaceBase`.
3. **Time is not one filter; it is a family of filters.** The codebase currently mixes single sprint, sprint range, date range, and fixed windows.
4. **Project / Package / State are already canonical in the portfolio backend, but not in the current UI.**
5. **Repository, validation drill-down, pipeline toggles, tags, and search fields are page-local by nature.**

---

## 2. Applicability Matrix

Definitions:

- **REQUIRED** — the page needs this filter dimension to support its primary use case
- **OPTIONAL** — the page can benefit from the filter, but it is not essential to the page’s core purpose
- **NOT APPLICABLE** — the page should not participate in that filter dimension

| Page | Product | Project | Package | State | Time | Other |
| --- | --- | --- | --- | --- | --- | --- |
| Home | REQUIRED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | Product Owner profile context REQUIRED |
| Health workspace | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | none |
| Health Overview | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Fixed rolling window |
| Backlog Health | REQUIRED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | none |
| Validation Triage | REQUIRED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | Validation category drill-down |
| Validation Queue | REQUIRED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | Validation category REQUIRED |
| Validation Fix | REQUIRED | NOT APPLICABLE | NOT APPLICABLE | OPTIONAL | NOT APPLICABLE | Validation category REQUIRED; validation rule REQUIRED |
| Delivery workspace | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | OPTIONAL | none |
| Sprint Delivery | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | none |
| Sprint Activity | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Work item route context REQUIRED |
| Sprint Execution | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | OPTIONAL | REQUIRED | Team REQUIRED |
| Portfolio Delivery | OPTIONAL | OPTIONAL | OPTIONAL | OPTIONAL | REQUIRED | Team REQUIRED |
| Trends workspace | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Team REQUIRED |
| Delivery Trends | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Team REQUIRED |
| Portfolio Progress | OPTIONAL | OPTIONAL | OPTIONAL | OPTIONAL | REQUIRED | Team REQUIRED |
| PR Overview | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Team OPTIONAL; repository OPTIONAL |
| PR Delivery Insights | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Team OPTIONAL |
| Pipeline Insights | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Team OPTIONAL; partial/canceled/SLO OPTIONAL |
| Bug Insights | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | OPTIONAL | OPTIONAL | Team OPTIONAL |
| Bug Detail | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | OPTIONAL | NOT APPLICABLE | bugId REQUIRED |
| Planning workspace | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | none |
| Plan Board | REQUIRED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | OPTIONAL | none |
| Product Roadmaps | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | multi-product overview by design |
| Product Roadmap Editor | REQUIRED | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | search OPTIONAL |
| Dependency Overview | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | none |
| Home Changes | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | REQUIRED | Sync window REQUIRED |
| Bugs Triage | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | tag filters REQUIRED |
| Work Item Explorer | OPTIONAL | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | NOT APPLICABLE | validation categories/search REQUIRED |

---

## 3. Global Filter Persistence

| Filter | Persist across pages | Exact rule |
| --- | --- | --- |
| Product | Conditional | Persist across Home, Health, Delivery, Trends, and Planning pages **except** pages whose primary purpose is explicitly multi-product (`Product Roadmaps`) or profile-wide triage/explorer flows that do not currently accept product scope. |
| Team | Conditional | Persist inside Delivery and Trends workspaces and into pages that load sprints from a selected team. Clear when entering Health or Planning pages unless the destination explicitly consumes `teamId`. |
| Sprint | Conditional | Persist only while the user stays in a workspace family using a single-sprint context and the selected team remains unchanged. Clear if team changes or if destination page uses sprint range/date range instead of single sprint. |
| Sprint Range | Conditional | Persist across aggregate delivery/trends pages that use contiguous sprint ranges. Clear when moving to single-sprint pages or fixed-window pages. |
| Date Range | Conditional | Persist only across pages that natively accept arbitrary `fromDate` / `toDate` values. Do not infer date range from sprint pages unless the page already does so explicitly. |
| Project | Conditional | Persist only inside portfolio read-model pages once those filters are surfaced in the UI. Do not carry into sprint-driven delivery/trends pages. |
| Package | Conditional | Same rule as Project: portfolio-read pages only. |
| State | Conditional | Persist only where the canonical state filter means `PortfolioLifecycleState`. Do not reuse the same persisted value for bug/work-item raw state until the domain meaning is standardized. |
| Repository | No | Page-local to PR Overview. Clear on navigation away from PR pages. |
| Validation Category | No | Route-local within Health drill-down flow only. |
| Validation Rule | No | Page-local to Validation Fix only. |
| Include Partially Succeeded | No | Persist only for the current Pipeline Insights page instance. |
| Include Canceled | No | Persist only for the current Pipeline Insights page instance. |
| SLO Duration Minutes | No | Visualization-only, page-local. |
| Tag Filter | No | Page-local to Bugs Triage. |
| Tag Match Mode | No | Page-local to Bugs Triage. |
| Search Text | No | Page-local to each local collection view. |

### Persistence decisions

1. **Only Product is near-global today.**
2. **Team and Time must be persisted conditionally, not universally.**
3. **Project / Package / State must not be promoted globally until there is a UI that consumes the existing `/api/portfolio/*` contracts.**
4. **Page-local toggles and searches should never leak into cross-page state.**

---

## 4. Filter Conflicts

| Filter | Conflicting behaviors | Pages involved | Required resolution approach |
| --- | --- | --- | --- |
| Time | Some pages use a single sprint, others use sprint range, others use arbitrary date range, and others use fixed non-editable windows | Sprint Delivery, Sprint Execution, Portfolio Delivery, Delivery Trends, Portfolio Progress, PR Overview, PR Delivery Insights, Health Overview, Bug Insights, Home Changes | Define `TimeContext` as a discriminated concept: `SingleSprint`, `SprintRange`, `DateRange`, or `FixedWindow`. Never coerce these silently. |
| Team vs Product | Product is propagated by Home and `WorkspaceBase`; Team is only partially propagated and often only used to load sprint lists rather than to filter backend data directly | Home, Trends workspace, Delivery Trends, Sprint Execution, Portfolio Delivery, PR Overview, PR Delivery Insights, Pipeline Insights | Define precedence: Product is global context; Team is conditional workspace context. If both are set and conflict, destination page must either reconcile through mapped products or prompt reset. |
| Sprint vs Date Range | PR pages derive date ranges from a selected sprint but do not expose date controls consistently; same conceptual filter is represented differently | PR Overview, PR Delivery Insights | Treat sprint selection as a date-range generator, not as a separate canonical backend dimension on PR pages. Preserve both the derived range and the originating sprint metadata. |
| Team used as scope vs Team used as loader | Several pages use Team only to fetch sprint lists, while the actual endpoint takes only sprint IDs; other pages actually pass Team into the backend query | Portfolio Delivery, Delivery Trends, Portfolio Progress vs PR Overview / PR Delivery Insights | Document Team’s page role explicitly. Where Team is only a sprint-loader, do not present it as a guaranteed backend scope filter. |
| Project/Package/State availability | Portfolio read-model endpoints support these filters, but the active portfolio trend page uses sprint-driven metrics endpoints that do not | Portfolio Progress vs `/api/portfolio/*` endpoints | Separate portfolio-read filters from sprint-driven filters. Do not expose Project/Package/State on sprint-driven pages until the backend contract is aligned. |
| Multi-product page purpose vs global product persistence | Some pages exist specifically to compare all products and would be harmed by forced single-product carry-over | Product Roadmaps, potentially Bugs Triage and Home Changes | Add opt-out behavior: pages flagged as multi-product overview ignore persisted Product unless they explicitly add support for narrowing. |

### Highest-priority conflicts

1. **Time conflict is the primary system risk.**
2. **Team/Product precedence is the second-largest consistency issue.**
3. **Portfolio read-model filters vs sprint-driven filters is the most important backend/UI contract mismatch.**

---

## 5. Backend Gaps

| Page | Filter | Endpoint | Missing parameters | Missing DTO fields | Query / service impacted |
| --- | --- | --- | --- | --- | --- |
| Bug Insights | Product, Team, State, Time | `GET /api/workitems` (`WorkItemsController.cs:32-45`) | `productIds[]`, `teamId`, `fromDate`, `toDate`, `state` | No dedicated `BugInsightsDto`; no server-side severity distribution / opened / resolved / resolution-time payload | `GetAllWorkItemsQuery`; likely requires new `GetBugInsightsQuery` and a bug insights aggregation service |
| Portfolio Delivery | Product, Project, Package, State | `GET /api/metrics/portfolio-delivery` (`MetricsController.cs:807-831`) | `productIds[]`, `projectNumber`, `workPackage`, `lifecycleState` | No applied-filter metadata or filtered-count fields comparable to `PortfolioProgressDto` | `GetPortfolioDeliveryQuery` and its handler/service |
| Portfolio Progress | Project, Package, State | `GET /api/metrics/portfolio-progress-trend` (`MetricsController.cs:735-759`) | `projectNumber`, `workPackage`, `lifecycleState`, `includeArchivedSnapshots`, `snapshotCount` | `PortfolioProgressTrendDto` has no explicit applied-filter metadata for portfolio-read dimensions | `GetPortfolioProgressTrendQuery` and its handler/service |
| Sprint Execution | State, multi-product scope | `GET /api/metrics/sprint-execution` (`MetricsController.cs:868-888`) | `productIds[]` (plural), `stateClassification` or equivalent canonical state filter | No filtered-count / applied-state metadata in `SprintExecutionDto` | `GetSprintExecutionQuery` and its handler |
| PR Overview | Product | `GET /api/pullrequests/insights` (`PullRequestsController.cs:307-325`) | `productId` or `productIds[]` | No applied-product metadata in `PullRequestInsightsDto` | `GetPullRequestInsightsQuery` and handler |
| PR Delivery Insights | Product, Repository | `GET /api/pullrequests/delivery-insights` (`PullRequestsController.cs:343-360`) | `productId` or `productIds[]`, `repositoryName` | No applied repository/product metadata in `PrDeliveryInsightsDto` | `GetPrDeliveryInsightsQuery` and handler |
| Pipeline Insights | Product, Repository, Pipeline | `GET /api/pipelines/insights` (`PipelinesController.cs:156-170`) | `productId` or `productIds[]`, `repositoryId`, `pipelineDefinitionId`, optionally `teamId` if team should be first-class backend scope | `PipelineInsightsDto` does not expose applied filter metadata or repository catalog for page-level filtering | `GetPipelineInsightsQuery` and handler |
| Home Changes | Product, Team | Change-summary endpoint used by `CacheSyncService` / `HomeChanges.razor:65-71` | product/team scope parameters are not exposed in the current summary flow | No product- or team-sliced change summary DTO | Cache sync change-summary query/service/controller path |

### Exact observations behind the gap map

1. **Portfolio read-model support already exists** for `ProductId`, `ProjectNumber`, `WorkPackage`, and `LifecycleState` in `PortfolioReadQueryOptions` (`PortfolioConsumptionDtos.cs:107-119`) and in `PortfolioReadModelFiltering.Apply(...)` (`PortfolioReadModelFiltering.cs:6-45`).
2. **Sprint-driven metrics endpoints do not mirror that support.**
3. **PR endpoints are date-range capable but not product-capable.**
4. **Bug Insights is the clearest server-side gap because it still loads all work items and filters entirely in the client.**

---

## 6. Filter UI Layering

| Filter | UI layer | Rationale |
| --- | --- | --- |
| Product | Primary | Core cross-page context; already visible on Home and repeatedly required elsewhere |
| Team | Primary | High-frequency workspace filter, especially for Delivery and Trends pages |
| Sprint | Primary | First-class time selector for single-sprint pages |
| Sprint Range | Primary | Core context for trend and aggregate delivery pages |
| Date Range | Advanced | Needed on some pages, but less frequent than sprint-based selection in the current UX |
| Project | Advanced | Only relevant to portfolio read-model pages |
| Package | Advanced | Only relevant to portfolio read-model pages |
| State | Advanced | Meaning differs by page family; should not be primary until standardized |
| Repository | Advanced | Important on PR pages, but not part of system-wide primary context |
| Validation Category | Primary | Core to Validation Queue navigation |
| Validation Rule | Primary | Core to Validation Fix navigation |
| Include Partially Succeeded | Advanced | Operational nuance for pipeline analysis |
| Include Canceled | Advanced | Operational nuance for pipeline analysis |
| SLO Duration Minutes | Advanced | Visualization-only tuning parameter |
| Tag Filter | Primary | Main working control on Bugs Triage |
| Tag Match Mode | Advanced | Secondary triage refinement control |
| Search Text | Advanced | Local refinement control, not shared context |

### Collapsed summary behavior

The following filters should appear in a **collapsed summary strip/chip row** when they are active:

- Product
- Team
- Sprint or Sprint Range
- Date Range
- Project
- Package
- State
- Repository

The following filters should **not** appear in global collapsed summaries because they are page-local working controls:

- Validation Category / Rule
- Pipeline toggles
- SLO
- Tag filters
- Search text

---

## 7. Filter System Model

### Global Filters

These are the filters that should form the **core shared context**:

- **Product**
  - Persisted conditionally across most analytical pages
  - Primary business context
- **Team**
  - Shared context candidate for Delivery and Trends families
  - Persisted conditionally, not universally

### Workspace Filters

These are shared within a workspace family, but not across the whole application:

- **Sprint**
- **Sprint Range**
- **Date Range**
- **Project** (portfolio-read pages only)
- **Package** (portfolio-read pages only)
- **State** (portfolio lifecycle state only, once surfaced)
- **Validation Category** (Health drill-down flow)

### Page Filters

These should remain local and should not be promoted into shared application-wide context:

- **Repository**
- **Validation Rule**
- **Include Partially Succeeded**
- **Include Canceled**
- **SLO Duration Minutes**
- **Tag Filter**
- **Tag Match Mode**
- **Search Text**

### Final system decisions

1. **Canonical global context = Product + Team.**
2. **Canonical workspace context = Time family.**
3. **Canonical portfolio advanced context = Project + Package + State.**
4. **Canonical page context = repository, validation drill-down, operational toggles, tags, search.**
5. **The main implementation blocker is not frontend structure alone; it is the mismatch between sprint-driven endpoints and richer portfolio read-model filters.**

---

## 8. Feasibility Summary

### Frontend-feasible now

- Unify Product and Team state handling
- Add a shared summary strip / primary filter bar pattern
- Standardize sprint vs sprint-range persistence behavior
- Keep page-only filters isolated from global state

### Backend changes required before a full unified system is possible

- Add product/team/time/state scope to Bug Insights
- Add product/project/package/state support to `portfolio-delivery`
- Add portfolio-read filter support to `portfolio-progress-trend` or switch the page to `/api/portfolio/*`
- Add product/repository scope to PR and pipeline insights endpoints where needed

This is the current feasible structure that fits the codebase without assuming behavior that is not already implemented.
