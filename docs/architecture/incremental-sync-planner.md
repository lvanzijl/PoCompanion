# Incremental Sync Planner Design

Status: Design only  
Purpose: Define a pure, unit-testable planner contract for work-item incremental sync decisions.  

Reference documents:

- `docs/analysis/2026-03-18-sync-testability-architecture.md`
- `docs/rules/copilot-architecture-contract.md`
- `docs/rules/architecture-rules.md`
- `docs/architecture/domain-model.md`
- `docs/rules/hierarchy-rules.md`
- `docs/rules/source-rules.md`

This document is intentionally limited to the **decision contract** for incremental work-item sync.  
It does not implement code, change persistence, or redesign the full sync pipeline.

## Purpose

The current work-item sync path has no explicit decision layer for incremental behavior.

Today, the most important rules are implicit across:

- `PoTool.Api/Services/Sync/SyncPipelineRunner.cs`
- `PoTool.Api/Services/Sync/WorkItemSyncStage.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`
- `PoTool.Api/Services/WorkItemResolutionService.cs`

That makes it difficult to answer, in isolation:

- which work item ids are now in effective product scope
- which ids entered scope
- which ids left scope
- which ids require field refresh
- when hierarchy/resolution/projection rebuilds are required

The goal of this design is to define one strict contract that makes those decisions explicit and unit-testable without TFS, HTTP, EF Core, or database state.

## Problem Statement

The current `ITfsClient.GetWorkItemsByRootIdsAsync(...)` boundary is too coarse for incremental-sync testing.

Its real implementation currently combines:

1. root-based graph discovery
2. hierarchy traversal
3. ancestor completion
4. changed-item selection
5. field hydration
6. TFS transport

`WorkItemSyncStage` then immediately combines the returned DTO set with EF upsert behavior.

As a result, a test can fake “what TFS already returned,” but cannot verify “why those ids were selected” or “what downstream rebuilds should happen because scope changed.”

## Design Goals

The planner contract MUST:

1. be pure logic
2. be unit-testable with MSTest using in-memory values only
3. avoid TFS, HTTP, EF Core, and entity types
4. make scope transitions explicit
5. make hierarchy invalidation explicit
6. support consistency between the base work-item snapshot and derived `ResolvedWorkItems`
7. remain small enough to adopt without rewriting the full sync pipeline

## Non-Goals

This design does **not** define:

- the final transport shape of TFS discovery calls
- a new database schema
- a new cache-ownership model
- full pipeline orchestration changes outside the planner boundary
- UI or user-visible behavior

## Canonical Terms

### Product analytical scope

For sync-planning purposes, **product scope** means:

- configured backlog roots
- all descendants reachable from those roots through hierarchy links

This follows the product-scoped hierarchy rules in `docs/rules/hierarchy-rules.md`.

### Closure scope

Some items may be required only to preserve a connected hierarchy for traversal or context.

Closure scope means:

- all analytically in-scope items
- plus any required ancestors needed to connect those items

Closure scope belongs to the base `WorkItems` snapshot contract.  
It does **not** imply that every closure item must produce a `ResolvedWorkItem`.

### Entered scope

A work item **entered scope** when it was not in the previous analytical scope and is in the current analytical scope.

### Left scope

A work item **left scope** when it was in the previous analytical scope and is not in the current analytical scope.

### Hierarchy change

A work item has a hierarchy change when any of the following changed between the previous and current graph facts:

- parent id
- root reachability
- analytical-scope membership
- closure membership

### Refresh

A work item requires refresh when its persisted snapshot fields can no longer be trusted for the current sync result.

Examples:

- the item is newly discovered
- the item was reported as changed since the previous watermark
- the item changed parent
- the item entered analytical scope

## Required Boundary Split

The planner is only meaningful if **graph discovery** and **field hydration** are treated as separate concerns.

The required future shape is:

1. an infrastructure discovery step provides current graph facts
2. the planner compares those facts with the prior local baseline
3. an infrastructure hydration step fetches fields only for ids selected by the plan

This document does **not** require a specific interface split in code yet, but the planner contract assumes that split conceptually.

## Planner Ownership Boundary

### The planner owns

The planner MUST own only these decisions:

- what the current analytical scope is
- what the current closure scope is
- which ids entered scope
- which ids left scope
- which ids require field refresh
- whether hierarchy, resolution, and projection invalidation is required

### The planner does not own

The planner MUST NOT:

- call TFS
- query EF Core
- mutate `WorkItems`
- mutate `ResolvedWorkItems`
- compute watermarks from hydrated DTOs
- resolve product ancestry itself from database entities

Those behaviors remain outside the contract.

## Contract Shape

The minimal contract is one planner interface plus pure request/result value objects.

```text
IIncrementalSyncPlanner
    Plan(IncrementalSyncPlannerRequest request) -> IncrementalSyncPlan
```

### IncrementalSyncPlannerRequest

The request MUST contain only pure values.

#### 1. Configuration facts

- `RootIds`
  - configured backlog-root ids for the current product-owner sync

#### 2. Previous local baseline

- `PreviousAnalyticalScopeIds`
  - ids that were previously considered analytically in scope
- `PreviousClosureScopeIds`
  - ids that were previously retained in the base snapshot for closure
- `PreviousParentById`
  - prior known parent mapping for ids in the previous closure scope

#### 3. Current discovered graph facts

- `CurrentAnalyticalScopeIds`
  - ids now reachable from the configured roots
- `CurrentClosureScopeIds`
  - ids now required in the base snapshot after ancestor completion
- `CurrentParentById`
  - current parent mapping for ids in the current closure scope

#### 4. Current change facts

- `ChangedIdsSinceWatermark`
  - ids reported by the discovery layer as changed since the previous watermark
- `ForceFullHydration`
  - explicit flag for first sync, reset, or fallback mode

The request MUST NOT contain EF entities, DTOs tied to TFS transport, or service references.

## Planner Result

The result MUST be deterministic for a given request.

### Scope outputs

- `AnalyticalScopeIds`
  - authoritative current product-scope membership
- `ClosureScopeIds`
  - authoritative current base-snapshot membership
- `EnteredAnalyticalScopeIds`
  - current analytical scope minus previous analytical scope
- `LeftAnalyticalScopeIds`
  - previous analytical scope minus current analytical scope
- `EnteredClosureScopeIds`
  - current closure scope minus previous closure scope
- `LeftClosureScopeIds`
  - previous closure scope minus current closure scope

### Change outputs

- `HierarchyChangedIds`
  - ids whose parent mapping or reachability changed
- `IdsToHydrate`
  - ids whose fields must be fetched from TFS in this sync

### Invalidation outputs

- `RequiresRelationshipSnapshotRebuild`
- `RequiresResolutionRebuild`
- `RequiresProjectionRefresh`

### Diagnostic outputs

- `PlanningMode`
  - `Full` or `Incremental`
- `ReasonCodes`
  - stable machine-readable reasons explaining why ids or invalidations were selected

## Required Result Rules

The planner MUST follow these rules.

### Rule 1 — Full mode

If `ForceFullHydration` is true, then:

- `PlanningMode = Full`
- `IdsToHydrate = CurrentClosureScopeIds`
- all entered/left sets are still computed normally
- all invalidation flags are true if either scope set or parent mapping differs from the previous baseline

### Rule 2 — Entered items are always hydrated

Every id in `EnteredClosureScopeIds` MUST also appear in `IdsToHydrate`.

Reason: new closure members do not have a trustworthy current snapshot locally.

### Rule 3 — Changed items are hydrated only if still relevant

Every id in `ChangedIdsSinceWatermark` that is also in `CurrentClosureScopeIds` MUST appear in `IdsToHydrate`.

Changed ids outside current closure scope do not require hydration, but they may still affect invalidation if they caused a scope transition.

### Rule 4 — Parent changes trigger hydration

If an id exists in both previous and current closure scope but `PreviousParentById[id] != CurrentParentById[id]`, then:

- the id MUST be in `HierarchyChangedIds`
- the id MUST be in `IdsToHydrate`

### Rule 5 — Leaving scope is explicit

Any id in `LeftAnalyticalScopeIds` MUST be explicit in the plan even if no hydration occurs for that id.

Reason: downstream cleanup and resolution invalidation depend on logical scope removal, not on a TFS field fetch.

### Rule 6 — Relationship rebuild invalidation

`RequiresRelationshipSnapshotRebuild` MUST be true when any of the following is non-empty:

- `EnteredClosureScopeIds`
- `LeftClosureScopeIds`
- `HierarchyChangedIds`

Otherwise it MUST be false.

### Rule 7 — Resolution rebuild invalidation

`RequiresResolutionRebuild` MUST be true when any of the following is non-empty:

- `EnteredAnalyticalScopeIds`
- `LeftAnalyticalScopeIds`
- `HierarchyChangedIds`

Otherwise it MUST be false.

### Rule 8 — Projection invalidation

`RequiresProjectionRefresh` MUST be true when any of the following is true:

- `IdsToHydrate` is non-empty
- `LeftAnalyticalScopeIds` is non-empty
- `RequiresResolutionRebuild` is true

Otherwise it MAY be false.

### Rule 9 — ResolvedWorkItems consistency contract

The planner MUST treat `ResolvedWorkItems` as a derived projection of **analytical scope**, not of full closure scope.

Therefore the strict invariant is:

- every resolved row MUST correspond to a work item id inside `AnalyticalScopeIds`
- every resolved row MUST correspond to a base snapshot row retained in `ClosureScopeIds`

The reverse is not required:

- a closure-only ancestor MAY exist in `WorkItems` without a matching `ResolvedWorkItem`

This preserves connected-hierarchy support without claiming a false one-to-one mapping between `WorkItems` and `ResolvedWorkItems`.

### Rule 10 — Determinism

The planner MUST produce the same plan for the same request regardless of:

- database ordering
- HTTP ordering
- hash-set iteration order

Returned collections should therefore be treated as deterministic sets, and implementations should normalize ordering for test assertions.

## Reason Codes

The planner SHOULD emit stable reason codes so tests and logging can explain why a plan was produced.

Minimum required reasons:

- `FullHydrationRequested`
- `EnteredClosureScope`
- `LeftAnalyticalScope`
- `ChangedSinceWatermark`
- `ParentChanged`
- `HierarchyMembershipChanged`

These reasons are design-level diagnostics only.  
They do not change planner semantics.

## Integration Contract with Current Pipeline

The intended future flow is:

1. `SyncPipelineRunner` still determines which roots belong to the product owner
2. an infrastructure discovery step produces current graph facts
3. the sync stage loads prior local baseline facts
4. the planner returns an `IncrementalSyncPlan`
5. the sync stage hydrates only `IdsToHydrate`
6. persistence and derived rebuild stages apply the plan

Within the current architecture, that means the planner becomes the missing decision seam between:

- `RealTfsClient.WorkItemsHierarchy`
- `WorkItemSyncStage`
- `WorkItemRelationshipSnapshotStage`
- `WorkItemResolutionService`

## Required Test Scenarios

The contract MUST support isolated unit tests for at least these cases.

### 1. First sync

Given:

- empty previous baseline
- non-empty current scope
- `ForceFullHydration = true`

Then:

- all current closure ids are hydrated
- all current analytical ids are reported as entered
- relationship, resolution, and projection invalidation are true

### 2. New child under existing root

Given:

- unchanged existing scope
- one newly discovered child in current analytical and closure scope

Then:

- that id appears in entered sets
- that id appears in `IdsToHydrate`
- relationship and resolution rebuilds are required

### 3. Existing item moved into scope by re-parenting

Given:

- item existed previously outside analytical scope
- current parent mapping makes it reachable from a configured root

Then:

- the item appears in entered analytical scope
- the item appears in `HierarchyChangedIds`
- the item appears in `IdsToHydrate`
- relationship and resolution rebuilds are required

### 4. Existing item moved out of scope

Given:

- item existed previously in analytical scope
- current reachability no longer includes it

Then:

- the item appears in `LeftAnalyticalScopeIds`
- resolution rebuild is required
- projection refresh is required even if no hydration occurs for that id

### 5. Parent changed inside the same scope

Given:

- analytical membership unchanged
- parent id changed within current closure scope

Then:

- the item appears in `HierarchyChangedIds`
- the item appears in `IdsToHydrate`
- relationship and resolution rebuilds are required

### 6. Field-only change

Given:

- scope unchanged
- parent mapping unchanged
- one id appears in `ChangedIdsSinceWatermark`

Then:

- only that id is hydrated
- relationship and resolution rebuilds are not required
- projection refresh is required

### 7. Closure-only ancestor added

Given:

- analytical scope unchanged
- one ancestor is newly required for closure

Then:

- the ancestor appears in `EnteredClosureScopeIds`
- the ancestor appears in `IdsToHydrate`
- relationship rebuild is required
- resolution rebuild is not required unless analytical scope or ancestry within scope also changed

## Adoption Constraints

The first implementation that follows this design SHOULD remain minimal:

1. introduce the planner as a pure backend contract
2. keep the existing pipeline stages
3. move only decision logic behind the planner
4. leave transport and persistence in their current boundaries initially

The planner is successful when the important incremental cases can be tested without:

- fake HTTP payload construction
- EF-based orchestration setup
- end-to-end sync-stage execution

## Final Design Statement

The minimum safe architectural change is to introduce a pure `IIncrementalSyncPlanner` contract that compares:

- configured roots
- previous local scope facts
- current discovered graph facts
- changed ids since the previous watermark

and returns one deterministic plan containing:

- analytical scope
- closure scope
- entered/left scope deltas
- ids to hydrate
- hierarchy-change detection
- explicit rebuild invalidation flags

That contract creates the missing unit-testable decision seam for incremental sync while preserving current architecture boundaries for TFS access, EF persistence, and staged orchestration.
