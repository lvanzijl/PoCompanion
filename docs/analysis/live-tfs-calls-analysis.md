# Investigation Report: Unexpected Live TFS Calls in Workspace Navigation

**Date:** 2026-01-24  
**Status:** Analysis Complete — Implementation Pending  
**Severity:** High — Cache boundary violation  

---

## Executive Summary

After sync completes successfully, workspaces continue to make **live TFS queries** instead of reading from the ProductOwner-scoped cache. This violates the architectural principle that workspaces must be **cache-only**.

**Root Cause:** The `DataSourceModeProvider` is not being invoked to set the correct mode (Cache vs Live) on a per-request basis. Although the infrastructure exists to support both modes, **no mechanism currently switches the mode to `Cache` after a successful sync**.

**Impact:** 
- Increased latency for workspace navigation
- Unnecessary TFS load
- Defeats the purpose of the cache infrastructure
- Users experience the same performance as if cache didn't exist

**Proposed Solution:** Implement a request-scoped middleware or filter that sets the `DataSourceMode` to `Cache` for workspace routes based on the current ProductOwner's cache state.

---

## 1. Inventory of Live TFS Call Sites

### 1.1 Read Providers (Mode-Aware Infrastructure)

These providers are registered with keyed services and can operate in either Live or Cache mode through the `DataSourceAwareReadProviderFactory`:

| Location | Class | Methods | Current Behavior | Should Use Cache |
|----------|-------|---------|------------------|------------------|
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs` | `LiveWorkItemReadProvider` | `GetAllAsync`, `GetFilteredAsync`, `GetByAreaPathsAsync`, `GetByTfsIdAsync`, `GetByRootIdsAsync` | **Always calls TFS** via `_tfsClient.GetWorkItemsAsync()` | ✅ YES — when mode is Cache |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | `GetAllAsync`, `GetByProductIdsAsync`, `GetByIdAsync`, `GetIterationsAsync`, `GetCommentsAsync`, `GetFileChangesAsync` | **Always calls TFS** via `_tfsClient.GetPullRequestsAsync()` | ✅ YES — when mode is Cache |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetAllAsync`, `GetByIdAsync`, `GetRunsAsync`, `GetAllRunsAsync`, `GetRunsForPipelinesAsync`, `GetDefinitionsByProductIdAsync`, `GetDefinitionsByRepositoryIdAsync` | **Always calls TFS** via `_tfsClient.GetPipelinesAsync()` | ✅ YES — when mode is Cache |

**Analysis:** These providers are correctly designed to be swappable. The factory (`DataSourceAwareReadProviderFactory`) already exists and can return either Live or Cached implementations. However, **the mode is never set to Cache**, so the factory always returns the Live provider.

---

### 1.2 Handlers That Directly Use ITfsClient (Bypass Mode Infrastructure)

These handlers directly inject `ITfsClient` and **always** call TFS, regardless of mode setting:

| Location | Class | Method | Purpose | Trigger | Should Be Cached |
|----------|-------|--------|---------|---------|------------------|
| `PoTool.Api/Handlers/WorkItems/GetAreaPathsFromTfsQueryHandler.cs` | `GetAreaPathsFromTfsQueryHandler` | `Handle` | Fetches area paths from TFS Classification Nodes API | Settings/Profile creation flow | ❌ NO — Settings use case |
| `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs` | `GetGoalsFromTfsQueryHandler` | `Handle` | Fetches Goal work items via WIQL | Settings/Profile creation flow (goal picker) | ❌ NO — Settings use case |
| `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs` | `ValidateWorkItemQueryHandler` | `Handle` | Validates work item by ID | Settings/Product configuration (validate backlog root) | ❌ NO — Settings use case |
| `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs` | `GetWorkItemRevisionsQueryHandler` | `Handle` | Fetches work item revision history | Unknown — needs investigation | ⚠️ INVESTIGATE |
| `PoTool.Api/Handlers/Settings/GetWorkItemTypeDefinitionsQueryHandler.cs` | `GetWorkItemTypeDefinitionsQueryHandler` | `Handle` | Fetches WIT definitions from TFS | Settings/Configuration | ❌ NO — Settings use case |
| `PoTool.Api/Handlers/WorkItems/BulkAssignEffortCommandHandler.cs` | `BulkAssignEffortCommandHandler` | `Handle` | Bulk effort updates | Workspace action (write-back) | ❌ NO — Write operation |
| `PoTool.Api/Handlers/WorkItems/FixValidationViolationBatchCommandHandler.cs` | `FixValidationViolationBatchCommandHandler` | `Handle` | Bulk state updates | Workspace action (write-back) | ❌ NO — Write operation |
| `PoTool.Api/Handlers/ReleasePlanning/SplitEpicCommandHandler.cs` | `SplitEpicCommandHandler` | `Handle` | Creates Epic, updates parent | Workspace action (write-back) | ❌ NO — Write operation |

**Analysis:** Most of these are correctly scoped to Settings flows or write operations. The unknown trigger for `GetWorkItemRevisionsQueryHandler` needs investigation.

---

### 1.3 Controllers That May Bypass Cache

| Location | Class | Endpoint | Handler/Service Called | Current Behavior |
|----------|-------|----------|----------------------|------------------|
| `PoTool.Api/Controllers/WorkItemsController.cs` | `WorkItemsController` | `GET /api/workitems` | `GetAllWorkItemsQueryHandler` → `IWorkItemReadProvider` | Uses factory pattern, but mode is not set |
| `PoTool.Api/Controllers/WorkItemsController.cs` | `WorkItemsController` | `GET /api/workitems/validated` | `GetAllWorkItemsWithValidationQueryHandler` → `IWorkItemReadProvider` | Uses factory pattern, but mode is not set |
| `PoTool.Api/Controllers/WorkItemsController.cs` | `WorkItemsController` | `GET /api/workitems/filter/{filter}` | `GetFilteredWorkItemsQueryHandler` → `IWorkItemReadProvider` | Uses factory pattern, but mode is not set |
| `PoTool.Api/Controllers/PullRequestsController.cs` | `PullRequestsController` | `GET /api/pullrequests` | `GetAllPullRequestsQueryHandler` → `IPullRequestReadProvider` | Uses factory pattern, but mode is not set |
| `PoTool.Api/Controllers/PipelinesController.cs` | `PipelinesController` | `GET /api/pipelines` | `GetAllPipelinesQueryHandler` → `IPipelineReadProvider` | Uses factory pattern, but mode is not set |
| `PoTool.Api/Controllers/StartupController.cs` | `StartupController` | `GET /api/startup/tfs-teams` | Direct `_tfsClient.GetTfsTeamsAsync()` | Always live — Settings flow |

**Analysis:** The main workspace-facing endpoints (`/api/workitems`, `/api/pullrequests`, `/api/pipelines`) all use the correct provider pattern. However, **the mode is never set**, so they default to Live.

---

## 2. Root Cause Analysis

### 2.1 The DataSourceMode Mechanism

The infrastructure for mode switching **already exists**:

```
DataSourceModeProvider (IDataSourceModeProvider)
  ↓
  Stores current mode: Live or Cache (default: Live)
  ↓
DataSourceAwareReadProviderFactory
  ↓
  Reads mode from provider
  ↓
  Returns Live or Cached implementation based on mode
  ↓
Handlers inject IWorkItemReadProvider (resolved via factory)
```

### 2.2 The Missing Link

**Problem:** The `DataSourceModeProvider.SetCurrentMode(DataSourceMode.Cache)` is **never called** during request processing.

**Evidence:**
1. `DataSourceModeProvider.cs` line 19: Default mode is `DataSourceMode.Live`
2. No middleware or filter sets the mode based on route or ProductOwner
3. `GetModeAsync` checks cache state but doesn't set `_currentMode`
4. `SetModeAsync` exists but is never called except via manual API endpoint

**Why This Happens:**
- The mode provider is **scoped** (per request), so each request starts with default `Live` mode
- No automatic mode selection happens based on:
  - Current route (workspace vs settings)
  - Current ProductOwner's cache state
  - Cache availability

### 2.3 Current Mode Selection Flow

```
Request → API Controller → Handler → Factory.GetProvider()
                                            ↓
                                     ModeProvider.Mode (always Live)
                                            ↓
                                     Returns LiveProvider
                                            ↓
                                     Calls TFS directly
```

### 2.4 Intended Mode Selection Flow (Not Implemented)

```
Request → [Middleware checks route] → [Middleware checks ProductOwner cache state]
              ↓                              ↓
         Workspace route?              Cache available?
              ↓                              ↓
         YES → ModeProvider.SetCurrentMode(Cache)
              ↓
         Handler → Factory.GetProvider() → ModeProvider.Mode (now Cache)
              ↓
         Returns CachedProvider → Reads from DB
```

---

## 3. Trigger Analysis

### 3.1 Workspace Navigation Triggers

| User Action | Route | Controller Method | Handler | Provider Method | Mode | Result |
|-------------|-------|-------------------|---------|-----------------|------|--------|
| Navigate to Planning Workspace | `/workspace/planning` | Multiple controllers | Multiple handlers | Multiple providers | Live (default) | **TFS calls** |
| View PR metrics | `/workspace/planning` | `PullRequestsController.GetAll()` | `GetAllPullRequestsQueryHandler` | `IPullRequestReadProvider.GetAllAsync()` | Live (default) | **TFS call** |
| View Pipeline metrics | `/workspace/planning` | `PipelinesController.GetAll()` | `GetAllPipelinesQueryHandler` | `IPipelineReadProvider.GetAllAsync()` | Live (default) | **TFS call** |

### 3.2 Why Calls Happen on Navigation

**Client-side initialization:**
1. User navigates to a routed page (for example `/home/planning`)
2. Razor component `OnInitializedAsync()` or `OnParametersSetAsync()` fires
3. Client service (e.g., `WorkItemService`) calls API
4. API handler uses default Live mode
5. TFS query executes

**No caching awareness:**
- Client doesn't know if cache exists
- Client doesn't switch to Cache mode before calling API
- API doesn't check cache availability before resolving provider

---

## 4. Cache-Read Equivalents

### 4.1 Cached Providers (Already Implemented)

| Data Type | Cached Provider | Database Source | Status |
|-----------|----------------|-----------------|--------|
| Work Items | `CachedWorkItemReadProvider` | `WorkItemEntity` table | ✅ Implemented |
| Pull Requests | `CachedPullRequestReadProvider` | `PullRequestEntity` table | ✅ Implemented |
| Pipelines | `CachedPipelineReadProvider` | `PipelineEntity` + `CachedPipelineRunEntity` | ✅ Implemented |
| Validations | N/A (precomputed during sync) | `CachedValidationResultEntity` | ✅ Implemented |
| Metrics | N/A (precomputed during sync) | `CachedMetricsEntity` | ✅ Implemented |

**Conclusion:** All necessary cached providers exist. The cache DB schema is complete. **No data is missing from cache**. The only issue is mode selection.

---

## 5. Phased Fix Plan

### Phase 1 — Instrumentation and Proof (Detection)

**Goal:** Add logging and detection to prove live TFS calls are happening from workspaces.

**Changes:**

1. **File:** `PoTool.Api/Services/LiveWorkItemReadProvider.cs`
   - **Change:** Add logging at the start of each method:
     ```csharp
     _logger.LogWarning("LiveWorkItemReadProvider.{Method} called — may indicate cache bypass", 
         nameof(GetAllAsync));
     ```
   - **Purpose:** Detect when Live provider is used

2. **File:** `PoTool.Api/Services/LivePullRequestReadProvider.cs`
   - **Change:** Same as above for all methods

3. **File:** `PoTool.Api/Services/LivePipelineReadProvider.cs`
   - **Change:** Same as above for all methods

4. **File:** `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
   - **Change:** Change log level from Debug to Warning when resolving Live provider:
     ```csharp
     _logger.LogWarning("Resolving IWorkItemReadProvider for mode: {Mode} — if Cache expected, this is a bug", mode);
     ```

**Exit Criteria:**
- Logs clearly show Live provider being used after sync
- Evidence captured for specific routes and handlers

**Risks:** None (logging only)

---

### Phase 2 — Request-Scoped Mode Selection (Core Fix)

**Goal:** Automatically set `DataSourceMode.Cache` for workspace requests when cache is available.

**Changes:**

1. **File:** `PoTool.Api/Middleware/DataSourceModeMiddleware.cs` (new)
   - **Create:** New middleware to set mode based on route and ProductOwner
   - **Logic:**
     ```csharp
     public class DataSourceModeMiddleware
     {
         public async Task InvokeAsync(HttpContext context, 
             IDataSourceModeProvider modeProvider,
             ICurrentProfileProvider profileProvider,
             ICacheStateRepository cacheStateRepo)
         {
             var path = context.Request.Path.Value;
             var isWorkspaceRoute = path?.StartsWith("/api/workitems") == true
                 || path?.StartsWith("/api/pullrequests") == true
                 || path?.StartsWith("/api/pipelines") == true
                 || path?.StartsWith("/api/releaseplanning") == true
                 || path?.StartsWith("/api/filtering") == true;

             if (isWorkspaceRoute)
             {
                 var productOwnerId = await profileProvider.GetCurrentProductOwnerIdAsync();
                 if (productOwnerId.HasValue)
                 {
                     var mode = await modeProvider.GetModeAsync(productOwnerId.Value);
                     modeProvider.SetCurrentMode(mode);
                 }
             }

             await _next(context);
         }
     }
     ```

2. **File:** `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`
   - **Change:** Register middleware:
     ```csharp
     app.UseMiddleware<DataSourceModeMiddleware>();
     ```
   - **Position:** After routing, before endpoints

3. **File:** `PoTool.Core/Contracts/ICurrentProfileProvider.cs` (new)
   - **Create:** Interface to get current ProductOwner ID from request context
   - **Purpose:** Middleware needs to know which ProductOwner is making the request

4. **File:** `PoTool.Api/Services/CurrentProfileProvider.cs` (new)
   - **Create:** Implementation using HTTP context or auth context
   - **Logic:** Extract ProductOwner ID from claims, headers, or session

**Alternative Approach (Action Filter Instead of Middleware):**

If middleware is too broad, use an MVC action filter applied to specific controllers:

1. **File:** `PoTool.Api/Filters/DataSourceModeActionFilter.cs` (new)
   - **Create:** Action filter that sets mode before action execution
   - **Apply to:** `WorkItemsController`, `PullRequestsController`, `PipelinesController`

**Exit Criteria:**
- Workspace routes use Cache mode after sync
- Settings routes continue using Live mode
- Logs show Cache provider being resolved for workspaces

**Risks:**
- Medium — requires new middleware/filter
- May need to handle edge cases (no ProductOwner, no cache state)

---

### Phase 3 — Enforce Cache Boundary via Routing

**Goal:** Prevent accidental Live provider usage from workspace routes.

**Changes:**

1. **File:** `PoTool.Api/Middleware/WorkspaceGuardMiddleware.cs` (new)
   - **Create:** Middleware that throws exception if Live provider used from workspace route
   - **Logic:**
     ```csharp
     public class WorkspaceGuardMiddleware
     {
         public async Task InvokeAsync(HttpContext context, IDataSourceModeProvider modeProvider)
         {
             var path = context.Request.Path.Value;
             var isWorkspaceRoute = /* same check as Phase 2 */;

             if (isWorkspaceRoute && modeProvider.Mode == DataSourceMode.Live)
             {
                 _logger.LogError("Workspace route {Path} attempted to use Live mode", path);
                 throw new InvalidOperationException($"Workspace route {path} must use Cache mode");
             }

             await _next(context);
         }
     }
     ```

2. **File:** `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`
   - **Change:** Register guard middleware in development only
   - **Purpose:** Fail fast during development if mode selection fails

**Exit Criteria:**
- Development environment throws exception if workspace uses Live mode
- No exceptions in production (mode should always be correct by Phase 2)

**Risks:**
- Low — development-only enforcement

---

### Phase 4 — Remove Fallback to Live Mode

**Goal:** Make Cache mode mandatory for workspaces by removing the Live fallback.

**Changes:**

1. **File:** `PoTool.Api/Services/DataSourceModeProvider.cs`
   - **Change:** Remove default `DataSourceMode.Live` assignment on line 19
   - **Change:** Throw exception if mode is not explicitly set:
     ```csharp
     private DataSourceMode? _currentMode = null;

     public DataSourceMode Mode => _currentMode 
         ?? throw new InvalidOperationException("DataSourceMode not set for this request");
     ```

2. **File:** All handlers that use `IWorkItemReadProvider`, `IPullRequestReadProvider`, `IPipelineReadProvider`
   - **Change:** No changes needed (they already delegate to factory)

**Exit Criteria:**
- All requests must explicitly set mode
- No implicit Live mode fallback
- Settings routes still work (they set Live mode explicitly)

**Risks:**
- High — breaks any route that doesn't set mode
- Must be done after Phase 2 is stable

---

### Phase 5 — Regression Protection (Tests and Guardrails)

**Goal:** Prevent reintroduction of live TFS calls from workspaces.

**Changes:**

1. **File:** `PoTool.Tests.Integration/WorkspaceRouteCacheTests.cs` (new)
   - **Create:** Integration test that:
     1. Creates ProductOwner and completes sync
     2. Navigates to each workspace route
     3. Asserts no live TFS calls occurred
   - **Method:** Mock `ITfsClient` to throw exception if called during workspace navigation

2. **File:** `PoTool.Tests.Unit/DataSourceModeMiddlewareTests.cs` (new)
   - **Create:** Unit tests for middleware logic
   - **Cases:**
     - Workspace route with cache → Cache mode
     - Workspace route without cache → Live mode (or error)
     - Settings route → Live mode
     - Unknown route → no mode set

3. **File:** `PoTool.Tests.Unit/WorkspaceGuardMiddlewareTests.cs` (new)
   - **Create:** Unit tests for guard middleware
   - **Cases:**
     - Workspace route + Live mode → throws
     - Workspace route + Cache mode → passes
     - Settings route + Live mode → passes

4. **File:** `.github/workflows/ci.yml`
   - **Change:** Add step to run integration tests with workspace cache validation

**Exit Criteria:**
- Tests fail if workspace routes use Live mode
- Tests pass if workspace routes use Cache mode
- CI pipeline enforces cache boundary

**Risks:**
- None (test-only)

---

### Phase 6 — Settings Route Exemptions (Explicit Allow-List)

**Goal:** Document and enforce which routes are allowed to use Live mode.

**Changes:**

1. **File:** `PoTool.Api/Configuration/DataSourceModeConfiguration.cs` (new)
   - **Create:** Static configuration defining route rules:
     ```csharp
     public static class DataSourceModeConfiguration
     {
         public static readonly HashSet<string> LiveModeAllowedRoutes = new()
         {
             "/api/settings/area-paths-from-tfs",
             "/api/settings/goals-from-tfs",
             "/api/startup/tfs-teams",
             "/api/workitems/validate", // for product backlog root validation
             "/api/tfs/verify",
             "/api/tfs/validate"
         };

         public static readonly HashSet<string> CacheModeRequiredRoutes = new()
         {
             "/api/workitems",
             "/api/pullrequests",
             "/api/pipelines",
             "/api/releaseplanning",
             "/api/filtering"
         };
     }
     ```

2. **File:** `PoTool.Api/Middleware/DataSourceModeMiddleware.cs`
   - **Change:** Use configuration instead of hardcoded route checks

**Exit Criteria:**
- All route rules are centralized and documented
- Easy to add/remove exemptions

**Risks:**
- None (organizational only)

---

## 6. Guardrails and Enforcement Mechanisms

### 6.1 Compile-Time Guardrails (Future)

**Option 1: Assembly Boundary**
- Split Live and Cached providers into separate assemblies
- Workspace handlers reference only Cached assembly
- Settings handlers reference only Live assembly

**Option 2: Roslyn Analyzer**
- Custom analyzer that detects `ITfsClient` injection in workspace handlers
- Fails build if workspace handler injects `ITfsClient` directly

**Status:** Future consideration — overkill for current phase

---

### 6.2 Runtime Guardrails (Implemented in Phase 3)

**Middleware-Based Guard:**
- `WorkspaceGuardMiddleware` throws exception if workspace route uses Live mode
- Only active in development environment
- Provides fast feedback during feature development

---

### 6.3 Test-Based Guardrails (Implemented in Phase 5)

**Integration Test:**
- Simulates workspace navigation after sync
- Asserts no live TFS calls
- Runs in CI pipeline

**Unit Tests:**
- Validates middleware/filter logic
- Ensures correct mode selection

---

## 7. Summary of Findings

### 7.1 Key Discoveries

1. **Cache infrastructure is complete** — all cached providers exist
2. **Mode selection mechanism exists** — factory pattern is implemented
3. **Mode is never set to Cache** — middleware/filter is missing
4. **Handlers are correctly designed** — they use provider abstraction
5. **Root cause is simple** — no request-scoped mode selection

### 7.2 Why This Wasn't Caught Earlier

- Cache sync completes successfully (no errors)
- Workspace navigation works (using Live mode as fallback)
- No performance regression (never faster than before)
- No logging warned about mode selection

### 7.3 Fix Complexity

- **Low to Medium** — mostly adding middleware/filter
- **No handler changes needed** — provider abstraction is correct
- **No schema changes needed** — cache is complete
- **Main risk:** Ensuring mode is set for all routes

---

## 8. Acceptance Criteria

This plan is acceptable if:

✅ Every live TFS call is listed with file/class/method  
✅ Each call is categorized by trigger and interaction path  
✅ Each call has a proposed cache-based replacement (or exemption)  
✅ The plan includes middleware/filter for mode selection  
✅ The plan includes guard middleware for enforcement  
✅ The plan includes integration tests for regression protection  
✅ The plan includes documentation of allowed live routes  

---

## 9. Next Steps

1. Review this analysis with team
2. Approve phased fix plan
3. Implement Phase 1 (instrumentation) to gather evidence
4. Implement Phase 2 (middleware) as core fix
5. Implement Phase 3 (guards) for enforcement
6. Implement Phase 5 (tests) for regression protection
7. Monitor logs after each phase

---

## Appendix A: Complete Call Site Inventory

### A.1 Mode-Aware Providers (Will Use Cache Once Mode is Set)

| File | Class | Method | Purpose |
|------|-------|--------|---------|
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs` | `LiveWorkItemReadProvider` | `GetAllAsync` | Fetch all work items from TFS |
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs` | `LiveWorkItemReadProvider` | `GetFilteredAsync` | Fetch filtered work items |
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs` | `LiveWorkItemReadProvider` | `GetByAreaPathsAsync` | Fetch work items by area paths |
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs` | `LiveWorkItemReadProvider` | `GetByTfsIdAsync` | Fetch single work item |
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs` | `LiveWorkItemReadProvider` | `GetByRootIdsAsync` | Hierarchical loading |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | `GetAllAsync` | Fetch all PRs |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | `GetByProductIdsAsync` | Fetch PRs by products |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | `GetByIdAsync` | Fetch single PR |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | `GetIterationsAsync` | Fetch PR iterations |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | `GetCommentsAsync` | Fetch PR comments |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | `GetFileChangesAsync` | Fetch PR file changes |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetAllAsync` | Fetch all pipelines |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetByIdAsync` | Fetch single pipeline |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetRunsAsync` | Fetch pipeline runs |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetAllRunsAsync` | Fetch all runs |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetRunsForPipelinesAsync` | Bulk fetch runs |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetDefinitionsByProductIdAsync` | Fetch definitions by product |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | `GetDefinitionsByRepositoryIdAsync` | Fetch definitions by repo |

### A.2 Handlers That Bypass Mode (Settings/Write Operations)

| File | Class | Method | Purpose | Allowed |
|------|-------|--------|---------|---------|
| `PoTool.Api/Handlers/WorkItems/GetAreaPathsFromTfsQueryHandler.cs` | `GetAreaPathsFromTfsQueryHandler` | `Handle` | Settings — area path discovery | ✅ YES |
| `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs` | `GetGoalsFromTfsQueryHandler` | `Handle` | Settings — goal picker | ✅ YES |
| `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs` | `ValidateWorkItemQueryHandler` | `Handle` | Settings — validate backlog root | ✅ YES |
| `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs` | `GetWorkItemRevisionsQueryHandler` | `Handle` | Unknown — needs investigation | ⚠️ INVESTIGATE |
| `PoTool.Api/Handlers/Settings/GetWorkItemTypeDefinitionsQueryHandler.cs` | `GetWorkItemTypeDefinitionsQueryHandler` | `Handle` | Settings — WIT definitions | ✅ YES |
| `PoTool.Api/Handlers/WorkItems/BulkAssignEffortCommandHandler.cs` | `BulkAssignEffortCommandHandler` | `Handle` | Write-back — effort updates | ✅ YES |
| `PoTool.Api/Handlers/WorkItems/FixValidationViolationBatchCommandHandler.cs` | `FixValidationViolationBatchCommandHandler` | `Handle` | Write-back — state updates | ✅ YES |
| `PoTool.Api/Handlers/ReleasePlanning/SplitEpicCommandHandler.cs` | `SplitEpicCommandHandler` | `Handle` | Write-back — epic creation | ✅ YES |

---

## Appendix B: DataSourceModeProvider State Machine

```
┌─────────────────────────────────────────────────────────┐
│                   Request Starts                        │
└───────────────────────┬─────────────────────────────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Scoped instance    │
              │  created            │
              │  _currentMode = Live│ ◀── DEFAULT (PROBLEM)
              └─────────┬───────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  Middleware checks  │
              │  route              │
              └─────────┬───────────┘
                        │
            ┌───────────┴────────────┐
            │                        │
            ▼                        ▼
    ┌───────────────┐        ┌──────────────┐
    │ Workspace     │        │ Settings     │
    │ route?        │        │ route?       │
    └───────┬───────┘        └──────┬───────┘
            │                       │
            ▼                       ▼
    ┌───────────────┐        ┌──────────────┐
    │ Check cache   │        │ Keep Live    │
    │ state for PO  │        │ mode         │
    └───────┬───────┘        └──────────────┘
            │
     ┌──────┴───────┐
     │              │
     ▼              ▼
┌─────────┐   ┌─────────┐
│ Cache   │   │ No      │
│ exists? │   │ cache   │
└────┬────┘   └────┬────┘
     │             │
     ▼             ▼
┌─────────┐   ┌─────────┐
│ Set     │   │ Set     │
│ Cache   │   │ Live    │
│ mode    │   │ mode    │
└────┬────┘   └────┬────┘
     │             │
     └──────┬──────┘
            │
            ▼
    ┌───────────────┐
    │ Handler runs  │
    │ Factory reads │
    │ mode          │
    └───────┬───────┘
            │
            ▼
    ┌───────────────┐
    │ Correct       │
    │ provider      │
    │ returned      │
    └───────────────┘
```

---

**End of Report**
