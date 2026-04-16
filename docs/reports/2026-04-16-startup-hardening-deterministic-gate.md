# Startup hardening deterministic gate

## Final state machine definition

Top-level authoritative states emitted by `ResolveStartupStateAsync()`:

| State | Meaning | Route outcome |
| --- | --- | --- |
| `NoProfile` | No valid active profile is selected after reconciling server state and browser hint | `/profiles?returnUrl=...` |
| `ProfileInvalid` | A previously selected profile is invalid (missing server profile or invalid cached hint) and was explicitly cleared | `/profiles?returnUrl=...` |
| `ProfileValid_NoSync` | A valid active profile exists, but the cache does not satisfy startup sync validity rules | `/sync-gate?returnUrl=...` |
| `Ready` | Profile selection and sync validity are fully resolved and the requested ready route may render | requested route or `/home` |
| `Blocked` | Startup cannot continue deterministically (missing configuration, backend unavailable, invalid response, or other blocking failure) | `/startup-blocked?...` |

Deterministic reconciliation rules:

1. Server configuration is evaluated first.
2. Server active profile is authoritative when present and valid.
3. Browser `ActiveProfileId` is only a hint:
   - if server already has a valid active profile, the browser hint is overwritten to match it
   - if server has no active profile but the browser hint resolves to a valid profile, the server is explicitly restored to that profile
   - if the browser hint is invalid, it is removed and startup resolves to `ProfileInvalid`
4. Sync validity is evaluated only after a single valid active profile is resolved.

## Before vs after startup flow

### Before

1. `App.razor` initialized `Router` immediately.
2. `StartupRouteView` resolved startup asynchronously after route matching had already started.
3. Startup decisions were split across:
   - `StartupRouteView`
   - `ProfilesHome.razor`
   - `SyncGate.razor`
   - `Onboarding.razor`
   - layout/profile callbacks
4. Browser profile state and server active profile could temporarily disagree.
5. Sync success, profile selection, and onboarding completion could navigate directly from pages.

### After

1. `App.razor` renders only `StartupGate`.
2. `StartupGate` blocks `Router` creation until `StartupGateCoordinator` finishes `ResolveStartupStateAsync()`.
3. `StartupOrchestratorService` resolves one atomic startup result:
   - configuration
   - server/client profile reconciliation
   - sync validity
   - final route target
4. Only after the current URI matches the resolved startup target does `Router` render.
5. Profile selection, onboarding completion, and sync completion now signal the root gate to re-resolve instead of navigating directly.

## Proof that no route renders before readiness resolution

- `PoTool.Client/App.razor` no longer instantiates `Router` directly.
- `PoTool.Client/Components/Common/StartupGate.razor` renders only a minimal loading shell while `StartupGateCoordinator.Snapshot.ShouldRenderRouter == false`.
- `PoTool.Client/Services/StartupGateCoordinator.cs` sets `ShouldRenderRouter = false` immediately when resolution starts and only enables router rendering after the orchestrator returns a final `StartupStateResolution`.
- `PoTool.Tests.Unit/Services/StartupGateCoordinatorTests.cs` includes a delayed-resolution test proving that the coordinator keeps router rendering disabled while startup resolution is still pending.

## List of removed redirect paths

Removed direct startup navigation from:

- `PoTool.Client/Components/Common/StartupRouteView.razor` (deleted)
- `PoTool.Client/Pages/ProfilesHome.razor`
- `PoTool.Client/Pages/SyncGate.razor`
- `PoTool.Client/Pages/Onboarding.razor`
- `PoTool.Client/Layout/MainLayout.razor` profile-change startup redirect path
- `PoTool.Client/Pages/Home/WorkspaceBase.cs` no-profile redirect path
- `PoTool.Client/Pages/TfsConfig.razor` startup-readiness page-level dependency

These surfaces now mutate state and request root-gate re-evaluation instead of navigating themselves.

## Sync validity rules defined

Startup now treats sync as valid only when all of the following are true:

1. `SyncStatus == Success`
2. `LastSuccessfulSync` is present
3. `LastAttemptSync` is present
4. `LastAttemptSync` is not later than `LastSuccessfulSync + 5 seconds`
5. `WorkItemWatermark` is present

Startup explicitly rejects:

- `Idle`
- `InProgress`
- `Failed`
- `SuccessWithWarnings`
- successful sync records missing `WorkItemWatermark`
- cache states where a later attempt has invalidated the last successful sync timestamp

## Test coverage summary

Updated or added tests:

- `StartupOrchestratorServiceTests`
  - backend unavailable -> `Blocked`
  - missing configuration -> `Blocked`
  - no profiles -> `NoProfile`
  - invalid server active profile -> `ProfileInvalid`
  - server profile overrides mismatched browser hint
  - valid browser hint restores server active profile when server is empty
  - no successful sync -> `ProfileValid_NoSync`
  - later failed attempt rejects stale sync
  - missing work-item watermark rejects partial sync
  - ready deep link preserves requested route

- `StartupGateCoordinatorTests`
  - delayed resolution keeps router hidden while startup is unresolved
  - redirect-required state leaves navigation pending
  - ready-state deep link enables router rendering without intermediate redirect

- `StartupGuardRouteMatcherTests`
  - startup target mapping for profiles/sync/blocked routes
  - startup-flow `returnUrl` extraction
  - invalid `returnUrl` fallback

Validation run:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~StartupOrchestratorServiceTests|FullyQualifiedName~StartupGuardRouteMatcherTests|FullyQualifiedName~StartupGateCoordinatorTests" --logger "console;verbosity=minimal"`

## Remaining architectural risks

- The authoritative startup contract is still resolved client-side from server APIs plus a browser hint; the logic is now single-sourced and atomic, but a future server-side startup-resolution endpoint would reduce client orchestration even further.
- `SyncGate` still owns sync execution/progress UX even though routing is centralized; if startup sync orchestration later moves server-side, this page should become a pure progress surface.
- Full `PoTool.Tests.Unit` continues to have unrelated pre-existing failures in governance/documentation/cache-migration audits outside this startup scope.
