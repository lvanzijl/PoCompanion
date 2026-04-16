# Startup server authority

## Full API contract definition

Authoritative endpoint:

- `GET /api/startup-state`

Query parameters:

- `returnUrl` — raw current client route, validated server-side and normalized into the contract
- `profileHintId` — optional browser hint only; never authoritative

Response contract (`StartupStateResponseDto`):

| Field | Type | Notes |
| --- | --- | --- |
| `startupState` | `StartupStateDto` | `NoProfile`, `ProfileInvalid`, `ProfileValid_NoSync`, `Ready`, `Blocked` |
| `targetRoute` | `string` | final normalized route the client must navigate to without reinterpretation |
| `returnUrl` | `string?` | server-validated safe return route |
| `activeProfileId` | `int?` | authoritative active profile after reconciliation |
| `syncStatus` | `StartupSyncStatusDto` | explicit startup sync result (`NotApplicable`, `Missing`, `InProgress`, `Success`, `SuccessWithWarnings`, `Failed`, `Invalidated`, `MissingData`) |
| `blockedReason` | `StartupBlockedReasonDto?` | structured blocked category |
| `diagnostics` | `StartupDiagnosticFlagsDto` | structured flags for configuration, profile-hint handling, cache presence, and sync validity inputs |

Server routing outcomes:

- `NoProfile` → `/profiles?returnUrl=...`
- `ProfileInvalid` → `/profiles?returnUrl=...`
- `ProfileValid_NoSync` → `/sync-gate?returnUrl=...`
- `Ready` → validated deep link or `/home`
- `Blocked` → `/startup-blocked`

## Comparison: client-state-machine vs server-authority

### Before

- Client called `/api/Startup/readiness`
- Client reconciled:
  - server active profile
  - browser `ActiveProfileId`
  - cache/sync validity
  - deep-link normalization
- Client built the startup target route itself

### After

- Client calls `/api/startup-state`
- Server resolves in one request:
  - configuration readiness
  - server active profile validity
  - optional browser hint restoration or rejection
  - sync validity
  - safe returnUrl normalization
  - final route target
- Client consumes the contract and only:
  - blocks render until the response arrives
  - mirrors the authoritative profile id back into client hint storage
  - compares current route to returned target route

## List of removed client responsibilities

Removed from client startup orchestration:

- profile reconciliation against server settings
- invalid-profile cleanup decisions
- sync validity evaluation
- stale/partial sync rejection rules
- deep-link returnUrl normalization for startup decisions
- startup route construction from readiness fragments

Retained client responsibilities:

- calling the endpoint
- blocking router render until the response arrives
- navigating to the returned route
- displaying blocked-state copy derived from the authoritative response/fallback transport error

## Proof that client no longer infers readiness

- `PoTool.Client/Services/StartupOrchestratorService.cs` no longer calls profile or cache endpoints to determine readiness.
- The client startup adapter now makes one `GetStartupStateAsync(...)` call and trusts:
  - `targetRoute`
  - `startupState`
  - `activeProfileId`
  - `syncStatus`
- `PoTool.Client/Services/StartupNavigationTargetResolver.cs` now only compares routes and provides the fallback blocked route for transport failures.
- All startup readiness decisions now live in `PoTool.Api/Services/StartupStateResolutionService.cs`.

## Test coverage summary

Backend unit coverage:

- missing configuration returns `Blocked`
- no profiles returns `NoProfile`
- invalid persisted active profile clears server selection and returns `ProfileInvalid`
- valid client hint restores server selection
- stale successful sync becomes `Invalidated`
- ready deep link returns `Ready` with preserved route

Client coverage:

- startup adapter maps server `targetRoute` directly for:
  - `NoProfile`
  - `ProfileInvalid`
  - `ProfileValid_NoSync`
  - `Ready`
  - `Blocked`
- backend failure falls back to `/startup-blocked`
- delayed startup resolution keeps router hidden until the response arrives
- coordinator leaves navigation pending until the resolved startup target is reached

Validation executed:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~StartupOrchestratorServiceTests|FullyQualifiedName~StartupGuardRouteMatcherTests|FullyQualifiedName~StartupGateCoordinatorTests|FullyQualifiedName~StartupStateResolutionServiceTests|FullyQualifiedName~BucketAGeneratedClientMigrationAuditTests|FullyQualifiedName~NswagGovernanceTests.CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource|FullyQualifiedName~NswagGovernanceTests.GeneratedClient_DoesNotRecreateSharedPublicTypes" --logger "console;verbosity=minimal"`

## Remaining risks or limitations

- Transport-level failures still require a client fallback to `/startup-blocked` because no server response exists to consume.
- The governed OpenAPI snapshot and generated client were updated manually in this change; future explicit NSwag regeneration should preserve the same `/api/startup-state` contract.
- Full `NswagGovernanceTests` still include an unrelated pre-existing `ApiProject_DoesNotReferenceClientProject` failure outside this startup scope.
