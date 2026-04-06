# Slice 4 — Migration Infrastructure

## 1. Scope Confirmation

Slice 4 scope was implemented as migration infrastructure only.

Included:
- migration ledger entities/schema
- migration issue tracking entities/schema
- migration run/unit lifecycle model
- migration job framework and orchestration interfaces
- dry-run/reporting hooks
- service registration required for migration infrastructure

Explicitly excluded:
- no actual legacy-to-new data movement
- no migration execution logic
- no onboarding CRUD endpoints
- no UI
- no cutover logic
- no status/completion changes

Implemented infrastructure entities:
- `MigrationRun`
- `MigrationUnit`
- `MigrationIssue`

Implemented framework services/interfaces:
- `IOnboardingMigrationLedgerService`
- `OnboardingMigrationLedgerService`
- `IOnboardingMigrationJobHandler`
- `OnboardingMigrationJobHandler`

## 2. Infrastructure Domain Model

### Added schema

New tables:
- `OnboardingMigrationRuns`
- `OnboardingMigrationUnits`
- `OnboardingMigrationIssues`

New EF configuration:
- `MigrationRunConfiguration`
- `MigrationUnitConfiguration`
- `MigrationIssueConfiguration`

New persisted enums:
- `OnboardingMigrationExecutionMode`
- `OnboardingMigrationRunStatus`
- `OnboardingMigrationUnitStatus`
- `OnboardingMigrationIssueSeverity`

### Migration run model

Persisted fields:
- unique run identity: `RunIdentifier`
- migration version: `MigrationVersion`
- environment/ring: `EnvironmentRing`
- trigger type: `TriggerType`
- execution mode: `ExecutionMode`
- source fingerprint: `SourceFingerprint`
- started/finished timestamps: `StartedAtUtc`, `FinishedAtUtc`
- overall status: `Status`
- summary counts:
  - `TotalUnitCount`
  - `SucceededUnitCount`
  - `FailedUnitCount`
  - `SkippedUnitCount`
  - `ProcessedEntityCount`
  - `SucceededEntityCount`
  - `FailedEntityCount`
  - `SkippedEntityCount`
  - `IssueCount`
  - `BlockingIssueCount`

### Migration unit model

Persisted fields:
- unique unit identity: `UnitIdentifier`
- parent migration run: `MigrationRunId`
- unit type/name: `UnitType`, `UnitName`
- execution order: `ExecutionOrder`
- started/finished timestamps: `StartedAtUtc`, `FinishedAtUtc`
- status: `Status`
- processed/succeeded/failed/skipped counts:
  - `ProcessedEntityCount`
  - `SucceededEntityCount`
  - `FailedEntityCount`
  - `SkippedEntityCount`

### Migration issue model

Persisted fields:
- unique issue identity: `IssueIdentifier`
- parent migration run: `MigrationRunId`
- optional parent unit: `MigrationUnitId`
- issue type/category: `IssueType`, `IssueCategory`
- severity: `Severity`
- source legacy reference: `SourceLegacyReference`
- target entity type: `TargetEntityType`
- target external identity if known: `TargetExternalIdentity`
- sanitized message/details: `SanitizedMessage`, `SanitizedDetails`
- blocking flag: `IsBlocking`
- created timestamp: `CreatedAtUtc`

### Relationships and persistence rules

- `MigrationUnit -> MigrationRun` required FK
- `MigrationIssue -> MigrationRun` required FK
- `MigrationIssue -> MigrationUnit` optional FK
- explicit EF configuration with `DeleteBehavior.Restrict`
- unique indices on run/unit/issue identifiers
- unique `(MigrationRunId, ExecutionOrder)` index for units

### Sample record structures

`MigrationRun`
- `RunIdentifier`: GUID
- `MigrationVersion`: `2026-04-06-slice-4`
- `EnvironmentRing`: `dev`
- `TriggerType`: `Manual`
- `ExecutionMode`: `DryRun`
- `Status`: `PartiallySucceeded`

`MigrationUnit`
- `UnitIdentifier`: GUID
- `MigrationRunId`: parent run PK
- `UnitType`: `ProjectSource`
- `UnitName`: `projects`
- `ExecutionOrder`: `2`
- `Status`: `Failed`

`MigrationIssue`
- `IssueIdentifier`: GUID
- `MigrationRunId`: parent run PK
- `MigrationUnitId`: optional parent unit PK
- `IssueType`: `MissingIdentity`
- `IssueCategory`: `Resolution`
- `Severity`: `Blocking`
- `SourceLegacyReference`: `legacy:project:7`
- `TargetEntityType`: `ProjectSource`
- `TargetExternalIdentity`: null
- `IsBlocking`: `true`

## 3. Status Model

Persisted run statuses:
- `NotStarted`
- `Running`
- `Succeeded`
- `Failed`
- `PartiallySucceeded`
- `Cancelled`

Persisted unit statuses:
- `Pending`
- `Running`
- `Succeeded`
- `Failed`
- `Skipped`

Persisted issue severities:
- `Blocking`
- `Warning`
- `Info`

Aggregation rules implemented:
- all succeeded or succeeded/skipped units -> run `Succeeded`
- all failed units -> run `Failed`
- mixed failed plus succeeded/skipped units -> run `PartiallySucceeded`
- cancelled run remains `Cancelled`
- finalization rejects pending/running units unless the run is already cancelled

## 4. Framework Services

Framework-only capabilities implemented:
- create migration run
- create ordered migration units
- start a unit
- complete a unit
- fail a unit
- skip a unit
- record migration issues
- cancel a run
- finalize a run from unit outcomes
- generate persisted run summaries

Dry-run orchestration:
- `OnboardingMigrationJobHandler.RunDryRunAsync(...)`
- creates run/unit/issue ledger records only
- uses supplied dry-run unit plans and outcomes
- never maps or upserts onboarding business entities

No legacy-to-new execution was implemented.

## 5. Dry-Run and Reporting Hooks

Dry-run support added through:
- persisted `ExecutionMode`
- persisted unit result counts
- persisted issue recording
- persisted run summary generation

Dry-run behavior validated:
- creates run/unit/issue records
- finalizes the run from recorded unit outcomes
- does not write `TfsConnection`, `ProjectSource`, `TeamSource`, `PipelineSource`, `ProductRoot`, or `ProductSourceBinding`

## 6. Observability

Added logs:
- migration run started
- migration unit started
- migration unit completed
- migration unit failed
- migration issue recorded
- migration run finalized

Added metrics:
- migration run count by status
- migration unit count by status/type
- migration issue count by severity/category
- migration run duration
- migration unit duration

Observability implementation:
- `OnboardingObservability`

## 7. Test Results

Build:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- passed

Targeted tests:
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingMigrationInfrastructurePersistenceTests|FullyQualifiedName~OnboardingMigrationLedgerServiceTests|FullyQualifiedName~OnboardingPersistenceFoundationTests|FullyQualifiedName~PersistenceRelationshipContractTests|FullyQualifiedName~OnboardingStatusServiceTests"`
- passed

Migration/schema validation:
- `dotnet ef database update AddOnboardingMigrationInfrastructure --project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --startup-project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --configuration Release --connection "Data Source=/tmp/onboarding-slice4-migration.db" --no-build`
- `dotnet ef database update AddOnboardingPersistenceFoundation --project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --startup-project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --configuration Release --connection "Data Source=/tmp/onboarding-slice4-migration.db" --no-build`
- upgrade and rollback passed

Covered behaviors:
- create migration run
- create ordered units
- mark unit running/succeeded/failed/skipped
- record blocking and non-blocking issues
- finalize run status from unit outcomes
- dry-run creates run/unit/issue records
- dry-run does not mutate onboarding business entities
- required fields enforced
- foreign keys enforced
- run/unit/issue relationships valid
- cancelled run remains cancelled

## 8. Governance Compliance

Slice 4 code paths touched:
- `PoTool.Api/Persistence/**`
- `PoTool.Api/Migrations/**`
- `PoTool.Api/Services/**`
- `PoTool.Api/Handlers/**`
- `PoTool.Tests.Unit/**` for slice-4 verification

No forbidden Slice 4 implementation paths were changed:
- no `PoTool.Client/**`
- no onboarding controllers/endpoints
- no UI flows
- no cutover/routing files

No leakage confirmed:
- no migration execution of legacy-to-new mapping exists yet
- no onboarding domain entities are written by this slice
- no CRUD endpoints added
- no UI changes
- no cutover/routing changes
- no legacy onboarding behavior modified

Validation summary:
- no dual-write path introduced
- migration infrastructure observability added
- no migration trigger exposed to UI or normal user flows
