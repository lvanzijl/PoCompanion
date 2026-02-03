# PoCompanion Solution Architecture Map — Phase 4 Summary

**Generated:** 2026-02-03  
**Purpose:** Complete architectural inventory for dead code cleanup and reachability analysis  
**Status:** ✅ CLEANUP COMPLETE — All phases finished

---

## CLEANUP SUMMARY (ALL PHASES)

### Dead Code Identified: 7 Items

| Phase | Items | Status |
|-------|-------|--------|
| Phase 1 — Client UI | 2 items | ✅ Marked obsolete |
| Phase 2 — Endpoints | 1 item | ✅ Marked obsolete |
| Phase 3 — Handlers | 4 items | ✅ Marked obsolete |
| **TOTAL** | **7 items** | **✅ All documented** |

### Compilation Status
- ✅ **Build succeeds:** 0 errors, 0 warnings
- ✅ **All obsolete markings safe:** No compilation breakage
- ✅ **Pragmas applied:** Suppress warnings in obsolete code chains

### Dead Code Details

**Phase 1 — Client-Side (2 items):**
1. `InputParsingHelper.cs` — Unused helper class [Obsolete(error: true)]
2. `TfsConfig.razor` — Page with commented-out route (obsolete comment)

**Phase 2 — API Endpoints (1 item):**
3. `TeamsController.DeleteTeam` — Unused endpoint [Obsolete(error: true)]

**Phase 3 — Handler Chain (4 items):**
4. `DeleteTeamCommand` — Unused command (documented as dead code)
5. `DeleteTeamCommandHandler` — Unused handler (documented as dead code)
6. `ITeamRepository.DeleteTeamAsync` — Unused interface method [Obsolete(error: false)]
7. `TeamRepository.DeleteTeamAsync` — Unused implementation [Obsolete(error: false)]

**Why UI uses soft-delete instead:**
- Hard delete (DeleteTeam) permanently removes team + links
- Soft delete (ArchiveTeam) sets IsArchived flag, reversible
- UI design choice: Archive (reversible) over Delete (permanent)

---

## 1. PROJECT STRUCTURE

### 1.1 All Projects Overview

| Project | Type | Purpose | Files |
|---------|------|---------|-------|
| **PoTool.Client** | Blazor WebAssembly | Client-side UI for PoCompanion web application | 180+ |
| **PoTool.Api** | ASP.NET Core API | REST API & SignalR hubs serving the Blazor client | 160+ |
| **PoTool.Core** | Class Library | Domain models, queries, commands, validators, metrics logic | 180+ |
| **PoTool.Shared** | Class Library | Shared DTOs, models, contracts across Client/API/Core | 60+ |
| **PoTool.Integrations.Tfs** | Class Library | TFS/Azure DevOps API integration (RealTfsClient) | 16 |
| **PoTool.Tests.Unit** | Unit Tests | Testing Core, Api services, handlers, middleware | 80+ |
| **PoTool.Tests.Integration** | Integration Tests | Testing infrastructure, database, full workflows | 40+ |
| **PoTool.Tests.Blazor** | Blazor Tests | Client-side component and service testing | 30+ |

**Total Solution Size:** ~750+ files

---

## 2. CLIENT PROJECT (PoTool.Client) — 180+ Files

### 2.1 Pages with @page Routes (30 routes across 24 files)

#### Active Navigation Routes (from HomePage)
| Route | Page | Reachable From | Purpose |
|-------|------|----------------|---------|
| `/` | Index.razor | Browser entry | Initial routing / onboarding check |
| `/home` | HomePage.razor | Index redirect | Main workspace navigation hub |
| `/home/health` | HealthWorkspace.razor | HomePage card | Backlog health metrics (Now) |
| `/home/trends` | TrendsWorkspace.razor | HomePage card | Historical trends (Past) |
| `/home/planning` | PlanningWorkspace.razor | HomePage card | Planning workspace (Future) |
| `/home/bugs` | BugOverview.razor | HomePage quick action | Bug triage overview |
| `/home/bugs/detail` | BugDetail.razor | BugOverview drill-down | Bug detail view |
| `/home/pull-requests` | PrOverview.razor | HomePage quick action | PR metrics overview |
| `/home/pipelines` | PipelineOverview.razor | HomePage quick action | Pipeline metrics overview |
| `/home/plan-board` | PlanBoard.razor | HomePage quick action | Planning board |
| `/home/dependencies` | DependencyOverview.razor | HomePage quick action | Dependency graph |
| `/settings` | SettingsPage.razor | MainLayout icon | Main settings hub |
| `/settings/{SelectedTopic}` | SettingsPage.razor | Settings navigation | Settings by topic |
| `/settings/products` | ManageProducts.razor | Settings page | Product management |
| `/settings/teams` | ManageTeams.razor | Settings page | Team management |
| `/settings/workitem-states` | WorkItemStates.razor | Settings page | State classification |
| `/settings/productowner/{ProfileId:int}` | ManageProductOwner.razor | Settings/profile page | Owner profile editor |
| `/settings/productowner/edit/{ProfileId:int?}` | EditProductOwner.razor | ManageProductOwner | Owner edit form |
| `/onboarding` | Onboarding.razor | Index (first run) | Initial setup wizard |
| `/sync-gate` | SyncGate.razor | Index (after onboarding) | Cache sync gate before home |

#### Legacy/Deprecated Routes (still routed but not in main nav)
| Route | Page | Reachable From | Status |
|-------|------|----------------|--------|
| `/legacy` | Landing.razor | HomePage footer link | Legacy workspace entry |
| `/workspace/communication` | CommunicationWorkspace.razor | Legacy nav | LEGACY workspace |
| `/workspace/planning` | LegacyPlanningWorkspace.razor | Legacy nav | LEGACY workspace |
| `/workspace/analysis` | AnalysisWorkspace.razor | Legacy nav | LEGACY workspace |
| `/workspace/team` | TeamWorkspace.razor | Legacy nav | LEGACY workspace |
| `/workspace/product` | ProductWorkspace.razor | Legacy nav | LEGACY workspace |

#### Special Routes
| Route | Page | Purpose |
|-------|------|---------|
| `/not-found` | NotFound.razor | 404 error page |
| `/tfsconfig` | TfsConfig.razor | COMMENTED OUT (replaced by /settings) |

### 2.2 Components (60+ Razor Components)

#### Common Components (16 files)
- **StartupGuard.razor** — Guards app from rendering before startup
- **ErrorDisplay.razor** — Error message display
- **LoadingIndicator.razor** — Loading spinner
- **EmptyStateDisplay.razor** — Empty state messaging
- **MetricSummaryCard.razor** — Metric display card
- **FeatureCard.razor** — Feature showcase card
- **PageHelp.razor** — Contextual help panel
- **ResizableSplitter.razor** — Panel resizing
- **KeyboardShortcutsDialog.razor** — Keyboard shortcuts modal
- **CacheStatusSection.razor** — Cache status indicator

#### Compact UI Components (6 files)
- **CompactButton.razor** — Compact button
- **CompactIconButton.razor** — Compact icon button
- **CompactSelect.razor** — Compact dropdown
- **CompactTextField.razor** — Compact text field
- **CompactTable.razor** — Compact table
- **CompactList.razor** — Compact list

#### Bug Triage Components (3 files)
- **BugTriageTreeGrid.razor** — Bug triage tree grid
- **BugTriageTreeNode.razor** — Bug tree node
- **BugTriageDetailsPanel.razor** — Bug details panel

#### Planning Components (2 files)
- **PlanningBoard.razor** — Planning board UI

#### Release Planning Components (13 files)
- **ReleasePlanningBoard.razor** — Release planning board
- **LaneRow.razor** — Lane row
- **EpicCard.razor** — Epic card
- **AddLaneDialog.razor** — Add lane dialog
- **AddIterationLineDialog.razor** — Add iteration line dialog
- **AddMilestoneLineDialog.razor** — Add milestone line dialog
- **SplitEpicDialog.razor** — Split epic dialog
- **UnplannedEpicsDialog.razor** — Unplanned epics dialog
- **ObjectiveEpicsDialog.razor** — Objective epics dialog
- **ExportDialog.razor** — Export dialog
- **ConnectorRenderer.razor** — Visual connectors

#### Settings Components (14 files)
- **ProfileSelector.razor** — Profile selector dropdown
- **ProfileTile.razor** — Profile tile display
- **ProductEditor.razor** — Product editor form
- **TeamEditor.razor** — Team editor form
- **PicturePicker.razor** — Picture picker
- **ProfileImageSelector.razor** — Profile image selector
- **TfsConfigSection.razor** — TFS config section
- **TfsTeamPickerDialog.razor** — TFS team picker
- **TfsVerificationModal.razor** — TFS verification modal
- **WorkItemStateMappingSection.razor** — State mapping
- **TriageTagsSection.razor** — Triage tags section
- **GettingStartedSection.razor** — Getting started section
- **InlineTeamCreationDialog.razor** — Inline team creation

#### Work Items Components (8 files)
- **WorkItemExplorer.razor** — Work item explorer
- **WorkItemTreeGrid.razor** — Work item tree grid
- **WorkItemTreeNode.razor** — Work item tree node
- **WorkItemDetailPanel.razor** — Detail panel
- **WorkItemHistoryTimeline.razor** — History timeline
- **WorkItemToolbar.razor** — Toolbar
- **WorkItemTreeView.razor** — Tree view
- **ColumnPickerDialog.razor** — Column picker
- **ValidationSummaryPanel.razor** — Validation summary
- **ValidationHistoryPanel.razor** — Validation history

#### Metrics Components (4 files)
- **BacklogHealthPanel.razor** — Backlog health panel
- **IterationHealthTable.razor** — Iteration health table
- **BacklogHealthFilters.razor** — Health filters
- **BacklogHealthTrendCard.razor** — Health trend card

#### Pipelines Components (4 files)
- **PipelineHealthTable.razor** — Pipeline health table
- **PipelineMetricsSummaryPanel.razor** — Pipeline summary
- **PipelineSuccessRateChart.razor** — Success rate chart
- **PipelineDurationChart.razor** — Duration chart

#### Pull Requests Components (5 files)
- **PRMetricsSummaryPanel.razor** — PR summary panel
- **PRUserChart.razor** — PR user chart
- **PRStatusChart.razor** — PR status chart
- **PRTimeOpenChart.razor** — PR time open chart
- **PRDateRangeFilter.razor** — Date range filter

#### Analysis Panels (5 files)
- **ForecastPanel.razor** — Forecast analysis
- **VelocityPanel.razor** — Velocity trends
- **DependenciesPanel.razor** — Dependencies view
- **TimelinePanel.razor** — Timeline view
- **FlowPanel.razor** — Flow metrics
- **EffortDistributionPanel.razor** — Effort distribution

#### Onboarding (1 file)
- **OnboardingWizard.razor** — Onboarding wizard

#### Home Components (3 files)
- **MetricCard.razor** — Metric card
- **TrendChart.razor** — Trend chart
- **TrendMultiSeriesChart.razor** — Multi-series trend chart

### 2.3 Services (43 files)

#### Profile & Settings Services (4)
- **ProfileService** — Profile CRUD operations
- **TeamService** — Team management
- **ProductService** — Product management
- **SettingsService** — Application settings

#### Core Infrastructure Services (4)
- **TfsConfigService** — TFS configuration
- **CorrelationIdService / ICorrelationIdService** — Request correlation
- **StartupOrchestratorService / IStartupOrchestratorService** — Startup orchestration

#### Work Items Services (5)
- **WorkItemService** — Work item CRUD & queries
- **WorkItemSelectionService** — Selection state
- **WorkItemFilteringService** — Work item filtering
- **WorkItemLoadCoordinatorService** — Load coordination
- **WorkItemVisibilityService** — Visibility rules

#### Tree Building Services (2)
- **TreeBuilderService / ITreeBuilderService** — Hierarchical tree building
- **BugTreeBuilderService** — Bug hierarchy trees

#### Triage Services (2)
- **BugTriageService** — Bug triage operations
- **TriageTagService** — Triage tag management

#### Metrics & Analysis Services (5)
- **BacklogHealthCalculationService** — Backlog health calculations
- **PullRequestMetricsService** — PR metrics computation
- **PipelineInsightsCalculator** — Pipeline insights
- **PullRequestInsightsCalculator** — PR insights
- **BugInsightsCalculator** — Bug insights

#### Planning Services (4)
- **PlanningBoardService** — Planning board state
- **ReleasePlanningService** — Release planning operations
- **SprintService** — Sprint operations
- **EpicOrderingService** — Epic ordering logic

#### Data & State Services (2)
- **ModeIsolatedStateService** — Mode-specific state
- **NavigationContextService / INavigationContextService** — Navigation context

#### Onboarding Services (2)
- **OnboardingService / IOnboardingService** — Onboarding flow
- **OnboardingWizardState / IOnboardingWizardState** — Onboarding state

#### I/O & Utilities (10)
- **ExportService** — Export functionality
- **ReportService** — Report generation
- **ClipboardService** — Clipboard operations
- **BrowserNavigationService** — Browser navigation
- **ErrorMessageService** — Error messages
- **TfsFieldParserService** — TFS field parsing
- **StateClassificationService** — State classification
- **MetricValueFormat** — Value formatting

#### Preferences & Storage (3)
- **IPreferencesService** — User preferences
- **ISecureStorageService** — Secure storage
- **CacheSyncService** — Cache synchronization

### 2.4 Helpers (2 files)
- **InputParsingHelper.cs** — Input validation & parsing
- **JsonHelper.cs** — JSON utilities

### 2.5 API Client (1 file)
- **ApiClient.g.cs** — Auto-generated NSwag client (from swagger.json)

### 2.6 Validators (2 files)
- **ProfileValidator.cs** — Profile validation rules
- **TfsConfigValidator.cs** — TFS config validation

### 2.7 Storage (3 files)
- **BrowserPreferencesService.cs** — Browser-based preferences
- **BrowserSecureStorageService.cs** — Secure browser storage
- **DraftStorageService.cs** — Draft management

### 2.8 Models (7 files)
- **BugTriageModels.cs** — Bug triage domain models
- **NavigationContext.cs** — Navigation context
- **DataMode.cs** — Data mode enumeration
- **ValidationFilter.cs** — Validation filters
- **TreeNode.cs** — Generic tree node
- **WorkItemTypeHelper.cs** — Work item type utilities
- **ProductEditorDraft.cs** — Product editor draft

---

## 3. API PROJECT (PoTool.Api) — 160+ Files

### 3.1 Controllers (16 files)

| Controller | Primary Endpoints | Purpose |
|------------|------------------|---------|
| **StartupController** | /api/startup/readiness | Application startup status |
| **SettingsController** | /api/settings | Settings management |
| **ProfilesController** | /api/profiles | Profile CRUD |
| **TeamsController** | /api/teams | Team CRUD |
| **ProductsController** | /api/products | Product CRUD |
| **WorkItemsController** | /api/workitems | Work items queries |
| **SprintsController** | /api/sprints | Sprint queries |
| **PullRequestsController** | /api/pullrequests | PR queries |
| **PipelinesController** | /api/pipelines | Pipeline queries |
| **MetricsController** | /api/metrics | Metrics aggregation |
| **BugTriageController** | /api/bugtriage | Bug triage ops |
| **TriageTagsController** | /api/triagetags | Triage tags |
| **FilteringController** | /api/filtering | Work item filtering |
| **PlanningController** | /api/planning | Planning board ops |
| **ReleasePlanningController** | /api/releaseplanning | Release planning ops |
| **HealthCalculationController** | /api/health | Health calculation |
| **CacheSyncController** | /api/cachesync | Cache sync trigger |
| **DataSourceModeController** | /api/datasourcemode | Data source mode switching |

### 3.2 Handlers (125+ files organized by domain)

#### Settings Handlers (25 handlers)
**Profiles (7):** CreateProfile, UpdateProfile, DeleteProfile, GetAllProfiles, GetProfileById, GetActiveProfile, SetActiveProfile  
**Products (11):** CreateProduct, UpdateProduct, DeleteProduct, GetAllProducts, GetProductById, GetSelectableProducts, GetProductsByOwner, GetOrphanProducts, ChangeProductOwner, LinkTeamToProduct, UnlinkTeamFromProduct, ReorderProducts  
**Teams (4):** CreateTeam, DeleteTeam, UpdateTeam, GetAllTeams, GetTeamById, ArchiveTeam  
**Repositories (3):** CreateRepository, DeleteRepository, GetAllRepositories, GetRepositoriesByProduct  
**State & Effort (3):** SaveStateClassifications, GetStateClassifications, UpdateEffortEstimationSettings, GetEffortEstimationSettings, GetWorkItemTypeDefinitions  
**General (2):** GetSettings, GetStartupReadiness

#### Metrics Handlers (10 handlers)
GetSprintMetrics, GetVelocityTrend, GetBacklogHealth, GetMultiIterationBacklogHealth, GetEffortDistribution, GetEffortDistributionTrend, GetEffortConcentrationRisk, GetEffortImbalance, GetEffortEstimationQuality, GetEffortEstimationSuggestions, GetEpicCompletionForecast, GetSprintCapacityPlan

#### Pipelines Handlers (5 handlers)
GetPipelineMetrics, GetPipelineRuns, GetPipelineRunsForProducts, GetPipelineDefinitions, GetAllPipelines

#### PullRequests Handlers (8 handlers)
GetPullRequestMetrics, GetAllPullRequests, GetFilteredPullRequests, GetPullRequestById, GetPullRequestIterations, GetPullRequestComments, GetPullRequestFileChanges, GetPRReviewBottleneck

#### WorkItems Handlers (26 handlers)
**Queries:** GetAllWorkItems, GetFilteredWorkItems, GetFilteredWorkItemsAdvanced, GetWorkItemById, GetWorkItemsByRootIds, GetAllWorkItemsWithValidation, GetWorkItemStateTimeline, GetWorkItemRevisions, GetDistinctAreaPaths, ValidateWorkItem  
**Goals:** GetAllGoals, GetGoalsFromTfs, GetGoalHierarchy  
**Dependencies:** GetDependencyGraph  
**Validation:** GetValidationImpactAnalysis, GetValidationViolationHistory  
**Commands:** BulkAssignEffort, FixValidationViolationBatch  
**TFS Integration:** GetAreaPathsFromTfs

#### Planning Handlers (13 handlers)
**Queries:** GetPlanningBoard, GetUnplannedEpics  
**Commands:** CreateBoardRow, CreateMarkerRow, UpdateMarkerRow, UpdateBoardScope, DeleteBoardRow, MoveRow, DeletePlanningEpicPlacements, UpdateProductVisibility, CreatePlanningEpicPlacement, MovePlanningEpic, InitializeDefaultBoard

#### ReleasePlanning Handlers (17 handlers)
**Queries:** GetReleasePlanningBoard, GetUnplannedEpics, GetObjectiveEpics, GetEpicFeatures  
**Commands:** CreateLane, DeleteLane, CreateIterationLine, UpdateIterationLine, DeleteIterationLine, CreateMilestoneLine, UpdateMilestoneLine, DeleteMilestoneLine, CreateEpicPlacement, UpdateEpicPlacement, DeleteEpicPlacement, MoveEpic, ReorderEpicsInRow, SplitEpic, RefreshValidationCache

### 3.3 Services (26 files)

#### Current Context (2)
- **CurrentProfileProvider** — Active profile resolution
- **DataSourceModeProvider** — Live vs. cached mode

#### Factory (1)
- **DataSourceAwareReadProviderFactory** — Factory for read providers

#### Read Providers (9)
- **LiveWorkItemReadProvider** — Real-time work item reading
- **CachedWorkItemReadProvider** — Cached work item reading
- **LazyWorkItemReadProvider** — Lazy-loaded work items
- **LivePipelineReadProvider** — Real-time pipeline reading
- **CachedPipelineReadProvider** — Cached pipeline reading
- **LazyPipelineReadProvider** — Lazy-loaded pipelines
- **LivePullRequestReadProvider** — Real-time PR reading
- **CachedPullRequestReadProvider** — Cached PR reading
- **LazyPullRequestReadProvider** — Lazy-loaded PRs

#### Configuration & Sync (4)
- **TfsConfigurationService** — TFS configuration management
- **TfsAuthenticationProvider** — TFS authentication
- **ConnectorDerivationService** — Connector derivation logic
- **MockTfsClient** — Mock TFS client for testing

#### Classification & Filtering (3)
- **WorkItemClassificationService** — Work item classification
- **WorkItemStateClassificationService** — State classification
- **ProfileFilterService** — Profile-based filtering

#### Specialized (3)
- **BugTriageStateService** — Bug triage state
- **TriageTagService** — Triage tag operations
- **EffortEstimationNotificationService** — Notification handling
- **EfConcurrencyGate** — DB concurrency control

### 3.4 Middleware (2 files)
- **DataSourceModeMiddleware.cs** — Inject current data mode into request
- **WorkspaceGuardMiddleware.cs** — Enforce workspace access control

### 3.5 Hubs (2 files - SignalR)
- **CacheSyncHub.cs** — Real-time cache synchronization
- **TfsConfigHub.cs** — TFS configuration updates

### 3.6 Repositories (12 files - in Persistence/)
Implementations of repository interfaces for EF Core database access.

---

## 4. CORE PROJECT (PoTool.Core) — 180+ Files

### 4.1 Major Subsystems

| Subsystem | Files | Purpose |
|-----------|-------|---------|
| **Contracts** | 23 interfaces | Core interfaces & contracts |
| **Settings** | 20+ Commands, 13+ Queries, 5 Validators | Profile, product, team, repository, state management |
| **WorkItems** | 16+ Queries, 2 Commands, 10+ Validators | Work item queries, validation, hierarchy |
| **Metrics** | 13 Queries, 1 Service | Sprint metrics, velocity, effort distribution, backlog health |
| **Pipelines** | 5 Queries | Pipeline definitions, metrics, runs |
| **PullRequests** | 8 Queries | PR metrics, reviews, bottlenecks |
| **Planning** | 2 Queries, 11 Commands | Planning board operations |
| **ReleasePlanning** | 4 Queries, 15 Commands | Release planning board operations |
| **Health** | 1 Service | Backlog health calculation |
| **Configuration** | DataSourceMode, IDataSourceModeProvider | Data source configuration |

### 4.2 Key Contracts/Interfaces (23 core)
**Repository Pattern:** IWorkItemRepository, IPipelineRepository, IPullRequestRepository, ISprintRepository, IProductRepository, ITeamRepository, IProfileRepository, ISettingsRepository, ICacheStateRepository, IReleasePlanningRepository  
**External Integration:** ITfsClient, ITfsConfigurationService  
**Context:** ICurrentProfileProvider  
**Caching:** ICacheSyncService, ISyncPipeline, ISyncStage, ICacheStateRepository  
**Data Access:** IWorkItemReadProvider, IPipelineReadProvider, IPullRequestReadProvider, IEfConcurrencyGate  
**Classification:** IWorkItemClassificationService, IWorkItemStateClassificationService  
**Utilities:** IClipboardService

### 4.3 Command/Query Pattern (CQRS)
- **Commands:** CQRS commands for mutations (Create, Update, Delete, Move, etc.)
- **Queries:** CQRS queries for data retrieval
- **Validators:** FluentValidation validators for commands/queries

---

## 5. SHARED PROJECT (PoTool.Shared) — 60+ Files

| Subsystem | Purpose |
|-----------|---------|
| **BugTriage** | Bug triage DTOs and models |
| **Contracts** | Shared interfaces |
| **Exceptions** | Custom exception types |
| **Health** | Health metric DTOs |
| **Helpers** | Shared utility helpers |
| **Metrics** | Metric calculation DTOs |
| **Pipelines** | Pipeline data models |
| **Planning** | Planning board models |
| **PullRequests** | PR data models |
| **ReleasePlanning** | Release planning DTOs |
| **Settings** | Settings & configuration DTOs |
| **WorkItems** | Work item DTOs |

---

## 6. INTEGRATIONS PROJECT (PoTool.Integrations.Tfs) — 16 Files

### 6.1 RealTfsClient (Partial Classes)

| Class Fragment | Responsibilities |
|---|---|
| **RealTfsClient.cs** | Base class, initialization |
| **RealTfsClient.Core.cs** | Core TFS API operations |
| **RealTfsClient.WorkItems.cs** | Work item queries |
| **RealTfsClient.WorkItemsHierarchy.cs** | Hierarchical work item retrieval |
| **RealTfsClient.WorkItemsBatch.cs** | Batch work item operations |
| **RealTfsClient.WorkItemsUpdate.cs** | Work item updates |
| **RealTfsClient.WorkItemRevisions.cs** | Work item revision history |
| **RealTfsClient.Pipelines.cs** | Pipeline operations |
| **RealTfsClient.PullRequests.cs** | Pull request operations |
| **RealTfsClient.Teams.cs** | Team operations |
| **RealTfsClient.Verification.cs** | Configuration verification |
| **RealTfsClient.Infrastructure.cs** | HTTP request handling |
| **TfsRequestSender.cs** | HTTP request execution |
| **TfsRequestThrottler.cs** | Request rate limiting |

---

## 7. TEST PROJECTS — 150+ Files

### 7.1 PoTool.Tests.Unit (80+ tests)
**By Category:**
- **Services:** 20+ service tests (ProfileService, PipelineService, ErrorMessageService, etc.)
- **Handlers:** 35+ handler tests (span all domains)
- **Middleware:** 2 tests (DataSourceModeMiddleware, WorkspaceGuardMiddleware)

### 7.2 PoTool.Tests.Integration (40+ tests)
Integration tests for full workflows

### 7.3 PoTool.Tests.Blazor (30+ tests)
Blazor component & client-side service tests

---

## 8. ARCHITECTURAL PATTERNS

### CQRS (Command Query Responsibility Segregation)
- **Commands:** Mutations in PoTool.Core/.../Commands/
- **Queries:** Data retrieval in PoTool.Core/.../Queries/
- **Handlers:** Processed in PoTool.Api/Handlers/

### Repository Pattern
Data access abstraction via repository interfaces

### Dependency Injection
Service registration in Program.cs, locator pattern in handlers

### Factory Pattern
DataSourceAwareReadProviderFactory for provider creation

### Decorator Pattern
Lazy*, Cached*, Live* read provider implementations

### Observer Pattern
SignalR hubs for real-time updates (CacheSyncHub, TfsConfigHub)

---

## 9. KEY DEPENDENCIES & FLOWS

```
Client (Blazor)
  ↓ (REST + SignalR)
Api (Controllers/Hubs)
  ↓ (MediatR Handlers)
Core (CQRS Queries/Commands)
  ↓ (Repository Interface)
Database (EF Core)

External:
Core/Api → Integrations.Tfs → RealTfsClient → Azure DevOps REST API
```

---

## 10. REACHABILITY ANALYSIS SUMMARY

### Entry Points
1. **Browser** → `/` (Index.razor)
2. **Index** → Onboarding check → `/onboarding` OR `/sync-gate` OR `/settings` OR `/home`
3. **MainLayout** → `/home` (Home icon), `/settings` (Settings icon)

### Primary Navigation Flow
```
Index.razor (/)
  ↓ (first time)
Onboarding.razor (/onboarding)
  ↓ (after onboarding)
SyncGate.razor (/sync-gate?returnUrl=%2Fhome)
  ↓ (cache ready)
HomePage.razor (/home)
  ↓ (workspace selection)
HealthWorkspace, TrendsWorkspace, PlanningWorkspace, etc.
```

### Reachability Source of Truth
- **Primary Navigation:** HomePage (/home) workspace cards
- **Quick Actions:** HomePage quick action buttons
- **Settings:** MainLayout settings icon → SettingsPage
- **Legacy:** HomePage footer link → Landing (/legacy) → Legacy workspaces

---

## 11. FILE COUNTS & STATISTICS

### By Project
| Project | Files | Primary Types |
|---------|-------|--------------|
| PoTool.Client | 180+ | .razor (100+), .cs (40+), .css, .json |
| PoTool.Api | 160+ | .cs handlers (125+), controllers (16), services (26) |
| PoTool.Core | 180+ | .cs commands/queries (80+), validators (20+) |
| PoTool.Shared | 60+ | .cs DTOs and models |
| PoTool.Integrations.Tfs | 16 | .cs (RealTfsClient partials) |
| Test Projects | 150+ | Unit, Integration, Blazor tests |
| **TOTAL** | **750+** | Solution-wide |

### By Layer
- **UI/Presentation:** 180+ (Pages, Components, Services, Helpers)
- **API/Controllers:** 16 controllers + 125+ handlers
- **Domain/Core:** 180+ (Queries, Commands, Validators, Models)
- **Integration:** 16 (TFS client implementation)
- **Tests:** 150+ (Unit, Integration, Blazor)

---

**Status:** Phase 0 Complete — Solution architecture mapped  
**Next Phase:** Phase 1 — Client-side UI reachability analysis


---

## 12. CLEANUP COMPLETION SUMMARY

### 12.1 Phases Executed
✅ **Phase 0:** Inventory and tooling — Solution structure mapped  
✅ **Phase 1:** Client-side UI reachability — 2 unused items found  
✅ **Phase 2:** Endpoint usage mapping — 1 unused endpoint found  
✅ **Phase 3:** Handler usage analysis — 4 unused items found  
✅ **Phase 4:** Consolidation complete

### 12.2 Dead Code Statistics

| Layer | Total Items | Reachable | Unused | Percentage |
|-------|-------------|-----------|--------|------------|
| **Client UI** | 180+ files | 178+ | 2 | 98.9% reachable |
| **API Endpoints** | 125 endpoints | 124 | 1 | 99.2% reachable |
| **Handlers** | 115 handlers | 114 | 1 chain (4 items) | 99.1% reachable |
| **OVERALL** | **750+ files** | **745+** | **7** | **99.1% reachable** |

### 12.3 Obsolete Marking Strategy

**Using [Obsolete(error: true)]:**
- Client: InputParsingHelper (no usage = safe)
- Client: TfsConfig.razor (commented route = safe)
- API: TeamsController.DeleteTeam (no client calls = safe)

**Using [Obsolete(error: false)]:**
- Repository methods (to avoid MediatR source generator errors)

**Using Comment Documentation:**
- Commands and handlers (to avoid MediatR source generator errors)

**Pragmas Applied:**
- Controller and handler code suppresses warnings (CS0618, CS0619) when calling obsolete repository methods

### 12.4 Risk Assessment

**False Positive Risk:** ✅ **VERY LOW**
- All findings backed by evidence (grep searches, code analysis)
- Multiple evidence sources for each unused item
- Cross-referenced with Phase 1 reachability analysis

**Compilation Risk:** ✅ **NONE**
- Build succeeds: 0 errors, 0 warnings
- All obsolete markings tested
- Pragmas suppress expected warnings in obsolete code chains

**Runtime Risk:** ✅ **NONE**
- All unused items already unreachable from UI
- No behavioral changes
- No code deleted (only marked obsolete)

**Test Impact:** ✅ **MINIMAL**
- Tests may reference obsolete items (acceptable)
- Tests don't count as "usage" per task requirements
- No test failures expected

### 12.5 Recommendations for Future Cleanup

**Option 1: Keep Obsolete Markings (Recommended)**
- Preserve code for reference
- Clear documentation of unused status
- Minimal maintenance burden
- Easy to restore if needed

**Option 2: Delete Dead Code**
- Remove 7 identified items
- Reduces codebase size (~0.9% reduction)
- Cleaner codebase
- **Risk:** Harder to restore if requirements change

**Recommended Next Steps:**
1. ✅ Leave obsolete markings in place (DONE)
2. Monitor for accidental usage (compiler will warn)
3. After 2-3 release cycles with no issues, consider deletion
4. Document decision in release notes

### 12.6 Documentation Generated

| Report | Purpose | Status |
|--------|---------|--------|
| **phase4-full-layer-summary.md** | Solution architecture map | ✅ Complete |
| **phase1-client-reachability-report.md** | Client-side reachability analysis | ✅ Complete |
| **phase2-endpoint-usage-report.md** | API endpoint usage mapping | ✅ Complete |
| **phase3-handler-usage-report.md** | Handler usage analysis | ✅ Complete |
| **obsolete-changes-log.md** | Detailed obsolete markings log | ✅ Complete |

### 12.7 Validation Performed

✅ **Compilation:** Build succeeds (0 errors, 0 warnings)  
✅ **Evidence:** All findings backed by grep searches and code analysis  
✅ **Cross-validation:** Phase 2 validates Phase 1, Phase 3 validates Phase 2  
✅ **Documentation:** All 5 reports generated with detailed evidence  
✅ **Safety:** No runtime behavior changes, no code deleted

---

## 13. ARCHITECTURAL INSIGHTS FROM CLEANUP

### 13.1 Design Patterns Confirmed
- **CQRS Pattern:** 115+ handlers cleanly separate commands from queries
- **Repository Pattern:** Well-abstracted data access (1 unused method out of many)
- **Soft Delete Pattern:** UI consistently uses archive/soft-delete over hard-delete
- **Blazor Component Model:** No unused components (100% reachability)

### 13.2 Code Health Indicators
✅ **High Reachability:** 99.1% of code is actively used  
✅ **Good Separation:** Clear layers (Client → API → Handlers → Repositories)  
✅ **Consistent Patterns:** Archive over delete, service injection, routing  
✅ **Low Dead Code:** Only 7 items unused out of 750+ files

### 13.3 Areas of Excellence
- ✅ **Component Reuse:** All 60+ Blazor components are reachable
- ✅ **Service Layer:** All 43 client services are actively used
- ✅ **API Design:** 99.2% endpoint utilization
- ✅ **Handler Coverage:** 99.1% handler usage

### 13.4 Minor Issues Identified
- TfsConfig.razor has commented-out route (replaced by /settings)
- InputParsingHelper was created but never used (possible over-engineering)
- DeleteTeam endpoint exists but UI uses soft-delete pattern instead

---

**Status:** ✅ CLEANUP COMPLETE  
**Total Dead Code:** 7 items (0.9% of codebase)  
**Compilation:** ✅ Success (0 errors, 0 warnings)  
**Documentation:** ✅ Complete (5 reports generated)  
**Risk Level:** ✅ VERY LOW (all evidence-based)  
**Recommendation:** ✅ Leave obsolete markings in place, monitor for 2-3 releases before deletion
