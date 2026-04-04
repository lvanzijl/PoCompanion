# Startup Guard Fix

## What was broken

- `StartupGuard` exempted every route because `"/"` was matched with `StartsWith`, so `/home`, `/planning`, and other feature routes were treated as exempt.
- `StartupOrchestratorService.GetStartupReadinessAsync()` returned `null` on HTTP failures, invalid responses, and unexpected exceptions.
- `Index.razor` treated missing readiness as a reason to continue into `/sync-gate`, which kept startup fail-open instead of blocking.
- `StartupGuard` also failed open by setting `_isReady = true` when readiness checks failed or threw.
- Non-ready states were inferred indirectly from nullable DTO data rather than expressed as explicit startup states.

## What was changed

- Added explicit startup states:
  - `Ready`
  - `NotReady`
  - `SetupRequired`
  - `SyncRequired`
  - `Unavailable`
  - `Error`
- Replaced nullable startup results with `StartupReadinessResult`, so readiness checks now always return a structured result.
- Updated `StartupOrchestratorService` to:
  - classify backend failures as `Unavailable` or `Error`
  - classify missing config/profile setup as `SetupRequired`
  - classify missing active profile as `NotReady`
  - classify missing cache sync as `SyncRequired`
  - return `Ready` only when startup and cache preconditions are satisfied
- Added `StartupGuardRouteMatcher` so `"/"` only matches the root route exactly, while explicit routes like `/settings` still support nested subpaths.
- Reworked `StartupGuard` to block non-exempt pages whenever readiness is not `Ready`.
- Added a shared `StartupBlockingPanel` and a dedicated `/startup-blocked` page for blocking startup failures.
- Updated `Index.razor` to route explicitly by readiness state instead of continuing on unknown readiness.
- Added unit coverage for exemption matching and startup routing helpers/state handling.

## Before vs after behavior

### Before

- `/home` was accidentally exempt because `"/"` matched every route prefix.
- Backend readiness failures returned `null`.
- Root startup routing continued into the app on readiness failure.
- Feature pages could render after readiness exceptions.

### After

- `"/"` is exact-match only; `/home` and other feature routes are no longer accidentally exempt.
- Startup readiness always returns an explicit result.
- `Error` and `Unavailable` route to a blocking startup page.
- `SetupRequired` routes to recovery pages.
- `SyncRequired` routes to sync flow.
- `StartupGuard` blocks feature rendering unless readiness is explicitly `Ready`.

## Remaining risks

- Exempt recovery pages such as `/settings`, `/profiles`, and `/sync-gate` still bypass `StartupGuard` by design so users can recover; if those pages need stricter degraded-mode handling later, that should be implemented separately.
- Root startup still preserves the onboarding branch for setup-required first-run scenarios; if product requirements change, onboarding and startup routing may need another pass to fully unify the flows.
- The new readiness model classifies cache readiness client-side using `CacheSyncService`; if that endpoint contract changes, startup classification tests should be updated alongside it.
