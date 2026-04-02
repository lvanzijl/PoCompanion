# Test Fixture Persistence Remediation

## Summary

This remediation updated persistence-backed tests and related setup code to comply with the repository-wide required-relationship persistence contract enforced by `PoToolDbContext.SaveChanges*`.

The work focused on repairing legacy fixtures that previously relied on incomplete entity graphs, especially products without projects, cache-state rows without profiles, repositories without products, and sprints without teams.

After the remediation pass, the full `PoTool.Tests.Unit` run no longer reports any persistence-contract failures. Remaining failing tests are unrelated to required-FK graph validity.

## Categorized inventory of failing fixture/setup patterns

Initial full-suite persistence failures grouped into these recurring categories:

1. **Product fixtures missing `ProjectEntity` roots**
   - `ProductEntity` instances were often created with `Name` and optional `ProductOwnerId`, but without `ProjectId` / `Project`
   - this was the dominant failure class across controllers, handlers, services, sync stages, and fixture builders

2. **Sprint fixtures missing `TeamEntity` roots**
   - `SprintEntity` rows were created with `TeamId` values but no tracked or persisted team parent
   - common in filter-resolution and sprint-trend tests

3. **Repository / pipeline fixtures missing `ProductEntity` roots**
   - `RepositoryEntity` and downstream pipeline entities used raw `ProductId` values without a valid product graph

4. **Cache-state fixtures missing `ProfileEntity` roots**
   - `ProductOwnerCacheStateEntity` rows were inserted without a matching profile row
   - common in cache-mode and sync orchestration tests

5. **Nested/shared fixture graphs missing required roots**
   - profile-owned products, backlog-root products, and portfolio/sprint setup helpers constructed partial graphs
   - several helpers seeded dependents before ensuring project/profile/team parents existed

6. **Import path creating invalid products**
   - `ImportConfigurationService` itself created imported `ProductEntity` rows without assigning a required project
   - this was a real persistence-path defect, not just a test-fixture defect

## Shared builders/helpers fixed

Added shared helper:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/TestSupport/PersistenceTestGraph.cs`

Provided reusable helpers for:
- ensuring tracked/persisted `ProjectEntity` roots
- ensuring tracked/persisted `ProfileEntity` roots
- ensuring tracked/persisted `TeamEntity` roots
- creating valid `ProductEntity` rows by default
- creating valid `RepositoryEntity` rows by default
- creating valid `ProductOwnerCacheStateEntity` rows for tests once profile roots exist

## Representative before/after examples

### Product fixture
Before:
- test created `new ProductEntity { Name = "Product A", ProductOwnerId = 7 }`
- save failed because `ProjectId` was required and no project root existed

After:
- tests now call `PersistenceTestGraph.EnsureProject(...)`
- product fixtures set `ProjectId = PersistenceTestGraph.DefaultProjectId` or use `PersistenceTestGraph.CreateProduct(...)`

### Sprint fixture
Before:
- test created `new SprintEntity { TeamId = 1, ... }` without a team parent

After:
- tests now ensure `TeamEntity` roots through `PersistenceTestGraph.EnsureTeam(...)` before saving sprint rows

### Cache-state fixture
Before:
- test created `new ProductOwnerCacheStateEntity { ProductOwnerId = 1 }` without a matching profile

After:
- tests now ensure the owning profile exists before persisting cache-state rows

### Import path
Before:
- `ImportConfigurationService` created imported products without setting `ProjectId`

After:
- the import path now creates/ensures a persisted project root and assigns imported products to it before persistence

## Count of tests fixed directly vs indirectly via shared helpers

- **Directly remediated files:** 40 test files plus 1 production import service path
- **Shared helper introduced:** 1 (`PersistenceTestGraph`)
- **Indirectly healed recurring setup patterns:** product/project, repository/product, sprint/team, and cache-state/profile construction patterns reused across those edited test files

The remediation combined both approaches:
- direct edits where test intent required local graph control
- indirect/shared helper usage for recurring root construction patterns

## Tests intentionally changed to assert rejection of invalid graphs

None in this remediation pass.

Invalid-graph rejection coverage already exists in:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/PersistenceRelationshipContractTests.cs`

This remediation kept existing behavioral tests valid by repairing their fixture graphs instead of changing their intent.

## Remaining gaps

No remaining persistence-contract failures were reported in the latest full `PoTool.Tests.Unit` run.

However, the full unit suite is still not completely green because of unrelated, non-persistence failures, including:
- DI test setup for `DataSourceAwareReadProviderFactory`
- route intent expectations in `DataSourceModeConfigurationTests`
- multiple documentation/audit baseline mismatches
- NSwag and architecture guard expectations unrelated to FK graph validity

These failures are outside the persistence-fixture remediation scope.

## Full-suite status under the persistence contract

- `dotnet build PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo` ✅
- full `PoTool.Tests.Unit` rerun completed ✅
- persistence-contract failures in the unit suite: **0** ✅
- full unit suite overall: **still failing due unrelated non-persistence tests**

## Confirmation

The repository-wide invalid test-fixture and setup patterns that violated the new required-relationship persistence invariant have been remediated for the current `PoTool.Tests.Unit` persistence surface.

The persistence contract remains enforced, was not weakened or bypassed, and the latest full unit-suite rerun showed that the remaining failures are no longer caused by unresolved required foreign keys.
