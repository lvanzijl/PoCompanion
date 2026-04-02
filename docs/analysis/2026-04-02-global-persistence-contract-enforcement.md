# Global Persistence Contract Enforcement

## Summary

This change promotes required-relationship validation from startup seeding into the central EF Core persistence boundary.

The repository now enforces a global persistence contract through `PoToolDbContext` save overrides and `RequiredRelationshipPersistenceValidator`, so any added or modified entity with required foreign keys is validated before EF Core writes to the database.

This eliminates silent persistence of unresolved required relationships for the validated save path. It also exposes pre-existing invalid fixture/setup patterns in parts of the unit-test suite that were previously relying on incomplete graphs.

## Inventory of persistence entry points

Repository scan result for write/persist boundaries:

### Central persistence boundary
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs`
  - single EF Core `DbContext`
  - 40+ `DbSet<>` roots
  - now overrides all `SaveChanges` / `SaveChangesAsync` entrypoints

### Repository layer write paths
Representative repository writers discovered during the scan:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProfileRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/TeamRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/RepositoryRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/PipelineRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/PullRequestRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ReleasePlanningRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/CacheStateRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SettingsRepository.cs`

### Service / background / synchronization write paths
Representative direct `DbContext` writers discovered during the scan:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ActivityEventIngestionService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemResolutionService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ForecastProjectionMaterializationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/TfsConfigurationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/TriageTagService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Configuration/ImportConfigurationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/MockConfigurationSeedHostedService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/*.cs`

### Direct handler write path
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/UpdateEffortEstimationSettingsCommandHandler.cs`

### Test persistence paths
SQLite and InMemory persistence appears across many test files under:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/**`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/**`

## Enforced contract

The repository-wide persistence contract is now:

- no entity with required foreign keys may be persisted unless every required relationship is resolved before persistence begins
- required foreign keys must be assigned explicitly, or the parent navigation must be assigned and tracked in the same persistence boundary
- parent entities for required relationships must already exist as tracked or persisted principals before child persistence
- no reliance on default values, empty strings, post-save mutation, or database exceptions to repair required relationships
- validation before persistence is mandatory and runs from the central `PoToolDbContext` boundary
- seeding and bootstrap flows must follow plan → validate → execute and must not partially persist graphs before required parents exist

## Validation integration

### Where it runs
- `PoToolDbContext.SaveChanges()`
- `PoToolDbContext.SaveChanges(bool)`
- `PoToolDbContext.SaveChangesAsync(CancellationToken)`
- `PoToolDbContext.SaveChangesAsync(bool, CancellationToken)`

### How it works
Added:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/RequiredRelationshipPersistenceValidator.cs`

The validator inspects `ChangeTracker` entries in `Added` and `Modified` state and, for each required EF foreign key, checks:
- whether a parent navigation is assigned and tracked
- whether the FK value is missing/default/blank
- whether a tracked principal already matches the FK
- whether a persisted principal exists for that FK value

If validation fails, it throws an `InvalidOperationException` before EF reaches database commit.

Diagnostics include:
- operation (`SaveChanges` or `SaveChangesAsync`)
- dependent entity type
- FK property name(s)
- expected parent entity type
- FK value(s) when a principal is missing

## Guardrails and analyzers added

### Structural guardrails
- central save overrides in `PoToolDbContext`
- reusable `RequiredRelationshipPersistenceValidator`
- startup seeding still retains explicit plan → validate → execute structure and transaction boundary

### Convention / audit checks
Added:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/PersistenceContractAuditTests.cs`

These audits verify that:
- `PoToolDbContext` overrides all save entrypoints
- the context source still invokes the central validator
- `MockConfigurationSeedHostedService` retains the plan/transaction/save structure
- the authoritative copilot rule file includes the global persistence contract

### Test guardrail
Added:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/PersistenceRelationshipContractTests.cs`

This test set includes a required-FK coverage manifest that scans model metadata and fails if a new required FK path appears without an explicit coverage mapping.

## Violations found and fixed

### Fixed in code
1. **Validator scope leak from startup seeding only**
   - before: required-relationship validation only ran from mock startup seeding
   - after: validation runs from the central `PoToolDbContext` save boundary

2. **Save path bypass risk**
   - before: repositories, services, sync stages, handlers, and tests could call `SaveChanges*` without pre-save relationship validation
   - after: all standard EF save entrypoints in `PoToolDbContext` are covered automatically

3. **Rule formalization gap**
   - before: repository instructions had EF concurrency rules, but no explicit repository-wide pre-persistence relationship contract
   - after: the authoritative rule source and human-readable mirror both describe the contract

### Existing invalid persistence setups exposed by the new invariant
Running the full unit suite after the save-hook change exposed pre-existing invalid test/setup graphs, including:
- `ProductEntity` persisted without `ProjectId` / `Project`
- `ProductOwnerCacheStateEntity` persisted with `ProductOwnerId` but no tracked/persisted `ProfileEntity`
- `SprintEntity` persisted with `TeamId` but no tracked/persisted `TeamEntity`
- a smaller number of dependent rows persisted against missing `ProductEntity` parents

These are real contract violations now caught earlier by design.

## Tests added

### New persistence contract tests
`/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/PersistenceRelationshipContractTests.cs`

Added coverage for:
- missing required FK value fails before commit
- missing parent principal fails before commit
- valid tracked parent graph persists successfully
- required FK coverage manifest matches EF model metadata

### Existing seeding regression tests still validated
`/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/MockData/MockConfigurationSeedHostedServiceTests.cs`

These continue to verify:
- dependency-ordered startup seeding
- SQLite-backed relational success
- idempotency
- required seed FK coverage

## Documentation and rule formalization

Updated authoritative rule source:
- `/home/runner/work/PoCompanion/PoCompanion/.github/copilot-instructions.md`

Added mirror document:
- `/home/runner/work/PoCompanion/PoCompanion/docs/rules/persistence-contract.md`

## Validation results

### Passed
- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~PersistenceContract|FullyQualifiedName~MockConfigurationSeedHostedServiceTests"`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --nologo`
- startup run succeeded
- `curl http://localhost:5293/health` returned `200 OK`

### Remaining gap
A full `PoTool.Tests.Unit` run currently exposes a broad set of pre-existing invalid test fixtures and helper setups that do not satisfy the new persistence contract. The most common uncovered issue is `ProductEntity` creation without an explicit or tracked `ProjectEntity` parent.

## Repository-wide status

### Prevented now
For all code paths that persist through the standard `PoToolDbContext` save boundary, the FK integrity bug class is now structurally prevented at save time:
- missing required FK values are rejected early
- missing principals for required relationships are rejected early
- tracked parent navigation graphs are allowed and persist correctly
- startup seeding remains dependency-aware and transactional

### Not fully closed yet
The repository is not yet fully green under the new invariant because existing unit-test fixtures still contain invalid persisted graphs that must be remediated to comply with the contract.

That means the runtime persistence guard is in place repository-wide, but repository-wide adoption is still incomplete in legacy test data builders/setup code until those fixtures are updated.
