# CONTEXT PACK — PO Companion Repository

**Generated**: 2026-01-17  
**Purpose**: Single authoritative context document for AI-assisted prompt generation

---

# 0) Repo Snapshot

## Repo Purpose
PO Companion (Product Owner Tool) is a desktop application for managing Azure DevOps/TFS work items using a hierarchical product backlog model (Goal → Objective → Epic → Feature → PBI → Task). The tool provides read-heavy TFS integration with local SQLite caching, real-time SignalR sync, and visualization features including backlog health, dependency graphs, PR insights, and release planning.

## Tech Stack Summary
- **Language**: C# 10.0, .NET 10.0
- **Frontend**: Blazor WebAssembly + MudBlazor 8.0 (open-source UI components only)
- **Backend**: ASP.NET Core Web API 10.0 + SignalR
- **Persistence**: SQLite (local cache) via EF Core 10.0
- **Mediator**: Source-generated Mediator (v2.1.7) - MediatR is forbidden
- **DI**: Microsoft.Extensions.DependencyInjection only
- **TFS Integration**: Azure DevOps Server REST API (api-version=7.0)
- **Authentication**: NTLM (on-prem) or PAT (client-side only)
- **Testing**: MSTest (unit), Reqnroll (integration), bUnit (Blazor components)
- **Build**: .NET SDK CLI, `TreatWarningsAsErrors=true`

## How to Build/Test/Run
```bash
# Build
dotnet build PoTool.sln

# Run API
cd PoTool.Api
dotnet run  # Defaults to http://localhost:5291

# Run Client (development)
cd PoTool.Client
dotnet run

# Run Tests
dotnet test PoTool.Tests.Unit
dotnet test PoTool.Tests.Integration
dotnet test PoTool.Tests.Blazor
```

**Database**: SQLite created automatically at `potool.db` via `EnsureCreated()` on startup.

---

# 1) Repository Map

## Projects

### PoTool.Shared
- **Path**: `PoTool.Shared/`
- **Responsibility**: Data contracts (DTOs) that cross assembly boundaries. Leaf assembly with no dependencies.
- **Entry Points**: None (library)
- **Key Types**: `WorkItemDto`, `PullRequestDto`, `PipelineDto`, `TfsVerificationReport`, `HtmlSanitizationHelper`
- **Dependencies**: HtmlSanitizer (8.1.870)
- **Root Namespace**: `PoTool.Shared`

### PoTool.Core
- **Path**: `PoTool.Core/`
- **Responsibility**: Domain logic, business rules, query/command definitions (Mediator), interfaces for TFS and repositories.
- **Entry Points**: None (library)
- **Key Abstractions**: `ITfsClient`, `IWorkItemRepository`, `IPullRequestRepository`, `IEfConcurrencyGate`, validator interfaces
- **Key Types**: Query/Command classes in `WorkItems/Queries/`, `WorkItems/Commands/`, `WorkItemType`, `WorkItemHierarchyHelper`
- **Dependencies**: Mediator.Abstractions (2.1.7), PoTool.Shared
- **Root Namespace**: `PoTool.Core`

### PoTool.Api
- **Path**: `PoTool.Api/`
- **Responsibility**: ASP.NET Core backend with controllers, handlers, EF Core persistence, TFS client implementations (RealTfsClient, MockTfsClient), SignalR hubs.
- **Entry Points**: `Program.cs` (Kestrel host on http://localhost:5291)
- **Key Services**: `RealTfsClient`, `MockTfsClient`, `WorkItemSyncService`, `TfsConfigurationService`, `TfsAuthenticationProvider`
- **Controllers**: `WorkItemsController`, `PullRequestsController`, `PipelinesController`, `SettingsController`, `ProfilesController`, `ProductsController`, `TeamsController`
- **Persistence**: `PoToolDbContext` (EF Core), entities in `Persistence/Entities/`
- **Dependencies**: EF Core (Sqlite/SqlServer), NSwag.AspNetCore, Mediator.SourceGenerator, PoTool.Core, PoTool.Shared, PoTool.Client (for hosting)
- **Root Namespace**: `PoTool.Api`

### PoTool.Client
- **Path**: `PoTool.Client/`
- **Responsibility**: Blazor WebAssembly frontend (RCL), pages, components, client services (HTTP wrappers around NSwag-generated clients), browser storage.
- **Entry Points**: `Program.cs` (WebAssembly host), `App.razor`
- **Key Components**: `WorkItemExplorer.razor`, `TfsConfig.razor`, `ProductHome.razor`, `PRInsight.razor`, Compact/* (reusable compact UI)
- **Services**: `WorkItemService`, `PullRequestService`, `ProfileService`, `TfsConfigService`, `OnboardingService`, `IPreferencesService` (browser storage), `ISecureStorageService` (browser local/session storage)
- **Dependencies**: MudBlazor (8.0), SignalR.Client, NSwag.MSBuild (codegen), PoTool.Shared (no reference to Core)
- **Root Namespace**: `PoTool.Client`
- **NSwag**: Generates API clients from OpenAPI on build (Debug or `GenerateApiClient=true`)

### PoTool.Tests.Unit
- **Path**: `PoTool.Tests.Unit/`
- **Responsibility**: MSTest unit tests for Core and Api logic (mocked infrastructure).
- **Key Tests**: `RealTfsClientVerificationTests`, `WorkItemHierarchyRetrievalTests`, `WorkItemSelectionServiceTests`, `OnboardingServiceTests`

### PoTool.Tests.Integration
- **Path**: `PoTool.Tests.Integration/`
- **Responsibility**: Reqnroll integration tests (BDD-style scenarios).

### PoTool.Tests.Blazor
- **Path**: `PoTool.Tests.Blazor/`
- **Responsibility**: bUnit tests for Blazor components.

## Namespace Strategy
- **Root**: `PoTool.<ProjectName>`
- **Shared**: `PoTool.Shared.<Domain>` (e.g., `PoTool.Shared.WorkItems`, `PoTool.Shared.PullRequests`)
- **Core**: `PoTool.Core.<Domain>` with subfolders `Queries/`, `Commands/`, `Validators/`
- **Api**: `PoTool.Api.<Area>` (e.g., `PoTool.Api.Controllers`, `PoTool.Api.Services`, `PoTool.Api.Persistence`)
- **Client**: `PoTool.Client.<Area>` (e.g., `PoTool.Client.Pages`, `PoTool.Client.Components`, `PoTool.Client.Services`)

---

# 2) Architecture Patterns In Use

## Layering
**Strict 4-layer architecture**:
```
PoTool.Client (UI)
    ↓ (HTTP/SignalR only)
PoTool.Api (Backend + Handlers)
    ↓
PoTool.Core (Domain + Mediator Contracts)
    ↓
PoTool.Shared (DTOs - leaf assembly)
```

**Rules** (`docs/ARCHITECTURE_RULES.md` §232-381):
- Client → Shared only (no Core reference)
- Api → Core + Shared
- Core → Shared only (infrastructure-free)
- Shared → nothing

**Forbidden**: Client calling Core directly, Core depending on EF/HTTP/TFS.

## Dependency Direction Rules
- **Inward**: Dependencies point toward Core and Shared.
- **Core is infrastructure-agnostic**: No ASP.NET, EF Core, SignalR, HTTP, TFS APIs in Core.
- **Client communicates via HTTP**: Uses NSwag-generated clients (`IWorkItemsClient`, `ISettingsClient`, etc.) to call API.
- **Api implements Core interfaces**: `RealTfsClient : ITfsClient`, `WorkItemRepository : IWorkItemRepository`.

## DI Patterns and Composition Roots
- **Api Composition Root**: `Configuration/ApiServiceCollectionExtensions.AddPoToolApiServices()`
  - Scoped: DbContext, repositories, TFS client
  - Singleton: `TfsRequestThrottler`, `TfsAuthenticationProvider`
  - Transient: Mediator handlers
- **Client Composition Root**: `PoTool.Client/Program.cs`
  - Scoped: HttpClient, HubConnection, NSwag clients, client services, browser storage
- **Lifetime Rules** (`docs/EF_RULES.md` §99-107):
  - DbContext: Scoped only
  - Hosted services: Use `IDbContextFactory<T>` or `IServiceScopeFactory`

## Error Handling Strategy
- **Core**: Domain methods return `Result<T>` (forbidden: throwing exceptions for business rule violations).
- **Api**: Controllers map `Result<T>` to HTTP status codes:
  - Success → 200 OK / 201 Created / 204 No Content
  - Validation failure → 400 Bad Request
  - Not found → 404
  - Unexpected → 500
- **Client**: Services catch HTTP exceptions and surface user-friendly messages via `ErrorMessageService`.
- **TFS Integration**: `ITfsClient` methods catch and categorize errors (auth, connectivity, validation, server-side).

## Logging/Telemetry Patterns
- Not explicitly standardized; controllers and services use `ILogger<T>`.
- **Sensitive data**: MUST NOT be logged (credentials, PATs, TFS URLs with tokens).
- **TFS diagnostics**: `TfsVerificationReport` provides structured error categorization (see `TFS_INTEGRATION_RULES.md` §126-243).

## Async/Concurrency Patterns and Rules
- **All public APIs are async**: `Task<T>` or `ValueTask<T>`.
- **Client-side**: Async-first lifecycle (`OnInitializedAsync`), no `.Result` or `.Wait()` in `PoTool.Client` (enforced by process rules).
- **EF Concurrency** (`docs/EF_RULES.md`):
  - **Hard Rule**: No EF operation may run concurrently on the same DbContext instance.
  - **Mandatory Two-Phase Pattern**:
    1. **Phase 1 (Parallel)**: HTTP/network calls, CPU-bound, no EF.
    2. **Phase 2 (Sequential)**: Fully awaited EF operations, no `Task.WhenAll`, one atomic `SaveChangesAsync`.
  - **Forbidden**: EF inside `Task.WhenAll`, `Parallel.ForEach`, throttlers, async `Select(...)`.
  - **DbContext lifetime**: Scoped only. Singletons use `IDbContextFactory`.

## Serialization, Mapping, Validation Patterns
- **Serialization**: System.Text.Json (ASP.NET Core default).
- **Mapping**: Manual mapping in handlers (Core → DTOs). No AutoMapper.
- **Validation**: FluentValidation for commands/queries (not yet fully implemented; see `ARCHITECTURE_RULES.md` §73-83).
- **Sanitization**: `HtmlSanitizationHelper` in Shared for XSS prevention (`docs/SECURITY_HTML_SANITIZATION.md`).

## Caching Patterns
- **Local SQLite cache**: Non-canonical. Cached data is disposable; loss MUST NOT break functionality.
- **Invalidation**: Manual via "Pull & Cache" button or background sync (`WorkItemSyncService`).
- **No distributed caching**: Single-user desktop app.

## Background Jobs / Schedulers
- **WorkItemSyncService**: Hosted service stub for periodic TFS sync (not fully implemented).
- **No job scheduler**: Background work via ASP.NET Core hosted services.

---

# 3) Functional Domain Model (Repo Terminology)

## Glossary (Canonical Terms)
- **Goal**: Top-level strategic objective (portfolio level). No parent.
- **Objective**: Major capability beneath a Goal (program level).
- **Epic**: Large feature beneath an Objective. **Determines team ownership** via area path.
- **Feature**: Specific functionality beneath an Epic. Inherits area path from Epic.
- **PBI (Product Backlog Item)**: User story beneath a Feature. Inherits area path.
- **Bug**: Defect beneath a Feature. Inherits area path.
- **Task**: Implementation work beneath PBI/Bug. Inherits area path.
- **Area Path**: TFS hierarchy defining team ownership. Format: `\<Project>\<Program>\<Team>`.
- **Iteration Path**: TFS sprint/iteration. Format: `\<Project>\<Sprint>`.
- **PAT (Personal Access Token)**: TFS authentication token. **MUST** be stored client-side only (see `PAT_STORAGE_BEST_PRACTICES.md`).
- **Mock Data**: Battleship incident handling theme (see `docs/mock-data-rules.md`).

## Core Entities/Aggregates/Value Objects
### WorkItemDto (Shared)
- **Fields**: `Id`, `Title`, `Type` (enum), `State`, `AreaPath`, `IterationPath`, `ParentId`, `Effort`, `AssignedTo`, `Tags`, `Description`, `Url`, `WorkItemLinks` (dependencies)
- **Immutable**: DTOs are records.
- **Relationships**: Parent-child via `ParentId`, dependencies via `WorkItemLinks`.

### WorkItemEntity (Api/Persistence/Entities)
- **EF Core entity**: Mutable, tracked by DbContext.
- **Concurrency**: `RowVersion` for optimistic concurrency.
- **Mapping**: Manual mapping from `WorkItemDto` to `WorkItemEntity` in handlers.

### PullRequestDto (Shared)
- **Fields**: `Id`, `Title`, `Status`, `CreatedDate`, `CreatedBy`, `SourceBranch`, `TargetBranch`, `RepositoryName`, `WorkItemIds`, `Reviewers`, `Comments`
- **Related**: `PullRequestIterationDto`, `PullRequestCommentDto`, `PullRequestFileChangeDto`

### TfsVerificationReport (Shared)
- **Purpose**: Diagnostic report from `ITfsClient.VerifyCapabilitiesAsync()`.
- **Fields**: List of capability checks with `CapabilityId`, `ImpactedFunctionality`, `ExpectedBehavior`, `ObservedBehavior`, `FailureCategory`, `RawEvidence`, `LikelyCauses`, `ResolutionGuidance`.

## Key Workflows (User-Visible → Code)

### "User pulls work items from TFS"
1. **UI**: `TfsConfig.razor` → User clicks "Pull & Cache" button
2. **Client**: `WorkItemService.SyncWorkItemsAsync()` → HTTP POST `/api/workitems/sync`
3. **Api**: `WorkItemsController.SyncWorkItems()` → Sends Mediator command `SyncWorkItemsCommand`
4. **Core**: `SyncWorkItemsCommandHandler` → Calls `ITfsClient.GetWorkItemsAsync(areaPath)`
5. **Api**: `RealTfsClient` → WIQL query to TFS REST API (`api-version=7.0`)
6. **Persistence**: Handler maps `WorkItemDto[]` → `WorkItemEntity[]` → `DbContext.SaveChangesAsync()`
7. **SignalR**: `WorkItemHub` broadcasts sync progress to connected clients
8. **UI**: `WorkItemExplorer.razor` refreshes tree view

### "User views backlog health"
1. **UI**: `BacklogHealth.razor` → `OnInitializedAsync()`
2. **Client**: `BacklogHealthCalculationService.CalculateHealthAsync()` → HTTP GET `/api/healthcalculation`
3. **Api**: `HealthCalculationController.GetBacklogHealth()` → Mediator query `GetBacklogHealthQuery`
4. **Core**: `GetBacklogHealthQueryHandler` → Calls `IWorkItemRepository.GetAllWorkItemsAsync()`
5. **Persistence**: Repository queries SQLite via EF Core
6. **Core**: Handler calculates metrics (unestimated %, blocked %, etc.)
7. **Api**: Returns `BacklogHealthDto`
8. **UI**: Displays metrics in `MetricSummaryCard` components

### "User configures TFS connection"
1. **UI**: `TfsConfig.razor` → User enters URL, project, PAT
2. **Client**: PAT stored via `ISecureStorageService.SetAsync("TFS_PAT", pat)` (browser local storage, encrypted)
3. **Client**: `TfsConfigService.SaveConfigAsync(config)` → HTTP POST `/api/settings/tfs` (no PAT in request)
4. **Api**: `SettingsController.SaveTfsConfig()` → Persists URL/project only (no PAT)
5. **Persistence**: `TfsConfigEntity` saved to SQLite
6. **Client**: On TFS API calls, PAT retrieved from `ISecureStorageService` and added to HTTP headers

## State Machines / Statuses
### Work Item States (by Type)
- **Goal/Objective**: Proposed → Active → Completed → Removed
- **Epic/Feature**: New → Active → Resolved → Closed → Removed
- **PBI/Bug**: New → Approved → Committed → Done → Removed
- **Task**: To Do → In Progress → Done → Removed

**Invalid States** (10-15% in mock data for testing detection features):
- Tasks marked "Done" but parent PBI still "New"
- Items in "Done" state but in future sprint

---

# 4) Data Model & Persistence

## Databases Used and Why
- **SQLite**: Local caching of TFS work items, PRs, pipelines. File: `potool.db`.
- **Why SQLite**: Lightweight, zero-config, disposable cache. No server setup needed.
- **Production note**: HTTP-only in development; HTTPS required in production (`ARCHITECTURE_RULES.md` §128-193).

## ORMs and Configuration
- **EF Core 10.0**: Code-First with `EnsureCreated()` (no migrations in dev).
- **DbContext**: `PoToolDbContext` in `PoTool.Api/Persistence/PoToolDbContext.cs`
- **Entity Configuration**: Inline in `OnModelCreating()` (no separate `IEntityTypeConfiguration<T>` files observed).
- **Naming**: Entities suffixed with `Entity` (e.g., `WorkItemEntity`), tables named by DbSet property name.

## Key Tables/Entities and Relationships
- **WorkItems**: `Id` (PK), `ParentId` (FK self-reference), `Type`, `State`, `AreaPath`, `IterationPath`, `Effort`, `RowVersion`
- **TfsConfigs**: `Id` (PK), `BaseUrl`, `ProjectName`, `CollectionName` (no PAT stored here)
- **Profiles**: `Id` (PK), `Name`, `AreaPaths` (JSON), `GoalIds` (JSON)
- **PullRequests**: `Id` (PK), `TfsPullRequestId`, `Title`, `Status`, `RepositoryName`, `CreatedDate`
- **PullRequestIterations**: `Id` (PK), `PullRequestId` (FK)
- **PullRequestComments**: `Id` (PK), `PullRequestId` (FK)
- **Products**: `Id` (PK), `Name`, `GoalIds` (JSON), `OwnerTeamId` (FK)
- **Teams**: `Id` (PK), `Name`, `AreaPath`

**Relationships**:
- WorkItems self-referencing via `ParentId`
- PullRequests → PullRequestIterations (1:N)
- PullRequests → PullRequestComments (1:N)
- Products → Teams (N:1 via `OwnerTeamId`)

## Transaction Boundaries and Consistency Rules
- **One transaction per sync operation**: Handler loads data from TFS, then single `SaveChangesAsync()`.
- **No distributed transactions**: Local SQLite only.
- **Idempotency**: Sync operations upsert by `Id` (TFS work item ID).

## Concurrency Strategy
- **Optimistic concurrency**: `RowVersion` on `WorkItemEntity` (see `docs/EF_RULES.md`).
- **Retry logic**: Not implemented at EF level; TFS client has retry on transient HTTP errors.
- **EF Concurrency Gate**: `IEfConcurrencyGate` abstraction in Core for sequential EF access enforcement.

---

# 5) API Surface

## Base URLs, Auth Model, Major Controllers/Endpoints

### Base URL
- **Development**: `http://localhost:5291`
- **Client Config**: `appsettings.json` → `ApiBaseUrl`
- **OpenAPI**: `/swagger` (development only)
- **Health**: `/health`

### Auth Model
- **TFS Auth**: NTLM (on-prem) or PAT (client-side). No auth between Client and Api (localhost trust in dev).
- **PAT Storage**: Client-side only via `ISecureStorageService` (browser local storage). See `docs/PAT_STORAGE_BEST_PRACTICES.md`.

### Controllers and Endpoints

#### WorkItemsController (`/api/workitems`)
- `GET /api/workitems` → Get all cached work items
- `POST /api/workitems/sync` → Trigger TFS sync
- `GET /api/workitems/{id}` → Get work item by ID
- `GET /api/workitems/hierarchy` → Get hierarchical view
- `POST /api/workitems/{id}/effort` → Update effort
- `POST /api/workitems/{id}/state` → Update state

#### PullRequestsController (`/api/pullrequests`)
- `GET /api/pullrequests` → Get cached PRs
- `POST /api/pullrequests/sync` → Trigger PR sync

#### PipelinesController (`/api/pipelines`)
- `GET /api/pipelines` → Get cached pipelines
- `POST /api/pipelines/sync` → Trigger pipeline sync

#### SettingsController (`/api/settings`)
- `GET /api/settings` → Get app settings
- `POST /api/settings` → Update settings
- `GET /api/settings/tfs` → Get TFS config (no PAT)
- `POST /api/settings/tfs` → Save TFS config (no PAT)

#### ProfilesController (`/api/profiles`)
- `GET /api/profiles` → Get user profiles
- `POST /api/profiles` → Create/update profile
- `DELETE /api/profiles/{id}` → Delete profile

#### ProductsController (`/api/products`)
- `GET /api/products` → Get products
- `POST /api/products` → Create product
- `PUT /api/products/{id}` → Update product

#### TeamsController (`/api/teams`)
- `GET /api/teams` → Get teams
- `POST /api/teams` → Create team

## Request/Response DTO Conventions, Versioning
- **DTOs**: All in `PoTool.Shared` namespace.
- **Naming**: Suffix with `Dto` (e.g., `WorkItemDto`, `PullRequestDto`).
- **Versioning**: None yet; assuming v1 implicit. API version not in URL.
- **Content-Type**: `application/json`.

## Error Model
- **HTTP Status Codes**: Standard (200, 201, 204, 400, 404, 500).
- **Problem Details**: Not explicitly implemented; controllers return custom error objects or status codes.
- **TFS Errors**: `TfsVerificationReport` for structured diagnostics.

## Pagination/Filter/Sort Conventions
- **Pagination**: Not standardized; queries return full collections (acceptable for local cache).
- **Filtering**: Query parameters (e.g., `/api/workitems?areaPath=...`).
- **Sorting**: Client-side in UI (no server-side sorting API).

---

# 6) Integrations & External Systems

## Azure DevOps Server (TFS, On-Prem)

### Client Code Location
- **Interface**: `PoTool.Core/Contracts/ITfsClient.cs`
- **Implementation**: `PoTool.Api/Services/RealTfsClient.cs` (production), `PoTool.Api/Services/MockTfsClient.cs` (testing/demo)

### Auth Mechanism and Configuration
- **NTLM**: Uses `HttpClientHandler.Credentials = CredentialCache.DefaultNetworkCredentials` (on-prem).
- **PAT**: Client sends PAT in HTTP header `Authorization: Basic <base64(:<PAT>)>` on API calls.
- **Config**: TFS URL, project, collection stored in `TfsConfigEntity` (no PAT). PAT stored client-side via `ISecureStorageService`.

### Rate Limits/Timeouts/Retry Policies
- **Throttling**: `TfsRequestThrottler` service limits concurrent TFS requests (prevents overload).
- **Timeouts**: HttpClient default (100 seconds).
- **Retry**: Not explicitly implemented; transient HTTP errors may be retried by TFS client.
- **Batch Operations**: `ITfsClient` has bulk methods (`GetPullRequestsWithDetailsAsync`, `UpdateWorkItemsEffortAsync`) to prevent N+1 patterns.

### Known Constraints (N+1 Patterns, Batching, Server Restrictions)
- **WIQL Limitations**: On-prem TFS has WIQL query limits (10,000 items per query).
- **API Version**: Must use `api-version=7.0` explicitly (see `TFS_INTEGRATION_RULES.md` §75-85).
- **N+1 Prevention**: Bulk methods mandatory for PR details, work item updates.
- **Cross-Team Dependencies**: 30-40% of dependencies must cross team boundaries (mock data rule).

---

# 7) Frontend/UI Composition

## Routing Map (Pages/Views)
- **Home**: `/` → `ProductHome.razor` (product selection, onboarding)
- **TFS Config**: `/tfs-config` → `TfsConfig.razor` (connection settings, verify, sync)
- **Work Items**: `/workitems` → `WorkItemExplorer.razor` (hierarchical tree, filter, search)
- **Backlog Health**: `/backlog-health` → `BacklogHealth.razor` (metrics, validation issues)
- **PR Insights**: `/pullrequests` → `PRInsight.razor` (PR metrics, charts)
- **Pipelines**: `/pipelines` → `Pipelines.razor` (pipeline runs, success rates)
- **Settings**: Modal via `SettingsModal.razor` (app-wide settings)

## State Management Approach
- **Component-level state**: `@code { }` blocks with private fields.
- **Shared services**: `ModeIsolatedStateService` (profile/product selection), `WorkItemSelectionService` (selected work items).
- **SignalR**: `IWorkItemSyncHubService` for real-time sync notifications.
- **No global state store**: No Redux/Flux pattern; services are scoped per user session.

## Component Library Patterns
- **MudBlazor**: Primary UI library (buttons, inputs, tables, dialogs, charts).
- **Compact Components**: Custom components in `Components/Common/Compact/` (e.g., `CompactButton.razor`, `CompactTextField.razor`) for dense UIs.
- **Reusable Components**: `LoadingIndicator.razor`, `ErrorDisplay.razor`, `EmptyStateDisplay.razor`, `MetricSummaryCard.razor`.
- **No custom JS/TS widgets**: Forbidden by `UI_RULES.md` §38-49.

## "Selection Context" Concepts
- **Product**: Current product (set of Goals) being viewed. Selected via `ProductService`.
- **Profile**: User's saved context (area paths, goals, teams). Selected via `ProfileService`.
- **Mode Isolation**: `ModeIsolatedStateService` ensures profile-scoped data (work items, PRs filtered by profile).

---

# 8) Rules & Conventions (MUST/SHOULD)

## Naming Conventions
- **PascalCase**: Public members, types.
- **camelCase**: Private fields, local variables.
- **Prefix `_`**: Private fields (`_fieldName`).
- **Suffix `Dto`**: Data transfer objects.
- **Suffix `Entity`**: EF Core entities.
- **Suffix `Service`**: Services.
- **Suffix `Controller`**: API controllers.

## Folder Conventions
- **Core**: `<Domain>/Queries/`, `<Domain>/Commands/`, `<Domain>/Validators/`
- **Api**: `Controllers/`, `Services/`, `Persistence/Entities/`, `Configuration/`
- **Client**: `Pages/`, `Components/`, `Services/`, `Storage/`
- **Tests**: Match project structure (`Services/`, `Controllers/`)

## Generated Code Boundaries (What Must Not Be Edited)
- **NSwag Clients**: `PoTool.Client/ApiClient/` (regenerated on build). Do not edit; change OpenAPI spec instead.
- **Mediator Handlers**: Source-generated by `Mediator.SourceGenerator`. Do not edit generated files.

## Coding Guidelines (Nullable, Analyzers, Formatting)
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). All reference types non-nullable unless `?`.
- **Warnings as Errors**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- **Implicit Usings**: Enabled.
- **Code Analyzers**: Not specified; standard .NET analyzers apply.

## Security Practices (Sanitization, Encoding, Secrets)
- **HTML Sanitization**: `HtmlSanitizationHelper.Sanitize()` for XSS prevention (see `docs/SECURITY_HTML_SANITIZATION.md`).
- **PAT Storage**: Client-side only, never server-side (see `docs/PAT_STORAGE_BEST_PRACTICES.md`).
- **Logging**: Credentials, PATs, TFS URLs with tokens MUST NOT be logged.
- **Secrets**: No secrets in source code or config files.

---

# 9) Testing Strategy

## Test Project Map
- **PoTool.Tests.Unit**: MSTest unit tests for Core and Api logic (mocked infrastructure).
- **PoTool.Tests.Integration**: Reqnroll integration tests (BDD-style scenarios).
- **PoTool.Tests.Blazor**: bUnit tests for Blazor components.

## Test Types (Unit/Integration/E2E)
- **Unit**: Core business logic, validators, services (no DB, no HTTP). Use mocks for `ITfsClient`, `IWorkItemRepository`.
- **Integration**: End-to-end flows via API (in-memory DB for EF Core).
- **Blazor**: Component rendering, UI interactions (bUnit).
- **E2E**: Exploratory tests with Playwright (manual workflow via `.github/workflows/exploratory-tests.yml.disabled`).

## Shared Fixtures/Factories
- Not explicitly documented; likely use MSTest `[TestInitialize]` and `[TestCleanup]`.

## How Tests Run in CI
- **CI**: All workflows disabled (`.yml.disabled`). No automated CI builds/tests on push.
- **Local**: `dotnet test` runs all test projects.

## Known Flaky Areas
- None explicitly documented.

---

# 10) Quality Gates & CI/CD

## Workflows/Pipelines Present
- **build.yml.disabled**: Build and publish artifacts.
- **release.yml.disabled**: Automated releases on version tags.
- **exploratory-tests.yml.disabled**: Playwright-based UI tests (manual trigger).
- **codeql.yml.disabled**: CodeQL security scanning.

**All workflows are disabled**. No automated CI/CD on push/PR.

## Linting/Analyzers
- `TreatWarningsAsErrors=true` enforces zero warnings.
- No explicit linter config (ESLint, StyleCop) observed.

## Coverage Gates
- None specified.

## Packaging/Deploy Steps
- **Build**: `dotnet build PoTool.sln`
- **Publish**: `dotnet publish PoTool.Api` (self-contained or framework-dependent not specified).
- **Artifacts**: `.nupkg` or published binaries (not documented).

---

# 11) Known Pain Points & Tech Debt (Observed)

## Hotspots (Files Frequently Referenced, Complex Services)
- **RealTfsClient** (`PoTool.Api/Services/RealTfsClient.cs`): Complex TFS integration, WIQL queries, error handling, batching.
- **WorkItemSyncService** (`PoTool.Api/Services/WorkItemSyncService.cs`): Background sync (stub implementation).
- **PoToolDbContext** (`PoTool.Api/Persistence/PoToolDbContext.cs`): Central EF Core context; changes cascade to all repositories.

## Performance Bottlenecks (Batching, N+1, Large Transactions)
- **N+1 PR Details**: Prevented by bulk method `GetPullRequestsWithDetailsAsync()`.
- **N+1 Work Item Updates**: Prevented by bulk methods `UpdateWorkItemsEffortAsync()`, `UpdateWorkItemsStateAsync()`.
- **Large Work Item Syncs**: WIQL query may return 10,000+ items; pagination not implemented (risk: timeout, memory).

## Fragile Boundaries (Generated Files, Codegen, Shared DTOs)
- **NSwag Clients**: Regenerated on build. Breaking API changes break client compilation.
- **Mediator Source Generation**: Changes to query/command signatures require rebuild to regenerate handlers.
- **Shared DTOs**: Any change to `WorkItemDto` affects Api, Client, and tests. Versioning not implemented.

## Areas Copilot Often Breaks (If Identifiable)
- **EF Concurrency**: Easy to accidentally introduce `Task.WhenAll` around EF calls (violates `EF_RULES.md`).
- **Client-Core Reference**: Easy to add `<ProjectReference Include="PoTool.Core">` to Client (violates `ARCHITECTURE_RULES.md`).
- **PAT Storage**: Easy to accidentally store PAT in API database (violates `PAT_STORAGE_BEST_PRACTICES.md`).
- **Area Path Mixing**: Easy to change area path below Epic level (violates `mock-data-rules.md` §115-229).

---

# 12) Unknowns / Ambiguities

1. **Migration Strategy**: How are EF Core schema changes managed? `EnsureCreated()` is dev-only; production needs migrations.
2. **API Versioning**: No `/v1/` prefix or versioning header. How will breaking changes be handled?
3. **Deployment Model**: Desktop app? Web app? Docker? Hosting model unclear.
4. **Observability**: No structured logging (Serilog, Application Insights) configured. How are production errors tracked?
5. **Background Sync**: `WorkItemSyncService` is a stub. What triggers sync? How often? On startup?
6. **User Management**: No authentication/authorization between Client and Api. How is multi-user support handled?
7. **TFS Server Version**: On-prem TFS version not specified. API version 7.0 compatibility assumed.
8. **Mock Data Volume**: Target 19,640 work items for testing. Does this cause performance issues in UI?
9. **Exploratory Tests**: Disabled workflows. Are they run manually? How often?
10. **Dependency Graph Rendering**: UI for dependency visualization not fully implemented (feature mentioned in README but not observed in Pages/).

---

# 13) "Prompting Interface" for ChatGPT

## Required Sections in Each Prompt

When generating Copilot prompts for this repository, ChatGPT MUST include these sections:

### 1. Functional Goal
- **What**: 1-2 sentences describing the user-visible outcome.
- **Why**: Business value or UX improvement.

### 2. Scope
- **In scope**: Specific files, components, controllers, or services to modify.
- **Out of scope**: Explicitly list areas NOT to touch (e.g., "Do not change navigation", "Do not refactor unrelated services").

### 3. Non-Goals
- List things that might seem related but are explicitly excluded.
- Example: "Do not add new UI components; reuse existing MudBlazor."

### 4. Acceptance Criteria
- Testable, unambiguous conditions for completion.
- Example: "Work items display in tree hierarchy", "Sync button triggers TFS API call".

### 5. Copilot Hints
- **Architecture**: Which layer(s) to modify (Core, Api, Client).
- **Patterns**: Use bulk methods, avoid N+1, follow two-phase EF pattern.
- **Constraints**: No new dependencies, no PAT in API, area path inheritance mandatory.

### 6. Files to Touch
- Explicit list of files expected to change.
- Example: `PoTool.Api/Controllers/WorkItemsController.cs`, `PoTool.Client/Pages/WorkItemExplorer.razor`.

### 7. Files NOT to Touch
- Explicit list of files that MUST NOT be modified.
- Example: "Do not change `PoTool.Client/ApiClient/` (NSwag-generated)", "Do not refactor `RealTfsClient` (out of scope)".

### 8. Tests
- Which test projects to update or add tests to.
- Example: "Add unit test in `PoTool.Tests.Unit/Services/WorkItemServiceTests.cs`".

## Preferred Style Constraints
- **Functional-first**: Lead with user story or feature name, not implementation details.
- **Technical hints allowed but not primary**: Include architecture notes in "Copilot Hints" section, not in the functional goal.
- **Concise**: Keep prompts under 500 words unless complexity demands more.

## Safety Rails
- **No broad refactors**: Changes must be minimal and focused.
- **Minimal diff**: Prefer editing existing methods over rewriting entire classes.
- **Keep architecture**: Do not change layer boundaries, dependency directions, or DI patterns.
- **Avoid generated files**: Do not edit NSwag clients or Mediator-generated code.

---

## Worked Example: "Add Dependency Graph Visualization"

### Functional Goal
Allow Product Owners to visualize work item dependencies as a directed graph, helping identify blocked items and cross-team coordination points. Clicking a node navigates to the work item detail view.

### Scope
**In scope**:
- Add new page `/workitems/dependencies` with graph visualization.
- Use MudBlazor components (e.g., `MudCard`, `MudButton`).
- Query cached work items via `IWorkItemsClient.GetAllAsync()`.
- Filter by selected profile (area paths from `ModeIsolatedStateService`).

**Out of scope**:
- Do not modify TFS sync logic.
- Do not add new API endpoints (reuse existing `/api/workitems`).
- Do not change navigation menu structure.

### Non-Goals
- Real-time updates via SignalR (deferred to future PR).
- Exporting graph as image (deferred to future PR).
- Editing dependencies in-place (read-only view only).

### Acceptance Criteria
1. New page renders at `/workitems/dependencies`.
2. Graph displays work items as nodes, dependencies as edges.
3. Nodes are colored by state (e.g., New=blue, Done=green, Blocked=red).
4. Clicking a node navigates to `/workitems/{id}`.
5. Graph filters by selected profile's area paths.
6. Graph handles up to 1,000 work items without performance degradation.

### Copilot Hints
- **Layer**: Client only. No Api or Core changes needed.
- **Architecture**: Use `IWorkItemsClient` (NSwag-generated) to fetch cached work items. Do not call API directly.
- **State Management**: Use `ModeIsolatedStateService` to get current profile and filter work items by area paths.
- **Performance**: Use virtualization or pagination if >500 nodes. Consider `MudVirtualize` or third-party graph library (if approved).
- **Dependency Format**: `WorkItemDto.WorkItemLinks` contains predecessor/successor IDs. Parse `LinkType` to distinguish "Predecessor" vs "Successor".
- **UI Library**: MudBlazor only. No custom JS/TS graph libraries unless explicitly approved.

### Files to Touch
- `PoTool.Client/Pages/DependencyGraph.razor` (new file)
- `PoTool.Client/Pages/DependencyGraph.razor.cs` (new file, code-behind)
- `PoTool.Client/Services/DependencyGraphService.cs` (new file, parse dependencies)
- `PoTool.Client/_Imports.razor` (if adding new `@using`)

### Files NOT to Touch
- `PoTool.Api/Controllers/WorkItemsController.cs` (reuse existing API)
- `PoTool.Client/ApiClient/` (NSwag-generated, do not edit)
- `PoTool.Core/WorkItems/` (no Core changes needed)
- Navigation menu (`MainLayout.razor`) (out of scope)

### Tests
- Add bUnit test in `PoTool.Tests.Blazor/Pages/DependencyGraphTests.cs`:
  - Test: Graph renders with mock work items.
  - Test: Clicking node calls navigation service.
  - Test: Graph filters by profile area paths.

---

**END OF CONTEXT PACK**
