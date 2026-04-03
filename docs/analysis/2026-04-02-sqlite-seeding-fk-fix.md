# SQLite Seeding Foreign Key Fix

## Summary

- Startup failure reproduced against the real SQLite-backed API startup path.
- Exact failing entity: `ProductEntity`
- Exact failing relationship: `ProductEntity.ProjectId -> ProjectEntity.Id`
- Root cause: mock seeding created products before creating the required project row and never assigned a valid `ProjectId`.
- Fix applied: seed a deterministic mock project first, assign every seeded product to it, validate dependencies before dependent inserts, and add SQLite-backed regression coverage.
- Result: startup now completes successfully and `/health` returns `200 OK`.

## Exact failing entity and FK relationship

The failing insert was the `INSERT INTO "Products"` statement during `MockConfigurationSeedHostedService.StartAsync(...)`.

The violated foreign key is:

- **Child**: `Products.ProjectId`
- **Parent**: `Projects.Id`
- **Model configuration**: `DeleteBehavior.Restrict`

Relevant model configuration:

```csharp
entity.Property(e => e.ProjectId)
    .HasMaxLength(64)
    .IsRequired();

entity.HasOne(e => e.Project)
    .WithMany(e => e.Products)
    .HasForeignKey(e => e.ProjectId)
    .OnDelete(DeleteBehavior.Restrict);
```

Before the fix, mock product seeding created `ProductEntity` rows without setting `ProjectId`, so the insert attempted to persist the default empty string for a required FK while no matching `ProjectEntity` existed.

## Root cause explanation

This was a **schema-versus-seed assumption mismatch**, not a middleware issue and not a `ProductTeamLinkEntity` ordering bug.

The schema requires every `ProductEntity` to reference an existing `ProjectEntity` through a non-nullable `ProjectId` FK. However, `MockConfigurationSeedHostedService`:

1. seeded teams first,
2. seeded profiles next,
3. created products,
4. but never created the required mock project row,
5. and never assigned `Product.ProjectId`.

That made the first product insert fail immediately under SQLite FK enforcement.

## Dependency graph of involved seed entities

```text
ProjectEntity
  └─ ProductEntity (requires ProjectId)
       ├─ ProductBacklogRootEntity (requires ProductId)
       ├─ ProductTeamLinkEntity (requires ProductId + TeamId)
       ├─ RepositoryEntity (requires ProductId)
       └─ PortfolioSnapshotEntity (requires ProductId)

ProfileEntity
  └─ ProductEntity (requires ProductOwnerId for seeded mock products)

TeamEntity
  └─ ProductTeamLinkEntity (requires TeamId)

SettingsEntity
  └─ references seeded active ProfileEntity

TfsConfigEntity
  └─ independent of the product/project FK chain
```

Corrected insertion order:

1. `ProjectEntity`
2. `TeamEntity`
3. `ProfileEntity`
4. `ProductEntity`
5. `ProductBacklogRootEntity` + `ProductTeamLinkEntity`
6. `RepositoryEntity`
7. `SettingsEntity`
8. `PortfolioSnapshotEntity`
9. `TfsConfigEntity`

## Schema/config mismatches found

The key mismatch was:

- **Schema reality**: `Product.ProjectId` is required and references `Projects.Id`.
- **Seed assumption**: mock products could be created without a project.

That assumption used to be invalid as soon as SQLite FK enforcement was active.

## Before/after code snippets

### Fix 1: create the required project before products

**Before**

```csharp
var teams = await SeedTeamsAsync(context, hierarchy, now, cancellationToken);
await SeedProfilesAndProductsAsync(context, hierarchy, goals, teams, now, cancellationToken);
```

**After**

```csharp
var project = await EnsureMockProjectAsync(context, cancellationToken);
var teams = await SeedTeamsAsync(context, hierarchy, now, cancellationToken);
await SeedProfilesAndProductsAsync(context, hierarchy, goals, teams, project, now, cancellationToken);
```

### Fix 2: assign each seeded product to the deterministic mock project

**Before**

```csharp
var product = new ProductEntity
{
    ProductOwnerId = profile.Id,
    Name = productSeed.Name,
    Order = productOrder,
    PictureType = (int)ProductPictureType.Default,
    DefaultPictureId = productSeed.DefaultPictureId,
    EstimationMode = (int)Shared.Settings.EstimationMode.StoryPoints,
    CreatedAt = now,
    LastModified = now
};
```

**After**

```csharp
var product = new ProductEntity
{
    ProductOwnerId = profile.Id,
    ProjectId = project.Id,
    Name = productSeed.Name,
    Order = productOrder,
    PictureType = (int)ProductPictureType.Default,
    DefaultPictureId = productSeed.DefaultPictureId,
    EstimationMode = (int)Shared.Settings.EstimationMode.StoryPoints,
    CreatedAt = now,
    LastModified = now
};
```

### Fix 3: add deterministic validation and diagnostics

**After**

```csharp
ValidateProductSeedDependencies(product, project, relevantAreaPaths);
await SaveChangesWithDiagnosticsAsync(
    context,
    cancellationToken,
    $"creating backlog roots and team links for product '{product.Name}'");
```

## Corrected seeding order

The corrected implementation now explicitly seeds the mock project before any products are created. Products then receive the deterministic project ID (`mock-project-battleship-systems`) before they are persisted. Only after a valid product exists are backlog roots and product-team links inserted.

This aligns the seed order with the actual EF Core dependency graph instead of relying on implicit assumptions.

## Added safeguards and tests

### Runtime safeguards

- Added `EnsureMockProjectAsync(...)` so the required parent project exists before any product insert.
- Added `ValidateProductSeedDependencies(...)` to fail fast if:
  - the project ID is missing,
  - a product is not assigned to the expected project,
  - no seeded teams are available for the product’s backlog roots.
- Added `SaveChangesWithDiagnosticsAsync(...)` to log the operation and pending tracked entity values when EF throws `DbUpdateException` during mock seeding.

### Regression coverage

Added SQLite-backed regression coverage in `MockConfigurationSeedHostedServiceTests` that:

- bootstraps `PoToolDbContext` with real SQLite in-memory storage,
- runs the full mock seeding flow,
- verifies the seeded project exists,
- verifies all seeded products reference that project,
- verifies product-team links persist successfully under SQLite FK enforcement.

## Validation performed

- `dotnet build PoTool.sln --configuration Release --nologo` ✅
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~MockConfigurationSeedHostedServiceTests"` ✅
- `ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --configuration Release --urls http://localhost:5291` ✅
- `curl http://localhost:5291/health` ✅ (`200 OK`)

## Confirmation

Startup now succeeds for a clean SQLite-backed application initialization path.

The blocking FK failure class is eliminated for `MockConfigurationSeedHostedService` because the required `ProjectEntity` parent row is now created deterministically before any dependent `ProductEntity` rows are inserted, and SQLite-backed regression coverage now protects that contract.
