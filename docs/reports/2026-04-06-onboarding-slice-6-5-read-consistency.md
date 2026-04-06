# Slice 6.5 — Read Consistency

## 1. Soft Delete Enforcement

Implemented EF Core global query filters for all onboarding graph entities through `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Configurations/Onboarding/OnboardingEntityConfigurationHelpers.cs`.

Enforced behavior:
- soft-deleted onboarding graph entities are excluded automatically at the data access layer
- list reads, detail reads, joins, and subqueries inherit the same exclusion rule
- tests now verify soft-deleted entities do not surface through DbContext queries, list queries, or detail queries

Scope intentionally remains limited to onboarding graph entities:
- `TfsConnection`
- `ProjectSource`
- `TeamSource`
- `PipelineSource`
- `ProductRoot`
- `ProductSourceBinding`

Migration ledger entities were intentionally not changed in this slice.

## 2. Read Path Audit

Audited onboarding read paths and enforced/fixed each path as follows.

### Controllers
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/OnboardingCrudController.cs`
  - delegates only
  - no direct `DbContext` access
  - no raw SQL
  - no fix required

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/OnboardingLookupController.cs`
  - delegates only
  - no direct `DbContext` access
  - no raw SQL
  - no fix required

### Handlers
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Onboarding/OnboardingCrudHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Onboarding/OnboardingLookupHandler.cs`
  - delegate only
  - no direct persistence bypass
  - no fix required

### Services
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingCrudService.cs`
  - fixed all read paths to consume centralized active-graph queries
  - removed fragmented manual read filtering as the primary enforcement mechanism
  - added active parent/dependency enforcement for projects, teams, pipelines, roots, and bindings
  - preserved deterministic ordering on list reads

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingStatusService.cs`
  - switched to active-graph query sources
  - status now reflects visible active graph data only
  - hidden deleted/orphaned data no longer influences counts or blockers

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingLookupService.cs`
  - connection read now uses centralized active connection query
  - soft-deleted connections cannot back lookup calls

### Shared read enforcement
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingReadQueries.cs`
  - added centralized read model queries for:
    - active connections
    - active projects
    - active teams
    - active pipelines
    - active roots
    - active bindings
  - binding reads now require:
    - visible root
    - visible project
    - visible team/pipeline when applicable
    - matching source external id
    - project binding presence for dependent team/pipeline bindings

### Raw query / bypass audit
- no onboarding raw SQL reads were found
- no onboarding read path bypassing service-layer enforcement was found
- no direct controller `DbContext` read usage was found

## 3. Graph Consistency

Added defensive read-layer consistency rules so returned graphs cannot contain broken relationships.

Enforced rules:
- project reads require a visible active connection
- team/pipeline/root reads require a visible active project chain
- binding reads require:
  - visible root
  - visible project
  - matching root/project scope
  - visible team/pipeline when referenced
  - matching source external id
  - visible project binding before team/pipeline binding exposure

Result:
- if a parent is deleted, descendants disappear from reads even if legacy rows remain undeleted
- if a binding dependency is missing, the binding does not appear
- partially resolved binding graphs are hidden from read results

## 4. Filtering Validation

Validated supported filters:
- connection
- project
- product root
- status

Results:
- connection and project scoping now operate only on visible active graph members
- product root filtering on bindings cannot surface broken/deleted associations
- status-filtered results are computed against the same visible active graph used by the status service
- no cross-scope leakage remains in tested combinations
- empty and broken legacy scenarios return empty sets instead of ghost rows

Added tests for:
- scoped team filtering with mixed connections/projects and deleted parents
- binding filtering against deleted roots and broken binding dependencies
- hidden results for deleted parent scope

## 5. Status Consistency

Status service now loads onboarding entities through the same active-graph read rules as CRUD reads.

Effects:
- soft-deleted entities no longer affect totals, validity counts, or blockers
- descendants of deleted parents no longer affect status output
- bindings hidden by read-consistency rules no longer contribute residual blockers
- status remains deterministic through explicit ordering before in-memory evaluation

## 6. Validation State Consistency

Read results continue to map directly from persisted validation state.

Verified behavior:
- update path refreshes persisted validation state
- subsequent detail reads reflect the latest validation state and latest snapshot metadata
- no stale validation data remains after update/read sequences in tested scenarios

## 7. Ghost Entity Prevention

Ghost entity prevention is now enforced for these scenarios:
- deleted entity returned directly
- child returned while parent is deleted
- binding returned while root/project/team/pipeline dependency is missing
- dependent binding returned without required project binding
- legacy inconsistent graph rows leaking into filtered reads

Added tests covering:
- deleted parent hides project/team descendants
- deleted root hides bindings
- broken binding source identity is hidden
- dependent binding without project binding is hidden

## 8. DTO Integrity

Verified mapping paths in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Onboarding/OnboardingCrudService.cs`.

Findings:
- DTO mapping does not reconstruct deleted entities
- DTO mapping operates only on already-filtered active entities
- broken graphs are prevented before projection, not repaired after projection
- no DTO path reintroduces hidden entities

## 9. Determinism

Explicit ordering is now consistently applied on list/status reads:
- connections: `Id`
- projects: `ProjectExternalId`
- teams: `TeamExternalId`
- pipelines: `PipelineExternalId`
- roots: `WorkItemExternalId`
- bindings: `ProductRootId`, `SourceType`, `SourceExternalId`

Result:
- same query produces stable results
- no random ordering remains in the audited onboarding read paths

## 10. Test Results

Validated with:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingCrudServiceTests|FullyQualifiedName~OnboardingMigrationExecutionServiceTests|FullyQualifiedName~OnboardingMigrationLedgerServiceTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingPersistenceFoundationTests|FullyQualifiedName~PersistenceRelationshipContractTests|FullyQualifiedName~OnboardingStatusServiceTests"`

Added / updated coverage for:
- global soft-delete filter enforcement
- detail reads hiding soft-deleted graph members
- list reads hiding descendants of deleted parents
- filter scoping and deterministic ordering
- deleted-root and broken-binding ghost prevention
- status ignoring deleted or hidden graph members
- validation-state freshness after update/read cycles

## 11. Governance Compliance

Confirmed:
- no UI changes
- no API expansion beyond existing Slice 6 surface
- no write-side behavior changes beyond read-consistency enforcement
- no migration execution logic changes
- no validation or status bypass introduced
- no raw-query onboarding read bypass introduced
