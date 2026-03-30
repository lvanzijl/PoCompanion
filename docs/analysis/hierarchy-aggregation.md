# Hierarchy & Aggregation Analysis

## 1. Hierarchy model

### Canonical operational model

The repository’s canonical analytics model is the four-level operational hierarchy documented in:

- `docs/domain/domain_model.md`
- `docs/rules/hierarchy-rules.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/propagation-rules.md`
- `docs/domain/cdc_reference.md`

That model is:

- `Epic`
- `Feature`
- `PBI` / `Product Backlog Item` / `User Story`
- `Task`

The code reflects that model in `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`, which:

- treats only `"Feature"` as a canonical feature type
- treats only `"Product Backlog Item"`, `"PBI"`, and `"User Story"` as authoritative story-point sources
- excludes Bugs and Tasks from authoritative story-point ownership

### Persistence and transport shape

Current hierarchy edges are persisted and transported through a nullable parent reference:

- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs` stores `ParentTfsId`
- `PoTool.Shared/WorkItems/WorkItemDto.cs` exposes `ParentTfsId`
- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs` carries `ParentWorkItemId` into CDC/domain services

This means the current hierarchy model is primarily an **adjacency list** keyed by work item ID rather than a dedicated tree object.

### Role of each level

| Level | Current role in code | Aggregation role |
| --- | --- | --- |
| Epic | container for Feature rollups and forecast scope | receives derived story-point rollups |
| Feature | immediate parent of authoritative PBI scope | receives derived story-point rollups |
| PBI | canonical delivery and story-point unit | authoritative story-point origin |
| Task | implementation/activity unit | excluded from story-point scope; can still drive activity/work propagation |

Bugs are handled as a separate special case throughout delivery code: they can participate in activity/work reporting, but not in canonical story-point aggregation.

## 2. Parent/child relationships and traversal

### Where parent/child relationships are handled

The repository uses `ParentTfsId` consistently as the main parent-child link:

- `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs` writes relationship edges from `ParentTfsId` and TFS relations
- `PoTool.Core/WorkItems/WorkItemHierarchyHelper.cs` recursively walks descendants using `ParentTfsId`
- `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs` builds a `childrenByParent` lookup and performs breadth-first descendant traversal
- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs` traverses descendants through `BacklogGraph.GetChildren(...)`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs` uses `DeliveryProgressRollupMath.PropagateActivityToAncestors(...)` to bubble activity upward through parents

### Traversal is not fully centralized

There is **no single hierarchy traversal service** used across all slices.

Instead, traversal logic is split by purpose:

- `WorkItemHierarchyHelper` for generic descendant filtering
- queue-based traversal in `GetWorkItemActivityDetailsQueryHandler`
- recursive descendant evaluation in backlog-quality rules
- ancestor propagation in `DeliveryProgressRollupMath`

So the repository has a **shared parent pointer model**, but not a single CDC-owned traversal abstraction for all hierarchy queries.

### Practical hierarchy assumptions

The production paths largely assume:

1. `Epic -> Feature -> PBI` is the analytics backbone
2. `Task` exists below PBI and can generate activity/work
3. direct children matter at each aggregation step
4. skip-level relations are tolerated only partially, not generically

Examples:

- `HierarchyRollupService` special-cases Features, then treats the parent level as “recurse into direct Feature children plus direct PBI children”
- `DeliveryProgressRollupService` computes Feature progress from PBIs resolved to a Feature ID
- `GetEpicCompletionForecastQueryHandler` asks for rollups only on Epic/Feature roots

That is aligned with the operational hierarchy, but it is not a generic arbitrary-depth hierarchy engine.

## 3. Story point aggregation

### Canonical resolution is centralized

`PoTool.Core.Domain/Domain/Estimation/CanonicalStoryPointResolutionService.cs` is the canonical story-point resolver.

Current behavior:

1. `StoryPoints`
2. fallback to `BusinessValue`
3. otherwise missing

Additional implemented rules:

- zero SP on a non-Done item is treated as missing
- zero SP on a Done item is accepted as a real zero-point completion
- sibling-based derived estimates are averaged from other authoritative PBIs under the same parent
- derived estimates remain fractional
- only PBIs / User Stories are authoritative; Bugs and Tasks are ignored

This is the clearest centralized part of the aggregation model.

### Feature and Epic rollups are centralized

`PoTool.Core.Domain/Domain/Hierarchy/HierarchyRollupService.cs` centralizes canonical story-point rollups for Features and Epics:

- Feature:
  - sums direct authoritative PBI children
  - if no child PBI estimate resolves, falls back to the parent estimate via `ResolveParentFallback(...)`
- Epic:
  - recursively rolls up direct Feature children
  - also sums direct PBI children when present
  - falls back to parent estimate only when no child scope exists

This matches the current Feature/Epic story-point aggregation contract used by:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`

### Sprint-level story-point totals use the same resolver, but a different orchestrator

Sprint metrics do not call `HierarchyRollupService` directly. Instead:

- `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs` resolves story points per work item through `CanonicalStoryPointResolutionService`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` uses sprint facts from the CDC sprint services
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` also uses sprint facts for `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, and `SpilloverSP`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs` resolves PBI story points through the same canonical resolver for planned/delivered/spillover trend metrics

### Centralized vs duplicated story-point behavior

Story-point **field semantics** are centralized, but story-point **orchestration** is only partly centralized.

Centralized:

- `CanonicalStoryPointResolutionService`
- `HierarchyRollupService`

Repeated orchestration helpers still exist in:

- `SprintCdcServices.BuildFeaturePbiCandidates(...)`
- `DeliveryProgressRollupMath.BuildFeatureRollupContext(...)`
- `DeliveryProgressRollupMath.ResolvePbiStoryPointEstimate(...)`
- `SprintDeliveryProjectionService.ResolveProjectionStoryPointMetrics(...)`
- `PortfolioFlowProjectionService` direct resolver usage

So the repository already has a strong canonical core for story points, but callers still rebuild local sibling context and rollup inputs in multiple places.

## 4. Effort usage and aggregation

### Where effort lives

Effort is stored end to end as a separate field:

- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs` -> `Effort`
- `PoTool.Shared/WorkItems/WorkItemDto.cs` -> `Effort`
- delivery trend inputs (`DeliveryTrendWorkItem`) also carry `Effort`

The codebase does **not** collapse story points and effort into the same field anymore.

### How effort is currently used

Effort is currently used mainly as:

- a reporting sum in sprint/delivery projections
- a backlog-readiness requirement for PBIs
- a diagnostic/capacity input

Concrete production examples:

- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs` defines `PbiMissingEffortRule` (`RC-2`) only for PBIs
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs` sums:
  - `PlannedEffort`
  - `WorkedEffort`
  - `CompletedPbiEffort`
  - `SpilloverEffort`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` builds sprint summary effort totals from raw `Effort ?? 0` sums
- `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs` sums planned effort for capacity comparison
- `PoTool.Core.Domain.EffortDiagnostics` owns separate effort imbalance / concentration diagnostics

### Canonical effort rollup is not centralized the same way as story points

The desired CDC/domain rules say effort should follow child-precedence rollup semantics:

1. if children contain effort -> sum children
2. otherwise use parent effort
3. once children contain effort -> ignore parent effort

But no equivalent of `HierarchyRollupService` exists for effort.

Observed current behavior:

- Feature and Epic progress calculations use story-point rollups, not effort rollups
- sprint trend services sum raw effort over selected items
- `SprintEffortDelta` is a sum of child PBI effort deltas, not a canonical hierarchy rollup
- backlog quality intentionally scopes missing-effort enforcement to PBIs only

So effort is **present and widely used**, but its hierarchy semantics are fragmented and mostly sum-based rather than CDC-rollup-based.

## 5. Centralization vs duplication

### What is centralized

The repository already centralizes the most important story-point semantics:

- `CanonicalStoryPointResolutionService` owns estimate resolution and derived-estimate rules
- `HierarchyRollupService` owns Feature/Epic story-point scope rollups
- `SprintCdcServices` owns sprint-fact formulas for commitment/add/remove/deliver/spillover story-point totals

### What is duplicated or fragmented

1. **Hierarchy traversal**
   - descendant and ancestor traversal are implemented in several local helpers
   - there is no single traversal abstraction shared across metrics, activity details, backlog quality, and relationship snapshots

2. **Story-point context construction**
   - several services rebuild sibling candidate sets or feature-local rollup context instead of reusing one input builder

3. **Effort aggregation**
   - effort totals are computed in multiple handlers/services as direct sums
   - there is no CDC-owned canonical effort hierarchy rollup

### Overall assessment

The implementation is **partly centralized**:

- story-point semantics: mostly centralized
- hierarchy traversal: not centralized
- effort aggregation: not centralized

## 6. Gaps vs desired CDC model

Comparing current code to:

- `docs/domain/cdc_reference.md`
- `docs/rules/hierarchy-rules.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/propagation-rules.md`

the main gaps are:

1. **No canonical effort hierarchy rollup service**  
   The CDC rules define effort child-precedence rollups, but production code currently uses raw effort sums and diagnostics rather than one reusable hierarchy rollup contract.

2. **Hierarchy traversal is not CDC-owned in one place**  
   The CDC reference emphasizes one canonical ownership model, but traversal logic is still split across helpers, handlers, and rule classes.

3. **Removed-item behavior is not enforced inside `HierarchyRollupService`**  
   The estimation rules say removed PBIs must not contribute to active scope rollups. `HierarchyRollupService` currently includes direct PBI children based on type and does not itself exclude removed items; callers only provide Done-state information, not Removed-state filtering.

4. **Hierarchy assumptions are more operational than generic**  
   The code is resilient enough for the main Epic/Feature/PBI flows, but the rollup services are not modeled as a general arbitrary hierarchy engine that fully understands Objective/Epic/Feature/PBI/Task as one reusable CDC graph.

5. **Effort propagation is weaker than story-point propagation**  
   Story-point semantics have a clear canonical core. Effort semantics are split between backlog quality, sprint summaries, delivery trends, capacity planning, and effort diagnostics.

## 7. Risks

1. **Traversal drift risk**  
   Because parent/child traversal is implemented in several places, future hierarchy changes may update one path but miss another.

2. **Operational-level assumption risk**  
   `CanonicalWorkItemTypes` and `HierarchyRollupService` are strongly tuned to Feature/PBI/Epic semantics. If Objectives or more anomalous skip-level trees become first-class analytics inputs, the current logic may not generalize cleanly.

3. **Removed-scope inflation risk**  
   Because `HierarchyRollupService` does not explicitly exclude removed PBIs, active parent scope can be overstated unless callers pre-filter those items.

4. **Effort semantic drift risk**  
   Effort is used in many places, but without one canonical hierarchy rollup owner. That makes it easier for capacity, delivery trends, summaries, and diagnostics to diverge over time.

5. **Partial centralization risk**  
   The code already has the right building blocks for story points, but repeated local context-building helpers mean a future change to estimate resolution or hierarchy assumptions may require coordinated updates across multiple slices.
