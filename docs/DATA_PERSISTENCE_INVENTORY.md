# Data Persistence Inventory

**Last updated:** 2026-03-04  
**Scope:** All 34 EF Core entities in `PoToolDbContext`, classified by persistence location, recoverability, and migration risk.

---

## Overview

PoCompanion uses a **single local SQLite database** (file-based, per installation) as its only persistent store.
TFS / Azure DevOps is the upstream source for work items, iterations, pull requests, and pipeline runs — but it is **read-only from PoCompanion's perspective** except for the validation fix feature (`UpdateWorkItemStateAsync`).

This means that any data PoCompanion generates itself — planning board layouts, triage state, product/team configuration — exists **only** in the local SQLite file and has no TFS equivalent.

---

## Risk Legend

| Symbol | Meaning |
|--------|---------|
| 🔴 CRITICAL | Permanently lost if the database is deleted. Cannot be recovered from TFS or re-derived algorithmically. |
| 🟠 HIGH | Would take significant manual effort to rebuild. No automated path to recovery. |
| 🟡 MEDIUM | Can be reconfigured by a user with moderate effort. |
| 🟢 LOW | Can be recovered by re-running sync or a background computation. |

---

## Category 1 — Planning Board (local-augmented data)

> **These tables exist entirely outside TFS. There is no TFS field, tag, or custom attribute that stores this information.**

| Entity | Table purpose | Risk | Notes |
|--------|--------------|------|-------|
| `BoardRowEntity` | Abstract rows on the Planning Board (ordered slots, marker rows with Iteration/Release type and label) | 🔴 CRITICAL | Row order and marker labels are invented inside PoCompanion. Nothing in TFS represents them. |
| `PlanningEpicPlacementEntity` | Which TFS epic (by `EpicId`) is placed in which row (`RowId`) and product column, with intra-cell order | 🔴 CRITICAL | The entire planning board layout is encoded here. A database loss destroys all placement decisions. |
| `PlanningBoardSettingsEntity` | Per-Product-Owner view settings: scope (All Products / Single Product), hidden columns | 🔴 CRITICAL | Per-user UI state with no TFS equivalent. |

### Legacy Release Planning Board

> These entities predate the current Planning Board and are maintained for backward compatibility.

| Entity | Table purpose | Risk | Notes |
|--------|--------------|------|-------|
| `LaneEntity` | Objective-keyed swimlane columns on the Release Planning Board | 🔴 CRITICAL | Display order is local-only even though `ObjectiveId` references a TFS item. |
| `EpicPlacementEntity` | Epic row/column placements on the Release Planning Board | 🔴 CRITICAL | Positional data only; not stored in TFS. |
| `MilestoneLineEntity` | Release, deadline, and custom milestone lines with labels and vertical positions | 🔴 CRITICAL | No TFS equivalent. User-authored annotations. |
| `IterationLineEntity` | Iteration boundary lines with labels and vertical positions | 🔴 CRITICAL | No TFS equivalent. User-authored annotations. |

**Migration consequence:** Migrating from one SQLite file to another (or to a different database engine) without exporting and re-importing these tables will destroy the entire planning board layout. TFS cannot regenerate it.

---

## Category 2 — Bug Triage Intelligence

> **Triage state is a PoCompanion concept. TFS has no "triaged" flag for bugs.**

| Entity | Table purpose | Risk | Notes |
|--------|--------------|------|-------|
| `BugTriageStateEntity` | Tracks per-bug triage state: `IsTriaged`, `FirstSeenAt`, `FirstObservedSeverity`, `LastTriageActionAt` | 🔴 CRITICAL | Represents accumulated PO decisions. Cannot be derived from TFS. Losing this resets all triage history. |
| `TriageTagEntity` | Catalog of user-defined triage tags (name, display order, enabled flag) | 🟠 HIGH | The catalog itself is user-authored. Tag assignments on bugs are stored separately (not yet persisted as a separate table — currently inferred from triage state changes). A database loss would require the user to recreate the tag catalog from memory. |

---

## Category 3 — Organisational Model

> **Products, teams, and profiles are PoCompanion constructs. TFS has no equivalent of a "Product" or a "Product Owner Profile".**

| Entity | Table purpose | Risk | Notes |
|--------|--------------|------|-------|
| `ProfileEntity` | Product Owner profile: name, picture | 🟠 HIGH | User-authored. Can be recreated manually but represents deliberate configuration. |
| `ProductEntity` | Product: name, picture, linked owner, display order, `LastSyncedAt` watermark | 🟠 HIGH | The whole product model is local. A database loss means re-entering every product definition and losing sync timestamps (forcing full re-sync). |
| `TeamEntity` | Team: name, area path, TFS team ID, picture, `LastSyncedIterationsUtc` | 🟠 HIGH | TFS team identifiers are stored here (`TfsTeamId`, `ProjectName`) which link to TFS, but the grouping into "teams" inside PoCompanion is local. |
| `ProductTeamLinkEntity` | Many-to-many join: which teams work on which products | 🟠 HIGH | Encoding of domain knowledge (who owns what) — not in TFS. Must be re-established manually. |
| `ProductBacklogRootEntity` | Which TFS work item IDs serve as the backlog root for a product | 🟠 HIGH | This is the scope definition for each product. Loss requires reconfiguring all products' backlog boundaries. |
| `RepositoryEntity` | Git repositories associated with a product | 🟠 HIGH | Repo-to-product assignments are local. Must be re-linked manually. |
| `SettingsEntity` | Global app settings: `ActiveProfileId` | 🟡 MEDIUM | Single-row table. Easy to re-set. |

---

## Category 4 — Application Configuration

> **These tables hold settings and mappings entered by administrators. They can be recreated with moderate effort.**

| Entity | Table purpose | Risk | Notes |
|--------|--------------|------|-------|
| `TfsConfigEntity` | TFS connection: URL, project name, area path, API version, credentials mode | 🟡 MEDIUM | Easy to re-enter from known values. |
| `EffortEstimationSettingsEntity` | Default effort values per work item type (Task, Bug, PBI, Feature, Epic…) | 🟡 MEDIUM | Small settings table. Defaults exist and are reasonable. |
| `WorkItemStateClassificationEntity` | Maps TFS state names → New/InProgress/Done/Removed for each work item type and project | 🟡 MEDIUM | Medium effort to re-enter. Values can be inferred from the TFS process template. |

---

## Category 5 — TFS Mirror (Cached Data)

> **These tables are populated by the sync pipeline. All data originates in TFS and can be recovered by re-running a full sync.**

| Entity | Table purpose | Risk | Notes |
|--------|--------------|------|-------|
| `WorkItemEntity` | Flat cached copy of every TFS work item in scope | 🟢 LOW | Full re-sync restores everything. TFS is the source of truth. |
| `SprintEntity` | Iteration/sprint metadata from TFS (path, dates, time frame) | 🟢 LOW | Restored on next sync for each team. |
| `PullRequestEntity` | Pull request headers from Azure DevOps | 🟢 LOW | Restored on next sync. |
| `PullRequestIterationEntity` | PR iteration (review round) metadata | 🟢 LOW | Restored on next sync. |
| `PullRequestCommentEntity` | PR comment threads and text | 🟢 LOW | Restored on next sync. |
| `PullRequestFileChangeEntity` | Files changed per PR | 🟢 LOW | Restored on next sync. |
| `PipelineDefinitionEntity` | Build pipeline definitions from TFS | 🟢 LOW | Restored on next sync. |
| `CachedPipelineRunEntity` | Individual pipeline run results | 🟢 LOW | Restored on next sync. |
| `ActivityEventLedgerEntryEntity` | Work item field-change events derived from TFS revision history | 🟢 LOW | Re-derived from TFS revision history on a full backfill. Backfill is slow but automated. |
| `WorkItemRelationshipEdgeEntity` | Snapshot of work item relations (parent/child hierarchy) | 🟢 LOW | Recomputed from `WorkItemEntity` relations during next sync. |

---

## Category 6 — Computed Projections

> **These tables are derived entirely from the cached TFS data above. They can be regenerated by running the relevant computation stages.**

| Entity | Table purpose | Risk | Notes |
|--------|--------------|------|-------|
| `ResolvedWorkItemEntity` | Pre-computed hierarchy resolution: which product/epic/feature each work item belongs to | 🟢 LOW | Regenerated automatically during sync (`WorkItemResolutionService`). |
| `SprintMetricsProjectionEntity` | Pre-computed sprint-level metrics (planned/worked effort, bug counts, completion) | 🟢 LOW | Regenerated by `SprintTrendProjectionSyncStage`. |
| `CachedMetricsEntity` | Legacy per-ProductOwner metric cache | 🟢 LOW | Regenerated on next metrics request. |
| `CachedValidationResultEntity` | Validation result cache for epic rows | 🟢 LOW | Regenerated by `ValidationComputeStage`. |
| `ProductOwnerCacheStateEntity` | Sync watermarks, status, counts, timestamps | 🟢 LOW | Reset to zero and rebuilt from scratch on next full sync. The only loss is history of _when_ syncs ran. |

---

## Summary: What is NOT in TFS and would be permanently lost

The following data exists **only** in the local SQLite database and has **no TFS equivalent**:

```
🔴 CRITICAL — CANNOT BE RECREATED FROM TFS

  Planning Board
  ├── BoardRowEntity          — row order and marker labels
  ├── PlanningEpicPlacementEntity — which epics are in which rows
  ├── PlanningBoardSettingsEntity — per-PO view preferences
  ├── LaneEntity              — (legacy) lane order
  ├── EpicPlacementEntity     — (legacy) epic-in-lane positions
  ├── MilestoneLineEntity     — release/deadline annotations
  └── IterationLineEntity     — iteration boundary annotations

  Bug Triage
  └── BugTriageStateEntity    — accumulated triage decisions (IsTriaged, timestamps)

🟠 HIGH — REQUIRES SIGNIFICANT MANUAL EFFORT TO RECREATE

  Organisational Model
  ├── ProfileEntity           — PO names and pictures
  ├── ProductEntity           — product names, display order, pictures
  ├── TeamEntity              — team names, area paths, TFS team IDs
  ├── ProductTeamLinkEntity   — product–team assignments
  ├── ProductBacklogRootEntity — product backlog scope boundaries
  └── RepositoryEntity        — repo-to-product assignments

  Bug Triage Configuration
  └── TriageTagEntity         — user-defined triage tag catalog
```

---

## Risk Analysis: Database Migration

When migrating from SQLite to another database engine (or between environments), the following apply:

### What migrates trivially
All 34 tables have standard relational structures. EF Core migrations handle schema creation. A `dotnet ef database update` on a fresh instance creates the schema correctly.

### What requires a data export/import
Any migration that involves a new database file (new environment, disaster recovery, database engine change) must include a data export of **all Category 1–4 tables** (planning board, triage, org model, configuration).

The recommended approach is:
1. Export via EF Core's `ToListAsync()` + JSON serialization (no tooling exists today)
2. Import into the new database before first use

### What can be left behind
Category 5 (TFS mirror) and Category 6 (computed projections) do **not** need to be migrated. They will be repopulated automatically from TFS on first sync. The only cost is time (a full sync + projection rebuild).

### The planning board is the highest-risk item

The Planning Board layout (`BoardRowEntity` + `PlanningEpicPlacementEntity`) represents accumulated product planning decisions made by the PO. It cannot be inferred from TFS work item fields. If this data is lost:
- All rows are gone (the board is empty)
- All epic-to-row assignments are gone
- Marker rows (sprint/release boundaries) and their labels are gone

**Recommendation:** Add an export-to-JSON and import-from-JSON capability for planning board data before any database migration is attempted.

### TFS tag/field write-back as a long-term mitigation

A lower-risk persistence strategy for planning board placements would be to write placement data back into TFS using a custom work item field or tag convention — for example, a field `PoCompanion.BoardRow` on each Epic. This would:
- Make the placement durable in TFS (survives local database loss)
- Allow multiple PoCompanion installations to share the same board state
- Require a TFS process template extension (admin action) or a tag-based encoding convention

This write-back does not exist today. Until it is implemented, the local database is the single copy of planning board state.

---

## Appendix: Entity-to-Table Mapping

| Entity class | Category | DB table (EF default) |
|---|---|---|
| `WorkItemEntity` | TFS Mirror | `WorkItems` |
| `TfsConfigEntity` | Configuration | `TfsConfigs` |
| `SettingsEntity` | Configuration | `Settings` |
| `ProfileEntity` | Org Model | `Profiles` |
| `PullRequestEntity` | TFS Mirror | `PullRequests` |
| `PullRequestIterationEntity` | TFS Mirror | `PullRequestIterations` |
| `PullRequestCommentEntity` | TFS Mirror | `PullRequestComments` |
| `PullRequestFileChangeEntity` | TFS Mirror | `PullRequestFileChanges` |
| `EffortEstimationSettingsEntity` | Configuration | `EffortEstimationSettings` |
| `LaneEntity` | Planning Board (legacy) | `Lanes` |
| `EpicPlacementEntity` | Planning Board (legacy) | `EpicPlacements` |
| `MilestoneLineEntity` | Planning Board (legacy) | `MilestoneLines` |
| `IterationLineEntity` | Planning Board (legacy) | `IterationLines` |
| `CachedValidationResultEntity` | Computed | `CachedValidationResults` |
| `BoardRowEntity` | Planning Board | `BoardRows` |
| `PlanningEpicPlacementEntity` | Planning Board | `PlanningEpicPlacements` |
| `PlanningBoardSettingsEntity` | Planning Board | `PlanningBoardSettings` |
| `ProductEntity` | Org Model | `Products` |
| `TeamEntity` | Org Model | `Teams` |
| `SprintEntity` | TFS Mirror | `Sprints` |
| `ProductTeamLinkEntity` | Org Model | `ProductTeamLinks` |
| `ProductBacklogRootEntity` | Org Model | `ProductBacklogRoots` |
| `RepositoryEntity` | Org Model | `Repositories` |
| `PipelineDefinitionEntity` | TFS Mirror | `PipelineDefinitions` |
| `WorkItemStateClassificationEntity` | Configuration | `WorkItemStateClassifications` |
| `ProductOwnerCacheStateEntity` | Computed | `ProductOwnerCacheStates` |
| `CachedMetricsEntity` | Computed | `CachedMetrics` |
| `CachedPipelineRunEntity` | TFS Mirror | `CachedPipelineRuns` |
| `BugTriageStateEntity` | Bug Triage | `BugTriageStates` |
| `TriageTagEntity` | Bug Triage | `TriageTags` |
| `ResolvedWorkItemEntity` | Computed | `ResolvedWorkItems` |
| `WorkItemRelationshipEdgeEntity` | Computed | `WorkItemRelationshipEdges` |
| `ActivityEventLedgerEntryEntity` | TFS Mirror | `ActivityEventLedgerEntries` |
| `SprintMetricsProjectionEntity` | Computed | `SprintMetricsProjections` |
