> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# PortfolioFlow Signal Enablement

## StoryPoints History Added

- `PoTool.Core/RevisionFieldWhitelist.cs` now includes `Microsoft.VSTS.Scheduling.StoryPoints`.
- `PoTool.Api/Services/ActivityEventIngestionService.cs` continues to use the existing generic field-change ledger shape, so StoryPoints revisions are now persisted as normal `ActivityEventLedgerEntryEntity` rows.
- Existing update deduplication remains unchanged because StoryPoints reuse the same `(WorkItemId, UpdateId, FieldRefName)` ledger identity as other tracked fields.

## Resolved Product Membership Timeline Added

- `PoTool.Api/Services/WorkItemResolutionService.cs` now compares the previous `ResolvedWorkItemEntity` snapshot with the newly resolved product membership for the current resolution run.
- When the effective resolved product changes, the service writes a synthetic activity ledger row with `FieldRefName = PoTool.ResolvedProductId`.
- The synthetic row stores the old resolved product value, the new resolved product value, and the detection timestamp while `ResolvedWorkItemEntity` remains the current snapshot table.

## Portfolio Entry Derivation Added

- `PoTool.Core.Domain/Domain/Portfolio/PortfolioEntryLookup.cs` provides reusable derivation for `EnteredPortfolio(workItemId, productId)`.
- The helper reads membership transitions from the ledger and returns the first event where:
  - `FieldRefName = PoTool.ResolvedProductId`
  - `OldValue` is null or different from the target product
  - `NewValue` equals the target product
- No separate `EnteredPortfolio` table was added.

## Tests Added

- `PoTool.Tests.Unit/Services/ActivityEventIngestionServiceTests.cs` covers StoryPoints revision ingestion into the ledger.
- `PoTool.Tests.Unit/Services/WorkItemResolutionServiceTests.cs` covers synthetic resolved product transitions for real membership changes and no-op re-resolution.
- `PoTool.Tests.Unit/Services/PortfolioEntryLookupTests.cs` covers first portfolio-entry derivation behavior.
- Existing ingestion and PortfolioFlow document tests continue to validate additive behavior.

## Remaining Work Before PortfolioFlow Projection

- The repository now contains `PortfolioFlowProjectionService` and `PortfolioFlowProjectionEntity`, so the new historical StoryPoints and resolved membership signals are consumed during sprint-projection rebuilds.
- Current portfolio pages and legacy effort-based handlers remain unchanged until a later migration points them at the canonical projection.
- PortfolioFlow CDC extraction and transport-contract cleanup are still future work.
