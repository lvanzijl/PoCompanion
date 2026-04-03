> **NOTE:** This document captures historical context around seed hardening and includes legacy terminology for traceability.

# Global Seeding Contract Hardening

## Summary

This hardening pass converts startup mock/configuration seeding into a deterministic, dependency-aware process with explicit validation before persistence.

The previously fixed `ProductEntity.ProjectId -> ProjectEntity.Id` failure was only one manifestation of a broader risk class: startup seeding relied on implicit ordering, repeated ad hoc `SaveChangesAsync` calls, and partial assumptions about required parents. The new implementation eliminates that class for the startup mock seed graph by:

- deriving a validated seed plan before any database writes
- persisting the core relational graph in an explicit dependency stage inside one transaction
- validating required EF foreign keys before every save boundary
- reconciling deterministic child collections instead of only appending data
- using SQLite-backed tests to verify relational integrity and idempotency

## Startup seed surface audit

Repository-level audit result:

- **Startup/bootstrap relational seeding path:** `PoTool.Api/Services/MockData/MockConfigurationSeedHostedService.cs`
- **Registered by:** `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- **Runtime condition:** hosted service is added in mock mode startup
- **Other startup code reviewed:** `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs` performs database creation/migration checks but does not seed relational data
- **Other hosted service reviewed:** `EffortEstimationNotificationService` does not seed relational data

Conclusion: the startup relational seed surface is concentrated in `MockConfigurationSeedHostedService`, so hardening that service hardens the full startup seed pipeline currently in scope.

## Complete seeded entity inventory

| Entity | Seed location | Required FK(s) relevant to seeding | Parent entities | Before fix | After hardening |
| --- | --- | --- | --- | --- | --- |
| `ProjectEntity` | `EnsureMockProjectAsync` | none | root | explicit but isolated save | explicit root, tracked in core stage |
| `TeamEntity` | `EnsureMockTeamsAsync` | none | root | explicit but separate stage | explicit root, deterministic ensure/update |
| `ProfileEntity` | `EnsureMockProfilesAndProductsAsync` | none | root | explicit, per-profile save | explicit root, tracked in core stage |
| `ProductEntity` | `EnsureMockProfilesAndProductsAsync` | `ProjectId`, `ProductOwnerId` | `ProjectEntity`, `ProfileEntity` | implicit dependence on prior saves; project bug existed | explicit nav assignment to parent roots before save |
| `ProductBacklogRootEntity` | `EnsureMockProfilesAndProductsAsync` | `ProductId` | `ProductEntity` | appended after product save | reconciled from deterministic plan using `Product` navigation |
| `ProductTeamLinkEntity` | `EnsureMockProfilesAndProductsAsync` | `ProductId`, `TeamId` | `ProductEntity`, `TeamEntity` | appended after product save; area-path assumptions implicit | reconciled from deterministic plan using `Product` and `Team` navigation |
| `RepositoryEntity` | `EnsureMockProfilesAndProductsAsync` | `ProductId` | `ProductEntity` | seeded later in separate pass | reconciled in core stage using `Product` navigation |
| `TfsConfigEntity` | `EnsureMockTfsConfigurationAsync` | none | root | create-if-missing only | deterministic ensure/update with diagnostics |
| `SettingsEntity` | `EnsureActiveProfileAsync` | no enforced FK in model, but logical dependency on active profile | `ProfileEntity` | selected first profile by ID | selects deterministic active profile by seeded name |
| `PortfolioSnapshotEntity` | `EnsureMockPortfolioSnapshotsAsync` | `ProductId` | `ProductEntity` | added per timeline save | planned after core save, batched in one snapshot save |
| `PortfolioSnapshotItemEntity` | created via mapper for snapshot entities | `SnapshotId` | `PortfolioSnapshotEntity` | implicit through mapper and per-timeline save | implicit through mapper but persisted only after validated parent snapshot batch |

## Dependency graph

```text
ProjectEntity
  └─ ProductEntity
       ├─ ProductBacklogRootEntity
       ├─ ProductTeamLinkEntity ──┐
       ├─ RepositoryEntity        │
       └─ PortfolioSnapshotEntity │
            └─ PortfolioSnapshotItemEntity
                                  │
TeamEntity ───────────────────────┘

ProfileEntity
  └─ ProductEntity

SettingsEntity
  └─ logical dependency on the deterministic active ProfileEntity

TfsConfigEntity
  └─ independent root
```

### Root entities
- `ProjectEntity`
- `TeamEntity`
- `ProfileEntity`
- `TfsConfigEntity`

### Dependent entities
- `ProductEntity`
- `ProductBacklogRootEntity`
- `ProductTeamLinkEntity`
- `RepositoryEntity`
- `PortfolioSnapshotEntity`
- `PortfolioSnapshotItemEntity`
- `SettingsEntity` (logical dependency)

### Multi-parent entities
- `ProductEntity` depends on both `ProjectEntity` and `ProfileEntity`
- `ProductTeamLinkEntity` depends on both `ProductEntity` and `TeamEntity`

### Circular dependencies
- No EF-required circular startup seed dependency was found in the current startup seed graph.

## Unsafe patterns found before the fix

1. **Implicit seed gating based on partial existence**
   - Core seeding only ran when profiles, products, and teams were all absent.
   - Partial startup seed states could bypass required dependency creation entirely.

2. **Repeated partial-stage `SaveChangesAsync` calls**
   - Profiles, products, child links, repositories, and snapshots were persisted in separate saves without a surrounding transaction.
   - Mid-seed failures could leave partially built roots behind.

3. **Child entities built from implicit assumptions**
   - Product backlog roots, team links, and repositories were appended after earlier saves instead of being derived from one validated plan.

4. **Late detection of invalid dependencies**
   - Required parent checks were incomplete and mostly delegated to database failure.

5. **Non-deterministic active profile selection**
   - Settings selected the first profile by ID rather than the seed contract’s intended active profile.

6. **Snapshot persistence split across multiple saves**
   - Portfolio snapshots were flushed per timeline entry instead of at one intentional snapshot stage.

## Structural changes made

### 1. Validated seed-plan construction before writes
`MockConfigurationSeedHostedService` now derives a `MockSeedPlan` before any database commit.

The plan:
- validates that the generated hierarchy contains goal roots and team area paths
- validates goal-index references from static profile/product seeds
- resolves deterministic product backlog roots, team area paths, and repository names
- fails before persistence if the logical seed plan is incomplete

### 2. Explicit dependency-ordered core stage
The startup seed flow now runs in this order:

1. build and validate seed plan
2. begin transaction
3. ensure project root
4. ensure team roots
5. ensure profiles and products
6. reconcile backlog roots, team links, and repositories for each seed product
7. save the full core graph
8. ensure TFS config
9. ensure deterministic active profile setting
10. ensure portfolio snapshots
11. commit transaction

### 3. Deterministic child reconciliation
Instead of only appending children, the hardening pass reconciles deterministic seed-managed collections:
- `ProductBacklogRootEntity`
- `ProductTeamLinkEntity`
- `RepositoryEntity`

This makes repeated startup seeding stable for seeded products and prevents silent drift from accumulating.

### 4. Reusable startup relationship validation
Added:
- `PoTool.Api/Services/MockData/StartupSeedRelationshipValidator.cs`

This validator inspects pending EF entries before save and:
- finds required foreign keys from the EF model
- detects missing required FK values early
- checks whether required parents are available through navigation, tracked principals, or persisted rows
- throws explicit diagnostics before SQLite has to reject the batch

## Validation mechanism added

The validation layer now has two levels:

### Seed-plan validation
Catches logical seed construction failures before EF persistence, including:
- missing goal roots in generated mock hierarchy
- invalid goal indexes in static seeds
- products whose roots resolve to no descendants
- products with no matching seeded teams
- products with no deterministic repository mapping

### EF relationship validation
Catches persistence contract failures before `SaveChangesAsync`, including:
- missing required FK values
- dependent entities without tracked or persisted parents
- unresolved required parent references for any pending added/modified entity

Diagnostics include:
- logical operation name
- entity type
- FK property name(s)
- expected parent type
- FK value(s) when a principal is missing

## SaveChanges boundary rationale

The hardening pass keeps save boundaries, but makes them intentional:

1. **Core configuration save**
   - persists project, teams, profiles, products, backlog roots, team links, and repositories together
   - rationale: this is the main relational dependency graph and should commit as one coherent stage

2. **TFS configuration save**
   - independent configuration row
   - rationale: separate for clarity; not part of the product/team/profile graph

3. **Active profile settings save**
   - requires persisted profile IDs
   - rationale: depends on the completed core stage

4. **Portfolio snapshots save**
   - requires persisted seeded products
   - rationale: snapshots are a later dependent stage and now batch in one save instead of one save per timeline entry

All save boundaries are inside one transaction in `StartAsync`, so startup seeding is now coherent instead of a chain of isolated commits.

## Idempotency strategy

The new strategy is deterministic ensure/reconcile, not “seed only when the DB looks empty.”

- Existing seeded roots are reused and updated to the expected contract
- Missing deterministic seed roots are created
- Seed-managed child collections are reconciled for seeded products
- Re-running startup seeding does not duplicate seeded profiles, products, repositories, or snapshots
- Partial mock core state is repaired rather than silently bypassed
- Dependency validation still runs on repeated executions, so idempotency does not suppress integrity checks

## Tests added and updated

Updated `PoTool.Tests.Unit/Services/MockData/MockConfigurationSeedHostedServiceTests.cs` to use SQLite-backed validation for startup seed integrity.

Coverage now includes:
- clean SQLite startup seed success
- repeated startup seed idempotency
- ordered portfolio history for the deterministic active profile
- full SQLite parent/child integrity checks for seeded products, links, repositories, and snapshots
- partial-state repair test proving seeding no longer depends on the database being completely empty
- model-based required-FK guard test that verifies seeded rows satisfy every required FK on the startup-seeded dependent entities

## Confirmation of startup/build/test results

Validated successfully:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~MockConfigurationSeedHostedServiceTests"`
- `ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --configuration Release --urls http://localhost:5292`
- `curl http://localhost:5292/health` returned `200 OK`

Startup log confirmation showed:
- 3 profiles
- 6 products
- 8 teams
- 6 repositories
- 16 portfolio snapshots

## Remaining risk / bug-class status

### Eliminated in current startup seed pipeline
For the startup mock/configuration seed graph currently present in `PoTool.Api`, the FK-dependency bug class is eliminated to the extent that:
- required parents are planned before dependent construction
- required FKs are assigned explicitly or through tracked parent navigation
- pending required relationships are validated before each save
- save stages are wrapped in one transaction
- SQLite-backed tests enforce relational integrity instead of relying on EF InMemory semantics

### Still outside this hardening pass
- `SettingsEntity.ActiveProfileId` remains a logical dependency without an EF FK constraint in the current model
- `PortfolioSnapshotEntity.ProductId` still uses cascade delete in the model; this did not break startup seeding, but it remains a separate persistence-semantics concern outside the direct startup FK-bug fix class

## Files changed for hardening

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/MockConfigurationSeedHostedService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/StartupSeedRelationshipValidator.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/MockData/MockConfigurationSeedHostedServiceTests.cs`
