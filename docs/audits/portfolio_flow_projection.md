# PortfolioFlow Projection

_Generated: 2026-03-16_

## Projection Entity

`PortfolioFlowProjectionEntity` materializes one canonical PortfolioFlow row per `(SprintId, ProductId)` pair.

Stored fields:

- `ProductId`
- `SprintId`
- `StockStoryPoints`
- `RemainingScopeStoryPoints`
- `InflowStoryPoints`
- `ThroughputStoryPoints`
- `CompletionPercent`
- `ProjectionTimestamp`

The entity is additive. It does not replace `SprintMetricsProjectionEntity`, and it keeps the legacy effort-based projection surface intact while canonical stock/flow data is introduced.

## Projection Algorithm

`PortfolioFlowProjectionService` rebuilds sprint/product rows from repository history using the canonical PortfolioFlow formulas:

- `Stock(s)` = sprint-end story-point scope for portfolio PBIs whose canonical state is not `Removed`
- `RemainingScope(s)` = sprint-end story-point scope for portfolio PBIs whose canonical state is `New` or `InProgress`
- `Inflow(s)` = story-point scope at `EnteredPortfolio(w)` for PBIs whose first portfolio-entry timestamp falls inside the sprint window
- `Throughput(s)` = story-point scope at `FirstDone(w)` for PBIs whose first canonical Done transition falls inside the sprint window
- `CompletionPercent(s)` = `((Stock - RemainingScope) / Stock) * 100`, or `null` when `Stock = 0`

Historical replay decisions:

1. Resolve candidate PBIs from current `ResolvedWorkItemEntity` rows plus historical `PoTool.ResolvedProductId` membership transitions.
2. Reconstruct portfolio membership at arbitrary timestamps by rewinding membership events from the current resolved product snapshot.
3. Reconstruct sprint-end state with `StateReconstructionLookup`.
4. Reconstruct historical `StoryPoints` / `BusinessValue` values from the ledger and pass the point-in-time work item into `CanonicalStoryPointResolutionService`.
5. Use `PortfolioEntryLookup` for canonical `EnteredPortfolio(w)` detection.
6. Use `FirstDoneDeliveryLookup` so reopen transitions do not create duplicate throughput.

The projection is rebuilt from the existing sprint analytics pipeline by invoking `PortfolioFlowProjectionService` from `SprintTrendProjectionService.ComputeProjectionsAsync()`.

## Signal Sources

The projection consumes these persisted signals:

- `ActivityEventLedgerEntryEntity` rows for:
  - `PoTool.ResolvedProductId`
  - `System.State`
  - `Microsoft.VSTS.Scheduling.StoryPoints`
  - `Microsoft.VSTS.Common.BusinessValue`
- `ResolvedWorkItemEntity` for the current product/feature resolution snapshot
- `WorkItemEntity` for the current work item baseline used during backward replay
- `PortfolioEntryLookup`
- `StateReconstructionLookup`
- `CanonicalStoryPointResolutionService`

This keeps the boundary aligned with `docs/architecture/portfolio_flow_data_signals.md`: the ledger remains the source of historical facts, and the projection remains the source of repeated portfolio-flow series queries.

## Validation Tests

Focused tests were added for:

- stock reconstruction when a PBI enters the portfolio mid-sprint
- inflow plus throughput when a PBI is added and delivered in the same sprint
- reopen handling so only the first Done transition contributes to throughput
- estimate-change handling before and after Done
- completion percent computed from `StockStoryPoints` and `RemainingScopeStoryPoints`
- SQLite-backed rebuild validation through the existing sprint projection pipeline

Relevant test files:

- `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
- `PoTool.Tests.Unit/Audits/PortfolioFlowProjectionDocumentTests.cs`

## Migration Path To PortfolioFlow CDC

This implementation intentionally stops at projection materialization.

What changed now:

- canonical stock/flow series are persisted in `PortfolioFlowProjectionEntity`
- the existing sprint analytics rebuild path now refreshes PortfolioFlow rows
- historical replay uses the new StoryPoints and resolved-product membership signals

What remains intentionally unchanged:

- legacy portfolio handlers still serve legacy effort-based DTOs
- legacy effort metrics still exist beside the new canonical projection
- no PortfolioFlow-specific API or UI contract migration was introduced in this phase

This keeps the migration path incremental:

1. persist canonical PortfolioFlow series
2. validate the projection against historical scenarios
3. migrate portfolio handlers and DTOs to the new projection in a later change
