> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Prompt 0 — BuildQuality Discovery Report

## 1. Solution Overview

| Project | What it currently contains |
| --- | --- |
| `PoTool.Client` | Blazor WebAssembly frontend. It registers generated API clients, client-side services, MudBlazor, SignalR cache-sync progress, browser storage, and the routed Razor pages/workspaces (`PoTool.Client/Program.cs`). |
| `PoTool.Api` | ASP.NET Core Web API host. It wires controllers, source-generated Mediator, EF Core, repositories, sync stages, SignalR broadcasters, read providers, and application services (`PoTool.Api/Program.cs`, `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`). |
| `PoTool.Core` | Application contracts and CQRS definitions. Queries/commands live here, along with interfaces such as `ITfsClient`, repository contracts, feature folders, and validators (`PoTool.Core/PoTool.Core.csproj`, `PoTool.Core/Contracts/ITfsClient.cs`, `PoTool.Core/Pipelines/Queries/GetPipelineInsightsQuery.cs`). |
| `PoTool.Shared` | Shared DTOs and contracts used across client/API/core boundaries. It contains feature DTOs for health, metrics, pipelines, pull requests, settings, planning, and work items (`PoTool.Shared/**/*.cs`, for example `PoTool.Shared/Pipelines/PipelineInsightsDto.cs`, `PoTool.Shared/Health/HealthWorkspaceSummaryDtos.cs`). |
| `PoTool.Integrations.Tfs` | Backend integration with TFS/Azure DevOps via `RealTfsClient`. It contains the REST-based retrieval code for work items, pull requests, pipelines, and verification (`PoTool.Integrations.Tfs/PoTool.Integrations.Tfs.csproj`, `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs`). |
| `PoTool.Core.Domain` | Pure domain library. It contains domain slices and services for CDC, delivery trends, forecasting, effort planning/diagnostics, hierarchy, metrics, portfolio flow, sprints, statistics, and work-item domain rules (`PoTool.Core.Domain/PoTool.Core.Domain.csproj`, `PoTool.Core.Domain/Domain/*`). |
| `PoTool.Tests.Unit` | MSTest unit/audit test project. It references the API, client, core, domain, and integration projects, and already contains document audit tests under `Audits/` (`PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`, `PoTool.Tests.Unit/Audits/*.cs`). |
| `PoTool.Core.Domain.Tests` | MSTest project focused on domain-level rules and pure domain services (`PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`). |

## 2. CDC (Canonical Domain Core)

### Where CDC is implemented

- **Dedicated CDC code entry point:** `PoTool.Core.Domain`, with explicit CDC namespace/code under `PoTool.Core.Domain.Cdc.Sprints` (`PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`).
- **Wider CDC slice map and boundaries:** documented in `docs/architecture/cdc-reference.md` and `docs/architecture/cdc-domain-map.md`.

### Existing CDC slices

The documented CDC slice inventory is:

| Slice | Responsibility |
| --- | --- |
| `BacklogQuality` | Current-state backlog validation, readiness scoring, and implementation readiness without historical sprint reconstruction (`docs/architecture/cdc-reference.md`). |
| `SprintCommitment` | Reconstructs historical sprint commitment, post-commitment scope movement, first-Done completion, spillover, and `SprintFactResult` (`docs/architecture/cdc-reference.md`, `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`). |
| `DeliveryTrends` | Builds sprint delivery projections, progress rollups, progression deltas, and product delivery summaries from sprint facts (`docs/architecture/cdc-reference.md`). |
| `Forecasting` | Owns future-looking forecast semantics derived from historical delivery facts (`docs/architecture/cdc-reference.md`). |
| `EffortDiagnostics` | Owns effort imbalance and concentration-risk formulas (`docs/architecture/cdc-reference.md`). |
| `EffortPlanning` | Owns effort distribution, estimation quality, and effort suggestion formulas (`docs/architecture/cdc-reference.md`). |
| `PortfolioFlow` | Owns stock, inflow, throughput, remaining scope, and completion semantics in story points (`docs/architecture/cdc-reference.md`). |
| `Shared Statistics` | Repository-wide shared math primitives used by CDC slices when the contract is truly shared (`docs/architecture/cdc-reference.md`). |

### Sprint CDC slices verified directly in code

The directly implemented sprint CDC contracts in `PoTool.Core.Domain.Cdc.Sprints` are:

| Slice | Responsibility |
| --- | --- |
| `ISprintCommitmentService` | Computes commitment timestamp, committed IDs, and commitment records (`PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`). |
| `ISprintScopeChangeService` | Detects `SprintScopeAdded` and `SprintScopeRemoved` after commitment (`PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`). |
| `ISprintCompletionService` | Builds first-Done timestamps and sprint completion records (`PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`). |
| `ISprintSpilloverService` | Resolves next sprint path and detects spillover for committed unfinished work (`PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`). |
| `ISprintExecutionMetricsCalculator` | Calculates canonical sprint execution metrics from reconstructed story-point totals (`PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`). |
| `ISprintFactService` | Builds `SprintFactResult` from canonical work items, snapshots, state events, and iteration events (`PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`). |

### Dependency and layering rules

- CDC layering is documented as **source layer -> canonical interpretation layer -> CDC slice layer -> application materialization layer -> consumer layer** (`docs/architecture/cdc-domain-map.md`).
- Explicit dependency rules include:
  - semantics flow upward from source data through core concepts into CDC slices;
  - `SprintCommitment` is upstream of `DeliveryTrends`;
  - `DeliveryTrends` may consume `SprintCommitment` but must not redefine commitment/spillover;
  - `Forecasting` may consume delivery-trend history but must not reconstruct sprint history;
  - `BacklogQuality` remains snapshot-driven and does not depend on historical delivery slices;
  - application/persistence/UI layers consume CDC outputs and must not feed semantics back into the CDC (`docs/architecture/cdc-domain-map.md`).
- The application-side CDC coverage audit also confirms handlers and projection services are expected to consume CDC interfaces instead of legacy helper functions (`docs/analysis/cdc-coverage-audit.md`).

## 3. Data Ingestion

### Builds

- **Exists:** YES.
- **Where implemented:**
  - pipeline definition discovery during sync context build in `PoTool.Api/Services/Sync/SyncPipelineRunner.cs`;
  - pipeline run ingestion in `PoTool.Api/Services/Sync/PipelineSyncStage.cs`;
  - TFS/Azure DevOps retrieval in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs`.
- **Approach:** REST over `HttpClient` through `ITfsClient`/`RealTfsClient`, using Azure DevOps/TFS build/release endpoints. The API code calls `GetPipelineDefinitionsForRepositoryAsync(...)` for discovery and `GetPipelineRunsAsync(...)` for build/release runs.
- **Frequency:** manual/on-demand cache sync via `POST /api/CacheSync/{productOwnerId}/sync` (`PoTool.Api/Controllers/CacheSyncController.cs`). The run sync is:
  - **full** when `PipelineWatermark` is null;
  - **incremental** when `PipelineWatermark` is present and passed as `minStartTime` (`PoTool.Api/Services/Sync/PipelineSyncStage.cs`, `PoTool.Api/Persistence/Entities/ProductOwnerCacheStateEntity.cs`).
- **What is actually ingested:** pipeline definitions and cached pipeline run metadata. The run entity currently stores run ID, name, state/result, created/finished timestamps, branch, source version, URL, and cache timestamp (`PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs`).

### Test Runs

- **Exists:** NOT FOUND.
- **Where implemented:** NOT FOUND.
- **Approach:** NOT FOUND.
- **Frequency:** NOT FOUND.
- **Evidence:** no `TestRun`/`TestResult` entity exists under `PoTool.Api/Persistence/Entities`; `ITfsClient` exposes work items, pull requests, pipelines, and TFS update methods but no test-run retrieval contract; `PoTool.Integrations.Tfs` contains pipeline retrieval but no test-run API retrieval.

### Coverage

- **Exists:** NOT FOUND.
- **Where implemented:** NOT FOUND.
- **Approach:** NOT FOUND.
- **Frequency:** NOT FOUND.
- **Evidence:** no coverage entity exists under `PoTool.Api/Persistence/Entities`; `CachedPipelineRunEntity` has no coverage fields; `ITfsClient` exposes no coverage retrieval contract; `PoTool.Integrations.Tfs` contains no coverage retrieval implementation.

## 4. Storage Model

### Database and entity location

- **Database:** EF Core database in `PoTool.Api/Persistence/PoToolDbContext.cs`.
- **Configured provider:** default local SQLite, with optional SQL Server when `SqlServerConnection` is configured (`PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`).
- **Entities defined in:** `PoTool.Api/Persistence/Entities/*.cs`.

### Are builds/tests/coverage stored already?

- **Builds:** YES.
  - `PipelineDefinitionEntity` stores configured/discovered pipeline definitions (`PoTool.Api/Persistence/PoToolDbContext.cs`).
  - `CachedPipelineRunEntity` stores cached run metadata (`PoTool.Api/Persistence/PoToolDbContext.cs`, `PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs`).
- **Test runs:** NOT FOUND.
- **Coverage:** NOT FOUND.

### Are metrics computed on read or persisted?

- **Both patterns exist.**

Persisted/precomputed:

- `CachedMetricsEntity` stores generic computed metrics per Product Owner (`PoTool.Api/Persistence/Entities/CachedMetricsEntity.cs`).
- `SprintMetricsProjectionEntity` stores pre-computed sprint metrics projections (`PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`).
- `PortfolioFlowProjectionEntity` stores pre-computed portfolio flow projections (`PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs`).
- `MetricsComputeStage` explicitly computes and upserts metrics during sync (`PoTool.Api/Services/Sync/MetricsComputeStage.cs`).

Computed on read:

- `GetPipelineInsightsQueryHandler` computes pipeline insight aggregates from cached runs at query time (`PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`).
- `GetHealthWorkspaceProductSummaryQueryHandler` computes per-product health summaries from current work-item data at query time (`PoTool.Api/Handlers/WorkItems/GetHealthWorkspaceProductSummaryQueryHandler.cs`).

### Existing patterns for snapshots, time-series, and aggregation tables

- **Snapshots:** `RoadmapSnapshotEntity` / `RoadmapSnapshotItemEntity` are point-in-time roadmap captures (`PoTool.Api/Persistence/Entities/RoadmapSnapshotEntity.cs`).
- **Time-series / append-only history:** `ActivityEventLedgerEntryEntity` stores append-only activity events; `CachedPipelineRunEntity` stores one row per cached run (`PoTool.Api/Persistence/Entities/ActivityEventLedgerEntryEntity.cs`, `PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs`).
- **Aggregation/projection tables:** `SprintMetricsProjectionEntity`, `PortfolioFlowProjectionEntity`, and `CachedMetricsEntity` (`PoTool.Api/Persistence/Entities/*.cs` listed above).

## 5. Application Layer

### Patterns used

- **CQRS with source-generated Mediator:** queries/commands are defined in `PoTool.Core`; handlers live in `PoTool.Api`; MediatR is explicitly forbidden by repository rules (`PoTool.Core/Pipelines/Queries/GetPipelineInsightsQuery.cs`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`, `docs/rules/copilot-architecture-contract.md`, `docs/rules/architecture-rules.md`).
- **Services:** both API services and client services are used heavily. API DI registers sync stages, read providers, configuration services, domain services, and validators; client DI registers typed API clients plus frontend orchestration services (`PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`, `PoTool.Client/Program.cs`).
- **Repositories:** repository abstractions exist and are used where needed; the architecture rules explicitly say repositories are optional and direct `DbContext` use is acceptable for simple cases (`docs/rules/architecture-rules.md`, `PoTool.Api/Repositories/*.cs`).

### Where queries, handlers, DTOs, and view models are defined

- **Queries/commands:** `PoTool.Core/{Feature}/Queries` and `PoTool.Core/{Feature}/Commands` (for example `PoTool.Core/Pipelines/Queries/GetPipelineInsightsQuery.cs`, `PoTool.Core/WorkItems/Queries/GetHealthWorkspaceProductSummaryQuery.cs`).
- **Handlers:** `PoTool.Api/Handlers/{Feature}` (for example `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`, `PoTool.Api/Handlers/WorkItems/GetHealthWorkspaceProductSummaryQueryHandler.cs`).
- **DTOs/shared response models:** `PoTool.Shared/{Feature}` (for example `PoTool.Shared/Pipelines/PipelineInsightsDto.cs`, `PoTool.Shared/Health/HealthWorkspaceSummaryDtos.cs`, `PoTool.Shared/Settings/ProductDto.cs`).
- **Client-side view/orchestration models and routes:** `PoTool.Client/Models` and `PoTool.Client/Services` (`PoTool.Client/Models/WorkspaceRoutes.cs`, `PoTool.Client/Services/*.cs`).

### Naming conventions observed

- Query names follow `Get...Query`; handler names follow `...QueryHandler`; DTOs typically end in `Dto`; entities end in `Entity`; routes are centralized in `WorkspaceRoutes`; feature folders are grouped by functional area (`PoTool.Core/*`, `PoTool.Api/Handlers/*`, `PoTool.Shared/*`, `PoTool.Client/Models/WorkspaceRoutes.cs`).
- Repository-wide naming rules are explicitly documented as:
  - PascalCase for public members and types;
  - camelCase for locals/private variables;
  - underscore-prefixed private fields (`docs/rules/architecture-rules.md`).

## 6. Existing Pages

### Health page

- **Route:** `/home/health`
- **Component:** `PoTool.Client/Pages/Home/HealthWorkspace.razor`
- **What it currently shows:**
  - current-state validation signal chips for refinement readiness/completeness and structural integrity;
  - one per-product card for each product on the active profile;
  - each product card loads `HealthWorkspaceProductSummaryDto`, which contains ready story points, features ready in pending epics, and top epics closest to ready (`PoTool.Client/Pages/Home/HealthWorkspace.razor`, `PoTool.Client/Pages/Home/SubComponents/HealthProductSummaryCard.razor`, `PoTool.Shared/Health/HealthWorkspaceSummaryDtos.cs`, `PoTool.Api/Handlers/WorkItems/GetHealthWorkspaceProductSummaryQueryHandler.cs`).
- **Responsibility:** a current-state “Health (Now)” workspace for backlog health and validation triage navigation, not historical trend analysis.

### Delivery page

- **Route:** `/home/delivery`
- **Component:** `PoTool.Client/Pages/Home/DeliveryWorkspace.razor`
- **What it currently shows:**
  - no analytics dataset of its own;
  - navigation tiles to Sprint Delivery (`/home/delivery/sprint`), Portfolio Delivery (`/home/delivery/portfolio`), and Sprint Execution (`/home/delivery/execution`);
  - cross-workspace navigation buttons (`PoTool.Client/Pages/Home/DeliveryWorkspace.razor`, `PoTool.Client/Models/WorkspaceRoutes.cs`).
- **Responsibility:** a workspace hub/router for delivery-focused views.

### Pipeline Insights page

- **Route:** `/home/pipeline-insights`
- **Component:** `PoTool.Client/Pages/Home/PipelineInsights.razor`
- **What it currently shows:**
  - filters for team, sprint, include-partial-success, include-canceled, and optional SLO duration;
  - global summary chips (total builds, failure rate, warning rate, P90 duration);
  - global top-3 troubled pipelines;
  - per-product sections with product-level troubled pipelines, scatter plot of runs, summary chips, and per-pipeline breakdown;
  - explicit note that data comes from the local cache (`PoTool.Client/Pages/Home/PipelineInsights.razor`, `PoTool.Shared/Pipelines/PipelineInsightsDto.cs`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`).
- **Responsibility:** read-only sprint-scoped pipeline stability/health analytics per Product Owner across products, using cached pipeline runs only.

## 7. Cross-cutting Rules

- **UI architecture:** UI is presentational/orchestration only; pages/components must not contain business logic or persistence logic; backend access must go through typed frontend services (`docs/rules/ui-rules.md`).
- **UI library:** MudBlazor is the mandatory UI component library (`docs/rules/ui-rules.md`).
- **Navigation:** intent-driven navigation; workspace navigation should remain contextual and explicit (`docs/rules/ui-rules.md`, `PoTool.Client/Models/WorkspaceRoutes.cs`).
- **UI loading:** pages must render immediately, then load data asynchronously; component-level loading states are required; full-page loading gates are forbidden (`docs/rules/ui-loading-rules.md`).
- **Compact density:** compact wrappers and dense MudBlazor defaults are mandatory (`docs/rules/fluent-ui-compat-rules.md`).
- **Architecture/layering:** Core must not depend on ASP.NET Core/EF/SignalR/HTTP/TFS/UI; only backend accesses TFS; queries live in Core, handlers in Api, UI must not use mediator directly (`docs/rules/copilot-architecture-contract.md`).
- **Repository/persistence rule:** local database is non-canonical and disposable cache storage (`docs/rules/copilot-architecture-contract.md`).
- **EF rule:** ingestion/aggregation flows should follow collect-then-persist; no concurrent EF operations on the same `DbContext` (`docs/rules/ef-rules.md`).
- **Naming:** PascalCase for public members/types, camelCase for locals, underscore-prefixed private fields (`docs/rules/architecture-rules.md`).
- **CDC layering rule:** application, persistence, and UI may consume CDC outputs but must not feed semantics back into CDC slices (`docs/architecture/cdc-domain-map.md`).

## 8. Multi-product Model

- **Does the system support multiple products?** YES.
  - `ProductEntity` is a first-class persisted concept.
  - A product belongs to a Product Owner through `ProductOwnerId`.
  - A product may contain multiple backlog roots (`PoTool.Api/Persistence/Entities/ProductEntity.cs`, `docs/rules/hierarchy-rules.md`).
- **Can a PO see multiple products aggregated?** YES.
  - `/home` exposes an `All Products` selector (`PoTool.Client/Pages/Home/HomePage.razor`).
  - `GetPipelineInsightsQueryHandler` loads **all** products for the active Product Owner and returns global plus per-product sections (`PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`).
  - The Health workspace enumerates all products on the active profile and renders one card per product (`PoTool.Client/Pages/Home/HealthWorkspace.razor`).
- **Where is this handled?**
  - persistence/model: `ProductEntity`, `ProductBacklogRootEntity`, `ProductTeamLinkEntity`;
  - repository/service layer: `ProductRepository.GetProductsByOwnerAsync(...)`, `ProductService.GetProductsByOwnerAsync(...)`;
  - client aggregation/navigation: `HomePage.razor`, `HealthWorkspace.razor`;
  - API aggregation example: `GetPipelineInsightsQueryHandler` (`PoTool.Api/...`, `PoTool.Client/...` files above).

## 9. Gaps for BuildQuality

These are observed gaps only. No implementation proposal is included.

### Missing ingestion capabilities

- Test-run ingestion is **NOT FOUND**.
- Coverage ingestion is **NOT FOUND**.
- Current build ingestion is limited to pipeline definitions plus build/release run metadata; no test/coverage retrieval is attached to the pipeline sync path (`PoTool.Api/Services/Sync/PipelineSyncStage.cs`, `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs`, `PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs`).

### Missing storage structures

- No persisted entity for test runs.
- No persisted entity for coverage snapshots or coverage aggregates.
- No visible link table/model that attaches test or coverage data to `CachedPipelineRunEntity`.

### Missing domain/application concepts

- The documented CDC slice inventory does not list a build/test/coverage slice; the current list is BacklogQuality, SprintCommitment, DeliveryTrends, Forecasting, EffortDiagnostics, EffortPlanning, PortfolioFlow, and Shared Statistics (`docs/architecture/cdc-reference.md`).
- No BuildQuality-specific query/handler/DTO/page name was found during this repository analysis.

## 10. Certainty Assessment

| Section | Assessment | Reason |
| --- | --- | --- |
| 1. Solution Overview | CONFIRMED | Directly verified from `PoTool.sln`, project files, and project contents. |
| 2. CDC | PARTIAL | Sprint CDC implementation is directly verified in code; wider CDC slice inventory and dependency rules are documented in `docs/architecture/cdc-reference.md` and `docs/architecture/cdc-domain-map.md`. |
| 3. Data Ingestion | CONFIRMED | Build ingestion is directly verified in sync/TFS integration code; test-run and coverage ingestion were not found in contracts, integration code, or persistence entities. |
| 4. Storage Model | CONFIRMED | Database configuration, entities, and projection tables are directly verified in API persistence code. |
| 5. Application Layer | CONFIRMED | Query/handler/DTO/repository patterns are directly visible in `PoTool.Core`, `PoTool.Api`, `PoTool.Shared`, and `PoTool.Client`. |
| 6. Existing Pages | CONFIRMED | Routes, components, and page responsibilities are directly visible in the Razor pages and supporting DTO/handler files. |
| 7. Cross-cutting Rules | CONFIRMED | Explicitly documented in repository rule documents. |
| 8. Multi-product Model | CONFIRMED | Directly verified in entities, repositories, client pages, and pipeline aggregation handler. |
| 9. Gaps for BuildQuality | PARTIAL | Gaps are evidence-based from what exists and from what was not found, but they are still derived by comparing the current repository against the requested BuildQuality scope. |
