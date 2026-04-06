# Slice 5 — Migration Execution

## 1. Scope Confirmation

Slice 5 was implemented as migration execution only.

Included:
- legacy configuration readers
- mapping from legacy model to new onboarding entities
- execution using the Slice 4 migration run/unit framework
- validation using Slice 2 services
- persistence using Slice 1 entities
- issue recording via Slice 4

Explicitly excluded:
- no UI
- no onboarding CRUD endpoints
- no cutover/routing changes
- no wizard/session state
- no modification of Slice 3 status logic

## 2. Legacy Input Model

Legacy sources used:
- `TfsConfigEntity`
- `TeamEntity`
- `PipelineDefinitionEntity`
- `ProductBacklogRootEntity`
- `ProductTeamLinkEntity`
- `RepositoryEntity` metadata attached to pipelines

Extraction implementation:
- `OnboardingLegacyMigrationReader`
- deterministic in-memory snapshot:
  - connection
  - project references
  - teams
  - pipelines
  - product roots
  - team bindings
  - pipeline bindings
- deterministic SHA-256 source fingerprint over ordered legacy records

Legacy entity handling:
- connection required for live migration execution
- missing `TfsTeamId` is blocking
- missing/unresolvable project dependency is blocking
- missing pipeline or work item external reference is blocking
- invalid legacy rows are not persisted; they become migration issues

## 3. Mapping Layer

Mapping implementation:
- `OnboardingMigrationMapper`

Pure mapping definitions:
- `TfsConfigEntity` → `TfsConnection`
- resolved project reference → `ProjectSource`
- `TeamEntity` → `TeamSource`
- `PipelineDefinitionEntity` → `PipelineSource`
- `ProductBacklogRootEntity` → `ProductRoot`
- legacy root/team/pipeline associations → `ProductSourceBinding`

Mapping rules:
- mapping is side-effect free
- mapping does not call TFS
- mapping does not call persistence
- mapping produces:
  - target entity candidate
  - mapping context for issue recording

## 4. Execution Flow

Execution implementation:
- `OnboardingMigrationExecutionService`

Ordered units:
1. connection
2. projects
3. teams
4. pipelines
5. roots
6. bindings

Per-unit flow:
- create run with Slice 4 ledger
- create ordered units
- mark unit running
- read/map legacy record
- validate via Slice 2
- valid in live mode: upsert via Slice 1 onboarding entities
- invalid: record blocking migration issue
- complete/fail unit with counts
- finalize run and return summary

Deterministic execution behaviors:
- no onboarding writes occur before validation
- invalid entities are not persisted
- dependency failures record `DependencyViolation`
- dry-run executes the same orchestration but does not write onboarding business entities

## 5. Validation Integration

Slice 2 services used:
- `IOnboardingValidationService`
- `IOnboardingLiveLookupClient`

Validation coverage:
- connection validation before connection write
- project validation before project write
- team validation before team write
- pipeline validation before pipeline write
- product root validation before root write
- binding validation before binding write

Failure categories recorded:
- `NotFound`
- `PermissionDenied`
- `TfsUnavailable`
- `ValidationFailed`

Persisted validation state:
- connection availability/permission/capability validation state
- entity validation state on `ProjectSource`, `TeamSource`, `PipelineSource`, `ProductRoot`, `ProductSourceBinding`

## 6. Idempotency Strategy

Fingerprint strategy:
- the reader computes a run-level source fingerprint from ordered legacy source records
- the execution service compares the fingerprint with prior migration runs
- the fingerprint is stored on every `MigrationRun`

Upsert rules:
- `TfsConnection` upserts by `ConnectionKey`
- `ProjectSource` upserts by `ProjectExternalId`
- `TeamSource` upserts by `TeamExternalId`
- `PipelineSource` upserts by `PipelineExternalId`
- `ProductRoot` upserts by `WorkItemExternalId`
- `ProductSourceBinding` upserts by `(ProductRootId, SourceType, SourceExternalId)`

Verified behaviors:
- rerun with same fingerprint creates a new run ledger record but no duplicate onboarding entities
- rerun with changed legacy data updates existing onboarding entities instead of duplicating them

## 7. Issue Recording

Issue recording uses Slice 4 migration issues.

Recorded issue types:
- `MissingRequiredLegacyField`
- `ValidationFailure`
- `DependencyViolation`

Each recorded issue includes:
- source legacy reference
- target entity type
- target external identity when known
- blocking severity
- sanitized message/details

Sample output structures:

`MigrationRun`
- `MigrationVersion`: `2026-04-06-slice-5`
- `ExecutionMode`: `Live`
- `SourceFingerprint`: SHA-256 hex string
- `Status`: `Succeeded` or `PartiallySucceeded`

`MigrationUnit`
- `UnitType`: `PipelineSource`
- `ExecutionOrder`: `4`
- `Status`: `Succeeded` or `Failed`
- `ProcessedEntityCount`: integer

`MigrationIssue`
- `IssueType`: `DependencyViolation`
- `IssueCategory`: `DependencyViolation`
- `SourceLegacyReference`: `TeamEntity:2`
- `TargetEntityType`: `TeamSource`
- `Severity`: `Blocking`

## 8. Test Results

Build:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- passed

Targeted tests:
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingMigrationExecutionServiceTests|FullyQualifiedName~OnboardingMigrationLedgerServiceTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingPersistenceFoundationTests|FullyQualifiedName~PersistenceRelationshipContractTests|FullyQualifiedName~OnboardingStatusServiceTests"`
- passed

Covered execution scenarios:
- full successful migration
- partial migration with failures
- dependency failure with missing project
- validation failure prevents root write
- rerun with same fingerprint does not duplicate entities
- rerun with changed legacy data updates existing records
- dry-run records ledger data without onboarding writes

## 9. Governance Compliance

Changed paths:
- `PoTool.Api/Services/Onboarding/**`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Tests.Unit/Services/Onboarding/**`
- `docs/reports/2026-04-06-onboarding-slice-5-migration-execution.md`

Governance confirmations:
- no UI files changed
- no onboarding CRUD endpoints added
- no cutover/routing changes
- no wizard/session state introduced
- no direct writes outside Slice 1 onboarding entities
- no validation bypass on onboarding writes
- no dual-write paths introduced
- migration execution is not exposed to normal user flows
- observability remains provided through Slice 4 run/unit/issue lifecycle logging and metrics
