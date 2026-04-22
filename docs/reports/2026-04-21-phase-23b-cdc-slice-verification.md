# Phase 23b CDC slice verification

## 1. Summary

- VERIFIED: the current repository architecture can reconstruct sprint execution facts from existing persistence and CDC services without changing CDC semantics, anomaly definitions, or contracts.
- VERIFIED: product-scoped single-sprint reconstruction already exists in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`.
- PARTIAL: there is no existing application-facing batch contract that returns 8 product-scoped `SprintFactResult` rows directly; batch orchestration is feasible but not already materialized.
- FAILED: the repository-provided mock sprint catalog does not contain 8 completed sprints, so the out-of-box mock environment cannot satisfy the Phase 23a evidence gate.
- RISK: if implementation uses the single-sprint handler pattern 8 times per request, activity-history reads will scale poorly.

## 2. Data availability validation

### 2.1 Can `SprintFactResult` be retrieved for the last 8 completed sprints?

- VERIFIED: `SprintFactResult` is already a stable CDC output.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Cdc/Sprints/SprintFactResult.cs`

- VERIFIED: the access path for one sprint already exists:
  1. load `SprintEntity`
  2. resolve target products
  3. load `ResolvedWorkItemEntity` for those products
  4. load `WorkItemEntity`
  5. load `ActivityEventLedgerEntryEntity` for `System.State` and `System.IterationPath`
  6. resolve next sprint path
  7. call `_sprintFactService.BuildSprintFactResult(...)`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`

- VERIFIED: the handler already applies product scope **before** calling `BuildSprintFactResult`, so product-scoped reconstruction is supported by current structures.
  - Evidence: `GetSprintExecutionQuery` supports optional `ProductId`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Queries/GetSprintExecutionQuery.cs`
  - Evidence: filtered `ResolvedWorkItems` and `WorkItems` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`

- PARTIAL: there is no existing repository method or query handler that returns “last 8 completed sprints + product-scoped `SprintFactResult` series” in one operation.
  - Current architecture supports it by orchestration over `SprintEntity` plus repeated or batched fact reconstruction.
  - Evidence: no dedicated batch `SprintFactResult` query found; nearest existing batch path is `SprintTrendProjectionService.ComputeProjectionsAsync(...)`.

### 2.2 Per product/team context feasibility

- VERIFIED: sprint/team anchoring already exists in persistence because each `SprintEntity` has `TeamId`, and products can be linked to teams through `ProductTeamLinkEntity`.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductTeamLinkEntity.cs`

- PARTIAL: the current product scope filter is product-based, while the 8-sprint window is team-local.
  - This is feasible only if the application selects one team-local sprint stream first, then reconstructs facts for the selected product scope inside that stream.

### 2.3 Data completeness

- VERIFIED: the required raw structures exist:
  - sprint metadata in `SprintEntity`
  - product resolution in `ResolvedWorkItemEntity`
  - current work-item state in `WorkItemEntity`
  - historical changes in `ActivityEventLedgerEntryEntity`
  - canonical formulas in `SprintExecutionMetricsCalculator`

- PARTIAL: data completeness is not guaranteed by one persisted read model.
  - fact reconstruction depends on joining multiple tables and reconstructing history in memory.

### 2.4 Executed repository validation

- VERIFIED: the repository test suite already proves deterministic sprint-fact replay on realistic structures.
  - Executed:
    - `CdcReplayFixtureValidationTests.SprintFacts_ReplayFixture_ReconstructsCommitmentCompletionAndSpillover`
    - `MockConfigurationSeedHostedServiceTests.StartAsync_WhenDatabaseIsEmpty_SeedsUsableMockConfiguration`
  - Result: 2/2 tests passed

## 3. Series reconstruction validation

### 3.1 `CommitmentCompletion`

- VERIFIED: the required inputs exist in `SprintFactResult`:
  - `CommittedStoryPoints`
  - `RemovedStoryPoints`
  - `DeliveredStoryPoints`

- VERIFIED: the repository already computes the rate with the canonical formula:
  - `DeliveredSP / (CommittedSP - RemovedSP)`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Metrics/SprintExecutionMetricsCalculator.cs`

- VERIFIED: the replay fixture proves the supporting totals are reconstructable from current data structures.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CdcReplayFixtureValidationTests.cs`

- PARTIAL: denominator validity is not persisted as a dedicated flag.
  - `SprintExecutionMetricsCalculator` returns `0` when the denominator is `<= 0`, so later implementation must inspect the denominator explicitly instead of trusting a zero-valued rate as authoritative.

### 3.2 `SpilloverRate`

- VERIFIED: the required inputs exist in `SprintFactResult`:
  - `CommittedStoryPoints`
  - `RemovedStoryPoints`
  - `SpilloverStoryPoints`

- VERIFIED: the repository already computes the rate with the canonical formula:
  - `SpilloverSP / (CommittedSP - RemovedSP)`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Metrics/SprintExecutionMetricsCalculator.cs`

- VERIFIED: spillover depends on existing team-local next-sprint resolution and historical iteration moves.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs`

- PARTIAL: rate authoritativeness still depends on the same denominator and history-quality checks as `CommitmentCompletion`.

### 3.3 Missing values frequency

- VERIFIED: the repository already stores estimation-quality counters in `SprintMetricsProjectionEntity`:
  - `MissingStoryPointCount`
  - `DerivedStoryPointCount`
  - `UnestimatedDeliveryCount`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`

- PARTIAL: these counters help estimate authority, but they do not themselves provide `CommittedSP`, `RemovedSP`, `CommitmentCompletion`, or `SpilloverRate`.

- FAILED: the current repository does not include a persisted 8-sprint product history where missing-value frequency can be measured across a real completed window out of the box.
  - The replay fixture covers only 2 sprints.
  - The mock sprint catalog exposes only 1 completed sprint.

## 4. Window integrity validation

### 4.1 Ordering logic

- VERIFIED: team-local sprint ordering is already explicit.
  - `SprintSpilloverLookup.GetNextSprintPath(...)` orders by:
    1. same `TeamId`
    2. later `StartUtc`
    3. ascending `StartUtc`
    4. `SprintId` tie-break
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs`

- VERIFIED: repository sprint queries also sort by sprint start date, which is consistent with the Phase 23a window design.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`

### 4.2 Continuity enforceability

- VERIFIED: continuity can be enforced from existing structures because each sprint row has:
  - `TeamId`
  - `Path`
  - `StartDateUtc`
  - `EndDateUtc`

- PARTIAL: continuity is enforceable only at application orchestration level.
  - There is no persisted “continuous last 8 completed sprints” artifact.
  - The implementation must derive continuity from ordered `SprintEntity` rows and next-sprint lookup behavior.

### 4.3 Missing sprints and inconsistent ordering

- VERIFIED: missing or undated sprints are detectable because `SprintTrendProjectionService` already filters out sprints without both dates.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs`

- PARTIAL: a gap in sprint rows does not raise a dedicated repository error today; it simply reduces the usable window.

### 4.4 Multi-team edge cases

- PARTIAL: products can link to multiple teams.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductTeamLinkEntity.cs`

- RISK: without anchoring the analysis to the team of the selected sprint stream, “last 8 completed sprints” becomes ambiguous for multi-team products.

## 5. Evidence gating impact

### 5.1 Repository-supported seed data impact

- VERIFIED: the repository’s mock configuration seeds 6 products.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/MockData/MockConfigurationSeedHostedServiceTests.cs`

- VERIFIED: the repository’s mock sprint catalog exposes only 5 iterations per team:
  - 1 past
  - 1 current
  - 2 future
  - 1 without dates
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipSprintSeedCatalog.cs`

- FAILED: the mock catalog provides only **1 completed sprint**, so no product/team context can satisfy an 8-completed-sprint gate.

### 5.2 Estimated impact

- VERIFIED: based on the repository-provided mock configuration:
  - estimated products affected by “Insufficient evidence”: **6 / 6 = 100%**
  - estimated valid 8-sprint windows available: **0%**
  - completed-sprint coverage relative to the 8-sprint requirement: **1 / 8 = 12.5%**
  - completed iterations within the mock iteration catalog: **1 / 5 = 20%**

- PARTIAL: these percentages are valid for the repository’s default mock/test structures only, not for unknown live environments.

## 6. Performance / complexity

### 6.1 Per-request reconstruction cost

- VERIFIED: the single-sprint handler path performs multiple database reads and in-memory reconstructions for one sprint:
  - sprint row
  - product IDs
  - resolved items
  - work items
  - product names
  - state classifications
  - state events
  - iteration events
  - team sprint list for next-sprint resolution

- RISK: repeating that path 8 times per request would multiply history reads and next-sprint lookups.

### 6.2 Existing batch-friendly path

- VERIFIED: `SprintTrendProjectionService.ComputeProjectionsAsync(...)` already batches:
  - sprint rows
  - resolved items
  - work items
  - activity range loads
  - team sprint definitions
  - per-sprint/per-product projection computation
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs`

- PARTIAL: this service proves batch feasibility, but it does not persist the exact Phase 23a rates.

### 6.3 Is caching or projection required later?

- PARTIAL: not strictly required to prove feasibility.
- VERIFIED: later caching or projection is likely required for acceptable runtime if the reality-check layer is evaluated on demand across 8 sprints and multiple products.
- RISK: because `CommitmentCompletion` and `SpilloverRate` are not stored in `SprintMetricsProjectionEntity`, later implementation must either:
  - recompute from raw facts in a batch path, or
  - introduce a dedicated internal materialization step in a later phase.

## Final section

### VERIFIED

- VERIFIED: current repository structures can reconstruct product-scoped sprint facts for a selected sprint.
- VERIFIED: current repository formulas can recompute `CommitmentCompletion` and `SpilloverRate`.
- VERIFIED: team-local sprint ordering and next-sprint continuity logic already exist.
- VERIFIED: batch-oriented historical reconstruction is architecturally supported by `SprintTrendProjectionService`.
- VERIFIED: repository tests confirm deterministic sprint-fact replay and deterministic mock configuration seeding.

### PARTIAL

- PARTIAL: no existing application-facing batch contract returns 8 product-scoped `SprintFactResult` rows directly.
- PARTIAL: denominator authority must be checked explicitly because the calculator normalizes invalid denominators to `0`.
- PARTIAL: continuity enforcement must be derived at orchestration time rather than read from a persisted artifact.
- PARTIAL: evidence-gating percentages are verifiable only against repository-provided mock/test data, not unknown live datasets.
- PARTIAL: multi-team product scenarios are feasible only when one team-local sprint stream is selected first.

### FAILED

- FAILED: the repository’s out-of-box mock sprint data cannot satisfy the 8 completed sprint requirement.
- FAILED: the repository does not currently persist `CommitmentCompletion` or `SpilloverRate` as reusable multi-sprint projection fields.
- FAILED: there is no current seeded repository dataset that allows direct measurement of missing-value frequency over a real 8-sprint completed window.

### RISKS

- RISK: a naive 8x replay of the single-sprint handler path will be expensive.
- RISK: multi-team products can produce ambiguous windows if the team stream is not fixed before reconstruction.
- RISK: the default mock environment will almost always return insufficient evidence, which may look like a broken implementation unless explicitly expected.
- RISK: using only `SprintMetricsProjectionEntity` would drop the strict denominator semantics required by Phase 23a.

### GO / NO-GO for Phase 23c implementation

- GO: Phase 23c implementation may proceed because the repository architecture is sufficient to reconstruct the required inputs, **provided** implementation uses batched historical reconstruction or equivalent orchestration, preserves explicit evidence gating, and treats the default mock environment’s lack of 8 completed sprints as an expected insufficient-evidence outcome.
