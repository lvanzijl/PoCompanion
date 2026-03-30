# PR Cache-Only Guardrails

## Summary

Provider-driven pull request analytics no longer fall back to `LivePullRequestReadProvider` when the cache is unavailable. Route intent classification now distinguishes cache-only analytical/workspace reads from live-allowed onboarding, configuration, discovery, and sync flows, and the middleware returns an explicit cache-not-ready response for analytical routes instead of silently switching to live mode.

## Intent Classification

### Cache-only analytical/workspace routes

These route prefixes now require cached data and are blocked with an explicit conflict response when no successful sync exists for the active profile:

- `/api/pullrequests`
- `/api/workitems`
- `/api/pipelines`
- `/api/releaseplanning`
- `/api/filtering`
- `/api/metrics`

For the pull request slice, this includes provider-driven analytical endpoints such as:

- `GET /api/pullrequests`
- `GET /api/pullrequests/{id}`
- `GET /api/pullrequests/filter`
- `GET /api/pullrequests/metrics`
- `GET /api/pullrequests/{id}/iterations`
- `GET /api/pullrequests/{id}/comments`
- `GET /api/pullrequests/{id}/filechanges`
- `GET /api/pullrequests/sprint-trends`
- `GET /api/pullrequests/review-bottleneck`

### Live-allowed configuration/discovery routes

These remain live-capable:

- `/api/settings`
- `/api/tfsconfig`
- `/api/startup`
- `/api/profiles`
- `/api/teams`
- `/api/products`
- `/api/repositories`
- `/api/workitems/area-paths-from-tfs`
- `/api/workitems/goals-from-tfs`
- `/api/workitems/validate`
- `/api/workitems/revisions`
- `/api/tfs/verify`
- `/api/tfs/validate`
- `/api/datasource`
- `/api/cachesync`
- `/health`

## Affected Files

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/DataSourceModeConfiguration.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/DataSourceModeMiddleware.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/WorkspaceGuardMiddleware.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Middleware/WorkspaceGuardMiddlewareTests.cs`

## Before vs After

### Before

- `DataSourceModeMiddleware` treated broad workspace prefixes as one bucket.
- `IDataSourceModeProvider.GetModeAsync(...)` returned `Live` when no successful sync existed yet.
- `LazyPullRequestReadProvider` resolved the provider at call time.
- `DataSourceAwareReadProviderFactory` mapped any non-`Cache` mode to the live provider.
- Result: analytical PR endpoints could silently hit live TFS.
- Explicit discovery routes under broader prefixes (for example `/api/workitems/area-paths-from-tfs`) were also vulnerable to coarse route classification.

### After

- Route intent is resolved explicitly.
- Explicit live-allowed routes are matched before broader cache-only prefixes.
- Cache-only analytical routes require an active profile plus a successful cache sync.
- If cache prerequisites are missing, middleware returns `409 Conflict` with a `Cache not ready` problem response and does not invoke the downstream endpoint.
- `WorkspaceGuardMiddleware` is always active as a defensive backstop and now guards cache-only analytical routes instead of being described as development-only.

## Validation

Verified with focused unit coverage for:

- route classification of PR analytical routes and live-allowed discovery routes
- middleware blocking cache-only analytical routes when cache is missing
- middleware preserving live access for explicit discovery routes under `/api/workitems/*`
- guard middleware allowing live discovery routes while still rejecting live execution on cache-only analytical routes

## Known Limitations

- The enforcement is still prefix-based rather than endpoint-metadata-based.
- The shared provider factory still resolves providers from the current mode only; the guardrails rely on route intent classification plus middleware enforcement to prevent analytical live fallback.
