# Startup-state route classification fix

## Root cause

`GET /api/startup-state` was introduced as a controller route with the exact path `/api/startup-state`, but `PoTool.Api/Configuration/DataSourceModeConfiguration.cs` only allowed the related startup discovery family through the prefix `/api/startup`.

The classifier matches whole route segments, so `/api/startup-state` does not match `/api/startup`. Because unknown fallback is intentionally disabled, `DataSourceModeMiddleware` threw `RouteNotClassifiedException` before the endpoint handler could run.

## Exact classification added

- Added `/api/startup-state` to `DataSourceModeConfiguration.LiveModeAllowedExactRoutes`.

## Why that classification is correct

- The endpoint is a read-only startup/configuration orchestration endpoint.
- It must run before a successful cache-backed workspace session exists because it decides whether the user should go to Profiles, Sync Gate, blocked startup, or the requested route.
- Its behavior is not a cache-only analytical read surface; classifying it as cache-required would block the startup flow it is responsible for resolving.
- The repository already classifies startup, onboarding, configuration, discovery, sync, and administrative endpoints as live-allowed. `startup-state` belongs to that existing model.

## Exact files changed

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/DataSourceModeConfiguration.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DataSourceRouteClassificationAuditTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## Tests added/updated

- Updated `DataSourceModeConfigurationTests` to assert `/api/startup-state` resolves to `LiveAllowed`.
- Updated `DataSourceModeMiddlewareTests` to verify `/api/startup-state` now sets live mode and no longer fails classification.
- Added `DataSourceRouteClassificationAuditTests` to reflect over managed controller routes and fail if any managed API controller endpoint resolves to `Unknown` in `DataSourceModeConfiguration`.

## Additional unclassified routes found

- No additional managed controller routes were found to be unclassified after adding the audit coverage.

## Remaining risks or follow-up items

- The new audit covers controller-based managed routes. Newly added managed minimal API routes still rely on deliberate updates to `DataSourceModeConfiguration` and targeted tests when introduced.
