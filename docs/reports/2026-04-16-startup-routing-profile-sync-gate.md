# Startup routing profile/sync gate

## Root cause

- Startup routing was split between `Index.razor` and `StartupGuard.razor`, so the root landing path and deep-link gating did not use one shared decision path.
- `StartupOrchestratorService` treated the server-side `ActiveProfileId` as sufficient proof that a profile was selected, even when the browser had no validated local profile selection for the current session.
- Because the client did not revalidate cached browser profile state before classifying readiness, a stale or server-leftover active profile could let startup route directly to `/home`.
- Deep links to workspace pages were blocked locally instead of being redirected deliberately into the prerequisite flow with a preserved return path.

## Exact files changed

- `PoTool.Client/App.razor`
- `PoTool.Client/Components/Common/StartupRouteView.razor`
- `PoTool.Client/Layout/MainLayout.razor`
- `PoTool.Client/Pages/Index.razor`
- `PoTool.Client/Pages/ProfilesHome.razor`
- `PoTool.Client/Pages/SyncGate.razor`
- `PoTool.Client/Services/StartupNavigationTargetResolver.cs`
- `PoTool.Client/Services/StartupOrchestratorService.cs`
- `PoTool.Client/Services/StartupReturnUrlHelper.cs`
- `PoTool.Tests.Unit/Services/StartupGuardRouteMatcherTests.cs`
- `PoTool.Tests.Unit/Services/StartupOrchestratorServiceTests.cs`
- `docs/release-notes.json`

## Startup decision table

| Conditions | Expected route |
| --- | --- |
| Root startup, setup incomplete, onboarding not completed | `/onboarding` |
| No profiles exist yet | `/profiles?returnUrl=%2Fhome` |
| Profiles exist but browser has no validated selected profile | `/profiles?returnUrl=<requested route>` |
| Browser cached profile is invalid or deleted | `/profiles?returnUrl=<requested route>` and clear cached selection |
| Valid selected profile exists but no successful cache sync exists | `/sync-gate?returnUrl=<requested route>` |
| Valid selected profile exists and cache has completed successfully | Requested workspace route, or `/home` from `/` |
| Startup backend/readiness call fails | `/startup-blocked?...` |

## Persisted state behaviors considered

- `localStorage["ActiveProfileId"]` is now treated as provisional until the profile ID is revalidated against the API.
- If the browser has no cached profile selection, startup classifies the session as profile-not-ready even when the backend still holds an old active profile.
- If the cached profile ID no longer resolves to a profile, the cached key is removed and startup routes back into profile selection.
- When the cached profile is valid but the backend active profile is missing or mismatched, startup restores the backend active profile from the validated browser selection before evaluating sync readiness.
- Return URLs for profile-selection and sync-gate redirects are normalized through one shared helper to reject unsafe values and preserve valid deep links.

## Tests added/updated

- Updated `StartupOrchestratorServiceTests` to cover:
  - missing cached profile selection -> `NotReady`
  - invalid cached profile selection -> `NotReady`
  - valid cached profile selection with missing server active profile -> restored and `Ready`
  - existing sync-required and routing-state coverage
- Updated `StartupGuardRouteMatcherTests` to cover:
  - deep-link return URL preservation for profile redirects
  - return URL normalization fallback behavior

## Remaining risks or follow-up items

- Full `PoTool.Tests.Unit` still has pre-existing unrelated failures in governance/documentation audits and cache-backed client migration checks.
- Manual browser verification of the new central gate flow was not performed in this environment.
