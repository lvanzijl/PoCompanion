# Code Quality Audit Report

**Date:** 2026-02-08
**Scope:** PoTool.Api, PoTool.Core, PoTool.Client, PoTool.Shared, PoTool.Integrations.Tfs
**Excludes:** Generated code (ApiClient.g.cs), EF Migrations, Test projects

---

## Executive Summary

1. **Largest non-generated file is BattleshipMockDataFacade.cs (1 544 LOC)** — a mock-data orchestrator that combines generation, validation, and persistence. High risk of accidental complexity leaking into production paths.
2. **RealTfsClient is split across 5 partial-class files totalling ~3 900 LOC** and exposes an ITfsClient interface with **35 async methods**. This is a violation of the Interface Segregation Principle and makes the TFS integration layer brittle.
3. **ReleasePlanningController (748 LOC, ~40 endpoint attributes)** and **WorkItemsController (671 LOC, ~42 endpoint attributes)** are the largest controllers — both far exceed reasonable controller size, mixing routing, mediation, and inline logic.
4. **Two boundary violations detected:** the API layer references `PoTool.Client.Services` (line 16 of `ApiServiceCollectionExtensions.cs`), and `PoTool.Integrations.Tfs` has an unnecessary `Microsoft.EntityFrameworkCore` package reference.
5. **Duplicate GetColor logic** exists in both `PoTool.Core/WorkItems/WorkItemType.cs` and `PoTool.Client/Models/WorkItemTypeHelper.cs` — identical switch expressions maintained separately.
6. **8 handlers catch generic `Exception`** with near-identical log-and-return-empty patterns — a cross-cutting concern better handled by a pipeline behavior.
7. **MapToDto / MapToEntity methods are repeated per-repository** (6+ repositories) without a shared mapping convention. Each is a private static method, preventing reuse and increasing maintenance cost.
8. **DataSourceMode (Live vs Cache) creates a pervasive behavioral split** affecting middleware, providers, and repositories. The mode resolution logic is scattered across 4+ files.
9. **TfsConfigEntity in PoTool.Shared** is misnamed — it is a DTO (no ORM mapping), but the "Entity" suffix suggests a persistence class.
10. **GetMultiIterationBacklogHealthQueryHandler (505 LOC)** is the largest single handler, combining data loading, metric calculation, trend analysis, and DTO construction in one method chain.

---

## 1. Hotspots

### 1a. Top Methods by Complexity (estimated cyclomatic complexity / nesting depth)

| # | File | Class | Method | LOC | Control-flow stmts | Risk |
|---|------|-------|--------|-----|---------------------|------|
| 1 | `PoTool.Api/Services/MockData/BattleshipWorkItemGenerator.cs` | BattleshipWorkItemGenerator | `GenerateHierarchy()` | ~218 | 18 (6 nested loops, 3 if, 1 switch) | Deep nesting makes logic hard to follow; any change risks breaking hierarchy consistency |
| 2 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | RealTfsClient | `VerifyWorkItemHierarchyAsync()` | ~147 | 14 (10 if/else, 2 foreach, 1 while) | Complex verification path with mixed concerns (HTTP + parsing + reporting) |
| 3 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | RealTfsClient | `VerifyWorkItemUpdateAsync()` | ~127 | 9 (7 if/else, 1 try/catch) | Large verification method performing create + update + delete checks |
| 4 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | RealTfsClient | `VerifyPipelinesAsync()` | ~122 | 12 (8 if/else, 2 try/catch) | Pipeline verification mixed with HTTP concerns |
| 5 | `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` | Handler | `Handle()` | ~92 | 9 (6 if, 1 foreach) | Orchestration + filtering + calculation in one method |
| 6 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | RealTfsClient | `GetFailureGuidance()` | ~81 | 5 (1 switch with many arms) | Large pattern-match; acceptable if stable |
| 7 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | RealTfsClient | `VerifyCapabilitiesAsync()` | ~78 | 8 (4 if/else, 9 method calls) | Orchestration + conditional write-checks |
| 8 | `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` | Handler | `GenerateSuggestion()` | ~71 | 5 (2 if, LINQ chains) | Metric logic inlined in handler |
| 9 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | RealTfsClient | `VerifyWorkItemFieldsAsync()` | ~65 | 6 (5 if/else, 1 try/catch) | Field validation mixed with HTTP |
| 10 | `PoTool.Client/Services/NavigationContextService.cs` | NavigationContextService | `FromQueryString()` | ~53 | 8 (6 if, 1 try/catch) | Many conditional parameter extractions |

### 1b. Top Classes by Dependency Count (Constructor Parameters)

| # | File | Class | Params | Dependencies |
|---|------|-------|--------|-------------|
| 1 | `PoTool.Integrations.Tfs/Clients/RealRevisionTfsClient.cs` | RealRevisionTfsClient | 6 | IHttpClientFactory, ITfsConfigurationService, ILogger, TfsRequestThrottler, TfsRequestSender, RevisionIngestionDiagnostics? |
| 2 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` | RealTfsClient | 5 | IHttpClientFactory, ITfsConfigurationService, ILogger, TfsRequestThrottler, TfsRequestSender |
| 3 | `PoTool.Integrations.Tfs/Clients/TfsRequestThrottler.cs` | TfsRequestThrottler | 4 | ILogger, int readConcurrency, int writeConcurrency, RevisionIngestionDiagnostics? |
| 4 | `PoTool.Api/Services/RevisionIngestionService.cs` | RevisionIngestionService | 4 | IServiceScopeFactory, ILogger, RevisionIngestionDiagnostics, TfsRequestThrottler |
| 5 | `PoTool.Client/Services/WorkItemService.cs` | WorkItemService | 3 | IWorkItemsClient, HttpClient, WorkItemLoadCoordinatorService |
| 6 | `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs` | DataSourceAwareReadProviderFactory | 3 | IServiceProvider, IDataSourceModeProvider, ILogger |
| 7 | `PoTool.Api/Services/Sync/WorkItemSyncStage.cs` | WorkItemSyncStage | 3 | ITfsClient, PoToolDbContext, ILogger |
| 8 | `PoTool.Api/Services/Sync/PipelineSyncStage.cs` | PipelineSyncStage | 3 | ITfsClient, PoToolDbContext, ILogger |

### 1c. Top Files by LOC (non-generated, non-migration)

| # | File | LOC | Risk |
|---|------|-----|------|
| 1 | `PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs` | 1 544 | God-class orchestrator for mock data |
| 2 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | 1 047 | Largest partial-class segment; mixed concerns |
| 3 | `PoTool.Api/Services/MockTfsClient.cs` | 876 | Large mock implementation mirroring ITfsClient |
| 4 | `PoTool.Integrations.Tfs/Clients/RealRevisionTfsClient.cs` | 841 | Complex revision fetching with retry/paging |
| 5 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs` | 836 | Pipeline queries with parsing |
| 6 | `PoTool.Api/Controllers/ReleasePlanningController.cs` | 748 | Controller with ~40 endpoints |
| 7 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs` | 728 | Work item mutation logic |
| 8 | `PoTool.Client/Services/TreeBuilderService.cs` | 673 | Tree construction logic |
| 9 | `PoTool.Api/Controllers/WorkItemsController.cs` | 671 | Controller with ~42 endpoints |
| 10 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs` | 658 | Work item read logic |

### 1d. File Churn

Git history contains only 2 commits (initial + one PR), so meaningful churn data is unavailable. Churn analysis is omitted per the issue instructions.

---

## 2. Responsibility & Cohesion

### BattleshipMockDataFacade (1 544 LOC)
- **Responsibilities:** (1) Orchestrates mock data generation, (2) Calls individual generators, (3) Validates generated data, (4) Persists mock data to DB.
- **Mixed concerns:** Orchestration + validation + persistence. The facade does too much; generators should be independently testable.
- **Split point:** Extract a `MockDataPersistenceService` and let the facade focus only on orchestration.

### RealTfsClient (5 partial files, ~3 900 LOC total)
- **Responsibilities:** (1) HTTP communication with TFS, (2) Response parsing/mapping, (3) Configuration verification, (4) Work item CRUD, (5) Pipeline queries, (6) Team/sprint queries.
- **Mixed concerns:** Verification logic (1 047 LOC) mixes HTTP calls with result interpretation and user-facing guidance text. Work item update logic mixes JSON patch construction with HTTP sending.
- **Split point:** Extract `TfsConnectionVerifier` from Verification.cs. Consider splitting ITfsClient into role-specific interfaces (ISP): `ITfsWorkItemReader`, `ITfsWorkItemWriter`, `ITfsPipelineClient`, `ITfsTeamClient`.

### ReleasePlanningController (748 LOC, ~40 endpoints)
- **Responsibilities:** (1) Lane CRUD, (2) Card CRUD, (3) Validation, (4) Epic splitting, (5) Board queries.
- **Mixed concerns:** Too many resource types in one controller. Each resource (Lane, Card, Board, Validation) could be its own controller.
- **Split point:** Extract `ReleasePlanningLanesController`, `ReleasePlanningCardsController`.

### GetMultiIterationBacklogHealthQueryHandler (505 LOC)
- **Responsibilities:** (1) Load work items, (2) Compute health metrics per iteration, (3) Compute trends across iterations, (4) Build response DTO.
- **Mixed concerns:** Data loading, metric calculation, and DTO construction in one handler.
- **Split point:** Extract metric calculation into a `BacklogHealthCalculator` service.

### GetEffortEstimationSuggestionsQueryHandler (328 LOC)
- **Responsibilities:** (1) Load historical data, (2) Compute similarity scores, (3) Generate effort suggestions.
- **Mixed concerns:** Data retrieval and algorithm in one class.
- **Split point:** Extract `EffortEstimationAlgorithm` into Core layer.

### BugTriageStateService (406 LOC)
- **Responsibilities:** (1) Read triage state from DB, (2) Mark bugs as triaged, (3) Call TFS to update severity/tags, (4) Update local cache.
- **Mixed concerns:** DB reads + TFS writes + cache updates in one service.
- **Split point:** Separate read operations from write/sync operations.

---

## 3. Layering & Boundary Violations

| # | Violation | Location | Direction | Severity | Suggested Fix |
|---|-----------|----------|-----------|----------|---------------|
| 1 | API references Client layer | `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:16` — `using PoTool.Client.Services;` | Api → Client (wrong direction) | **High** | Move the referenced service interface to `PoTool.Shared` or `PoTool.Core`, or register the client service from the Client project's own DI extension method. |
| 2 | Integrations.Tfs references EF Core | `PoTool.Integrations.Tfs/PoTool.Integrations.Tfs.csproj:18` — `Microsoft.EntityFrameworkCore` package | Integrations → Infrastructure (unwanted coupling) | **Medium** | Remove the EF Core package reference. If any type requires EF annotations, move that type to `PoTool.Api`. |
| 3 | ITfsClient has 35 methods | `PoTool.Core/Contracts/ITfsClient.cs` | Core exposes a fat interface | **Medium** | Split into role-specific interfaces per ISP. |
| 4 | Duplicate GetColor logic across layers | `PoTool.Core/WorkItems/WorkItemType.cs:33` and `PoTool.Client/Models/WorkItemTypeHelper.cs:35` | Core ↔ Client drift | **Low** | Client should reference Core's implementation via `PoTool.Shared` or use the Core version directly. |
| 5 | TfsConfigEntity named as Entity in Shared | `PoTool.Shared/Settings/TfsConfigEntity.cs:5` | Naming confusion | **Low** | Rename to `TfsConfigDto` to match its actual role as a data-transfer object. |

---

## 4. Duplication & Drift

### 4a. MapToDto / MapToEntity Duplication
**Evidence:** 6+ repositories each implement private static `MapToDto` and/or `MapToEntity` methods:
- `ProductRepository.cs` (MapToDto, MapToEntity)
- `TeamRepository.cs` (MapToDto)
- `WorkItemRepository.cs` (MapToDto, MapToEntity)
- `PullRequestRepository.cs` (8 mapping methods)
- `PipelineRepository.cs` (MapToDto)
- `CacheStateRepository.cs` (MapToDto)

**Impact:** Each mapping is independent, making property additions error-prone. No compile-time guarantee that mappings stay in sync with entity/DTO changes.

### 4b. Generic Exception Handling in Handlers
**Evidence:** 8 handlers use identical `catch (Exception ex) { _logger.LogError(ex, "..."); return <empty>; }` pattern:
- `GetMultiIterationBacklogHealthQueryHandler.cs`
- `GetSprintMetricsQueryHandler.cs`
- `GetSprintTrendMetricsQueryHandler.cs`
- `RefreshValidationCacheCommandHandler.cs`
- `SplitEpicCommandHandler.cs`
- `GetAreaPathsFromTfsQueryHandler.cs`
- `GetGoalsFromTfsQueryHandler.cs`
- `ValidateWorkItemQueryHandler.cs`

**Impact:** Cross-cutting concern duplicated in business logic. Changing error-handling policy requires touching every handler.

### 4c. WorkItemType.GetColor Duplication
**Evidence:**
- `PoTool.Core/WorkItems/WorkItemType.cs:33` — `GetColor(string type)` switch expression with 8 cases
- `PoTool.Client/Models/WorkItemTypeHelper.cs:35` — `GetColor(string type)` with overlapping cases

**Impact:** Risk of color drift between server and client if one is updated without the other.

### 4d. Upsert Logic Divergence
**Evidence:**
- `PullRequestRepository.cs` uses `existingIterations.FirstOrDefault(...)` pattern to separate update/insert lists
- `WorkItemRepository.cs` uses `existingIds.Contains(...)` pattern for the same purpose

**Impact:** Two different implementations of the same logical operation. One may have edge-case bugs the other doesn't.

---

## 5. Control Flow & Configuration Flags

| # | Location | Flag/Mode | Behavior Change | Suggestion |
|---|----------|-----------|-----------------|------------|
| 1 | `DataSourceMode` enum (Core) | `Live` vs `Cache` | Affects middleware, providers, repositories — pervasive behavioral split across 4+ files | Consider strategy pattern: `IDataSourceStrategy` with `LiveStrategy` / `CacheStrategy` implementations. Reduce scattered `if (mode == Cache)` checks. |
| 2 | `ApiApplicationBuilderExtensions.cs:39-126` | `isDevelopment` bool + database provider detection | 5-level nested conditional for migration/seeding strategy | Extract to `DatabaseInitializationStrategy` with `Development` / `Production` implementations. |
| 3 | `RealTfsClient.Verification.cs` | `includeWriteChecks` bool | Controls whether write verification runs during capability check | Acceptable — simple guard. No change needed. |
| 4 | `TeamRepository.cs` | `bool isArchived` | Archive/unarchive toggle | Acceptable — simple state toggle. No change needed. |
| 5 | `RevisionIngestionDiagnostics.cs` | `bool isBackfill` | Changes diagnostic logging behavior | Acceptable — simple diagnostic flag. No change needed. |

---

## 6. Naming & API Surface

| # | Issue | Location | Suggestion |
|---|-------|----------|------------|
| 1 | `TfsConfigEntity` is not an EF entity | `PoTool.Shared/Settings/TfsConfigEntity.cs` | Rename to `TfsConfigDto` |
| 2 | `MockTfsClient` (876 LOC) mirrors full ITfsClient | `PoTool.Api/Services/MockTfsClient.cs` | If ITfsClient is split, MockTfsClient shrinks proportionally |
| 3 | `BugTriageStateService` mixes queries and commands | `PoTool.Api/Services/BugTriageStateService.cs` | Split into `BugTriageQueryService` / `BugTriageCommandService` or use CQRS handlers |
| 4 | `WorkItemResolutionService` name is vague | `PoTool.Api/Services/WorkItemResolutionService.cs` | Consider `WorkItemHierarchyTracker` or similar — the class tracks first-seen state and parent chains |
| 5 | `DataSourceAwareReadProviderFactory` is a long name | `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs` | Consider `ReadProviderFactory` — context makes the "data source aware" part obvious |
| 6 | Controllers with 40+ endpoints | `ReleasePlanningController`, `WorkItemsController` | Split by resource or sub-domain (e.g., `LaneController`, `CardController`) |

---

## Top 10 Refactor Candidates (Ranked by Impact vs. Risk)

| Rank | Candidate | Impact | Risk | Effort |
|------|-----------|--------|------|--------|
| 1 | Extract error-handling pipeline behavior for handlers | High (8 handlers, cross-cutting) | Low (additive, no logic change) | Small |
| 2 | Remove `using PoTool.Client.Services` from Api layer | High (architecture violation) | Low (single line + possible interface move) | Small |
| 3 | Remove EF Core reference from Integrations.Tfs | Medium (unnecessary coupling) | Low (may require moving a type) | Small |
| 4 | Consolidate `GetColor` into single shared location | Medium (prevents drift) | Low (delete duplicate, update references) | Small |
| 5 | Split ReleasePlanningController into resource-specific controllers | Medium (readability, testability) | Medium (route changes, client regeneration) | Medium |
| 6 | Split ITfsClient into role-specific interfaces | High (ISP, testability) | Medium (wide surface change) | Medium |
| 7 | Extract metric calculation from large handlers into Core services | Medium (testability, SRP) | Medium (new classes, handler rewiring) | Medium |
| 8 | Introduce shared upsert pattern for repositories | Medium (consistency) | Low (internal refactor) | Small |
| 9 | Extract database initialization strategy from ApiApplicationBuilderExtensions | Medium (readability) | Medium (startup path) | Medium |
| 10 | Rename TfsConfigEntity to TfsConfigDto | Low (clarity) | Low (find-replace) | Trivial |

---

## "Do Not Touch Yet" List

| Area | Reason |
|------|--------|
| `BattleshipMockDataFacade.cs` (1 544 LOC) | Mock-data only; no production impact. Refactoring gains are low relative to effort. |
| `RealRevisionTfsClient.cs` (841 LOC) | Recently added (revision ingestion feature). Active development area — let it stabilize before refactoring. |
| `TreeBuilderService.cs` (673 LOC) | Client-side tree construction. Complex but isolated. Needs usage analysis before refactoring. |
| `PoToolDbContext.cs` (647 LOC) | EF context with many DbSets. Size is inherent to the domain model count. |
| `MockTfsClient.cs` (876 LOC) | Will shrink automatically if ITfsClient is split. No independent refactor needed. |
| Per-repository MapToDto/MapToEntity | While duplicated, each mapping is simple and co-located with its entity. Introducing a mapping framework (e.g., AutoMapper) has its own costs. Prefer shared conventions over a new dependency. |
