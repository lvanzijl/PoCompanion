# PoTool Projection and Trend Pipeline Domain Audit

_Generated: 2026-03-14_

## Summary

### Files analyzed

- `docs/domain/domain_model.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/propagation_rules.md`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Persistence/Entities/ProductOwnerCacheStateEntity.cs`
- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`
- `PoTool.Api/Services/CacheManagementService.cs`
- `PoTool.Api/Services/FirstDoneDeliveryLookup.cs`
- `PoTool.Api/Services/SprintCommitmentLookup.cs`
- `PoTool.Api/Services/SprintSpilloverLookup.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/Sync/SprintTrendProjectionSyncStage.cs`
- `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

### Verdict

**Fully compliant**

The audited projection pipeline is aligned with the canonical sprint and estimation domain rules in its production execution path. Stored sprint trend projections are built from historical activity and iteration reconstruction, use the shared canonical services for commitment, first-Done delivery, spillover, and story-point resolution, and can be regenerated on demand, during sync, or after cache reset. The remaining concerns are low-risk architectural and documentation follow-ups rather than metric-semantic violations.

## Domain Rules Reviewed

- `docs/domain/domain_model.md` §§ 2.3-2.4, 3.3-3.9, 5.1-5.10, 7
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/propagation_rules.md`

## Compliant Areas

- **Projection correctness is anchored on the canonical lookups and resolver.**  
  `PoTool.Api/Services/SprintTrendProjectionService.cs` computes committed scope with `SprintCommitmentLookup.BuildCommittedWorkItemIds()` and `SprintCommitmentLookup.GetCommitmentTimestamp()`, computes first-Done delivery via `FirstDoneDeliveryLookup.Build()`, computes spillover through `SprintSpilloverLookup.BuildSpilloverWorkItemIds()`, and resolves story points through `CanonicalStoryPointResolutionService` before storing `PlannedStoryPoints`, `CompletedPbiStoryPoints`, `SpilloverStoryPoints`, derived-estimate diagnostics, and unestimated-delivery diagnostics.

- **Production projections rely on historical reconstruction, not snapshot-only state.**  
  `SprintTrendProjectionService.ComputeProjectionsAsync()` loads historical `System.State` and `System.IterationPath` events from `ActivityEventLedgerEntries`, reconstructs commitment using the canonical commitment timestamp (`SprintStart + 1 day`), and attributes delivery by the first Done transition within the sprint window. Current `WorkItems` are used only as the present-day baseline that is rewound through the event history.

- **Velocity semantics match the canonical PBI-only rules.**  
  `ComputeProductSprintProjection()` iterates only `ResolvedWorkItemEntity` entries of type `Pbi` for delivered story points, excludes Bugs and Tasks from story-point delivery totals, and uses `IsVelocityStoryPointEstimate()` to exclude `Missing` and `Derived` estimates from `CompletedPbiStoryPoints`. `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` verifies that planned scope can include derived estimates while delivered velocity excludes them and counts them under `UnestimatedDeliveryCount`.

- **Bug and task handling matches the domain model.**  
  Bugs are tracked in separate diagnostics (`BugsPlannedCount`, `BugsWorkedCount`, `BugsCreatedCount`, `BugsClosedCount`) and never contribute to `CompletedPbiStoryPoints`, `PlannedStoryPoints`, or `SpilloverStoryPoints`. Tasks contribute only through propagated activity/work visibility and never as story-point delivery units.

- **Projection DTOs expose canonical sprint-trend metrics and keep effort diagnostics separate.**  
  `PoTool.Shared/Metrics/SprintTrendDtos.cs` and `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs` expose planned/delivered/spillover story points, derived/missing estimate diagnostics, and effort-based diagnostics as separate fields. `GetSprintTrendMetricsQueryHandler` preserves those fields rather than recomputing alternate formulas.

- **Projection results are reproducible and rebuildable from raw data.**  
  `GetSprintTrendMetricsQueryHandler` can force recomputation with `Recompute = true`, automatically recomputes on cache miss, and marks cached results stale when `ActivityEventWatermark` exceeds `SprintTrendProjectionAsOfUtc`. `SprintTrendProjectionSyncStage` recomputes projections during sync stage 6, and `CacheManagementService` / `CacheStateRepository` can invalidate projection timestamps so projections can be rebuilt from the underlying ledger and resolved hierarchy.

- **Focused tests lock in the audited semantics.**  
  `SprintTrendProjectionServiceTests` verify canonical delivered story points, derived-estimate exclusion from velocity, and the preserved approximation diagnostics. `GetSprintTrendMetricsQueryHandlerTests` verify cached retrieval vs recompute behavior and confirm that story-point trend totals, spillover story points, derived diagnostics, and unestimated delivery diagnostics flow through the handler unchanged.

## Violations Found

No remaining production-rule violations were found in the audited projection and trend pipeline.

## Architectural Risks

- **`ComputeProductSprintProjection()` instantiates a canonical resolver directly instead of reusing the injected instance.**  
  `PoTool.Api/Services/SprintTrendProjectionService.cs` uses `new CanonicalStoryPointResolutionService()` inside the static projection method. This is still semantically correct today because it uses the same canonical implementation, but it does bypass the service instance injected into `SprintTrendProjectionService`, which increases the chance of drift if the resolver is ever decorated or substituted in DI.

- **Projection staleness tracking is tied to activity ingestion, not every configuration mutation.**  
  `GetSprintTrendMetricsQueryHandler` marks projections stale when `ActivityEventWatermark > SprintTrendProjectionAsOfUtc`, and sync/reset paths update or clear the projection timestamp. However, the audit did not find equivalent automatic invalidation for changes to state classifications or sprint-date edits, so those changes currently rely on explicit recompute/sync/reset discipline rather than targeted invalidation.

- **Incremental recomputation is not implemented yet.**  
  `SprintMetricsProjectionEntity.IncludedUpToRevisionId` exists for incremental rebuilds, but `ComputeProductSprintProjection()` currently stores `0` for that field. This does not break correctness because recomputation is full and reproducible, but it means the pipeline has no partial-update optimization or audit trail over processed revision ranges.

- **One DTO comment had drifted from the implemented metric.**  
  `PoTool.Shared/Metrics/SprintTrendDtos.cs` previously described `ScopeChangeEffort` as story points even though the handler populates it from `SprintEffortDelta`. This was corrected during this audit so the contract documentation matches the actual non-canonical effort diagnostic.

## Recommended Fixes

1. **Keep projection semantics centralized on injected canonical services.**  
   When `SprintTrendProjectionService` next changes, pass the injected `ICanonicalStoryPointResolutionService` through all internal projection paths instead of constructing a new resolver inside `ComputeProductSprintProjection()`.

2. **Extend invalidation triggers beyond activity ingestion.**  
   If sprint dates or state classifications change, clear or refresh `SprintTrendProjectionAsOfUtc` so cached projections cannot remain logically stale after domain-configuration updates.

3. **Implement or remove incremental projection metadata deliberately.**  
   Either wire `IncludedUpToRevisionId` into a true incremental rebuild story or document it explicitly as reserved metadata to avoid false assumptions about partial recomputation support.

4. **Keep effort-only diagnostics clearly labeled.**  
   Continue treating `ScopeChangeEffort`, `WorkedEffort`, `PlannedEffort`, and similar fields as diagnostics separate from canonical story-point delivery metrics, and avoid describing them as story points in DTO or API documentation.

## Final Compliance Classification

**Fully compliant**

### Prioritized follow-up list

1. Route all projection story-point resolution through the injected canonical resolver instance.
2. Add targeted projection invalidation for sprint-date and state-classification changes.
3. Decide whether `IncludedUpToRevisionId` should become a real incremental rebuild contract or remain explicitly unused metadata.
4. Preserve the current focused trend-projection tests so future refactors cannot reintroduce projection drift.
