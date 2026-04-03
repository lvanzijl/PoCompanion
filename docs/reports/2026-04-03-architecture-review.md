# 1. Overall architectural direction
**Rating:** 7/10

## Strengths
- The repository has a recognizable layered direction: `PoTool.Client` depends on `PoTool.Shared`, `PoTool.Api` depends on `PoTool.Core`, `PoTool.Core.Domain`, `PoTool.Shared`, and `PoTool.Integrations.Tfs`, and the domain project itself stays free of EF and HTTP dependencies (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/PoTool.Core.Domain.csproj`).
- The codebase is already moving toward canonical, analytics-oriented architecture through dedicated domain services, projection tables, and guarded external access (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/TfsAccessGateway.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs`).

## Weaknesses
- Problem: The intended layered direction is undermined by central monolithic composition points such as `ApiServiceCollectionExtensions` and `PoToolDbContext`, where many unrelated concerns are wired together in one place instead of through narrower architectural seams.
  - Concrete improvement: Split composition and persistence registration by bounded context so analytics, planning, cache sync, and configuration are composed independently.
  - Rating impact: 7 → 8
- Problem: Transitional architecture is a first-class structural reality: canonical domain services coexist with legacy DTO aliases, adapters, deprecated tables, and redirect layers (`PortfolioProgressTrendDtos`, `HierarchicalToLegacyValidatorAdapter`, deprecated EF entities).
  - Concrete improvement: Run an explicit legacy retirement program with dated removal targets for compatibility aliases, adapters, and deprecated persistence artifacts.
  - Rating impact: 7 → 8
- Problem: Architectural direction is enforced unevenly; there are strong local guardrails for some concerns, but the overall architecture still relies heavily on convention and review discipline rather than broad automated boundary enforcement.
  - Concrete improvement: Add repository-wide architecture tests for layer dependencies, client HTTP access rules, and persistence access placement.
  - Rating impact: 7 → 9

# 2. Layer separation
**Rating:** 6/10

## Strengths
- The project reference graph preserves major layer intent: the client does not reference API or Core, and the core/domain layers are kept reusable from the server and tests (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/PoTool.Core.csproj`).
- `PoTool.Core.Domain` is structurally cleaner than its name alone would suggest; it contains domain services and models without direct `DbContext` or `EntityFrameworkCore` usage in source files.

## Weaknesses
- Problem: API handlers still cross multiple layers directly; for example, `GetSprintMetricsQueryHandler` coordinates repositories, domain services, classification services, and raw EF access in one handler, so application and persistence responsibilities are blurred.
  - Concrete improvement: Move historical loading and persistence access into dedicated application services or read stores so handlers orchestrate only use-case flow.
  - Rating impact: 6 → 7
- Problem: The client layer is not consistently separated from transport concerns because many services call raw `HttpClient` directly (`ReleasePlanningService`, `CacheSyncService`, `ProjectService`, `WorkItemService`) instead of using one typed API access pattern.
  - Concrete improvement: Standardize all client-to-API communication behind generated or hand-authored typed clients and prohibit direct `HttpClient` in feature services.
  - Rating impact: 6 → 8
- Problem: Presentation code still owns some non-trivial semantics, especially in client-side calculation services like `PipelineInsightsCalculator`, which recalculates metrics instead of only rendering server-owned results.
  - Concrete improvement: Move reusable analytical calculations behind backend/domain contracts and leave the client responsible only for presentation mapping.
  - Rating impact: 6 → 8

# 3. Module boundaries
**Rating:** 6/10

## Strengths
- External integration is isolated as a real module: `PoTool.Integrations.Tfs` contains the TFS client, factory, throttling, and request segmentation logic rather than scattering that code through controllers and services.
- `PoTool.Shared` gives the system a clear cross-boundary contract module for DTOs, enums, and shared request/response types.

## Weaknesses
- Problem: The persistence module boundary is weak because `PoToolDbContext` spans planning, cache state, metrics projections, bug triage, pull requests, pipelines, and deprecated release-planning artifacts in one schema root.
  - Concrete improvement: Split persistence into bounded-context DbContexts or at least bounded entity configuration groups with explicit ownership.
  - Rating impact: 6 → 8
- Problem: Module composition is centralized instead of modularized; `ApiServiceCollectionExtensions` wires repositories, sync stages, providers, cache services, domain services, and integration access in a single startup class.
  - Concrete improvement: Introduce per-module registration extensions so each architectural area owns its DI surface.
  - Rating impact: 6 → 7
- Problem: Some module contracts are duplicated across layers, such as client-local request DTOs in `ReleasePlanningService`, which creates a second contract surface outside Shared/generated API contracts.
  - Concrete improvement: Collapse duplicated request/response shapes into canonical shared or generated contracts and remove layer-local copies.
  - Rating impact: 6 → 8

# 4. Canonical data / CDC architecture
**Rating:** 6/10

## Strengths
- The repository has a real canonical-history architecture for sprint analytics: activity events are persisted in `ActivityEventLedgerEntries`, projections are stored explicitly, and sprint CDC services define commitment, scope change, completion, spillover, and fact calculations.
- The code distinguishes between current-state persistence and historical/event-based reconstruction instead of pretending analytics can come from one data source alone (`PoToolDbContext`, `SprintCdcServices`, `GetSprintMetricsQueryHandler`).

## Weaknesses
- Problem: Canonical CDC is strong in sprint analytics but not system-wide; many other features still read directly from cached operational tables without the same canonical event model, so the architecture is partial rather than pervasive.
  - Concrete improvement: Expand the canonical event/projection pattern to the other analytical domains that currently compute from mixed cache tables.
  - Rating impact: 6 → 7
- Problem: Endpoint-specific handlers still own too much reconstruction logic; `GetSprintMetricsQueryHandler` directly queries the ledger, groups events, builds lookups, and mixes orchestration with canonical semantics.
  - Concrete improvement: Extract CDC read pipelines so handlers consume stable projection/query services rather than reassembling historical truth themselves.
  - Rating impact: 6 → 8
- Problem: Canonical contract clarity is diluted by legacy aliases such as `PercentDone`, `TotalScopeEffort`, `AddedEffort`, and `NetFlow` that remain active beside the canonical fields in shared DTOs.
  - Concrete improvement: Version and retire legacy aliases so the canonical vocabulary is singular across API and UI.
  - Rating impact: 6 → 8

# 5. Domain semantics ownership
**Rating:** 6/10

## Strengths
- Canonical business semantics do exist in the right place for several important areas: backlog quality, hierarchy rollups, sprint CDC, forecasting, and delivery trends all have dedicated domain services under `PoTool.Core.Domain`.
- The codebase has started to protect semantics ownership with targeted architecture tests, such as keeping build-quality semantics out of the Blazor client and raw TFS clients behind the gateway boundary.

## Weaknesses
- Problem: Metric semantics leak into the client in services like `PipelineInsightsCalculator`, which calculates success rate, failure rate, MTTR, duration, and flakiness from raw run data in the presentation layer.
  - Concrete improvement: Move client-side metrics calculation into backend/domain services and expose only finalized semantic results to the UI.
  - Rating impact: 6 → 8
- Problem: The legacy validation pipeline still owns part of the meaning through `HierarchicalToLegacyValidatorAdapter`, so hierarchical validation semantics are translated into older issue categories instead of having one canonical result model.
  - Concrete improvement: Replace the adapter bridge with one canonical validation result contract used end-to-end.
  - Rating impact: 6 → 7
- Problem: Some domain semantics are still encoded opportunistically in handlers, such as work-item type interpretation and sprint-scope derivation in `GetSprintMetricsQueryHandler`, rather than being fully encapsulated in domain/application services.
  - Concrete improvement: Push remaining semantic decisions into reusable domain services so handlers stop carrying feature-specific business rules.
  - Rating impact: 6 → 8

# 6. Integration architecture
**Rating:** 7/10

## Strengths
- TFS integration is not sprayed through the application; it is concentrated in a dedicated module with a factory, throttler, request helpers, and a gateway.
- `TfsAccessGateway` and the related architecture test provide real enforcement that production code should not depend directly on `RealTfsClient` or `MockTfsClient`.

## Weaknesses
- Problem: `ITfsClient` is a very broad surface that mixes reads, writes, verification, pipeline access, pull requests, teams, and repository queries, so upstream modules depend on more external-system semantics than necessary.
  - Concrete improvement: Split the gateway contract into smaller capability interfaces aligned to use cases such as work items, pull requests, pipelines, and verification.
  - Rating impact: 7 → 8
- Problem: Integration mode policy is distributed across startup registration, middleware, data-source providers, read-provider factories, and the gateway, which makes the external boundary harder to reason about and change safely.
  - Concrete improvement: Consolidate runtime mode policy behind one integration orchestration module with a smaller public surface.
  - Rating impact: 7 → 8
- Problem: The integration composition root is still embedded inside general API startup code instead of being owned by a dedicated integration registration boundary.
  - Concrete improvement: Move integration registration and configuration validation into a module-specific composition layer.
  - Rating impact: 7 → 8

# 7. Persistence and caching architecture
**Rating:** 5/10

## Strengths
- Persistence has a valuable central safeguard: `PoToolDbContext` validates required relationships before every save, which is stronger than leaving integrity to calling code.
- Cached projections and cache-state entities are explicit, making the system’s analytics and synchronization behavior inspectable rather than hidden in ad hoc memory caches.

## Weaknesses
- Problem: `PoToolDbContext` is a monolithic persistence root with more than forty `DbSet`s plus deprecated entities, which is a maintainability and ownership bottleneck.
  - Concrete improvement: Partition the persistence model into bounded contexts or separate DbContexts for configuration, analytics/projections, planning, and collaboration data.
  - Rating impact: 5 → 7
- Problem: Cache, operational state, UI state, and analytics projections all live in the same relational boundary, so cache lifecycle concerns are tightly coupled to the main application schema.
  - Concrete improvement: Introduce clearer storage segmentation between source snapshots, durable projections, mutable app configuration, and UI-local state.
  - Rating impact: 5 → 7
- Problem: Persistence access patterns are inconsistent: repositories, EF query stores, direct `DbContext` access, and read providers coexist without a clear architectural rule for when each pattern is allowed.
  - Concrete improvement: Define and enforce one persistence access model per concern, such as repositories for writes and query stores for reads, and remove mixed usage from handlers.
  - Rating impact: 5 → 6

# 8. Read/write separation and mode strategy
**Rating:** 6/10

## Strengths
- The application has an explicit read-mode concept with live and cached providers, cache-only analytical routes, and middleware that sets request mode before provider resolution.
- The gateway distinguishes read, mutation, and verification access purposes when delegating to TFS, which is better than a fully implicit integration boundary.

## Weaknesses
- Problem: The mode strategy is route-name driven through `DataSourceModeConfiguration`, so architectural correctness depends on maintaining path classification tables instead of expressing read/write intent in handler contracts.
  - Concrete improvement: Make mode selection an explicit use-case concern through typed query/command pathways rather than path-based middleware classification.
  - Rating impact: 6 → 8
- Problem: `ITfsClient` still collapses reads, writes, and verification into one contract, which weakens command/query separation at the integration boundary even if the gateway labels the calls internally.
  - Concrete improvement: Separate integration contracts by operation mode and inject only the capability each feature needs.
  - Rating impact: 6 → 7
- Problem: Mode concerns bleed across middleware, controllers, providers, and cache-response filters, which makes feature work sensitive to infrastructure policy details.
  - Concrete improvement: Encapsulate mode policy behind a narrower application service so endpoint code no longer has to coordinate cache/live rules indirectly.
  - Rating impact: 6 → 7

# 9. Frontend architecture and page composition
**Rating:** 5/10

## Strengths
- The shell composition is coherent: `MainLayout` centralizes workspace navigation, global filters, onboarding entry points, and startup guarding, which gives pages a shared frame.
- The client is actively consolidating legacy navigation into reusable navigation components and redirects instead of leaving old route flows unmanaged.

## Weaknesses
- Problem: The client service boundary is inconsistent; `Program.cs` registers generated clients and many separate raw-HTTP services, and the UI layer still contains numerous feature transport wrappers.
  - Concrete improvement: Consolidate client data access behind one consistent typed-service pattern and reduce service sprawl in the composition root.
  - Rating impact: 5 → 7
- Problem: Several client services bypass the intended typed contract model with direct `HttpClient` calls and local request DTOs, especially `ReleasePlanningService`, creating transport duplication inside the UI layer.
  - Concrete improvement: Replace direct `HttpClient` feature services with generated or shared contract-based clients.
  - Rating impact: 5 → 7
- Problem: The client still owns some calculation and shaping logic that should be architectural backend concerns, including pipeline metric calculations and backlog-health chart shaping.
  - Concrete improvement: Move analytical transformation into backend/query contracts so pages compose view state from finalized DTOs.
  - Rating impact: 5 → 6

# 10. API and contract design
**Rating:** 6/10

## Strengths
- Controllers are generally thin enough to delegate meaningful work to Mediator handlers and dedicated services, and shared DTO contract filters show active concern for API consistency.
- Shared contracts are broad and explicit, giving the frontend and tests a common DTO vocabulary instead of reaching into server entities.

## Weaknesses
- Problem: Controller error handling is often broad and string-based; actions in `MetricsController` catch `Exception`, log it, and return generic `500` strings instead of structured error contracts.
  - Concrete improvement: Standardize API failures through shared result/error DTOs and centralized exception mapping middleware.
  - Rating impact: 6 → 7
- Problem: Request preprocessing and response wrapping are repeated across endpoints via multiple filter-resolution services and helper calls, which adds contract ceremony without one consistent endpoint pipeline.
  - Concrete improvement: Introduce reusable endpoint pipeline components for filter resolution, cache-state wrapping, and validation responses.
  - Rating impact: 6 → 7
- Problem: Contract evolution is additive and transitional rather than cleanly versioned; legacy aliases remain active in shared DTOs, so consumers see multiple names for the same concept.
  - Concrete improvement: Define a formal deprecation/versioning policy and remove obsolete aliases once replacement clients are migrated.
  - Rating impact: 6 → 8

# 11. Observability and diagnostics
**Rating:** 5/10

## Strengths
- The backend uses structured logging in important architectural seams such as data-source middleware, the TFS gateway, and handlers, which gives operators some insight into route/mode decisions and integration access.
- There are explicit diagnostics-oriented capabilities for TFS verification, sync progress broadcasting, and cache-state reporting instead of relying only on generic logs.

## Weaknesses
- Problem: End-to-end correlation is incomplete; the client registers a correlation service, but diagnostic flow across browser, API, gateway, and SignalR is not consistently visible, and `MainLayout` still falls back to `Console.WriteLine`.
  - Concrete improvement: Implement consistent correlation ID propagation and structured client logging across all request and hub flows.
  - Rating impact: 5 → 6
- Problem: Diagnostics are fragmented by feature area—cache state, TFS verification, health, and ledger validation each expose their own model without one operational diagnostics contract.
  - Concrete improvement: Create a unified diagnostics/readiness surface that aggregates integration, cache, sync, and query health consistently.
  - Rating impact: 5 → 6
- Problem: There is little visible architectural instrumentation for query cost, sync stage duration, or projection freshness beyond logs and ad hoc DTOs.
  - Concrete improvement: Add explicit metrics/tracing around expensive reads, sync stages, and projection generation.
  - Rating impact: 5 → 7

# 12. Testability of the architecture
**Rating:** 7/10

## Strengths
- The solution has substantial automated coverage split between unit/domain tests and broader unit/integration-style tests, and it avoids live TFS by using mocks and recorded payloads.
- The repository already contains architecture governance tests for key boundaries, which is stronger than relying on human review alone.

## Weaknesses
- Problem: Architecture tests are selective rather than comprehensive; TFS boundary and build-quality ownership are guarded, but the broader layer model is not enforced with the same rigor.
  - Concrete improvement: Add broad architecture tests for project references, client HTTP access, direct `DbContext` use in handlers, and Shared contract ownership.
  - Rating impact: 7 → 8
- Problem: Handlers that directly depend on `PoToolDbContext` are inherently harder to test in isolation than handlers that depend on read/write abstractions.
  - Concrete improvement: Move EF access behind query stores or application services so handler tests stay lightweight and focused on orchestration.
  - Rating impact: 7 → 8
- Problem: Client services that handcraft HTTP calls and local DTOs are harder to contract-test and easier to drift from the API than generated typed clients.
  - Concrete improvement: Standardize the client API layer so test doubles and contract verification work through one access pattern.
  - Rating impact: 7 → 8

# 13. Change resilience / maintainability
**Rating:** 5/10

## Strengths
- The multi-project structure gives the codebase some natural containment; client, API, domain, shared contracts, and integration are at least separated into reviewable units.
- Navigation, route catalog, and some shared UI/filter components reduce duplication pressure compared with fully page-local composition.

## Weaknesses
- Problem: Transitional coexistence is a real maintenance tax: compatibility aliases, deprecated tables, adapters, and legacy redirects increase the number of places that must stay semantically aligned.
  - Concrete improvement: Remove superseded architecture in batches and stop extending legacy compatibility surfaces.
  - Rating impact: 5 → 7
- Problem: High-churn files such as `Program.cs`, `ApiServiceCollectionExtensions.cs`, and `PoToolDbContext.cs` act as merge magnets and make unrelated changes collide.
  - Concrete improvement: Break up composition, persistence, and registration into smaller module-owned files.
  - Rating impact: 5 → 6
- Problem: Repeated patterns such as multiple filter-resolution services, multiple raw-HTTP client wrappers, and repeated response-shaping logic increase the cost of cross-cutting changes.
  - Concrete improvement: Extract shared query/filter and transport abstractions so one change updates one implementation path.
  - Rating impact: 5 → 7

# 14. Strategic architecture fitness
**Rating:** 6/10

## Strengths
- The architecture is capable of supporting additional analytical features because it already has durable projections, CDC-oriented domain services, and an isolated external-system module.
- The repository is close enough to a clean modular monolith that it can still improve without a full rewrite if the current seams are tightened.

## Weaknesses
- Problem: The single API host, single relational persistence root, and single large DbContext will make independent scaling of sync, analytics, planning, and collaboration workloads increasingly difficult.
  - Concrete improvement: Introduce bounded operational slices with separate persistence and composition ownership before additional feature growth hardens the monolith further.
  - Rating impact: 6 → 8
- Problem: The system is strongly TFS-shaped; the wide `ITfsClient` contract and broad gateway surface make future source diversification or major API version shifts more expensive than they need to be.
  - Concrete improvement: Refactor integration behind capability-focused abstractions that reflect internal use cases rather than raw external-system breadth.
  - Rating impact: 6 → 7
- Problem: Architectural fitness currently depends more on documentation, discipline, and targeted tests than on pervasive compile-time or runtime boundary enforcement.
  - Concrete improvement: Add automated boundary enforcement for transport, persistence, and module ownership so the architecture resists future erosion.
  - Rating impact: 6 → 8

# Conclusion
**Overall architecture rating:** 6/10

## Top 5 highest-impact improvements across the system
- **Split the monolithic persistence model into bounded contexts**
  - Why it matters: `PoToolDbContext` is the largest architectural bottleneck and currently collapses unrelated domains into one persistence root.
  - Cross-cutting impact: Improves overall architectural direction, module boundaries, persistence and caching architecture, testability, change resilience, and strategic architecture fitness.
  - Estimated rating gain: +1.0 overall
- **Standardize all client-to-API traffic behind typed contracts and eliminate raw feature-level `HttpClient` usage**
  - Why it matters: The frontend currently mixes generated clients with many raw HTTP wrappers, which weakens layer separation and duplicates transport contracts.
  - Cross-cutting impact: Improves layer separation, module boundaries, frontend architecture, API and contract design, and testability.
  - Estimated rating gain: +0.8 overall
- **Finish the canonical contract migration and retire legacy aliases, adapters, and deprecated persistence artifacts**
  - Why it matters: Transitional architecture is the main source of semantic drift and maintenance overhead in the current codebase.
  - Cross-cutting impact: Improves overall architectural direction, canonical data/CDC architecture, domain semantics ownership, API and contract design, and change resilience.
  - Estimated rating gain: +0.8 overall
- **Move handler-owned reconstruction and semantic logic into reusable application/domain read pipelines**
  - Why it matters: Endpoint-specific orchestration currently carries too much historical reconstruction and business interpretation, especially in analytical handlers.
  - Cross-cutting impact: Improves layer separation, canonical data/CDC architecture, domain semantics ownership, API and contract design, and testability.
  - Estimated rating gain: +0.7 overall
- **Replace route-driven data mode governance with explicit query/command capability boundaries**
  - Why it matters: The current cache/live strategy works, but it is path-classification driven and leaks infrastructure policy into multiple layers.
  - Cross-cutting impact: Improves integration architecture, read/write separation and mode strategy, observability and diagnostics, change resilience, and strategic architecture fitness.
  - Estimated rating gain: +0.7 overall
