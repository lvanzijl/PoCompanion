# Snapshot & Budget Model Analysis

## 1. Existing patterns

### Snapshot-like persistence already exists

The repository already has one explicit immutable snapshot pattern:

- `PoTool.Api/Persistence/Entities/RoadmapSnapshotEntity.cs`
- `PoTool.Api/Persistence/Entities/RoadmapSnapshotItemEntity.cs`
- `PoTool.Api/Services/RoadmapSnapshotService.cs`
- `PoTool.Api/Controllers/RoadmapSnapshotsController.cs`

That flow is important because it is:

- **application-side only** (`RoadmapSnapshotService` states that snapshots never modify TFS)
- **point-in-time** (`CreatedAtUtc`)
- **user-triggered** (`POST /api/RoadmapSnapshots`)
- **row-based** (header entity plus child item rows)
- **deletable** (`DeleteSnapshotAsync`)

This is the closest existing shape to a future budget baseline or planning snapshot model.

### Historical records and append-only time tracking exist

The codebase also has a real historical ledger:

- `PoTool.Api/Persistence/Entities/ActivityEventLedgerEntryEntity.cs`
- `PoTool.Api/Services/ActivityEventIngestionService.cs`

`ActivityEventLedgerEntryEntity` stores timestamped field-change records with:

- `WorkItemId`
- `UpdateId`
- `FieldRefName`
- `EventTimestamp`
- `EventTimestampUtc`
- `OldValue`
- `NewValue`
- resolved hierarchy context (`ParentId`, `FeatureId`, `EpicId`)

`ActivityEventIngestionService` appends new rows and advances `ProductOwnerCacheStateEntity.ActivityEventWatermark`. It deduplicates by `(WorkItemId, UpdateId, FieldRefName)` rather than overwriting prior rows. This is the main existing **historical record** mechanism in the repository.

### Time-based projections and caches are already persisted

Several tables persist time-based or sync-based analytical state:

- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`
- `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs`
- `PoTool.Api/Persistence/Entities/CachedMetricsEntity.cs`
- `PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs`
- `PoTool.Api/Persistence/Entities/ProductOwnerCacheStateEntity.cs`

These patterns are not immutable business snapshots. They are mostly:

- recomputed during sync
- updated in place
- tracked with timestamps/watermarks such as `LastComputedAt`, `ProjectionTimestamp`, `ComputedAt`, `CreatedDateUtc`, `FinishedDateUtc`, `WorkItemWatermark`, and `ActivityEventWatermark`

The best examples are:

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Api/Services/Sync/SprintTrendProjectionSyncStage.cs`
- `PoTool.Api/Services/Sync/MetricsComputeStage.cs`

So the repository already distinguishes between:

1. **manual immutable snapshot capture**
2. **append-only historical event storage**
3. **rebuildable analytical projections**

### Relationship snapshots exist for current hierarchy capture

`PoTool.Api/Persistence/Entities/WorkItemRelationshipEdgeEntity.cs` plus `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs` add another relevant pattern:

- relationship edges are captured with `SnapshotAsOfUtc`
- the snapshot is rebuilt by deleting prior edges for the product owner and inserting a fresh set
- metadata is stored in `ProductOwnerCacheStateEntity.RelationshipsSnapshotAsOfUtc`

This is useful for budget modeling because a work-package breakdown usually depends on the hierarchy shape at the time of capture, not only on current `ParentTfsId`.

### Manual data input exists, but mostly for configuration rather than historical business data

There are existing manual or user-maintained persistence flows:

- `PoTool.Api/Persistence/Entities/WorkItemStateClassificationEntity.cs`
- `PoTool.Api/Services/WorkItemStateClassificationService.cs`
- `PoTool.Api/Handlers/Settings/SaveStateClassificationsCommandHandler.cs`
- `PoTool.Api/Persistence/Entities/EffortEstimationSettingsEntity.cs`
- `PoTool.Api/Persistence/Entities/SettingsEntity.cs`
- `PoTool.Api/Persistence/Entities/BugTriageStateEntity.cs`
- `PoTool.Api/Services/Configuration/ExportConfigurationService.cs`
- `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`

Observed manual-input patterns:

- **delete-and-replace configuration** for state classifications
- **settings overwrite** for app defaults and effort settings
- **local user-maintained bug triage state** (`BugTriageStateEntity`)
- **configuration import/export** with optional destructive wipe
- **manual roadmap snapshot creation** as the only current user-driven historical capture flow

What does **not** exist today is a manual data-entry flow for budget amounts, funding allocations, or work-package financial values.

### Timestamped entities and time-based tracking are common

The repository already persists many timestamps relevant to snapshot feasibility:

- `RoadmapSnapshotEntity.CreatedAtUtc`
- `ActivityEventLedgerEntryEntity.EventTimestampUtc`
- `SprintMetricsProjectionEntity.LastComputedAt`
- `PortfolioFlowProjectionEntity.ProjectionTimestamp`
- `CachedMetricsEntity.ComputedAt`
- `ProductOwnerCacheStateEntity.*Watermark`, `LastSuccessfulSync`, `RelationshipsSnapshotAsOfUtc`
- `WorkItemEntity.RetrievedAt`, `TfsChangedDate`, `TfsChangedDateUtc`, `CreatedDate`, `ClosedDate`
- `ProductEntity.CreatedAt`, `LastModified`, `LastSyncedAt`
- `WorkItemStateClassificationEntity.CreatedAt`, `UpdatedAt`

So there is no platform gap around timestamp support. The gap is specifically around **which business facts are captured at snapshot time**.

### Existing reports already mention snapshot patterns

The current repository already documents part of this landscape in:

- `docs/audits/buildquality_discovery_report.md`
- `docs/analysis/field-contract.md`

`buildquality_discovery_report.md` already identifies `RoadmapSnapshotEntity`, `ActivityEventLedgerEntryEntity`, `SprintMetricsProjectionEntity`, `PortfolioFlowProjectionEntity`, and `CachedMetricsEntity` as existing snapshot/history/projection patterns. `field-contract.md` also confirms that `Rhodium.Funding.ProjectNumber` and `Rhodium.Funding.ProjectElement` are currently absent end to end.

## 2. How data is persisted over time

### Current-state caches are overwritten or upserted

The base operational entities are current-state caches, not historical series:

- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`
- `PoTool.Api/Persistence/Entities/ProductEntity.cs`
- `PoTool.Api/Persistence/Entities/SprintEntity.cs`

Those models represent the latest synchronized state. They carry timestamps such as `RetrievedAt` and `TfsChangedDateUtc`, but they do not preserve prior versions of the same row.

### Historical event data is append-only

`ActivityEventLedgerEntryEntity` is the only clear append-only history table for work-item change events. It is fed by `ActivityEventIngestionService` and used to reconstruct time-based analytics such as sprint history.

This means the repository already has one authoritative answer for “what changed over time,” but only for **whitelisted work-item fields** that are ingested into the activity ledger.

### Projection tables are recomputed from current state plus history

`SprintMetricsProjectionEntity` and `PortfolioFlowProjectionEntity` are derived models. `SprintTrendProjectionService` and `PortfolioFlowProjectionService` recalculate these tables during sync and persist fresh values. They are analytical accelerators, not baseline records.

That distinction matters for budget snapshots:

- a projection can answer “what do we currently calculate?”
- a snapshot must answer “what was the planned baseline at the moment we approved it?”

### Deletion and correction flows do exist

The repository already supports correction/rebuild patterns:

- `RoadmapSnapshotService.DeleteSnapshotAsync()` deletes a snapshot
- `WorkItemStateClassificationService.SaveClassificationsAsync()` deletes old rows and replaces them
- `WorkItemRelationshipSnapshotService` deletes prior relationship edges for a product owner and rebuilds them
- `CacheStateRepository.ResetCacheStateAsync()` clears caches, projections, relationship edges, activity events, and watermarks
- `ImportConfigurationService` supports wipe-and-reimport flows

So the codebase already accepts that some persisted analytical tables are disposable and rebuildable, while explicit user snapshots are intentionally kept until deleted.

## 3. Fit for a snapshot model

### Fit for project-level snapshots

There is a good structural fit for a project-level budget snapshot **if “project-level” is interpreted as the repository’s analytical Product scope**.

That recommendation follows the domain rules:

- `docs/domain/domain_model.md`
- `docs/rules/hierarchy-rules.md`

Those documents define **Product** as the primary analytics boundary. In contrast, TFS project scope is currently used mainly for shared configuration, such as `WorkItemStateClassificationEntity.TfsProjectName`.

Because of that, the cleanest project-level budget snapshot integration point is:

- `ProductEntity` as the owning scope
- a new immutable snapshot header entity, similar to `RoadmapSnapshotEntity`

This aligns with how products, backlog roots, and sprint/delivery analytics are already organized.

### Fit for work package breakdown snapshots

There is a partial fit, with one important gap.

What already fits:

- hierarchy is persisted through `WorkItemEntity.ParentTfsId`
- current relationship snapshots are available through `WorkItemRelationshipEdgeEntity`
- rollup source values already exist on `WorkItemEntity` (`Effort`, `StoryPoints`, `BusinessValue`)
- product resolution exists through `ResolvedWorkItemEntity` and related projection services

What does not fit yet:

- there is **no explicit work package entity**
- there are **no budget/cost fields**
- `Rhodium.Funding.ProjectNumber` and `Rhodium.Funding.ProjectElement` are not retrieved, persisted, or exposed today (`docs/analysis/field-contract.md`)

So a work package breakdown snapshot can still be introduced, but today it would have to derive its rows from the existing hierarchy:

- Epic
- Feature
- PBI

If the intended breakdown depends on funding codes or project elements, the existing data model is currently insufficient and that field-contract gap must be closed first.

### Best existing technical pattern to reuse

The best technical pattern is to combine:

1. the **immutable header + rows** model from `RoadmapSnapshotEntity`
2. the **current hierarchy resolution** approach from `WorkItemRelationshipSnapshotService`
3. the **current estimate values** from `WorkItemEntity`

The projection tables are useful as analytical inputs, but they are a worse fit as the persistence model for a budget baseline because they are intentionally recomputed and overwritten.

## 4. Risks

### 1. Budget semantics do not exist yet

The repository has estimation semantics (`Effort`, `StoryPoints`, `BusinessValue`) but not budget semantics. A budget snapshot model would need an explicit decision about whether “budget” means:

- hours (`Effort`)
- size (`StoryPoints`)
- funding/cost values
- or a combination

Without that contract, a snapshot table would only preserve structure, not a reliable business meaning.

### 2. Funding and work-package fields are currently missing

`docs/analysis/field-contract.md` shows that:

- `Rhodium.Funding.ProjectNumber`
- `Rhodium.Funding.ProjectElement`

are absent from retrieval, revision ingestion, persistence, DTOs, and analytics.

If the future snapshot model depends on those fields for project/work-package grouping, the snapshot can be created only after those fields are first added to the normal ingestion contract.

### 3. Current caches are not historical truth

`WorkItemEntity` is a latest-state cache. If a future snapshot were generated later from live work items instead of persisting row values at capture time, historical baselines would drift as estimates, parents, or titles change.

That means a budget snapshot model must persist its own row data and must not rely on live joins for past baselines.

### 4. History coverage is whitelist-based

`ActivityEventIngestionService` only records fields listed in `RevisionFieldWhitelist.Fields`. If future snapshot reconstruction needs historical budget-related fields, those fields must be included in the revision whitelist first. Otherwise the ledger cannot reconstruct them later.

### 5. Product scope and TFS project scope are different concepts

The codebase uses:

- **Product** as the main analytical scope
- **TFS project** for shared settings/configuration

If “project-level snapshot” is interpreted inconsistently, the model could end up attached to the wrong scope. This needs to be kept explicit in implementation and UI wording.

## 5. Recommended integration approach

### Recommended model

Introduce a new immutable snapshot model that mirrors the roadmap snapshot shape instead of extending a projection table.

Recommended shape:

- `BudgetSnapshotEntity`
  - `Id`
  - `ProductId`
  - `CreatedAtUtc`
  - optional user description / baseline label
  - optional source metadata such as work-item watermark or relationship snapshot timestamp
- `BudgetSnapshotItemEntity`
  - `SnapshotId`
  - `WorkItemTfsId`
  - `ParentWorkItemTfsId`
  - `WorkItemType`
  - `Title`
  - captured estimate values (`Effort`, `StoryPoints`, `BusinessValue`)
  - any future budget/funding fields
  - ordering / level metadata for stable breakdown rendering

### Recommended source of truth at capture time

At snapshot creation time, build rows from the **current resolved product hierarchy**, using:

- `ProductEntity` and backlog-root scope
- `WorkItemEntity` for latest current values
- `ResolvedWorkItemEntity` for product membership
- `WorkItemRelationshipEdgeEntity` or `ParentTfsId` for hierarchy shape

Persist the resulting row values directly into the snapshot items so later corrections to live work items do not rewrite historical baselines.

### Recommended interpretation of “project-level”

Map “project-level snapshot” to **Product-level** ownership in this repository, because Product is the canonical analytical boundary. If a higher TFS-project-wide budget view is needed later, that should be a separate aggregate/reporting layer built on top of product snapshots rather than the first persistence model.

### Recommended interpretation of “work package breakdown”

Use the existing operational hierarchy as the first work-package breakdown:

- Epic
- Feature
- PBI

Do not invent a separate work-package entity until there is a confirmed business rule that the existing hierarchy is insufficient.

### Recommended future extension path

If the snapshot eventually needs true financial grouping:

1. add `Rhodium.Funding.ProjectNumber` / `Rhodium.Funding.ProjectElement` to the retrieval and persistence contract
2. decide whether they are snapshot-only metadata or also part of historical ingestion
3. then extend `BudgetSnapshotItemEntity` with those captured values

That sequencing matches the current repository architecture and avoids introducing a budget model that cannot actually be populated from source data.
