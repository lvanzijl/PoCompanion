# Slice 3 — Status Engine

## 1. Scope Confirmation

Slice 3 was implemented strictly as the onboarding completion/status engine.

Included:
- persisted-state status computation for:
  - connection
  - data source setup
  - domain configuration
- server-side `OnboardingStatusDto` contract
- `GET /api/onboarding/status`
- deterministic blocking reason derivation
- deterministic warning derivation
- status observability hooks

Explicitly excluded:
- no onboarding writes
- no CRUD endpoints
- no UI changes
- no onboarding wizard/session state
- no migration execution
- no routing/cutover behavior

## 2. Endpoint Implemented

Implemented endpoint:
- `GET /api/onboarding/status`

Added API/controller path:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/OnboardingStatusController.cs`

Added supporting contracts:
- `OnboardingConfigurationStatus`
- `OnboardingStatusIssueDto`
- `OnboardingStatusCountsDto`
- `OnboardingStatusDto`

## 3. Status Computation Flow

Status is computed server-side from persisted onboarding state only.

Implemented flow:
1. Load persisted onboarding entities from Slice 1 tables
2. Read persisted validation state from:
   - connection availability/permission/capability state
   - project/team/pipeline/root/binding validation state
3. Compute effective valid entity sets
4. Compute per-flow status:
   - `connectionStatus`
   - `dataSourceSetupStatus`
   - `domainConfigurationStatus`
5. Derive:
   - `overallStatus`
   - `blockingReasons[]`
   - `warnings[]`
   - valid/total counts
6. Emit status metrics and blocker/warning logs
7. Return the success envelope

Implemented rules match the authoritative reports:
- `NotConfigured` only when no persisted onboarding state exists
- `PartiallyConfigured` when persisted state exists but complete criteria are not satisfied
- `Complete` only when the persisted connection, required project sources, product roots, and bindings satisfy the Slice 3 rules

## 4. Blocking & Warning Strategy

Blocking reasons are derived deterministically from persisted invalid or missing prerequisites, including:
- missing/invalid connection
- missing required enabled valid project source
- missing required enabled valid product root
- invalid enabled source/root entities
- missing required project binding per product root
- invalid or scope-mismatched bindings

Warnings are derived deterministically from persisted snapshot metadata only:
- rename detected
- stale snapshot

Observed log/metric hooks added in:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingObservability.cs`

Added observability identifiers:
- `onboarding.status.count`
- `onboarding.status.blocker.count`
- `onboarding.status.warning.count`

## 5. Persisted Inputs & Derived Output

Status reads these persisted Slice 1 entities:
- `TfsConnection`
- `ProjectSource`
- `TeamSource`
- `PipelineSource`
- `ProductRoot`
- `ProductSourceBinding`

Derived output structure:

```text
OnboardingStatusDto
  overallStatus
  connectionStatus
  dataSourceSetupStatus
  domainConfigurationStatus
  blockingReasons[]
    code
    message
    entityType
    entityExternalId
  warnings[]
    code
    message
    entityType
    entityExternalId
  counts
    projectSourcesTotal/projectSourcesValid
    teamSourcesTotal/teamSourcesValid
    pipelineSourcesTotal/pipelineSourcesValid
    productRootsTotal/productRootsValid
    bindingsTotal/bindingsValid
```

Persisted input examples used by the computation:

```text
TfsConnection
  availabilityValidationState.status
  permissionValidationState.status
  capabilityValidationState.status

ProjectSource
  enabled
  validationState.status
  snapshot.metadata

ProductSourceBinding
  enabled
  sourceType
  projectSourceId
  teamSourceId/pipelineSourceId
  validationState.status
```

## 6. Error Handling

The endpoint uses the existing onboarding API result mapper and shared error contracts.

Mapped response behavior:
- `ValidationFailed` → 400
- `NotFound` → 404
- `PermissionDenied` → 403
- `TfsUnavailable` → 503
- `Conflict` → 409
- `DependencyViolation` → 409

The status service itself is read-only and does not introduce any write path.

## 7. Test Results

Validation executed:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~OnboardingStatusServiceTests|FullyQualifiedName~OnboardingStatusControllerTests|FullyQualifiedName~OnboardingLookupControllerTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingLookupServiceTests|FullyQualifiedName~OnboardingLiveLookupClientTests"`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build`

Targeted onboarding test coverage added:
- not configured outcome
- partially configured outcome
- complete outcome
- blocked domain configuration outcome
- degradation from invalid enabled source
- controller success envelope
- controller error-code mapping

Results:
- solution build: passed
- targeted onboarding status/onboarding tests: passed
- full unit suite: 5 pre-existing unrelated failures remain

Pre-existing unrelated full-suite failures observed:
- `CacheBackedGeneratedClientMigrationAuditTests`
- `DocumentationVerificationBatch6Tests`
- `TestCategoryEnforcementTests` (3 failures)

## 8. Governance Compliance

Verified:
- no UI files changed
- no write model or CRUD endpoints added
- no migration execution added
- no onboarding status derived from client or wizard state
- no legacy onboarding code modified
- status logic reads persisted Slice 1 entities only
- observability hooks were added for Slice 3 status computation
- new public shared contracts were added to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`

Changed files:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Onboarding/StatusDtos.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingStatusService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Onboarding/OnboardingStatusHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/OnboardingStatusController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingObservability.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/Onboarding/OnboardingStatusServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Controllers/OnboardingStatusControllerTests.cs`
