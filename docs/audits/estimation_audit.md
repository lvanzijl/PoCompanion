# PoTool Estimation Audit

## Summary

### Files analyzed

- `docs/domain/domain_model.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/hierarchy_rules.md`
- `docs/domain/rules/propagation_rules.md`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`
- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`
- `PoTool.Shared/WorkItems/WorkItemDto.cs`
- `PoTool.Api/Repositories/WorkItemRepository.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`

### Verdict

**Needs fixes**

The repository has some correct estimation foundations. TFS retrieval already requests `StoryPoints`, `BusinessValue`, and `Effort`, and sprint metrics already use event-history helpers for commitment reconstruction and first-done attribution. However, the current implementation is not estimation-domain compliant because story points are collapsed into `Effort` during ingestion, downstream metrics treat `Effort` as story points, missing estimates silently become zero or rounded integers, and parent/bug/task rules are not consistently enforced.

## Domain Rules Reviewed

- `docs/domain/domain_model.md` §§ 3.1-3.12
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/hierarchy_rules.md`
- `docs/domain/rules/propagation_rules.md`

## Compliant Areas

- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` requests `Microsoft.VSTS.Scheduling.StoryPoints`, `Microsoft.VSTS.Common.BusinessValue`, and `Microsoft.VSTS.Scheduling.Effort`, so the source layer can retrieve the canonical estimation fields.
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` uses `SprintCommitmentLookup` and `FirstDoneDeliveryLookup`, which matches the domain requirement that sprint metrics depend on commitment reconstruction and first-done history rather than only current snapshots.
- `PoTool.Api/Services/SprintTrendProjectionService.cs` calculates `CompletedPbiCount`, `CompletedPbiEffort`, and spillover from `pbiResolved`, so those specific projection counters already exclude bugs and tasks from the PBI-specific counts.
- `PoTool.Api/Services/SprintTrendProjectionService.cs` surfaces `MissingEffortCount` and `IsApproximate`, which means missing-estimate diagnostics are at least exposed at aggregate level, even though the implementation is incomplete for the canonical derived-estimate behavior.

## Violations Found

| Priority | File | Class | Method | Rule violation |
| --- | --- | --- | --- | --- |
| P0 | `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` | `RealTfsClient` | `ParseEffortField` | Collapses effort hours and story points into one integer field by falling back from `Effort` to `StoryPoints`. |
| P0 | `PoTool.Api/Persistence/Entities/WorkItemEntity.cs` | `WorkItemEntity` | `Effort` / `BusinessValue` properties | The persisted work-item model has no dedicated story-point field. Because authoritative story points are not stored separately, the repository cannot enforce `StoryPoints -> BusinessValue -> Missing` resolution or distinguish real story points from effort hours. |
| P0 | `PoTool.Shared/WorkItems/WorkItemDto.cs` | `WorkItemDto` | Record shape | The public DTO only exposes `Effort` and `BusinessValue`, so every consumer is forced to treat story-point data indirectly. This prevents explicit authoring of PBI-only story point logic and derived-estimate flags. |
| P0 | `PoTool.Api/Repositories/WorkItemRepository.cs` | `WorkItemRepository` | `MapToDto`, `MapToEntity` | Repository mapping preserves only `Effort` and `BusinessValue`. Even if TFS retrieval sees story points, the mapping layer discards them as a first-class concept, so parent rollups and velocity queries cannot resolve canonical story points later. |
| P0 | `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` | `GetSprintMetricsQueryHandler` | `Handle` | Computes `CompletedStoryPoints` and `PlannedStoryPoints` from `wi.Effort`, so velocity uses hours, includes non-PBI items, and hides missing estimates. |
| P0 | `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | `GetEpicCompletionForecastQueryHandler` | `Handle` | Forecasting sums descendant `Effort` across all child items, including tasks and bugs, then divides remaining effort by average sprint `CompletedStoryPoints`. That mixes effort-hours scope with story-point velocity and ignores the rule that task/bug story points are not authoritative. |
| P1 | `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeProductSprintProjection` | Completed, planned, worked, and spillover totals are all based on `wi.Effort ?? 0`. Missing estimates therefore become zero in aggregations, and no fractional derived estimate is produced even when sibling estimates exist. `IsApproximate` is only a boolean marker on the aggregate row, not a distinguishable derived value. |
| P1 | `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeProgressionDelta` | Missing PBI estimates are approximated by rounding sibling average to `int`, then folded into completion percentages as if they were real estimates. The domain model requires fractional derived estimates that remain distinguishable from real estimates and are never reused as velocity. |
| P1 | `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeFeatureProgress`, `ComputeEpicProgress` | Feature and epic progress rollups use descendant `Effort` values as the sizing basis. This bypasses the rule that story points propagate upward from PBIs, that parent story-point values must be ignored once child PBIs have estimates, and that task estimates should not drive story-point progress. |
| P1 | `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs` | `SprintMetricsProjectionEntity` | `CompletedPbiEffort`, `PlannedEffort`, `WorkedEffort`, `SpilloverEffort`, `MissingEffortCount`, `IsApproximate` | The projection schema only stores effort-based aggregate numbers plus a coarse approximation flag. There is no place to persist resolved story points, derived fractional estimates, or real-vs-derived distinction, so the sprint-trend pipeline cannot satisfy the canonical estimation model. |
| P2 | `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `GetSprintTrendMetricsQueryHandler` | `Handle` | The handler exposes only effort-based projection totals to sprint-trend consumers. That is acceptable for support metrics, but it means the current trend pipeline has no canonical story-point metric stream for velocity-aligned sprint delivery reporting. |

## Architectural Risks

- **Semantic overload in the core work-item model**: the repository uses `Effort` as both an hours field and an implicit story-point carrier. Any downstream fix that does not first separate those concepts will keep reintroducing mixed semantics.
- **Projection/storage lock-in**: sprint trend projections and DTOs are named and typed around integer effort aggregates. This makes it hard to add fractional derived story-point values without expanding the persistence contract.
- **No first-class derived estimate representation**: current code can only say “some approximation happened” at row level. It cannot show which PBIs were derived, what the derived value was, or exclude those derived values from velocity while still using them for forecasting.
- **Rollup behavior is scattered**: sprint metrics, forecasting, feature progress, and epic progress all implement their own estimate aggregation. Without a shared estimation-resolution service, domain drift is likely to continue.

## Recommended Fixes

1. **Introduce explicit story-point storage end to end**  
   Add a dedicated story-point field to TFS parsing, `WorkItemDto`, `WorkItemEntity`, repository mapping, and any API contracts that need it. Stop falling back from story points into `Effort`.
2. **Centralize estimation resolution in a shared service**  
   Implement one service that resolves canonical story points using `StoryPoints -> BusinessValue -> Missing`, applies the done-without-estimate zero rule, marks derived estimates, and keeps fractional derived values.
3. **Rebuild velocity and sprint delivery metrics on resolved PBI story points**  
   Update `GetSprintMetricsQueryHandler` and any velocity consumers so only PBIs contribute, bugs/tasks are excluded, and parent estimates are ignored once child PBI estimates exist.
4. **Keep effort rollups separate from story-point rollups**  
   Preserve effort-hours analytics for support metrics, but compute feature/epic story-point progress from PBI rollups and use effort only where the domain model explicitly allows it.
5. **Expand projections/tests for derived-estimate behavior**  
   Persist and surface resolved story-point totals plus derived-estimate diagnostics, then add focused tests for missing estimates, done-without-estimate, bug/task exclusion, and parent-fallback behavior.

## Final Compliance Classification

**Needs fixes**

### Prioritized fix list

1. Separate `StoryPoints` from `Effort` in ingestion, persistence, and DTOs.
2. Replace `Effort`-based velocity and sprint story-point calculations with canonical PBI story-point resolution.
3. Implement shared derived-estimate handling with fractional values and explicit derived markers.
4. Update feature/epic rollups and forecasting to use the canonical story-point/effort rules instead of descendant-effort shortcuts.
5. Extend unit tests around sprint metrics, trend projections, and forecasting to lock in the canonical estimation behavior.
