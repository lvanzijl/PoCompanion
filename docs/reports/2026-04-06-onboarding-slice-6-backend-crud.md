# Slice 6 — Backend CRUD

## 1. Scope

Implemented backend CRUD coverage for TfsConnection, ProjectSource, TeamSource, PipelineSource, ProductRoot, and ProductSourceBinding.

Added:
- list and detail reads per entity
- create flows for manual onboarding additions
- restricted update flows for allowed mutable fields only
- soft delete with required deletion reason and audit retention

## 2. API Design

Added `OnboardingCrudController` under `/api/onboarding` with REST routes:
- `/api/onboarding/connections`
- `/api/onboarding/projects`
- `/api/onboarding/teams`
- `/api/onboarding/pipelines`
- `/api/onboarding/roots`
- `/api/onboarding/bindings`

Implemented per-entity:
- `GET` list
- `GET` detail by id
- `POST` create
- `PUT` update
- `DELETE` soft delete

List filters support the required scopes through query parameters:
- connection
- project
- product root
- status

Responses now return structured entity DTOs with:
- entity data
- validation data
- status summary
- audit data

## 3. Validation Enforcement

All writes flow through `IOnboardingValidationService` before persistence.

Create and update operations:
- resolve required dependencies first
- execute Slice 2 validation before `SaveChangesAsync`
- reject invalid requests without persisting
- store refreshed validation state on the entity

Controllers do not access `DbContext` directly.

## 4. Update Rules

Implemented guarded update contracts that allow only controlled fields.

Allowed updates:
- display metadata fields persisted in snapshots
- `Enabled`
- non-identity connection configuration (`AuthenticationMode`, `TimeoutSeconds`, `ApiVersion`)

Rejected updates:
- external ids
- foreign keys
- source type/source external id
- connection key / organization url

Forbidden mutation attempts return validation failures.

## 5. Delete Strategy

Implemented soft delete for the six onboarding graph entities through `OnboardingGraphEntityBase`.

Soft delete stores:
- `DeletedAtUtc`
- `DeletionReason`
- `IsDeleted`

Delete safety rules block removal when active dependencies still exist, including:
- projects under a connection
- teams/pipelines/roots/bindings under a project
- bindings referencing teams, pipelines, or roots

Soft-deleted onboarding graph entities are excluded from reads and onboarding status evaluation.

## 6. Status Integration

CRUD reads use Slice 3 status output via `IOnboardingStatusService`.

Per-entity responses include a status summary derived from Slice 3 blocking reasons and warnings instead of recomputing controller-side or duplicating status logic.

## 7. Consistency Guarantees

Implemented safeguards to prevent:
- orphan creation when required parents are missing
- mismatched binding scope across root/project/team/pipeline relationships
- delete operations that would break active references
- write paths that bypass validation

Soft delete scope was limited to onboarding graph entities only and intentionally excluded migration ledger entities.

## 8. Test Results

Validated with:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingCrudServiceTests|FullyQualifiedName~OnboardingMigrationExecutionServiceTests|FullyQualifiedName~OnboardingMigrationLedgerServiceTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingPersistenceFoundationTests|FullyQualifiedName~PersistenceRelationshipContractTests|FullyQualifiedName~OnboardingStatusServiceTests"`

Added targeted unit coverage for:
- create valid entity
- create invalid entity rejection
- allowed update
- forbidden update rejection
- dependency-blocked delete
- validation invocation on every write
- orphan prevention
- broken binding prevention
- soft delete audit persistence
- status ignoring soft-deleted onboarding entities

## 9. Governance Compliance

Confirmed:
- no UI changes
- no onboarding migration execution logic changes
- no direct `DbContext` usage in controllers
- no validation bypass on write paths
- no cross-slice controller-side status computation
- report written to canonical path
