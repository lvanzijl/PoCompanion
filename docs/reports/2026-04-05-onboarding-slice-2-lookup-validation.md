# Slice 2 — Lookup & Validation APIs

## 1. Scope Confirmation

Slice 2 was implemented as the live lookup and validation foundation only.

Included:
- live lookup APIs for projects, teams, pipelines, and work items
- live validation services for `TfsConnection`, `ProjectSource`, `TeamSource`, `PipelineSource`, `ProductRoot`, and `ProductSourceBinding`
- deterministic snapshot shaping from live TFS responses into authoritative contracts
- structured success and error contracts
- categorized failure handling for `NotFound`, `PermissionDenied`, `TfsUnavailable`, and `ValidationFailed`

Explicitly excluded:
- no onboarding write CRUD endpoints
- no UI changes or client runtime behavior changes
- no migration execution logic
- no onboarding status/completion computation
- no cutover or routing changes
- no legacy wizard/session-state work

## 2. Endpoints Implemented

Added lookup endpoints:
- `GET /api/onboarding/lookups/projects`
- `GET /api/onboarding/lookups/projects/{projectExternalId}/teams`
- `GET /api/onboarding/lookups/projects/{projectExternalId}/pipelines`
- `GET /api/onboarding/lookups/work-items`
- `GET /api/onboarding/lookups/work-items/{workItemExternalId}`

Lookup outputs:
- projects: `projectExternalId`, `name`, `description`
- teams: `teamExternalId`, `projectExternalId`, `name`, `description`, `defaultAreaPath`
- pipelines: `pipelineExternalId`, `projectExternalId`, `name`, `folder`, `yamlPath`, `repositoryExternalId`, `repositoryName`
- work items: `workItemExternalId`, `title`, `workItemType`, `state`, `projectExternalId`, `areaPath`

Response contract behavior:
- successful responses return `OnboardingSuccessEnvelope<T>` with `data` and `timestampUtc`
- failure responses return `OnboardingErrorDto` with categorized error metadata

## 3. Validation Services

Added validation services:
- `ValidateConnectionAsync`
- `ValidateProjectSourceAsync`
- `ValidateTeamSourceAsync`
- `ValidatePipelineSourceAsync`
- `ValidateProductRootAsync`
- `ValidateProductSourceBindingAsync`

Validation behavior implemented:
- confirms live TFS existence
- distinguishes permission failures from availability failures
- confirms team and pipeline project scope
- confirms product-root project scope
- confirms binding scope validity for project, team, and pipeline bindings
- confirms required snapshot fields needed for deterministic snapshot construction
- does not persist onboarding writes in Slice 2

Observed validation outcomes covered by tests:
- valid connection
- invalid connection
- permission denied
- entity not found
- team project mismatch
- pipeline project mismatch
- binding scope mismatch
- TFS unavailable

## 4. Snapshot and Result Contracts

Added shared lookup contracts:
- `ProjectLookupResultDto`
- `TeamLookupResultDto`
- `PipelineLookupResultDto`
- `WorkItemLookupResultDto`

Added shared snapshot contracts:
- `ProjectSnapshotDto`
- `TeamSnapshotDto`
- `PipelineSnapshotDto`
- `ProductRootSnapshotDto`
- `SnapshotMetadataDto`

Added shared validation/result contracts:
- `TfsConnectionValidationResultDto`
- `ProjectSourceValidationResultDto`
- `TeamSourceValidationResultDto`
- `PipelineSourceValidationResultDto`
- `ProductRootValidationResultDto`
- `ProductSourceBindingValidationResultDto`
- `OnboardingValidationStateDto`
- `OnboardingSuccessEnvelope<T>`
- `OnboardingErrorDto`
- `OnboardingOperationResult<T>`

Snapshot shaping rules implemented:
- project IDs come from live TFS project IDs
- team and pipeline snapshots inherit authoritative `projectExternalId`
- work-item snapshots resolve `projectExternalId` deterministically from live project catalog plus returned area path
- snapshot metadata is emitted consistently with current UTC confirmation timestamps and `isCurrent = true`

## 5. Error Contract and Failure Mapping

Implemented structured error model:
- `code`
- `message`
- `details`
- `retryable`

Allowed categories implemented:
- `ValidationFailed`
- `NotFound`
- `PermissionDenied`
- `TfsUnavailable`
- `Conflict`
- `DependencyViolation`

HTTP mapping verified:
- `ValidationFailed` → `400`
- `NotFound` → `404`
- `PermissionDenied` → `403`
- `TfsUnavailable` → `503`
- `Conflict` → `409`
- `DependencyViolation` → `409`

Failure mapping behavior:
- malformed lookup parameters and scope mismatches return `ValidationFailed`
- missing project, team, pipeline, or work item returns `NotFound`
- authorization and authentication failures map to `PermissionDenied`
- transport and timeout failures map to `TfsUnavailable`

Observability added for Slice 2:
- structured lookup start/completion logging
- structured revalidation completion logging
- lookup failure counters by operation and failure code
- validation failure counters by entity type and failure code

## 6. Test Results

Targeted Slice 2 tests:
- command: `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingLiveLookupClientTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingLookupServiceTests|FullyQualifiedName~OnboardingLookupControllerTests"`
- result: passed
- totals: 16 passed, 0 failed

Covered lookup scenarios:
- successful project search
- successful team search in project
- successful pipeline search in project
- successful work-item search with type filter
- successful work-item lookup by ID

Covered validation scenarios:
- valid connection
- invalid connection
- permission denied
- entity not found
- team and pipeline project mismatch
- binding scope mismatch
- TFS unavailable

Covered error mapping scenarios:
- all six structured error categories mapped to the expected HTTP status

Repository-wide validation:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release` succeeded
- full unit suite still reports the same 5 pre-existing unrelated failures observed at baseline:
  - `CacheBackedGeneratedClientMigrationAuditTests`
  - `DocumentationVerificationBatch6Tests`
  - `TestCategoryEnforcementTests`

## 7. Governance Compliance

Leakage checks:
- no onboarding write CRUD endpoints were added
- no migration execution logic was added
- no onboarding status engine was added
- no legacy onboarding service coupling was introduced
- no direct TFS access was added to any UI/runtime client code
- no dual-write path was introduced

Changed functional code stayed in Slice 2 backend/shared/test areas:
- `PoTool.Api/Controllers/**`
- `PoTool.Api/Handlers/**`
- `PoTool.Api/Services/**`
- `PoTool.Shared/**`
- `PoTool.Tests.Unit/**`

Additional governed support updates:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` was updated only for service registration and approved raw-client factory placement required by existing architecture tests
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json` was updated only to keep shared-contract ownership aligned with existing NSwag governance for the new shared onboarding contracts

No UI pages, client services, onboarding write APIs, migration runners, or completion-routing flows were implemented in this slice.
