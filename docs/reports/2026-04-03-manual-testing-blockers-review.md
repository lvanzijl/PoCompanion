# Manual Testing Blockers Review

## 1. Executive summary

- **Overall assessment:** Manual testing is **not reliably ready**. The backend and frontend can both be started in development, but the first-run/testing path still contains blockers that either stop the app from being reached at all, bypass the intended startup gates, or make key pages fail/misreport when cache-backed endpoints are not ready yet.
- **Critical blockers:** 2
- **Major blockers:** 3
- **Medium risks:** 3

## 2. Critical blockers

### 2.1 API host does not serve the SPA shell
- **Severity:** Critical
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj`
- **Why it breaks manual testing:** A tester who starts the backend and opens the backend URL never reaches the app UI. The API host advertises Blazor hosting middleware, but it has no client static assets to serve.
- **Exact failure path:** `PoTool.Api` startup → `UseBlazorFrameworkFiles()` / `MapFallbackToFile("index.html")` configured → runtime logs warn that `/PoTool.Api/wwwroot` is missing → `GET /` and `GET /home` on `http://localhost:5291` return `404`.
- **Likely user-visible symptom:** “The app starts, but the browser shows 404 instead of the application.”
- **Root cause:** The API project is configured like a hosted Blazor entry point, but it has no `PoTool.Client` project reference or client static web assets in its own web root, so the fallback route has nothing to serve.
- **Recommended fix:** Either make `PoTool.Api` the real hosted entry point by referencing the client static assets, or stop pretending it is and provide one supported local launch path that starts both API and client together.
- **Bug class:** Split-host startup model drift.

### 2.2 Cache-backed contract mismatch breaks core pages on cold cache
- **Severity:** Critical
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BuildQualityService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Filters/CacheBackedDataStateContractFilter.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/DataSourceModeConfiguration.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- **Why it breaks manual testing:** A tester can open major workspace pages before cache is ready; the API correctly returns a cache-state wrapper, but the client deserializes it as a business DTO and fails.
- **Exact failure path:** `/home/health/overview` → `HealthOverviewPage.LoadOverviewAsync()` → `BuildQualityService.GetRollingWindowAsync()` → `GET /api/buildquality/rolling` → `DataSourceModeConfiguration` classifies `/api/buildquality/*` as cache-only → `CacheBackedDataStateContractFilter` wraps the response as `DataStateResponseDto<DeliveryQueryResponseDto<BuildQualityPageDto>>` when cache is not ready → client tries to deserialize directly to `DeliveryQueryResponseDto<BuildQualityPageDto>` → JSON required properties are missing.
- **Likely user-visible symptom:** The page renders an explicit hard error such as `Build Quality overview unavailable right now: JsonRequiredPropertiesMissing...`.
- **Root cause:** The client-side build-quality service is built against the inner DTO contract, while the backend transparently upgrades cache-backed routes to the outer `DataStateResponseDto<>` contract.
- **Recommended fix:** Change cache-backed client services to consume `DataStateResponseDto<>` first, then unwrap to canonical client responses.
- **Bug class:** Cache-backed endpoint / raw client contract drift.

## 3. Major blockers

### 3.1 StartupGuard is effectively disabled for every route
- **Severity:** Major
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/StartupGuard.razor`
- **Why it breaks manual testing:** The component that should stop testers from entering pages before setup/profile/cache readiness is never actually enforcing anything.
- **Exact failure path:** `StartupGuard.DefaultExemptPages` contains `"/"` → `CheckReadinessAsync()` uses `currentPath.StartsWith(p)` → every normalized route starts with `"/"` → `_isReady = true` for all pages.
- **Likely user-visible symptom:** Testers can click straight from onboarding/profiles into workspace pages that should have been blocked, then hit confusing empty states or downstream request failures.
- **Root cause:** Prefix matching plus a root-route exemption turns the exemption list into a global bypass.
- **Recommended fix:** Treat `"/"` as an exact-match exemption, not a prefix exemption; then explicitly list only the routes that must bypass startup gating.
- **Bug class:** Route guard wildcard bug.

### 3.2 Project planning overview silently shows false empty data when cache is not ready
- **Severity:** Major
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProjectService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/DataSourceModeConfiguration.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Filters/CacheBackedDataStateContractFilter.cs`
- **Why it breaks manual testing:** This is worse than a visible error: the page can look valid while showing fabricated zero values.
- **Exact failure path:** `/planning/{project}/overview` → `ProjectPlanningOverview.OnInitializedAsync()` → `ProjectService.GetPlanningSummaryAsync()` → `GET /api/projects/{alias}/planning-summary` → backend returns `DataStateResponseDto<ProjectPlanningSummaryDto>` when cache is not ready → client deserializes directly to `ProjectPlanningSummaryDto`, whose default constructor produces zero counts and empty lists.
- **Likely user-visible symptom:** The page renders “0 products / 0 work / 0 effort” even though the API actually said “cache not built yet.”
- **Root cause:** Another cache-backed contract mismatch, but here the DTO shape is forgiving enough to deserialize into a plausible-looking empty object instead of throwing.
- **Recommended fix:** Make `ProjectService.GetPlanningSummaryAsync()` consume the data-state envelope and surface not-ready/failed states explicitly.
- **Bug class:** Cache-backed endpoint / raw client contract drift.

### 3.3 Readiness failures are treated as “ready enough,” so broken backends route users into dead screens
- **Severity:** Major
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/StartupOrchestratorService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Index.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/StartupGuard.razor`
- **Why it breaks manual testing:** When the readiness check fails, the app does not stop and explain that startup failed; it resets onboarding state, allows guarded pages, and pushes the tester deeper into failing flows.
- **Exact failure path:** `StartupOrchestratorService.GetStartupReadinessAsync()` returns `null` for any HTTP failure or non-success response → `Index.razor` falls back to `/sync-gate?returnUrl=%2Fhome` → `StartupGuard` also treats `null` readiness and caught exceptions as `_isReady = true`.
- **Likely user-visible symptom:** Instead of one clear blocking message, testers see a mix of onboarding resets, sync-gate retries, null data, and scattered page-level errors.
- **Root cause:** “Graceful degradation” is implemented as “assume access” instead of “fail closed with a single explicit startup error.”
- **Recommended fix:** Make readiness failure a first-class blocking state with one dedicated screen and stop routing into feature pages until readiness can be retrieved.
- **Bug class:** Fail-open startup orchestration.

## 4. Medium risks

### 4.1 Production exception handling points to a route that does not exist
- **Severity:** Medium
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`, repository-wide search found no `/Error` route/page
- **Why it breaks manual testing:** Development works because this branch is not used, but any production-style manual verification can degrade into a secondary routing failure instead of a controlled error response.
- **Exact failure path:** non-development startup → `UseExceptionHandler("/Error")` → exception occurs → no `/Error` endpoint/page exists.
- **Likely user-visible symptom:** A secondary 404/error-handler failure instead of a stable error page.
- **Root cause:** Exception middleware is configured for a route the application does not implement.
- **Recommended fix:** Add a real `/Error` endpoint/page or replace the handler with API-appropriate problem-details middleware.
- **Bug class:** Missing error-route wiring.

### 4.2 Backend startup can hard-stop on incompatible local database state
- **Severity:** Medium
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`
- **Why it breaks manual testing:** A tester who already has an older or manually created database can be blocked before the app starts at all.
- **Exact failure path:** startup database probe → tables exist but migration history does not → explicit `InvalidOperationException` is thrown before the host finishes starting.
- **Likely user-visible symptom:** Backend refuses to start and logs “Please delete the database and restart.”
- **Root cause:** Startup chooses hard-fail compatibility enforcement with no automated recovery path.
- **Recommended fix:** Keep the hard guard, but add one documented/dev-only reset path or automatic detection tooling so testers do not have to reverse-engineer why startup failed.
- **Bug class:** Local-state startup fragility.

### 4.3 Build-quality cache contract drift is broader than one page
- **Severity:** Medium
- **Where it is:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BuildQualityService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/TrendsWorkspace.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor`
- **Why it breaks manual testing:** Even where pages do not crash, the same contract mismatch degrades major tiles and build-quality panels into “unavailable” states during cold-cache journeys.
- **Exact failure path:** workspace/trend/sprint page → `BuildQualityService` raw deserialize → cache-backed API returns data-state wrapper while cache is not ready → page catches and suppresses the exception into a generic unavailable message.
- **Likely user-visible symptom:** Home/trends/delivery surfaces quietly lose build-quality data instead of explaining that cache readiness is the real blocker.
- **Root cause:** The same cache-envelope mismatch is reused across multiple build-quality entry points.
- **Recommended fix:** Fix the client service once and reuse the same data-state-aware contract everywhere.
- **Bug class:** Shared client-service contract drift.

## 5. Journey review

### Backend startup
- **Status:** Pass
- **Explanation:** In development/mock mode the backend starts cleanly, applies/uses the SQLite database, seeds mock data, and listens on `http://localhost:5291`.

### Frontend startup
- **Status:** Risk
- **Explanation:** The Blazor client dev server starts cleanly on `http://localhost:5292`, but the backend host does not serve the client shell, so startup succeeds only if the tester knows to run the separate client project.

### Home/landing load
- **Status:** Risk
- **Explanation:** `/` and `/profiles` load under the client dev server, and `/home` renders, but the route immediately produces missing-current-sprint 404s and degraded workspace signals; the startup flow is not robustly enforcing readiness before reaching home.

### Navigation between major hubs/workspaces
- **Status:** Risk
- **Explanation:** Navigation links function, but because `StartupGuard` is effectively disabled, testers can enter hubs before cache/setup state is valid. Some pages degrade gracefully; others fail or misreport data.

### Representative page loads
- **Status:** Fail
- **Explanation:** `/home/health/overview` fails with a deserialization error, and `/planning/battleship-systems/overview` can render an apparently valid zero-data summary while the API is actually reporting “cache not built yet.”

### Empty/fresh system behavior
- **Status:** Risk
- **Explanation:** Fresh development runs are rescued by mock-mode seeding, so the repository looks healthier than the underlying startup paths really are. In a true empty/real-mode scenario, the current guard/orchestration logic would not reliably stop users before broken feature paths.

### Partially configured system behavior
- **Status:** Fail
- **Explanation:** Readiness failures are handled as “go forward anyway,” and incompatible local databases can still abort backend startup outright. The system does not consistently isolate testers in a recoverable setup state.

## 6. Bug-class analysis

- **Cache-backed endpoint / raw client drift**
  - The backend intentionally wraps cache-only routes in `DataStateResponseDto<>`.
  - Several client services (`BuildQualityService`, `ProjectService`, likely other raw `HttpClient` services) still deserialize straight to the inner DTO.
  - This produces both hard failures and silent false-empty screens.

- **Fail-open startup gating**
  - Startup readiness failures return `null`.
  - `Index` and `StartupGuard` convert that into more navigation, not less.
  - The app therefore amplifies backend/startup failures into scattered page failures.

- **Wildcard exemption in route guarding**
  - The guard’s exemption list contains `"/"` and uses prefix matching.
  - That makes every route exempt and defeats the guard’s purpose.

- **Split-host launch ambiguity**
  - The API host is configured like a hosted SPA, but does not actually host the SPA.
  - The client works only when run separately, which is not enforced by the backend startup path.

- **Cold-cache paths are insufficiently first-class**
  - Early manual journeys frequently hit cache-not-ready states.
  - Some pages model that state correctly; others treat it as data, an exception, or an empty business result.

## 7. Fix order

### First fix now
- **Fix `StartupGuard` and readiness failure handling together**
- **Why this first:** One change closes the biggest blast radius. It stops testers from reaching pages that are guaranteed to misbehave when startup/cache/profile state is incomplete.

### Second fix
- **Fix cache-backed client contract handling for build quality and project planning summary routes**
- **Why this second:** It restores two high-visibility workspace pages and removes both a hard failure mode and a silent false-data mode.

### Third fix
- **Fix the supported launch model**
- **Why this third:** Manual testing should not depend on tribal knowledge about starting a separate client dev server. Either the API must host the SPA correctly, or the repository must provide one explicit two-project launch profile/script.

## 8. Final verdict

- **Is manual testing currently likely to be blocked?** Yes.
- **Where will testing likely end first?** Either at app startup if the tester opens the backend URL expecting the UI, or on the first cold-cache visit to build-quality/planning pages where client/API contracts diverge.
- **What minimum fixes are needed before serious manual testing is worth doing?** Restore real startup gating, make cache-backed client services understand `DataStateResponseDto<>`, and provide one unambiguous working app-start path.
