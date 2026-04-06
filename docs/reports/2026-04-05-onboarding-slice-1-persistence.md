# Slice 1 — Persistence Foundation

## 1. Scope Confirmation

Slice 1 scope implemented exactly as the persistence foundation slice.

Included:
- new persisted onboarding entities: `TfsConnection`, `ProjectSource`, `TeamSource`, `PipelineSource`, `ProductRoot`, `ProductSourceBinding`
- database schema for the onboarding persistence foundation
- explicit EF Core fluent configuration for tables, keys, foreign keys, indexes, and required columns
- generated EF Core migration and updated model snapshot
- SQLite-backed persistence tests for valid inserts, duplicate rejection, required-field enforcement, and parent-reference enforcement

Explicitly excluded:
- no API endpoints or controller changes
- no UI changes
- no migration execution logic beyond generated EF migration files and validation commands
- no onboarding service workflow logic
- no validation behavior beyond schema-level persistence constraints and existing central required-relationship enforcement

## 2. Entity Definitions

Implemented entities:

1. `TfsConnection`
   - internal primary key: `Id`
   - singleton logical key: `ConnectionKey`
   - required fields: `OrganizationUrl`, `AuthenticationMode`, `TimeoutSeconds`, `ApiVersion`
   - persisted validation state fields for availability, permission, and capability
   - audit fields: `CreatedAtUtc`, `UpdatedAtUtc`

2. `ProjectSource`
   - internal primary key: `Id`
   - stable external ID: `ProjectExternalId`
   - required fields: `Enabled`, persisted snapshot fields, persisted validation state fields
   - audit fields: `CreatedAtUtc`, `UpdatedAtUtc`

3. `TeamSource`
   - internal primary key: `Id`
   - stable external ID: `TeamExternalId`
   - required fields: `Enabled`, persisted snapshot fields, persisted validation state fields
   - audit fields: `CreatedAtUtc`, `UpdatedAtUtc`

4. `PipelineSource`
   - internal primary key: `Id`
   - stable external ID: `PipelineExternalId`
   - required fields: `Enabled`, persisted snapshot fields, persisted validation state fields
   - audit fields: `CreatedAtUtc`, `UpdatedAtUtc`

5. `ProductRoot`
   - internal primary key: `Id`
   - stable external ID: `WorkItemExternalId`
   - required fields: `Enabled`, persisted snapshot fields, persisted validation state fields
   - audit fields: `CreatedAtUtc`, `UpdatedAtUtc`

6. `ProductSourceBinding`
   - internal primary key: `Id`
   - logical identity fields: `ProductRootId`, `SourceType`, `SourceExternalId`
   - required fields: `ProjectSourceId`, `SourceType`, `SourceExternalId`, `Enabled`, persisted validation state fields
   - optional source FK fields: `TeamSourceId`, `PipelineSourceId`
   - audit fields: `CreatedAtUtc`, `UpdatedAtUtc`

Persisted snapshot fields were stored as explicit columns with required metadata:
- project snapshot: external ID, name, description, metadata timestamps/currentness
- team snapshot: external ID, project external ID, name, default area path, description, metadata timestamps/currentness
- pipeline snapshot: external ID, project external ID, name, folder, YAML path, repository metadata, metadata timestamps/currentness
- product root snapshot: external ID, title, type, state, project external ID, area path, metadata timestamps/currentness

## 3. Relationships

Implemented required persistence relationships:
- `ProjectSource` → `TfsConnection` via `TfsConnectionId`
- `TeamSource` → `ProjectSource` via `ProjectSourceId`
- `PipelineSource` → `ProjectSource` via `ProjectSourceId`
- `ProductRoot` → `ProjectSource` via `ProjectSourceId`
- `ProductSourceBinding` → `ProductRoot` via `ProductRootId`
- `ProductSourceBinding` → `ProjectSource` via `ProjectSourceId`
- optional source-specific references for bindings:
  - `ProductSourceBinding` → `TeamSource` via `TeamSourceId`
  - `ProductSourceBinding` → `PipelineSource` via `PipelineSourceId`

Deletion behavior:
- every onboarding foreign key uses `DeleteBehavior.Restrict`
- slice 1 does not permit cascade delete across onboarding entities

Binding source integrity:
- `CK_OnboardingProductSourceBindings_SourceReference` enforces exactly one active source path:
  - `Project` bindings require no team/pipeline FK
  - `Team` bindings require `TeamSourceId`
  - `Pipeline` bindings require `PipelineSourceId`

## 4. Constraints and Indexes

Created tables:
- `OnboardingTfsConnections`
- `OnboardingProjectSources`
- `OnboardingTeamSources`
- `OnboardingPipelineSources`
- `OnboardingProductRoots`
- `OnboardingProductSourceBindings`

Unique constraints and indexes:
- `OnboardingTfsConnections`
  - unique: `IX_OnboardingTfsConnections_ConnectionKey`
  - non-unique: `IX_OnboardingTfsConnections_OrganizationUrl`
- `OnboardingProjectSources`
  - unique: `IX_OnboardingProjectSources_TfsConnectionId_ProjectExternalId`
- `OnboardingTeamSources`
  - unique: `IX_OnboardingTeamSources_ProjectSourceId_TeamExternalId`
- `OnboardingPipelineSources`
  - unique: `IX_OnboardingPipelineSources_ProjectSourceId_PipelineExternalId`
- `OnboardingProductRoots`
  - unique: `IX_OnboardingProductRoots_ProjectSourceId_WorkItemExternalId`
- `OnboardingProductSourceBindings`
  - unique: `IX_OnboardingProductSourceBindings_ProductRootId_SourceType_SourceExternalId`
  - non-unique: `IX_OnboardingProductSourceBindings_ProjectSourceId`
  - non-unique: `IX_OnboardingProductSourceBindings_TeamSourceId`
  - non-unique: `IX_OnboardingProductSourceBindings_PipelineSourceId`

Foreign key constraints:
- `FK_OnboardingProjectSources_OnboardingTfsConnections_TfsConnectionId`
- `FK_OnboardingTeamSources_OnboardingProjectSources_ProjectSourceId`
- `FK_OnboardingPipelineSources_OnboardingProjectSources_ProjectSourceId`
- `FK_OnboardingProductRoots_OnboardingProjectSources_ProjectSourceId`
- `FK_OnboardingProductSourceBindings_OnboardingProductRoots_ProductRootId`
- `FK_OnboardingProductSourceBindings_OnboardingProjectSources_ProjectSourceId`
- `FK_OnboardingProductSourceBindings_OnboardingTeamSources_TeamSourceId`
- `FK_OnboardingProductSourceBindings_OnboardingPipelineSources_PipelineSourceId`

Required-column enforcement:
- all external IDs, logical keys, enabled flags, validation state columns, snapshot identity fields, metadata timestamps, and audit timestamps are non-null in schema

## 5. EF Configuration

EF configuration was implemented with explicit fluent configuration under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Configurations/Onboarding`.

Configuration characteristics:
- explicit table names for all onboarding tables
- explicit primary keys for all onboarding entities
- explicit property max lengths for string identity and snapshot fields
- explicit required/non-null column configuration
- explicit index definitions for logical identity and lookup fields
- explicit foreign key mapping with `DeleteBehavior.Restrict`
- explicit check constraint for polymorphic binding-source enforcement
- `PoToolDbContext` updated only to register onboarding `DbSet`s and apply configuration assembly scanning

## 6. Migration Results

Generated migration files:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260405195943_AddOnboardingPersistenceFoundation.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260405195943_AddOnboardingPersistenceFoundation.Designer.cs`

Apply verification:
- command: `dotnet ef database update AddOnboardingPersistenceFoundation --project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --startup-project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --connection "Data Source=/tmp/onboarding-slice1.db" --verbose`
- result: succeeded on an empty SQLite database
- resulting onboarding tables:
  - `OnboardingPipelineSources`
  - `OnboardingProductRoots`
  - `OnboardingProductSourceBindings`
  - `OnboardingProjectSources`
  - `OnboardingTeamSources`
  - `OnboardingTfsConnections`

Rollback verification:
- command: `dotnet ef database update AddEffortSettingsLastModifiedUtcForSqliteOrdering --project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --startup-project /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --connection "Data Source=/tmp/onboarding-slice1.db" --verbose`
- result: succeeded
- post-rollback verification on `/tmp/onboarding-slice1.db`: no `Onboarding*` tables remained

## 7. Test Results

Targeted slice 1 persistence tests:
- command: `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~PersistenceRelationshipContractTests|FullyQualifiedName~OnboardingPersistenceFoundationTests"`
- result: passed
- totals: 10 passed, 0 failed

Targeted onboarding persistence coverage:
- valid onboarding graph insert succeeds
- duplicate project external ID within one connection is rejected
- duplicate binding logical identity is rejected
- missing required scalar field is rejected
- missing required parent reference is rejected before commit
- invalid binding source pattern is rejected by schema constraints

Repository-wide validation after slice 1 changes:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release` succeeded
- full unit suite still reports 5 pre-existing unrelated failures in:
  - `CacheBackedGeneratedClientMigrationAuditTests`
  - `DocumentationVerificationBatch6Tests`
  - `TestCategoryEnforcementTests`

## 8. Governance Compliance

Changed files stayed within slice-1-appropriate paths:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/**`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/**`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/**`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/**`

Leakage check:
- no API/controller files changed
- no client/UI files changed
- no onboarding service/workflow logic added
- no migration execution orchestration added
- no legacy onboarding client code modified
- no dual-write path introduced

Slice 1 output is limited to persisted schema, explicit EF configuration, migration artifacts, persistence coverage tests, and validation evidence.
