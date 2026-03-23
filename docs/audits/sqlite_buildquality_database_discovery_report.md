# SQLite BuildQuality Database Discovery Report

This report is a discovery-only inspection of the current EF Core persistence model used by PoTool for local SQLite storage.

Scope inspected:

- `PoTool.Api/Persistence/PoToolDbContext.cs`
- `PoTool.Api/Persistence/Entities/*.cs`
- `PoTool.Api/Migrations/20260321223826_AddBuildQualityDataFoundation.cs`
- `PoTool.Api/Migrations/PoToolDbContextModelSnapshot.cs`
- `PoTool.Api/Services/Sync/PipelineSyncStage.cs`
- `PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Api/appsettings.json`

## 1. Database overview

### Supported providers

- **SQLite** is the default local provider.
- **SQL Server** is also supported when `ConnectionStrings:SqlServerConnection` is configured.

Evidence:

- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` chooses SQL Server only when `SqlServerConnection` is present; otherwise it uses SQLite.
- `PoTool.Api/appsettings.json` defines `"DefaultConnection": "Data Source=potool.db"`.

### Default local database type

- **Default local database type:** SQLite
- **Default local connection string:** `Data Source=potool.db`

### Likely local database file location

- **Likely file name:** `potool.db`
- **Likely location for local development:** the current working directory of the `PoTool.Api` process
- **Practical local expectation:** often `PoTool.Api/potool.db` when the API is run from the project directory
- **Exact absolute path:** **UNCERTAIN**, because the configured SQLite connection string is relative and the code does not pin it to an absolute path

### Table naming convention

- Table names are **not customized per entity in `OnModelCreating`**.
- The effective table names in the model snapshot are pluralized EF table names such as:
  - `CachedPipelineRuns`
  - `TestRuns`
  - `Coverages`
  - `PipelineDefinitions`
  - `Products`
  - `Repositories`
  - `Profiles`
- So the names do **not** come directly from the entity class names like `CachedPipelineRunEntity`; they map to the EF table names visible in the migration snapshot.

## 2. Relevant BuildQuality tables

| Table | Purpose | Key columns | Linkage |
| --- | --- | --- | --- |
| `CachedPipelineRuns` | Cached build/run anchor rows used for BuildQuality time scoping and result selection | `Id`, `ProductOwnerId`, `PipelineDefinitionId`, `TfsRunId`, `Result`, `FinishedDateUtc`, `SourceBranch` | `PipelineDefinitionId -> PipelineDefinitions.Id`, `ProductOwnerId -> Profiles.Id` |
| `TestRuns` | Raw test-run facts linked to one cached build anchor | `Id`, `BuildId`, `ExternalId`, `TotalTests`, `PassedTests`, `NotApplicableTests`, `Timestamp` | `BuildId -> CachedPipelineRuns.Id` |
| `Coverages` | Raw coverage facts linked to one cached build anchor | `Id`, `BuildId`, `CoveredLines`, `TotalLines`, `Timestamp` | `BuildId -> CachedPipelineRuns.Id` |
| `PipelineDefinitions` | Cached pipeline definition metadata used to tie builds to products/repositories and default branch | `Id`, `PipelineDefinitionId`, `ProductId`, `RepositoryId`, `Name`, `DefaultBranch` | `ProductId -> Products.Id`, `RepositoryId -> Repositories.Id` |
| `Products` | Product scope boundary used by BuildQuality queries | `Id`, `ProductOwnerId`, `Name` | `ProductOwnerId -> Profiles.Id` |
| `Repositories` | Repository scope for products and pipeline definitions | `Id`, `ProductId`, `Name` | `ProductId -> Products.Id` |
| `Profiles` | Product-owner scope used by cached builds | `Id`, `Name` | Parent of `Products`; directly linked from `CachedPipelineRuns.ProductOwnerId` |

### Tables most directly relevant to manual BuildQuality debugging

1. `CachedPipelineRuns`
2. `TestRuns`
3. `Coverages`
4. `PipelineDefinitions`

The `Products`, `Repositories`, and `Profiles` tables matter when you need to understand which product, repository, or product owner a build belongs to.

## 3. Build anchor model

### Which table is the build anchor?

- **Build anchor table:** `CachedPipelineRuns`

### Is the TFS external build id stored directly?

- **Yes.**
- The external TFS/Azure DevOps build/run identifier is stored in:
  - `CachedPipelineRuns.TfsRunId`

### What column should you use to look up build `168570`?

- First lookup column for the external build id:
  - `CachedPipelineRuns.TfsRunId`

Example anchor lookup:

```sql
SELECT *
FROM CachedPipelineRuns
WHERE TfsRunId = 168570;
```

### Is there also an internal database id?

- **Yes.**
- Internal database primary key:
  - `CachedPipelineRuns.Id`

### Critical linkage rule

- `TestRuns.BuildId` and `Coverages.BuildId` link to **`CachedPipelineRuns.Id`**
- They do **not** link to `CachedPipelineRuns.TfsRunId`

That means the normal manual debugging path is:

1. Find the `CachedPipelineRuns` row by `TfsRunId = 168570`
2. Note the internal `Id`
3. Use that internal `Id` to query `TestRuns.BuildId` and `Coverages.BuildId`

### Important uniqueness detail

`CachedPipelineRuns` has a unique index on:

```text
(ProductOwnerId, PipelineDefinitionId, TfsRunId)
```

So `TfsRunId = 168570` is expected to be unique only **within** a product-owner + pipeline-definition combination, not necessarily globally across all cached runs.

## 4. Table-by-table column reference

### 4.1 `CachedPipelineRuns`

| Column | Meaning | Likely SQLite type | Key / FK / nullable |
| --- | --- | --- | --- |
| `Id` | Internal cached build row id | `INTEGER` | PK, not null |
| `ProductOwnerId` | Product owner / profile scope | `INTEGER` | FK to `Profiles.Id`, not null |
| `PipelineDefinitionId` | Internal pipeline definition row id | `INTEGER` | FK to `PipelineDefinitions.Id`, not null |
| `TfsRunId` | External TFS/Azure DevOps build id | `INTEGER` | Indexed, not null |
| `RunName` | Build/run number or name | `TEXT` | nullable |
| `State` | Build state such as `completed` | `TEXT` | nullable |
| `Result` | Build result such as `succeeded`, `failed`, `canceled` | `TEXT` | nullable |
| `CreatedDate` | Original run created timestamp as `DateTimeOffset` | `TEXT` | nullable |
| `CreatedDateUtc` | UTC timestamp used in SQLite predicates/sorting | `TEXT` | indexed, nullable |
| `FinishedDate` | Original run finished timestamp as `DateTimeOffset` | `TEXT` | nullable |
| `FinishedDateUtc` | UTC timestamp used in SQLite predicates/sorting | `TEXT` | indexed, nullable |
| `SourceBranch` | Branch name / ref used by the run | `TEXT` | nullable |
| `SourceVersion` | Source commit SHA/version | `TEXT` | nullable |
| `Url` | TFS/Azure DevOps run URL | `TEXT` | nullable |
| `CachedAt` | When PoTool cached the row | `TEXT` | not null |

Relevant indexes and constraints:

- PK: `Id`
- Unique index: `(ProductOwnerId, PipelineDefinitionId, TfsRunId)`
- Index: `CreatedDateUtc`
- Index: `FinishedDateUtc`

### 4.2 `TestRuns`

| Column | Meaning | Likely SQLite type | Key / FK / nullable |
| --- | --- | --- | --- |
| `Id` | Internal test-run row id | `INTEGER` | PK, not null |
| `BuildId` | Internal cached build anchor id | `INTEGER` | FK to `CachedPipelineRuns.Id`, not null |
| `ExternalId` | Stable external test-run id when source provides one | `INTEGER` | nullable, part of unique composite index |
| `TotalTests` | Raw total tests | `INTEGER` | not null |
| `PassedTests` | Raw passed tests | `INTEGER` | not null |
| `NotApplicableTests` | Raw not-applicable tests | `INTEGER` | not null |
| `Timestamp` | Optional UTC timestamp from source payload | `TEXT` | indexed, nullable |
| `CachedAt` | When PoTool cached the row | `TEXT` | not null |

Relevant indexes and constraints:

- PK: `Id`
- FK: `BuildId -> CachedPipelineRuns.Id`
- Index: `BuildId`
- Index: `Timestamp`
- Unique index: `(BuildId, ExternalId)`

### 4.3 `Coverages`

| Column | Meaning | Likely SQLite type | Key / FK / nullable |
| --- | --- | --- | --- |
| `Id` | Internal coverage row id | `INTEGER` | PK, not null |
| `BuildId` | Internal cached build anchor id | `INTEGER` | FK to `CachedPipelineRuns.Id`, not null |
| `CoveredLines` | Raw covered lines | `INTEGER` | not null |
| `TotalLines` | Raw total lines | `INTEGER` | not null |
| `Timestamp` | Optional UTC timestamp from source payload | `TEXT` | indexed, nullable |
| `CachedAt` | When PoTool cached the row | `TEXT` | not null |

Relevant indexes and constraints:

- PK: `Id`
- FK: `BuildId -> CachedPipelineRuns.Id`
- Index: `BuildId`
- Index: `Timestamp`

### 4.4 `PipelineDefinitions`

| Column | Meaning | Likely SQLite type | Key / FK / nullable |
| --- | --- | --- | --- |
| `Id` | Internal pipeline definition row id | `INTEGER` | PK, not null |
| `PipelineDefinitionId` | External TFS pipeline definition id | `INTEGER` | part of unique product-scoped index, not null |
| `ProductId` | Product owning the pipeline definition | `INTEGER` | FK to `Products.Id`, not null |
| `RepositoryId` | Repository used by the pipeline definition | `INTEGER` | FK to `Repositories.Id`, not null |
| `RepoId` | External repository GUID | `TEXT` | not null |
| `RepoName` | Repository name from TFS | `TEXT` | not null |
| `Name` | Pipeline definition name | `TEXT` | not null |
| `YamlPath` | YAML path in repository | `TEXT` | nullable |
| `Folder` | TFS pipeline folder/path | `TEXT` | nullable |
| `Url` | TFS pipeline definition URL | `TEXT` | nullable |
| `DefaultBranch` | Default branch used for BuildQuality branch scoping | `TEXT` | nullable |
| `LastSyncedUtc` | Last sync timestamp | `TEXT` | not null |

Relevant indexes and constraints:

- PK: `Id`
- Unique index: `(ProductId, PipelineDefinitionId)`
- Index: `RepositoryId`

### 4.5 `Products`

| Column | Meaning | Likely SQLite type | Key / FK / nullable |
| --- | --- | --- | --- |
| `Id` | Internal product id | `INTEGER` | PK, not null |
| `ProductOwnerId` | Owning profile id | `INTEGER` | FK to `Profiles.Id`, nullable |
| `Name` | Product name | `TEXT` | not null |
| `Order` | UI/config ordering | `INTEGER` | not null |
| `PictureType` | Picture mode | `INTEGER` | not null |
| `DefaultPictureId` | Default picture selection | `INTEGER` | not null |
| `CustomPicturePath` | Custom picture path | `TEXT` | nullable |
| `CreatedAt` | Created timestamp | `TEXT` | not null |
| `LastModified` | Modified timestamp | `TEXT` | not null |
| `LastSyncedAt` | Product sync timestamp | `TEXT` | nullable |

### 4.6 `Repositories`

| Column | Meaning | Likely SQLite type | Key / FK / nullable |
| --- | --- | --- | --- |
| `Id` | Internal repository id | `INTEGER` | PK, not null |
| `ProductId` | Owning product id | `INTEGER` | FK to `Products.Id`, not null |
| `Name` | Repository name | `TEXT` | not null |
| `CreatedAt` | Created timestamp | `TEXT` | not null |

### 4.7 `Profiles`

| Column | Meaning | Likely SQLite type | Key / FK / nullable |
| --- | --- | --- | --- |
| `Id` | Internal product-owner/profile id | `INTEGER` | PK, not null |
| `Name` | Product-owner/profile name | `TEXT` | not null |
| `GoalIds` | Stored goal ids | `TEXT` | not null |
| `PictureType` | Picture mode | `INTEGER` | not null |
| `DefaultPictureId` | Default picture selection | `INTEGER` | not null |
| `CustomPicturePath` | Custom picture path | `TEXT` | nullable |
| `CreatedAt` | Created timestamp | `TEXT` | not null |
| `LastModified` | Modified timestamp | `TEXT` | not null |

## 5. Linkage explanation

### Build ↔ test runs

- Persisted linkage uses **both** identifiers during ingestion:
  - external build id from TFS arrives as test-run DTO `BuildId`
  - PoTool resolves that external build id to `CachedPipelineRuns.Id`
- Persisted foreign key uses **internal entity id only**:
  - `TestRuns.BuildId -> CachedPipelineRuns.Id`

Code evidence:

- `PipelineSyncStage` builds a lookup from `CachedPipelineRuns.TfsRunId` to `CachedPipelineRuns.Id`
- `UpsertTestRunsAsync` persists `BuildId = internalBuildId`
- `BuildQualityScopeLoader` later reads `TestRuns` by matching `BuildId` to selected cached build `Id` values

### Build ↔ coverage

- Persisted linkage also uses **both** identifiers during ingestion:
  - external coverage DTO `BuildId`
  - resolved to internal `CachedPipelineRuns.Id`
- Persisted foreign key uses **internal entity id only**:
  - `Coverages.BuildId -> CachedPipelineRuns.Id`

### Build ↔ pipeline definition

- `CachedPipelineRuns.PipelineDefinitionId -> PipelineDefinitions.Id`
- The cached build row stores the internal pipeline definition id, not the external pipeline definition id
- The external pipeline definition id is stored separately in:
  - `PipelineDefinitions.PipelineDefinitionId`

### Build ↔ product

- Build-to-product is **indirect**:
  - `CachedPipelineRuns.PipelineDefinitionId -> PipelineDefinitions.Id`
  - `PipelineDefinitions.ProductId -> Products.Id`

There is also direct owner scoping on the build row:

- `CachedPipelineRuns.ProductOwnerId -> Profiles.Id`

### Build ↔ repository

- Build-to-repository is also **indirect**:
  - `CachedPipelineRuns.PipelineDefinitionId -> PipelineDefinitions.Id`
  - `PipelineDefinitions.RepositoryId -> Repositories.Id`

### Branch scoping used by BuildQuality reads

`BuildQualityScopeLoader` narrows BuildQuality reads by:

1. product owner
2. product / repository / pipeline filters
3. `CachedPipelineRuns.FinishedDateUtc`
4. matching `CachedPipelineRuns.SourceBranch` to `PipelineDefinitions.DefaultBranch`

So manual debugging should remember that a build row can exist in `CachedPipelineRuns` but still be excluded from BuildQuality queries if the branch does not match the pipeline definition default branch.

## 6. Manual SQLite query cookbook

Assumption for the examples below:

- external build id to inspect: `168570`

### Query A — list all relevant tables

```sql
SELECT name
FROM sqlite_master
WHERE type = 'table'
  AND name IN (
    'Profiles',
    'Products',
    'Repositories',
    'PipelineDefinitions',
    'CachedPipelineRuns',
    'TestRuns',
    'Coverages'
  )
ORDER BY name;
```

### Query B — inspect schema for a table

```sql
PRAGMA table_info('CachedPipelineRuns');
```

Useful related inspection commands:

```sql
PRAGMA foreign_key_list('TestRuns');
PRAGMA index_list('TestRuns');
PRAGMA foreign_key_list('Coverages');
PRAGMA index_list('CachedPipelineRuns');
```

### Query C — find build row for external build id `168570`

```sql
SELECT
    Id,
    ProductOwnerId,
    PipelineDefinitionId,
    TfsRunId,
    RunName,
    State,
    Result,
    SourceBranch,
    CreatedDateUtc,
    FinishedDateUtc,
    CachedAt
FROM CachedPipelineRuns
WHERE TfsRunId = 168570
ORDER BY Id;
```

### Query D — find linked test-run rows for build `168570`

```sql
WITH build_anchor AS (
    SELECT Id, TfsRunId, ProductOwnerId, PipelineDefinitionId
    FROM CachedPipelineRuns
    WHERE TfsRunId = 168570
)
SELECT
    b.TfsRunId,
    b.Id AS CachedBuildId,
    tr.Id AS TestRunRowId,
    tr.ExternalId,
    tr.TotalTests,
    tr.PassedTests,
    tr.NotApplicableTests,
    tr.Timestamp,
    tr.CachedAt
FROM build_anchor b
JOIN TestRuns tr
    ON tr.BuildId = b.Id
ORDER BY tr.Timestamp, tr.Id;
```

### Query E — find linked coverage rows for build `168570`

```sql
WITH build_anchor AS (
    SELECT Id, TfsRunId
    FROM CachedPipelineRuns
    WHERE TfsRunId = 168570
)
SELECT
    b.TfsRunId,
    b.Id AS CachedBuildId,
    c.Id AS CoverageRowId,
    c.CoveredLines,
    c.TotalLines,
    c.Timestamp,
    c.CachedAt
FROM build_anchor b
JOIN Coverages c
    ON c.BuildId = b.Id
ORDER BY c.Timestamp, c.Id;
```

### Query F — show build + test-run counts

```sql
SELECT
    b.Id AS CachedBuildId,
    b.TfsRunId,
    b.Result,
    COUNT(tr.Id) AS TestRunRowCount,
    COALESCE(SUM(tr.TotalTests), 0) AS TotalTests,
    COALESCE(SUM(tr.PassedTests), 0) AS PassedTests,
    COALESCE(SUM(tr.NotApplicableTests), 0) AS NotApplicableTests
FROM CachedPipelineRuns b
LEFT JOIN TestRuns tr
    ON tr.BuildId = b.Id
WHERE b.TfsRunId = 168570
GROUP BY b.Id, b.TfsRunId, b.Result
ORDER BY b.Id;
```

### Query G — show build + coverage counts

```sql
SELECT
    b.Id AS CachedBuildId,
    b.TfsRunId,
    b.Result,
    COUNT(c.Id) AS CoverageRowCount,
    COALESCE(SUM(c.CoveredLines), 0) AS CoveredLines,
    COALESCE(SUM(c.TotalLines), 0) AS TotalLines
FROM CachedPipelineRuns b
LEFT JOIN Coverages c
    ON c.BuildId = b.Id
WHERE b.TfsRunId = 168570
GROUP BY b.Id, b.TfsRunId, b.Result
ORDER BY b.Id;
```

### Query H — check whether rows exist but are not linked correctly

This checks for orphaned child rows. Under the current schema and foreign keys this should normally return zero rows, but it is still a useful sanity check when inspecting a copied database or suspecting corruption.

```sql
SELECT 'TestRuns' AS TableName, tr.Id AS ChildRowId, tr.BuildId
FROM TestRuns tr
LEFT JOIN CachedPipelineRuns b
    ON b.Id = tr.BuildId
WHERE b.Id IS NULL

UNION ALL

SELECT 'Coverages' AS TableName, c.Id AS ChildRowId, c.BuildId
FROM Coverages c
LEFT JOIN CachedPipelineRuns b
    ON b.Id = c.BuildId
WHERE b.Id IS NULL
ORDER BY TableName, ChildRowId;
```

### Bonus query — trace build to pipeline, product, repository, and owner

```sql
SELECT
    b.Id AS CachedBuildId,
    b.TfsRunId,
    b.Result,
    b.SourceBranch,
    b.FinishedDateUtc,
    pd.Id AS PipelineDefinitionDbId,
    pd.PipelineDefinitionId AS ExternalPipelineDefinitionId,
    pd.Name AS PipelineName,
    pd.DefaultBranch,
    r.Id AS RepositoryDbId,
    r.Name AS RepositoryName,
    p.Id AS ProductDbId,
    p.Name AS ProductName,
    pr.Id AS ProfileDbId,
    pr.Name AS ProductOwnerName
FROM CachedPipelineRuns b
JOIN PipelineDefinitions pd
    ON pd.Id = b.PipelineDefinitionId
JOIN Repositories r
    ON r.Id = pd.RepositoryId
JOIN Products p
    ON p.Id = pd.ProductId
LEFT JOIN Profiles pr
    ON pr.Id = b.ProductOwnerId
WHERE b.TfsRunId = 168570
ORDER BY b.Id;
```

## 7. BuildQuality-specific debugging path

Practical path for debugging build `168570`:

1. Run Query C on `CachedPipelineRuns` using `TfsRunId = 168570`
2. Note the internal cached build `Id`
3. Note `PipelineDefinitionId`, `ProductOwnerId`, `SourceBranch`, and `FinishedDateUtc`
4. Run Query D using the cached build `Id` linkage path to inspect `TestRuns`
5. Run Query E using the cached build `Id` linkage path to inspect `Coverages`
6. Run Query F and Query G to see whether rows exist and how many child rows were aggregated
7. If needed, run the bonus trace query to connect the build to pipeline, repository, product, and owner
8. If BuildQuality still looks wrong, compare:
   - `CachedPipelineRuns.SourceBranch`
   - `PipelineDefinitions.DefaultBranch`
   because BuildQuality read logic filters to the default branch

## 8. Risks / caveats

- **Internal id vs external build id confusion is the main risk.**
  - Look up the build using `CachedPipelineRuns.TfsRunId`
  - Join child rows using `CachedPipelineRuns.Id`

- **`TfsRunId` is not guaranteed globally unique by itself.**
  - The enforced uniqueness is `(ProductOwnerId, PipelineDefinitionId, TfsRunId)`

- **Multiple test runs per build are allowed.**
  - `TestRuns` stores raw rows; later aggregation happens in BuildQuality logic

- **Multiple coverage rows per build are allowed.**
  - `Coverages` also stores raw rows; later aggregation happens in BuildQuality logic

- **Missing child rows are valid.**
  - A cached build can exist without any `TestRuns`
  - A cached build can exist without any `Coverages`

- **Branch filtering matters.**
  - BuildQuality reads compare `CachedPipelineRuns.SourceBranch` with `PipelineDefinitions.DefaultBranch`
  - A build row may exist in SQLite but still be excluded from the BuildQuality selection window

- **Coverage rows are replace-on-sync for affected builds.**
  - `PipelineSyncStage.ReplaceCoverageAsync` deletes existing coverage rows for the affected build anchors and inserts the current rows

- **Test runs use external-child-id-based upsert semantics.**
  - `PipelineSyncStage.UpsertTestRunsAsync` requires `ExternalId`
  - test-run rows missing a stable external id are skipped

- **Nullable `TestRuns.ExternalId` can be confusing in manual inspection.**
  - The column is nullable in the schema
  - current ingestion code skips incoming rows without it
  - if you are examining historical/local data, do not assume every row must have a non-null `ExternalId`

- **SQLite timestamp columns are stored as `TEXT`.**
  - The schema uses `DateTime` / `DateTimeOffset` mapped to SQLite `TEXT`
  - manual query ordering/filtering should prefer the UTC helper columns on the build anchor (`CreatedDateUtc`, `FinishedDateUtc`) where available

## 9. Final summary

### Most important tables

1. `CachedPipelineRuns`
2. `TestRuns`
3. `Coverages`
4. `PipelineDefinitions`

### Most important id columns

- External build id: `CachedPipelineRuns.TfsRunId`
- Internal cached build id: `CachedPipelineRuns.Id`
- Test-run linkage FK: `TestRuns.BuildId`
- Coverage linkage FK: `Coverages.BuildId`
- External pipeline definition id: `PipelineDefinitions.PipelineDefinitionId`
- Internal pipeline definition id: `PipelineDefinitions.Id`

### First 3 queries to run for build `168570`

1. **Find the build anchor**

```sql
SELECT *
FROM CachedPipelineRuns
WHERE TfsRunId = 168570;
```

2. **Find linked test runs through the internal cached build id**

```sql
WITH build_anchor AS (
    SELECT Id
    FROM CachedPipelineRuns
    WHERE TfsRunId = 168570
)
SELECT tr.*
FROM TestRuns tr
JOIN build_anchor b
    ON tr.BuildId = b.Id;
```

3. **Find linked coverage rows through the internal cached build id**

```sql
WITH build_anchor AS (
    SELECT Id
    FROM CachedPipelineRuns
    WHERE TfsRunId = 168570
)
SELECT c.*
FROM Coverages c
JOIN build_anchor b
    ON c.BuildId = b.Id;
```

Bottom line:

- `CachedPipelineRuns` is the persisted build anchor
- `TfsRunId` is the external lookup key
- `Id` is the internal join key used by both `TestRuns` and `Coverages`
