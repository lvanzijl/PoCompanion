# PortfolioFlow Projection Validation

_Generated: 2026-03-16_

## Legacy Comparison

Validation compared the legacy portfolio progress path in `GetPortfolioProgressTrendQueryHandler` with the canonical `PortfolioFlowProjectionEntity` output on a representative two-sprint dataset.

Representative dataset:

- Sprint 1 starts with one open PBI and one PBI completed during the sprint.
- Sprint 2 keeps the original open PBI, adds one PBI that is completed in the same sprint, and adds one more PBI that remains open at sprint end.
- Legacy inflow uses `SprintMetricsProjectionEntity.PlannedEffort`.
- PortfolioFlow inflow uses actual `PoTool.ResolvedProductId` portfolio-entry events.

Observed results:

| Sprint | Legacy total scope / remaining / throughput / inflow proxy | PortfolioFlow stock / remaining / throughput / inflow |
| --- | --- | --- |
| Sprint 1 | `33 effort`, `13 effort`, `20 effort`, `13 effort` | `13 SP`, `5 SP`, `8 SP`, `0 SP` |
| Sprint 2 | `62 effort`, `34 effort`, `8 effort`, `34 effort` | `28 SP`, `17 SP`, `3 SP`, `15 SP` |

Comparison outcome:

- **Trend direction:** both series classify the two-sprint range as expanding because cumulative net flow is negative in both models.
- **Completion ratio:** the shapes stay aligned even though the absolute percentages differ slightly (`60.6% → 45.2%` in legacy effort space versus `61.5% → 39.3%` in canonical story-point space).
- **Throughput per sprint:** differences are expected because the comparison is `effort → story points`.
- **Inflow behavior:** the canonical projection excludes sprint commitment proxies and uses real backlog-entry events, so the difference is the intended semantic shift `commitment proxy → real inflow`.

Conclusion:

- No unexplained divergence was found in the representative dataset.
- The important behavior change is semantic, not unstable: the new projection keeps the same stock/flow shape while replacing ambiguous effort proxies with canonical story-point scope and explicit entry events.

## Edge Case Validation

Focused unit coverage now validates the required edge cases in `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs`:

1. **PBI added mid sprint**  
   `ComputeProductSprintProjection_ReconstructsStockRemainingAndInflow_ForMidSprintPortfolioEntry` confirms a mid-sprint portfolio entry contributes to sprint-end stock, remaining scope, and inflow.

2. **PBI added and completed in same sprint**  
   `ComputeProductSprintProjection_CountsInflowAndThroughput_WhenPbiEntersAndCompletesInSameSprint` confirms one PBI can contribute to both inflow and throughput in the same sprint without double counting stock.

3. **Estimate change before Done**  
   `ComputeProductSprintProjection_UsesHistoricalEstimateAtFirstDone_AndSprintEndEstimateForStock` confirms throughput uses the scope at the first `Done` transition, not the later sprint-end estimate.

4. **Estimate change after Done**  
   The same test confirms stock follows the sprint-end estimate even after delivery while throughput remains fixed at first-done scope.

5. **Reopen after Done**  
   `ComputeProductSprintProjection_UsesFirstDoneForThroughput_WhenPbiIsReopened` confirms reopen behavior affects remaining scope at sprint end but does not create a second throughput event.

6. **Parent change moving PBI into portfolio**  
   `ComputeProductSprintProjection_TreatsResolvedProductChangeAsPortfolioInflow_WhenPbiMovesIntoPortfolio` confirms a resolved-product membership transition is treated as canonical portfolio inflow.

The edge-case results match the canonical model in `docs/architecture/portfolio-flow-model.md`:

- backlog entry is based on portfolio membership, not sprint commitment
- throughput is based on first `Done`
- reopen is rework, not a second delivery
- sprint-end stock and remaining scope come from reconstructed sprint-end state

## Historical Reconstruction Validation

Historical reconstruction was validated against the three requested replay concerns:

1. **StoryPoints changes affect historical scope correctly**  
   `ComputeProductSprintProjection_UsesHistoricalEstimateAtFirstDone_AndSprintEndEstimateForStock` proves the service rewinds story-point changes for throughput while still using the sprint-end estimate for stock.

2. **Portfolio membership transitions affect inflow correctly**  
   Mid-sprint entry and cross-product membership-change tests prove `PortfolioEntryLookup` uses the first resolved-product transition into the portfolio as canonical inflow.

3. **Sprint-end state reconstruction affects remaining scope correctly**  
   Reopen coverage proves `StateReconstructionLookup` can classify a PBI as open at sprint end even after an earlier done event, which keeps remaining scope historically correct.

This means the projection is reconstructing:

- point-in-time story-point scope
- point-in-time portfolio membership
- point-in-time canonical state

without falling back to residual arithmetic for remaining scope.

## Determinism Check

`ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionDeterministicallyWithoutDuplicates` in `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs` rebuilds the same sprint projection three times and verifies:

- each rebuild returns exactly one row for the `(SprintId, ProductId)` key
- persisted row count remains `1`
- inflow stays `8 SP`
- throughput stays `8 SP`
- completion stays `100%`

This confirms the projection is deterministic and does not accumulate duplicate inflow or duplicate throughput on repeated rebuilds.

## Projection Readiness For Application Migration

Readiness conclusion:

- The current `PortfolioFlowProjectionEntity` implementation now has targeted validation for legacy comparison, edge cases, historical reconstruction, and deterministic rebuilds.
- The observed differences from `GetPortfolioProgressTrendQueryHandler` are explainable by the intended semantic changes:
  - `effort → story points`
  - `commitment proxy → real inflow`
- No handler migration is included in this change.

Based on the validation above, the projection is trustworthy enough to use as the foundation for a later application-layer migration, provided that the migration preserves the explicit semantic shift and does not attempt to preserve legacy effort naming.
