# Onboarding Domain & API Alignment

Authoritative inputs:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-redesign.md`

This document defines the backend/domain model and API contract required to implement the authoritative onboarding redesign. It does not change the redesign.

## 1. Domain Model

Backend onboarding state is persisted configuration state only. No wizard state, step state, or current-session state may exist in the backend contract.

### 1.1 Shared persisted value objects

#### ValidationState

Persisted on every onboarding entity that depends on live TFS validation.

Required fields:
- `status`: `Unknown | Valid | Invalid | Unavailable | PermissionDenied | CapabilityDenied`
- `checkedAtUtc`
- `validatedFrom`: `Live | SnapshotOnly`
- `errorCode`
- `errorMessageSanitized`
- `warningCodes`

Optional fields:
- `permissionScopeSummary`
- `capabilitySummary`
- `notFoundExternalId`

Rules:
- `ValidationState` is persisted after every successful revalidation.
- Write-time hard validation uses live TFS and does not rely on stale `ValidationState`.
- `SnapshotOnly` is never sufficient for create/update authorization.

#### SnapshotMetadata

Persisted with every external entity snapshot.

Required fields:
- `confirmedAtUtc`
- `lastSeenAtUtc`
- `isCurrent`

Optional fields:
- `renameDetected`
- `staleReason`

### 1.2 TfsConnection

- **Name**: `TfsConnection`
- **Identity (primary key)**: singleton key `connection`
- **External identity**: none
- **Required fields**
  - `organizationUrl`
  - `authenticationMode`
  - `timeoutSeconds`
  - `apiVersion`
  - `availabilityValidationState`
  - `permissionValidationState`
  - `capabilityValidationState`
  - `lastSuccessfulValidationAtUtc`
  - `lastAttemptedValidationAtUtc`
- **Optional fields**
  - `validationFailureReason`
  - `lastVerifiedCapabilitiesSummary`
- **Relationships**
  - parent of all onboarding source entities
  - delete blocked while any onboarding source/domain entity exists

### 1.3 ProjectSource

- **Name**: `ProjectSource`
- **Identity (primary key)**: `projectExternalId`
- **External identity**: TFS project ID
- **Required fields**
  - `projectExternalId`
  - `enabled`
  - `snapshot`
  - `validationState`
- **Optional fields**
  - none
- **Relationships**
  - belongs to `TfsConnection`
  - parent of `TeamSource`
  - parent of `PipelineSource`
  - referenced by `ProductSourceBinding`

#### ProjectSnapshot

Required fields:
- `projectExternalId`
- `name`
- `description`
- `metadata`

### 1.4 TeamSource

- **Name**: `TeamSource`
- **Identity (primary key)**: `teamExternalId`
- **External identity**: TFS team ID
- **Required fields**
  - `teamExternalId`
  - `projectExternalId`
  - `enabled`
  - `snapshot`
  - `validationState`
- **Optional fields**
  - none
- **Relationships**
  - child of `ProjectSource`
  - referenced by `ProductSourceBinding`

#### TeamSnapshot

Required fields:
- `teamExternalId`
- `projectExternalId`
- `name`
- `defaultAreaPath`
- `description`
- `metadata`

### 1.5 PipelineSource

- **Name**: `PipelineSource`
- **Identity (primary key)**: `pipelineExternalId`
- **External identity**: TFS pipeline definition ID
- **Required fields**
  - `pipelineExternalId`
  - `projectExternalId`
  - `enabled`
  - `snapshot`
  - `validationState`
- **Optional fields**
  - `repositoryExternalId`
- **Relationships**
  - child of `ProjectSource`
  - referenced by `ProductSourceBinding`

#### PipelineSnapshot

Required fields:
- `pipelineExternalId`
- `projectExternalId`
- `name`
- `folder`
- `yamlPath`
- `repositoryExternalId`
- `repositoryName`
- `metadata`

### 1.6 ProductRoot

- **Name**: `ProductRoot`
- **Identity (primary key)**: `workItemExternalId`
- **External identity**: TFS work item ID
- **Required fields**
  - `workItemExternalId`
  - `enabled`
  - `snapshot`
  - `validationState`
- **Optional fields**
  - none
- **Relationships**
  - belongs to `TfsConnection`
  - parent of `ProductSourceBinding`

#### ProductRootSnapshot

Required fields:
- `workItemExternalId`
- `title`
- `workItemType`
- `state`
- `projectExternalId`
- `areaPath`
- `metadata`

### 1.7 ProductSourceBinding

- **Name**: `ProductSourceBinding`
- **Identity (primary key)**: composite
  - `workItemExternalId`
  - `sourceType`
  - `sourceExternalId`
- **External identity**: same as primary key
- **Required fields**
  - `workItemExternalId`
  - `sourceType`: `Project | Team | Pipeline`
  - `sourceExternalId`
  - `enabled`
  - `validationState`
- **Optional fields**
  - `projectExternalId` for scope enforcement and query efficiency
- **Relationships**
  - many bindings per `ProductRoot`
  - references exactly one source entity by `sourceType`
  - team binding requires matching project scope
  - pipeline binding requires matching project scope

Binding snapshot storage:
- none as an authoritative independent snapshot
- binding display is composed from linked `ProductRoot` and source snapshots

### 1.8 Validation state storage rules

1. `TfsConnection`, `ProjectSource`, `TeamSource`, `PipelineSource`, `ProductRoot`, and `ProductSourceBinding` persist validation state.
2. Validation state is part of authoritative persisted configuration.
3. Derived completion uses persisted validation state plus dependency rules.
4. Sanitized errors only; no secrets, tokens, or raw credentials.

### 1.9 Snapshot storage rules

1. Snapshots are persisted per external entity after live confirmation.
2. A snapshot update on rename replaces the existing snapshot for the same external identity.
3. Snapshots are never treated as draft input.
4. Snapshots remain readable when TFS is unavailable.

## 2. API Contracts

All endpoints return structured contracts and structured failures. Raw booleans are insufficient for onboarding actions.

### Shared response/error contract

#### Success envelope

Required fields:
- `data`
- `timestampUtc`

#### Error envelope

Required fields:
- `code`
- `message`
- `details`
- `retryable`

Allowed error codes:
- `ValidationFailed`
- `NotFound`
- `PermissionDenied`
- `TfsUnavailable`
- `Conflict`
- `DependencyViolation`

Status mapping:
- `400` malformed request
- `403` permission denied
- `404` live entity not found or persisted entity not found
- `409` duplicate identity or dependency rule violation
- `503` TFS unavailable

### 2.1 Connection

#### PUT `/api/onboarding/connection`

Purpose:
- create or update the singleton connection with full live validation

Input:
- `organizationUrl`
- `authenticationMode`
- `timeoutSeconds`
- `apiVersion`

Output:
- `TfsConnectionDto`
  - `organizationUrl`
  - `authenticationMode`
  - `timeoutSeconds`
  - `apiVersion`
  - `availabilityValidationState`
  - `permissionValidationState`
  - `capabilityValidationState`
  - `lastSuccessfulValidationAtUtc`
  - `lastAttemptedValidationAtUtc`

Error cases:
- `400 ValidationFailed` invalid URL or missing required field
- `403 PermissionDenied`
- `503 TfsUnavailable`
- `409 Conflict` if an incompatible destructive update is attempted while dependent entities exist

Rule:
- write occurs only if full required validation succeeds

#### POST `/api/onboarding/connection/revalidate`

Purpose:
- revalidate the existing persisted connection without changing identity

Input:
- none

Output:
- `TfsConnectionDto`

Error cases:
- `404 NotFound` no connection configured
- `403 PermissionDenied`
- `503 TfsUnavailable`

#### GET `/api/onboarding/connection`

Purpose:
- get connection plus persisted validation state

Output:
- `TfsConnectionDto`

Error cases:
- `404 NotFound`

#### DELETE `/api/onboarding/connection`

Purpose:
- delete connection only when no dependent onboarding entities exist

Input:
- none

Output:
- `204 NoContent`

Error cases:
- `404 NotFound`
- `409 DependencyViolation`

### 2.2 Lookup

Lookup endpoints are live-backed and return confirmation-ready snapshots. They do not persist onboarding state.

#### GET `/api/onboarding/lookups/work-items`

Purpose:
- search work items for onboarding selection

Input parameters:
- `query`
- `projectExternalId` optional
- `workItemTypes[]`
- `top`
- `skip`

Output:
- `WorkItemLookupResultDto[]`
  - `workItemExternalId`
  - `title`
  - `workItemType`
  - `state`
  - `projectExternalId`
  - `areaPath`

Error cases:
- `403 PermissionDenied`
- `503 TfsUnavailable`

#### GET `/api/onboarding/lookups/work-items/{workItemExternalId}`

Purpose:
- retrieve a single work item by external ID with full confirmation detail

Output:
- `WorkItemLookupResultDto`

Error cases:
- `404 NotFound`
- `403 PermissionDenied`
- `503 TfsUnavailable`

#### GET `/api/onboarding/lookups/projects`

Purpose:
- list/search projects

Input parameters:
- `query`
- `top`
- `skip`

Output:
- `ProjectLookupResultDto[]`
  - `projectExternalId`
  - `name`
  - `description`

Error cases:
- `403 PermissionDenied`
- `503 TfsUnavailable`

#### GET `/api/onboarding/lookups/projects/{projectExternalId}/teams`

Purpose:
- list/search teams within one project

Input parameters:
- `query`
- `top`
- `skip`

Output:
- `TeamLookupResultDto[]`
  - `teamExternalId`
  - `projectExternalId`
  - `name`
  - `description`
  - `defaultAreaPath`

Error cases:
- `404 NotFound` project not found
- `403 PermissionDenied`
- `503 TfsUnavailable`

#### GET `/api/onboarding/lookups/projects/{projectExternalId}/pipelines`

Purpose:
- list/search pipelines within one project

Input parameters:
- `query`
- `top`
- `skip`

Output:
- `PipelineLookupResultDto[]`
  - `pipelineExternalId`
  - `projectExternalId`
  - `name`
  - `folder`
  - `yamlPath`
  - `repositoryExternalId`
  - `repositoryName`

Error cases:
- `404 NotFound` project not found
- `403 PermissionDenied`
- `503 TfsUnavailable`

### 2.3 CRUD

All persisted onboarding CRUD endpoints must return both entity data and persisted validation state.

#### A. ProjectSource

- `PUT /api/onboarding/project-sources/{projectExternalId}`
  - upsert by external identity
  - body may include `enabled`
- `GET /api/onboarding/project-sources`
- `GET /api/onboarding/project-sources/{projectExternalId}`
- `PATCH /api/onboarding/project-sources/{projectExternalId}/enabled`
  - body: `enabled`
- `DELETE /api/onboarding/project-sources/{projectExternalId}`

`ProjectSourceDto`:
- `projectExternalId`
- `enabled`
- `snapshot`
- `validationState`

Enforced rules:
- idempotent by `projectExternalId`
- no duplicates
- delete blocked if dependent team sources, pipeline sources, or bindings exist

#### B. TeamSource

- `PUT /api/onboarding/team-sources/{teamExternalId}`
  - body includes `projectExternalId`, `enabled`
- `GET /api/onboarding/team-sources`
- `GET /api/onboarding/team-sources/{teamExternalId}`
- `PATCH /api/onboarding/team-sources/{teamExternalId}/enabled`
- `DELETE /api/onboarding/team-sources/{teamExternalId}`

`TeamSourceDto`:
- `teamExternalId`
- `projectExternalId`
- `enabled`
- `snapshot`
- `validationState`

Enforced rules:
- parent project must exist
- idempotent by `teamExternalId`
- delete blocked if dependent bindings exist

#### C. PipelineSource

- `PUT /api/onboarding/pipeline-sources/{pipelineExternalId}`
  - body includes `projectExternalId`, `enabled`
- `GET /api/onboarding/pipeline-sources`
- `GET /api/onboarding/pipeline-sources/{pipelineExternalId}`
- `PATCH /api/onboarding/pipeline-sources/{pipelineExternalId}/enabled`
- `DELETE /api/onboarding/pipeline-sources/{pipelineExternalId}`

`PipelineSourceDto`:
- `pipelineExternalId`
- `projectExternalId`
- `enabled`
- `snapshot`
- `validationState`

Enforced rules:
- parent project must exist
- idempotent by `pipelineExternalId`
- delete blocked if dependent bindings exist

#### D. ProductRoot

- `PUT /api/onboarding/product-roots/{workItemExternalId}`
  - body includes `enabled`
- `GET /api/onboarding/product-roots`
- `GET /api/onboarding/product-roots/{workItemExternalId}`
- `PATCH /api/onboarding/product-roots/{workItemExternalId}/enabled`
- `DELETE /api/onboarding/product-roots/{workItemExternalId}`

`ProductRootDto`:
- `workItemExternalId`
- `enabled`
- `snapshot`
- `validationState`

Enforced rules:
- upsert requires live lookup confirmation
- idempotent by `workItemExternalId`
- delete blocked if bindings exist

#### E. ProductSourceBinding

- `PUT /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}`
  - body may include `enabled`
- `GET /api/onboarding/product-source-bindings`
- `GET /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}`
- `PATCH /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}/enabled`
- `DELETE /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}`

`ProductSourceBindingDto`:
- `workItemExternalId`
- `sourceType`
- `sourceExternalId`
- `projectExternalId`
- `enabled`
- `validationState`

Enforced rules:
- idempotent by composite identity
- duplicate composite identity rejected
- referenced root and source must already exist and be valid
- team/pipeline bindings require matching project scope

### 2.4 Validation

Separate validation endpoints are allowed because revalidation is an explicit persisted action.

#### POST `/api/onboarding/project-sources/{projectExternalId}/revalidate`
#### POST `/api/onboarding/team-sources/{teamExternalId}/revalidate`
#### POST `/api/onboarding/pipeline-sources/{pipelineExternalId}/revalidate`
#### POST `/api/onboarding/product-roots/{workItemExternalId}/revalidate`
#### POST `/api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}/revalidate`

Output:
- updated entity DTO with refreshed `validationState` and updated snapshot if changed

Error cases:
- `404 NotFound`
- `403 PermissionDenied`
- `503 TfsUnavailable`

#### POST `/api/onboarding/validation/revalidate-all`

Purpose:
- bulk revalidate all persisted onboarding entities

Output:
- `BulkRevalidationResultDto`
  - entity counts
  - changed entities
  - warnings
  - blocking reasons

### 2.5 Completion Status

#### GET `/api/onboarding/status`

Output:
- `OnboardingStatusDto`
  - `overallStatus`: `NotConfigured | PartiallyConfigured | Complete`
  - `connectionStatus`
  - `dataSourceSetupStatus`
  - `domainConfigurationStatus`
  - `blockingReasons[]`
  - `warnings[]`
  - `counts`
    - `projectSourcesTotal`
    - `projectSourcesValid`
    - `teamSourcesTotal`
    - `teamSourcesValid`
    - `pipelineSourcesTotal`
    - `pipelineSourcesValid`
    - `productRootsTotal`
    - `productRootsValid`
    - `bindingsTotal`
    - `bindingsValid`

Rule:
- status is computed server-side from persisted state only

### 2.6 Import

#### POST `/api/onboarding/import/validate`

Purpose:
- validate import payload without writing

Input:
- `OnboardingImportRequestDto`
  - connection
  - projectSources
  - teamSources
  - pipelineSources
  - productRoots
  - bindings

Output:
- `OnboardingImportValidationResultDto`
  - `canImport`
  - `errors[]`
  - `warnings[]`
  - `entityResults[]`

#### POST `/api/onboarding/import`

Purpose:
- execute merge/upsert import

Input:
- `OnboardingImportRequestDto`

Output:
- `OnboardingImportExecutionResultDto`
  - `createdCount`
  - `updatedCount`
  - `skippedCount`
  - `errorCount`
  - `entityResults[]`
  - `postImportStatus`

`entityResults[]` fields:
- `entityType`
- `identity`
- `result`: `Created | Updated | Skipped | Error`
- `message`

Import error cases:
- `400 ValidationFailed`
- `403 PermissionDenied`
- `503 TfsUnavailable`

## 3. Write Semantics

### 3.1 Atomicity

1. Every write endpoint is atomic for exactly one logical action.
2. Connection upsert is one logical action.
3. Source upsert is one logical action per source entity.
4. Binding upsert is one logical action per binding.
5. Import validate performs no writes.
6. Import execute is atomic for the entire import payload; no partial import success is allowed.

### 3.2 Validation-before-write

1. Live validation is required before every create/update for all externally identified entities.
2. Parent existence and dependency checks run before persistence.
3. Duplicate identity checks run before persistence.
4. Team/pipeline scope rules run before binding persistence.

### 3.3 Idempotency

1. Same identity + same effective input returns the same persisted state.
2. Repeating an upsert must update the existing record instead of creating another.
3. Repeating an enable/disable request with the same target state is a no-op success.
4. Repeating a delete for a missing entity returns `404`, not a recreated resource.

### 3.4 Error handling

1. No write endpoint may return success if any required validation or dependency rule failed.
2. No write endpoint may partially persist subordinate data and then return failure.
3. All failures must be structured and classifiable as validation, dependency, permission, availability, or not found.

## 4. Validation Model

### 4.1 Synchronous write-time validation

Validated synchronously during writes:
- connection URL validity
- live TFS availability
- permission presence
- capability presence
- external entity existence
- allowed work item type
- parent-child integrity
- duplicate prevention
- binding scope compatibility

### 4.2 Explicit revalidation

Revalidated asynchronously only when explicitly invoked by:
- entity revalidate endpoint
- bulk revalidate endpoint

Explicit revalidation updates:
- `validationState`
- persisted snapshot if live data changed
- derived warnings/blocking reasons

### 4.3 Stored and exposed validation

1. Persisted entities store latest validation state.
2. API read contracts always expose validation state.
3. Status computation uses persisted validation state plus dependency rules.
4. Validation errors must be sanitized and deterministic.

## 5. Snapshot Model

### 5.1 Stored fields per entity

- **ProjectSource**: `projectExternalId`, `name`, `description`, metadata
- **TeamSource**: `teamExternalId`, `projectExternalId`, `name`, `description`, `defaultAreaPath`, metadata
- **PipelineSource**: `pipelineExternalId`, `projectExternalId`, `name`, `folder`, `yamlPath`, `repositoryExternalId`, `repositoryName`, metadata
- **ProductRoot**: `workItemExternalId`, `title`, `workItemType`, `state`, `projectExternalId`, `areaPath`, metadata
- **ProductSourceBinding**: no standalone authoritative snapshot

### 5.2 Snapshot update rules

1. Snapshot updates occur after successful live lookup during upsert.
2. Snapshot updates occur after explicit revalidation if live data changed.
3. Snapshot update on rename must preserve identity.

### 5.3 Stale vs current

`isCurrent = true` only when:
- the latest live revalidation succeeded
- the current snapshot matches the latest live response for required fields

`isCurrent = false` when:
- live entity is unavailable
- permission denied prevents confirmation
- rename or field drift is detected but not yet refreshed

## 6. Dependency Rules

The backend must enforce all of the following:

1. Cannot create or update `ProjectSource`, `TeamSource`, `PipelineSource`, or `ProductRoot` without a valid persisted `TfsConnection`.
2. Cannot create `TeamSource` without an existing `ProjectSource`.
3. Cannot create `PipelineSource` without an existing `ProjectSource`.
4. Cannot create `ProductSourceBinding` without an existing valid `ProductRoot`.
5. Cannot create `ProductSourceBinding` without an existing valid referenced source.
6. Cannot create `Team` binding unless the referenced team belongs to the same project scope as an existing project binding for that root.
7. Cannot create `Pipeline` binding unless the referenced pipeline belongs to the same project scope as an existing project binding for that root.
8. Cannot delete `ProjectSource` if dependent `TeamSource`, `PipelineSource`, or bindings exist.
9. Cannot delete `TeamSource` if bindings exist.
10. Cannot delete `PipelineSource` if bindings exist.
11. Cannot delete `ProductRoot` if bindings exist.
12. Cannot delete `TfsConnection` while any onboarding entity exists.
13. Cannot persist duplicate external identities.
14. Cannot authorize create/update from snapshot-only data while TFS is unavailable.

## 7. Backend Gaps

Current backend gaps relative to the redesign:

1. **Connection write semantics are incorrect**
   - Current `/api/tfsconfig/save-and-verify` saves configuration before connection and capability validation complete.
   - This violates the redesign rule that invalid new values must not become authoritative persisted state.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:333-412`

2. **Current live lookup surface is incomplete**
   - Current backend exposes live project/team/repository discovery, but not onboarding-grade work item search by type, scoped team search, scoped pipeline lookup, or confirmation-ready unified lookup contracts.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/StartupController.cs:37-84`

3. **Current persisted model is product/team/repository oriented, not onboarding-source oriented**
   - Current entities center on `ProductEntity`, `TeamEntity`, `RepositoryEntity`, `PipelineDefinitionEntity`, and local generated `ProjectEntity`.
   - The redesign requires persisted `ProjectSource`, `TeamSource`, `PipelineSource`, `ProductRoot`, and `ProductSourceBinding` as the onboarding authority.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductEntity.cs:9-108`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/TeamEntity.cs:9-101`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProjectEntity.cs:9-36`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/RepositoryEntity.cs:9-41`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/PipelineDefinitionEntity.cs:9-99`

4. **Current identity model is not aligned**
   - `ProductEntity` uses local integer IDs plus generated local `ProjectEntity` GUIDs.
   - `TeamEntity` uses local integer IDs and optional TFS fields.
   - `RepositoryEntity` is keyed by local ID and repository name, not external repository ID.
   - The redesign requires idempotent external-ID identity.

5. **Current CRUD surface lacks idempotent upsert-by-external-identity**
   - Current product/team/repository endpoints are create/update by local ID or create-only patterns.
   - They do not provide source-centric upsert semantics.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs:75-127,254-284`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/TeamsController.cs:55-109`

6. **Current backend assumes product/team linking flows that belong to the old model**
   - Current `LinkTeamToProduct` and repository-under-product endpoints encode old onboarding structure instead of explicit domain bindings from product root to source entities.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs:163-205,254-284`

7. **Validation is too shallow for the new onboarding contract**
   - Current validators check field format, but do not enforce live external confirmation for product roots or source entities.
   - Example: backlog root IDs are only checked as positive integers, not as live resolvable work items.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Validators/CreateProductCommandValidator.cs:14-37`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Validators/CreateTeamCommandValidator.cs:14-57`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Validators/CreateRepositoryCommandValidator.cs:14-29`

8. **Current backend has no persisted onboarding status contract**
   - Startup readiness is not the redesign’s onboarding completion contract.
   - There is no authoritative status endpoint exposing overall status, per-flow status, blocking reasons, warnings, and counts.

9. **Current import contract is broader and differently shaped**
   - Current settings import/export covers settings, profiles, teams, and products, includes destructive wipe behavior, and does not match onboarding merge/upsert-by-identity semantics.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ConfigurationTransferDto.cs:5-62`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/SettingsController.cs:110-144`

10. **Current backend does not persist onboarding snapshots and validation state as first-class onboarding entities**
    - TFS config stores a few validation flags, but source/domain entities do not expose the required uniform snapshot + validation contract.
    - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TfsConfigEntity.cs:5-64`

## 8. Minimal API Surface

Minimal but sufficient endpoint set:

### 8.1 `OnboardingConnectionController`
- `PUT /api/onboarding/connection`
- `GET /api/onboarding/connection`
- `POST /api/onboarding/connection/revalidate`
- `DELETE /api/onboarding/connection`

### 8.2 `OnboardingLookupController`
- `GET /api/onboarding/lookups/projects`
- `GET /api/onboarding/lookups/projects/{projectExternalId}/teams`
- `GET /api/onboarding/lookups/projects/{projectExternalId}/pipelines`
- `GET /api/onboarding/lookups/work-items`
- `GET /api/onboarding/lookups/work-items/{workItemExternalId}`

### 8.3 `OnboardingSourcesController`
- `PUT /api/onboarding/project-sources/{projectExternalId}`
- `GET /api/onboarding/project-sources`
- `GET /api/onboarding/project-sources/{projectExternalId}`
- `PATCH /api/onboarding/project-sources/{projectExternalId}/enabled`
- `DELETE /api/onboarding/project-sources/{projectExternalId}`
- `POST /api/onboarding/project-sources/{projectExternalId}/revalidate`
- `PUT /api/onboarding/team-sources/{teamExternalId}`
- `GET /api/onboarding/team-sources`
- `GET /api/onboarding/team-sources/{teamExternalId}`
- `PATCH /api/onboarding/team-sources/{teamExternalId}/enabled`
- `DELETE /api/onboarding/team-sources/{teamExternalId}`
- `POST /api/onboarding/team-sources/{teamExternalId}/revalidate`
- `PUT /api/onboarding/pipeline-sources/{pipelineExternalId}`
- `GET /api/onboarding/pipeline-sources`
- `GET /api/onboarding/pipeline-sources/{pipelineExternalId}`
- `PATCH /api/onboarding/pipeline-sources/{pipelineExternalId}/enabled`
- `DELETE /api/onboarding/pipeline-sources/{pipelineExternalId}`
- `POST /api/onboarding/pipeline-sources/{pipelineExternalId}/revalidate`

### 8.4 `OnboardingDomainController`
- `PUT /api/onboarding/product-roots/{workItemExternalId}`
- `GET /api/onboarding/product-roots`
- `GET /api/onboarding/product-roots/{workItemExternalId}`
- `PATCH /api/onboarding/product-roots/{workItemExternalId}/enabled`
- `DELETE /api/onboarding/product-roots/{workItemExternalId}`
- `POST /api/onboarding/product-roots/{workItemExternalId}/revalidate`
- `PUT /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}`
- `GET /api/onboarding/product-source-bindings`
- `GET /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}`
- `PATCH /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}/enabled`
- `DELETE /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}`
- `POST /api/onboarding/product-source-bindings/{workItemExternalId}/{sourceType}/{sourceExternalId}/revalidate`

### 8.5 `OnboardingStatusController`
- `GET /api/onboarding/status`
- `POST /api/onboarding/validation/revalidate-all`

### 8.6 `OnboardingImportController`
- `POST /api/onboarding/import/validate`
- `POST /api/onboarding/import`

This surface is the minimum required to satisfy the redesign without reintroducing wizard behavior, implicit persistence, or local-session authority.
