# Pipeline Definitions Analysis — Why `pipelineDefs=0`

> **Status:** Research-only — no code changes.

---

## 1. Where `pipelineDefs` Is Computed

In `SyncPipelineRunner.BuildSyncContextAsync()` (lines 470–512), the sync context
is assembled **from the database**:

```csharp
var pipelineDefinitionIds = await context.PipelineDefinitions
    .Where(pd => productIds.Contains(pd.ProductId))
    .Select(pd => pd.PipelineDefinitionId)
    .ToListAsync(cancellationToken);
```

The resulting array is stored in `SyncContext.PipelineDefinitionIds` and logged at
line 522 as `pipelineDefs={PipelineCount}`. When the array is empty, a warning is
logged at line 541 and `PipelineSyncStage` (Stage 7) skips execution entirely
(line 39).

---

## 2. Current Source of Pipeline Definitions

| Layer | Component | Reads? | Writes to DB? |
|-------|-----------|--------|---------------|
| DB entity | `PipelineDefinitionEntity` / table `PipelineDefinitions` | — | — |
| Repository | `PipelineRepository.SaveDefinitionsAsync()` | — | **Yes** (upsert + stale removal) |
| TFS integration | `RealTfsClient.GetPipelineDefinitionsForRepositoryAsync()` | **Yes** (TFS `_apis/build/definitions`) | No |
| Live provider | `LivePipelineReadProvider.GetDefinitionsByProductIdAsync()` | Fetches from TFS per repo | No |
| Cached provider | `CachedPipelineReadProvider` | Reads from DB | No |
| Sync pipeline | `PipelineSyncStage` | Reads definition IDs from context | Writes **runs**, not definitions |
| API | `GET /api/pipelines/definitions` | Reads definitions via provider | No |

**Key finding:** `SaveDefinitionsAsync()` is declared on `IPipelineRepository` and
fully implemented in `PipelineRepository`, but **it has zero callers anywhere in the
codebase**. There is no sync stage, API endpoint, or background job that calls it.

---

## 3. Relationship Between Repos and Pipeline Definitions

```
ProductOwner
  └─ Product (1:N)
      └─ Repository (1:N)            [RepositoryEntity]
          └─ PipelineDefinition (1:N) [PipelineDefinitionEntity]
```

- Each `PipelineDefinitionEntity` has both `ProductId` (FK) and `RepositoryId` (FK).
- `LivePipelineReadProvider` knows how to discover definitions: for each repository
  in a product, it calls `ITfsClient.GetPipelineDefinitionsForRepositoryAsync(repo.Name)`
  and enriches the DTOs with `ProductId` and `RepositoryId`.
- However, this live-discovery result is **never persisted** back to the
  `PipelineDefinitions` table.

---

## 4. TFS Endpoints for Pipeline/Build Definitions

`RealTfsClient.GetPipelineDefinitionsForRepositoryAsync()` calls:

```
GET {Project}/_apis/build/definitions?includeAllProperties=true&api-version=7.0
```

The response is paginated (`continuationToken`) and filtered client-side by
`repository.id` (GUID) or `repository.name`. Each matching definition is mapped to
a `PipelineDefinitionDto` containing `PipelineDefinitionId`, `Name`, `YamlPath`,
`Folder`, `Url`, `RepoId`, and `RepoName`.

---

## 5. Why `repos=1, pipelineDefs=0` for ProductOwner 1

The root cause is straightforward:

1. **Repositories are configured** — `RepositoryEntity` rows exist for the
   product(s) under ProductOwner 1, so `repos=1`.
2. **Pipeline definitions are never populated** — No code path calls
   `SaveDefinitionsAsync()`. The `PipelineDefinitions` table is empty.
3. **`BuildSyncContextAsync` reads from an empty table** → `pipelineDefinitionIds`
   is an empty list → `pipelineDefs=0`.
4. **`PipelineSyncStage` skips** when `PipelineDefinitionIds.Length == 0`.

The application has all the building blocks:
- TFS client can discover definitions per repository.
- `LivePipelineReadProvider` can orchestrate discovery across a product's repos.
- `PipelineRepository.SaveDefinitionsAsync()` can persist them.

But these components are **not wired together** — the "discovery → persist" step is
missing from the sync pipeline.

---

## 6. What Minimal Future Change Would Enable Pipeline Sync

Two options, from least to most effort:

### Option A — New Sync Stage (recommended)

Add a `PipelineDefinitionDiscoveryStage` that runs **before** `PipelineSyncStage`
(current Stage 7). It would:

1. Load repositories for the ProductOwner's products (already available in
   `SyncContext.RepositoryNames`).
2. For each repository, call
   `ITfsClient.GetPipelineDefinitionsForRepositoryAsync(repoName)`.
3. Enrich DTOs with `ProductId` and `RepositoryId`.
4. Call `IPipelineRepository.SaveDefinitionsAsync(definitions, productIds)`.
5. **Re-populate** `SyncContext.PipelineDefinitionIds` with the newly discovered
   IDs so the downstream `PipelineSyncStage` has definitions to work with.

This aligns with the existing architecture (staged pipeline, upsert repository
pattern) and uses only already-implemented methods.

### Option B — Manual / Admin Configuration Endpoint

Add a `POST /api/pipelines/definitions/discover/{productId}` endpoint that triggers
discovery + persistence on demand, independent of sync. This keeps the sync pipeline
simple but requires an explicit admin action.

---

## Files Referenced

| File | Lines | Purpose |
|------|-------|---------|
| `PoTool.Api/Services/Sync/SyncPipelineRunner.cs` | 470–547 | Context build + scope logging |
| `PoTool.Api/Services/Sync/PipelineSyncStage.cs` | 39–46 | Skip when 0 definitions |
| `PoTool.Core/Contracts/ISyncStage.cs` | 34–70 | `SyncContext` class |
| `PoTool.Core/Contracts/IPipelineRepository.cs` | 76–79 | `SaveDefinitionsAsync` interface |
| `PoTool.Api/Repositories/PipelineRepository.cs` | 138–232 | `SaveDefinitionsAsync` impl (unused) |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | 106–144 | TFS discovery (no persist) |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs` | 679–835 | TFS `build/definitions` call |
| `PoTool.Api/Persistence/Entities/PipelineDefinitionEntity.cs` | 9–91 | DB entity |
