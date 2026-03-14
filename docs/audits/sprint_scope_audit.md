# PoTool Sprint Scope Domain Audit

_Generated: 2026-03-14_

## Summary

### Files analyzed

- `docs/domain/domain_model.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/state_rules.md`
- `PoTool.Api/Services/SprintCommitmentLookup.cs`
- `PoTool.Api/Services/SprintSpilloverLookup.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/ActivityEventIngestionService.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs`
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

### Verdict

**Fully compliant**

The repository's active sprint-scope analytics path follows the canonical domain rules for sprint commitment, added scope, removed scope, spillover, and sprint-window usage. Commitment is reconstructed from `System.IterationPath` history at `SprintStart + 1 day`, churn is derived from timestamped iteration-path events after that commitment point, spillover requires committed scope plus not-Done-at-end plus a direct move into Sprint N+1, and the audited sprint metrics/execution/trend handlers all use the sprint's dated window instead of current snapshot-only membership.

## Domain Rules Reviewed

- `docs/domain/domain_model.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/state_rules.md`

### Canonical rules reconstructed

- **Sprint window** = `[SprintStart, SprintEnd]`.
- **Commitment timestamp** = `SprintStart + 1 day`.
- **Committed scope** = items whose `IterationPath` equals the sprint path at the commitment timestamp, reconstructed from iteration history rather than current membership.
- **Scope added** = item enters the sprint after commitment.
- **Scope removed** = item leaves the sprint after commitment.
- **Spillover** = committed item, not Done at `SprintEnd`, then moved directly to Sprint N+1.

## Compliant Areas

- **Commitment reconstruction uses historical iteration membership at the canonical timestamp.**  
  `PoTool.Api/Services/SprintCommitmentLookup.cs` (`GetCommitmentTimestamp`, `BuildCommittedWorkItemIds`, `GetIterationPathAtTimestamp`) reconstructs `System.IterationPath` at `SprintStart + 1 day` by walking later iteration-path updates backward from the current snapshot. `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`, `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`, and `PoTool.Api/Services/SprintTrendProjectionService.cs` all use this helper in their active sprint-scope paths.

- **Scope added and scope removed are derived from `System.IterationPath` update events after commitment.**  
  `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` (`Handle`) identifies added scope from events whose `NewValue == sprint.Path` and removed scope from events whose `OldValue == sprint.Path`, both filtered to timestamps after commitment and within the sprint window. `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` uses the same post-commitment entry events to include scope added after commitment in historical sprint totals.

- **Spillover logic matches the canonical three-part rule.**  
  `PoTool.Api/Services/SprintSpilloverLookup.cs` (`BuildSpilloverWorkItemIds`) requires committed membership, reconstructs state at sprint end, excludes items already Done, and then checks that the first post-sprint iteration move is a direct move from Sprint N to Sprint N+1. `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` and `PoTool.Api/Services/SprintTrendProjectionService.cs` both delegate spillover detection to this helper.

- **Sprint window semantics are date-based, not current-time or snapshot-only.**  
  `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` requires dated sprint metadata and filters delivery to first-Done transitions within the sprint window. `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` loads sprint metadata first, derives `commitmentTimestamp` from `SprintStart`, and applies `SprintEnd` when classifying additions, removals, deliveries, and spillover. `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` consumes projections generated from the same dated sprint windows.

- **Activity-event ingestion stores the timestamped iteration-history input needed by the canonical model.**  
  `PoTool.Api/Services/ActivityEventIngestionService.cs` (`IngestAsync`) persists `System.IterationPath` and `System.State` field changes with UTC event timestamps into `ActivityEventLedgerEntryEntity`, which is the event source used by the sprint-scope reconstruction logic.

- **Current snapshot sprint membership caches are not used as the source of truth for audited sprint-scope analytics.**  
  `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs` explicitly documents `ResolvedSprintId` as current iteration-path membership only. The audited production paths in `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler`, and `SprintTrendProjectionService.ComputeProjectionsAsync` reconstruct historical sprint scope from activity history instead of treating `ResolvedSprintId` as commitment truth.

- **Unit coverage exercises the canonical sprint-scope behaviors.**  
  `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs` verifies historical commitment reconstruction, post-commitment added scope, and first-Done-only delivery. `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` verifies added scope, removed scope, direct-next-sprint spillover, backlog round-trip exclusion, and unfinished-on-sprint exclusion. `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` verifies committed planned scope and spillover semantics inside trend projections.

## Violations Found

No sprint-scope rule violations were found in the audited production paths.

## Architectural Risks

- **`SprintTrendProjectionService.ComputeProductSprintProjection()` still contains a snapshot-based fallback for internal callers.**  
  File: `PoTool.Api/Services/SprintTrendProjectionService.cs`  
  Class: `SprintTrendProjectionService`  
  Method: `ComputeProductSprintProjection`  
  Risk: when `committedWorkItemIds` is not supplied, the helper falls back to `ResolvedSprintId == sprint.Id`. `ComputeProjectionsAsync()` correctly passes reconstructed committed IDs today, so this is not an active violation, but future reuse of the helper without those inputs could reintroduce snapshot-based sprint scope drift.

- **Portfolio stock/flow trend remains a separate effort-oriented proxy and should not be reused as canonical sprint-scope truth.**  
  File: `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`  
  Class: `GetPortfolioProgressTrendQueryHandler`  
  Method: `Handle` / `ComputeHistoricalScopeEffort`  
  Risk: the handler explicitly documents `AddedEffort` as a proxy based on planned effort, not iteration-entry events after commitment. That is acceptable for its documented stock/flow purpose, but it should stay isolated from sprint commitment/churn semantics.

## Recommended Fixes

1. **Preserve `SprintCommitmentLookup` and `SprintSpilloverLookup` as the only shared sprint-scope reconstruction helpers.**  
   Any future sprint analytics should continue to route commitment, churn, and spillover through these helpers rather than introducing handler-local variants.

2. **Avoid calling `ComputeProductSprintProjection()` without reconstructed committed IDs in production code.**  
   If the helper gains additional callers, require `committedWorkItemIds` explicitly or remove the `ResolvedSprintId` fallback to prevent semantic drift.

3. **Keep portfolio effort-proxy analytics documented as non-canonical.**  
   `GetPortfolioProgressTrendQueryHandler` should remain clearly labeled as a stock/flow helper so it is not mistaken for sprint-scope churn reporting.

## Final Compliance Classification

**Fully compliant**

### Prioritized fix list

1. Guard future projection-helper reuse against the `ResolvedSprintId` fallback path.
2. Keep effort-proxy portfolio trends separate from canonical sprint-scope analytics.
