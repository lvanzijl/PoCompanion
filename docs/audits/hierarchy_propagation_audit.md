# PoTool Hierarchy and Propagation Domain Audit

_Generated: 2026-03-14_

## Summary

### Files analyzed

- `docs/domain/domain_model.md`
- `docs/rules/hierarchy-rules.md`
- `docs/rules/propagation-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/sprint-rules.md`
- `docs/rules/estimation-rules.md`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/SprintSpilloverLookup.cs`
- `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs`
- `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs`
- `PoTool.Core/WorkItems/WorkItemHierarchyHelper.cs`
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

### Verdict

**Fully compliant**

The audited hierarchy and propagation paths now align with the canonical domain model. PBI story-point authority is enforced through `CanonicalStoryPointResolutionService`, sprint metrics and execution exclude bugs/tasks from story-point delivery, epic and feature rollups derive scope from PBI descendants with canonical parent fallback only when child PBIs lack estimates, and descendant activity now propagates through the full Task → PBI → Feature → Epic chain in the sprint trend service and feature visibility filters.

## Domain Rules Reviewed

- `docs/domain/domain_model.md` §§ 2.2-2.8, 3.3-3.9, 5.5-5.9
- `docs/rules/hierarchy-rules.md`
- `docs/rules/propagation-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/sprint-rules.md`
- `docs/rules/estimation-rules.md`

## Compliant Areas

- **Hierarchy traversal consistently uses the canonical parent chain.**  
  `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs` walks descendants from `ParentTfsId` with a queue so activity details cover the selected work item and its full subtree. `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` resolves epic/feature children from `ParentTfsId`, and `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs` persists hierarchy edges from `ParentTfsId` plus TFS hierarchy relations. No audited production path assumes an alternate Feature/PBI/Task linkage.

- **PBI is the canonical story-point and delivery unit.**  
  `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs` allows authoritative story-point resolution only for PBIs / User Stories. `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` and `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` both use canonical story-point resolution for sprint totals and explicitly exclude missing/derived estimates from velocity-aligned delivery.

- **Tasks are isolated from delivery metrics.**  
  `GetSprintMetricsQueryHandler` tracks completed tasks separately from `CompletedStoryPoints`, and `GetSprintExecutionQueryHandler` only loads PBIs and Bugs into sprint execution scope, then applies canonical PBI-only story-point aggregation. Tasks still contribute to activity/work signals through descendant propagation, which matches the domain model.

- **Bugs are visible but do not distort story-point velocity.**  
  `CanonicalStoryPointResolutionService` rejects Bugs as authoritative story-point sources. `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler`, and `SprintTrendProjectionService` surface bug counts/work diagnostics separately while keeping `CompletedStoryPoints`, `DeliveredSP`, `PlannedStoryPoints`, and `CompletedPbiStoryPoints` PBI-only.

- **Epic and Feature rollups derive from PBI descendants with canonical fallback.**  
  `GetEpicCompletionForecastQueryHandler.RollupCanonicalScope()` and `RollupPbiChildren()` aggregate scope from direct PBI descendants and recurse through Features for Epic scope. `SprintTrendProjectionService.ComputeFeatureScope()` applies the same canonical pattern: use resolved PBI estimates when present, otherwise fall back to a parent estimate through `ResolveParentFallback`.

- **Descendant activity propagates upward and metadata-only activity is ignored.**  
  `SprintTrendProjectionService` filters out `System.ChangedBy` and `System.ChangedDate` before activity classification. The service now propagates activity recursively through ancestor chains so task-level changes activate parent PBIs, Features, and Epics in worked counts and feature visibility filters. `GetWorkItemActivityDetailsQueryHandler` applies the same metadata exclusion in the detail feed.

- **Sprint execution and trend contracts preserve canonical hierarchy semantics.**  
  `PoTool.Shared/Metrics/SprintExecutionDtos.cs` exposes only story-point formulas derived from canonical PBI scope (`CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `SpilloverSP`, and rate formulas), while `GetSprintTrendMetricsQueryHandler` surfaces story-point and effort diagnostics separately from cached/recomputed projections.

- **Focused unit coverage locks in the audited behavior.**  
  `GetSprintMetricsQueryHandlerTests` covers bug/task story-point exclusion and first-Done sprint delivery. `GetSprintExecutionQueryHandlerTests` covers canonical story-point formulas, spillover, and bug/task exclusion. `GetEpicCompletionForecastQueryHandlerTests` covers nested feature/PBI rollups and task exclusion. `SprintTrendProjectionServiceTests` now cover full descendant activity propagation from Task to Feature and feature visibility when only a task descendant is active.

## Violations Found

No remaining violations were found in the audited production paths.

## Architectural Risks

- **Canonical rollup orchestration still exists in more than one place.**  
  `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` and `PoTool.Api/Services/SprintTrendProjectionService.cs` both contain compliant PBI-descendant rollup orchestration around the shared canonical resolver. They are semantically aligned today, but future hierarchy-rule changes would require coordinated updates unless the orchestration is centralized.

- **Portfolio effort trend helpers should remain isolated from canonical hierarchy metrics.**  
  `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` is explicitly effort-based and documents `AddedEffort` as a proxy. That is acceptable for stock/flow reporting, but it should not be reused as hierarchy rollup or canonical sprint-scope truth.

- **Recursive activity propagation is currently local to `SprintTrendProjectionService`.**  
  The audited fix is intentionally surgical and correct for current consumers, but if more handlers start implementing ancestor activity visibility, the propagation helper should move into a shared Core helper to avoid duplicated ancestry-walk logic.

## Recommended Fixes

1. **Keep canonical rollup logic anchored on shared services.**  
   If new hierarchy metrics are added, prefer extracting the remaining PBI-descendant rollup orchestration into a shared Core helper instead of duplicating Feature/Epic rollup loops in handlers or services.

2. **Reuse one ancestor-propagation helper for future activity visibility.**  
   The recursive propagation implemented in `SprintTrendProjectionService` should be the template for any future hierarchy-aware activity logic; if another consumer appears, extract it rather than copying it.

3. **Preserve focused regression tests around descendant propagation.**  
   Keep test coverage for Task → PBI → Feature propagation and feature visibility under descendant-only activity so future refactors do not regress parent activation rules.

## Final Compliance Classification

**Fully compliant**

### Prioritized fix list

1. Centralize remaining compliant rollup orchestration if additional hierarchy consumers are added.
2. Reuse the recursive ancestor-propagation helper rather than duplicating lineage walks in new handlers.
3. Maintain the focused propagation and exclusion tests that lock in Task/Bug/PBI hierarchy semantics.

## Fix Progress — Descendant Activity Propagation

- Updated `PoTool.Api/Services/SprintTrendProjectionService.cs` so functional activity now propagates recursively to all in-scope ancestors instead of stopping at direct parents.
- Updated `SprintTrendProjectionService.ComputeFeatureProgress()` so activity-gated feature visibility treats task-descendant changes as parent activity, matching the boolean propagation rule in `docs/rules/propagation-rules.md`.
- Expanded `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` to assert full Task → PBI → Feature bubbling in `WorkedCount` and to verify that a feature remains visible when only a task descendant changed during the sprint.
