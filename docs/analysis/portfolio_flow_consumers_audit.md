# PortfolioFlow Projection Consumer Audit

_Generated: 2026-03-16_

Reference documents:

- `docs/analysis/portfolio_flow_application_migration.md`
- `docs/analysis/portfolio_flow_semantic_audit.md`
- `docs/architecture/domain-model.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/source-rules.md`

Files analyzed:

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs`
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`

## Consumer Inventory

| File | Type | Portfolio role | Current source | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | Query handler | Canonical portfolio progress / flow reader | `PortfolioFlowProjectionEntity` | Migrated | Loads per-sprint product rows, delegates completion/net-flow/range rollups to `IPortfolioFlowSummaryService`, and maps the canonical result. |
| `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs` | DTO surface | API/client transport for portfolio flow | Canonical story-point fields plus compatibility aliases | Migrated with aliases | `PercentDone`, `RemainingEffort`, `AddedEffort`, and related names are compatibility aliases only; they map to canonical story-point fields and do not reconstruct scope. |
| `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` | Blazor page | Portfolio flow visualization | `PortfolioProgressTrendDto` canonical fields | Migrated | Charts and calculations bind `StockStoryPoints`, `RemainingScopeStoryPoints`, `ThroughputStoryPoints`, and `InflowStoryPoints`. |
| `PoTool.Api/Services/PortfolioFlowProjectionService.cs` | Projection service | Computes canonical PortfolioFlow rows | Activity ledger + snapshots -> `PortfolioFlowProjectionEntity` | Canonical producer | Produces the persisted projection used by portfolio progress consumers. |
| `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs` | Projection entity | Canonical persisted source | `StockStoryPoints`, `RemainingScopeStoryPoints`, `InflowStoryPoints`, `ThroughputStoryPoints`, `CompletionPercent` | Canonical source | This is the authoritative portfolio stock-and-flow projection for application consumers. |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | Orchestration service | Computes sprint projections and triggers PortfolioFlow projection refresh | `SprintMetricsProjectionEntity` and `PortfolioFlowProjectionEntity` | Mixed infrastructure | Not itself a portfolio progress reader, but it still maintains legacy sprint metrics for other features and invokes `PortfolioFlowProjectionService`. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | Query handler | Portfolio delivery distribution snapshot | `SprintMetricsProjectionEntity` plus feature progress outputs | Portfolio-adjacent CDC consumer | Delegates delivery totals and contribution shares to `IPortfolioDeliverySummaryService`; not a PortfolioFlow reader, but no longer re-aggregates delivery math in the handler. |
| `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs` | DTO surface | Portfolio delivery transport | Legacy `CompletedEffort` naming over delivery aggregates | Out of scope for PortfolioFlow | Legacy property names remain compatibility aliases over canonical delivered story-point values and share percentages. |

## Legacy Reconstruction Paths

The legacy reconstruction path identified for portfolio progress was the prior `GetPortfolioProgressTrendQueryHandler` implementation that rebuilt portfolio scope using:

- `SprintMetricsProjectionEntity`
- `PlannedEffort`
- `CompletedPbiEffort`
- derived `RemainingEffort`
- derived `PercentDone`

That legacy path is already removed from the current production handler. The current handler no longer contains historical scope replay helpers such as `ComputeHistoricalScopeEffort` and no longer reads `SprintMetricsProjectionEntity` for portfolio progress.

Remaining legacy portfolio-adjacent paths are limited to:

- `PoTool.Api/Services/SprintTrendProjectionService.cs`, which still persists `PlannedEffort` and `CompletedPbiEffort` into `SprintMetricsProjectionEntity` for sprint and delivery consumers
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`, which retains `AddedEffort`, `RemainingEffort`, and `PercentDone` as compatibility aliases only
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`, which retains `CompletedEffort` and `EffortShare` as compatibility aliases over story-point delivery fields

These are legacy naming or legacy sprint-metric dependencies, not active reconstruction logic for the portfolio progress trend.

## Canonical Projection Usage Verification

The canonical projection entity is `PortfolioFlowProjectionEntity`, with these fields:

- `StockStoryPoints`
- `RemainingScopeStoryPoints`
- `InflowStoryPoints`
- `ThroughputStoryPoints`
- `CompletionPercent`

Verification results:

1. `GetPortfolioProgressTrendQueryHandler.cs` reads `PortfolioFlowProjectionEntity` rows from `_context.PortfolioFlowProjections`.
2. The handler selects and aggregates `StockStoryPoints`, `RemainingScopeStoryPoints`, `InflowStoryPoints`, and `ThroughputStoryPoints` directly from the projection rows.
3. The handler delegates portfolio-level `CompletionPercent`, `NetFlowStoryPoints`, cumulative net flow, scope deltas, and `Trajectory` to `IPortfolioFlowSummaryService`, which is the CDC-owned summary seam over the aggregated projection rows.
4. `PortfolioProgressTrendDtos.cs` exposes these canonical fields on `PortfolioSprintProgressDto`, while legacy names remain explicit aliases.
5. `PortfolioProgressPage.razor` renders the canonical story-point metrics and no longer binds its charts to effort-proxy names.

Result: the production portfolio progress / flow consumer path uses the canonical `PortfolioFlowProjectionEntity` instead of reconstructing scope from effort proxies.

## Consumers Already Migrated

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs`

## Legacy Reconstruction Removed

- The portfolio progress trend handler no longer reads `SprintMetricsProjectionEntity` for stock/flow calculations.
- The old `PlannedEffort` -> `AddedEffort` proxy path was removed from the portfolio progress trend handler.
- The old reconstructed `RemainingEffort` / `PercentDone` path was removed from the portfolio progress trend handler.
- The old `ComputeSummary(...)` rollup path was removed from the portfolio progress trend handler and replaced by `IPortfolioFlowSummaryService`.
- The portfolio delivery handler no longer computes totals or contribution percentages locally; it now maps `IPortfolioDeliverySummaryService` outputs.
- No current production portfolio progress handler reconstructs scope from `SprintMetricsProjectionEntity`, `PlannedEffort`, `CompletedPbiEffort`, or replay-only effort helpers.

## Remaining Migration Tasks

- Remove `PercentDone`, `RemainingEffort`, `AddedEffort`, `ThroughputEffort`, `TotalScopeEffort`, and `NetFlow` compatibility aliases from `PortfolioProgressTrendDtos.cs` once downstream API consumers no longer rely on them.
- Keep new portfolio progress or flow consumers on `PortfolioFlowProjectionEntity`; do not introduce new readers over `SprintMetricsProjectionEntity` for stock/flow semantics.
- Decide separately when `CompletedEffort` / `EffortShare` compatibility aliases in `PortfolioDeliveryDtos.cs` can be removed now that canonical delivered story-point aliases are available.
