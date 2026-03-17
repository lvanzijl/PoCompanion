# CDC Freeze Audit

_Generated: 2026-03-17_

Reference documents:

- `docs/audits/cdc_completion_summary.md`
- `docs/audits/cdc_usage_coverage.md`
- `docs/audits/cdc_invariant_tests.md`
- `docs/audits/cdc_replay_fixture_validation.md`
- `docs/audits/test_cleanup_step1.md`
- `docs/audits/test_ownership_normalization.md`
- `docs/domain/cdc_reference.md`
- `docs/domain/cdc_domain_map.md`

## Semantic Ownership Status

Semantic ownership is stable and CDC-owned for the audited delivery analytics scope.

Confirmed ownership signals:

- `docs/audits/cdc_completion_summary.md` classifies the CDC slices as complete at the semantic level and explicitly frames the remaining work as structural cleanup rather than additional slice extraction.
- `docs/audits/cdc_usage_coverage.md` reports zero CDC bypass findings in the audited handler set.
- The same usage audit classifies 12 of 14 audited handlers as `CDC compliant` and the remaining 2 as `unavoidable adapter logic`, not as semantic reimplementation.
- Sprint commitment, scope change, completion, spillover, delivery trend, forecasting, backlog quality, portfolio flow, effort diagnostics, and effort planning semantics are all documented as slice-owned in `docs/domain/cdc_reference.md`.

Ownership conclusion:

- audited delivery analytics semantics are owned by CDC slices
- handlers are reduced to loading, orchestration, filtering, and DTO mapping
- DTOs preserve compatibility naming in some places, but they do not redefine canonical formulas
- no client-side calculator was identified in the referenced audits as a competing owner of the audited CDC semantics

## Test Ownership Status

Test ownership is normalized and aligned with the intended boundaries.

Confirmed ownership split:

- CDC/domain tests own formulas, invariants, and edge-case semantics
- handler tests own orchestration, request scoping, filtering, and DTO mapping
- projection tests own persistence behavior and deterministic replay outputs
- adapter tests own formatting and compatibility mapping where such coverage exists

Supporting evidence from the referenced audits:

- `docs/audits/test_cleanup_step1.md` records the removal of duplicate semantic assertions from handler suites and the strengthening of `SprintCommitmentCdcServicesTests.cs`
- `docs/audits/test_ownership_normalization.md` documents the final ownership split and states that no production code changes were required to normalize it
- `docs/audits/cdc_replay_fixture_validation.md` confirms deterministic replay coverage for SprintFacts, PortfolioFlow, DeliveryTrends, Forecasting, and EffortPlanning
- `docs/audits/cdc_invariant_tests.md` corrects the canonical invariant set so semantic assertions now live in the proper CDC/domain tests

Ownership conclusion:

- CDC tests are the single semantic owners
- handler tests are orchestration-only
- projection tests are persistence-and-determinism-only
- duplicate semantic risk is low in the audited scope

## Boundary Cleanliness

The CDC is clean enough to freeze semantically, with only compatibility-oriented seams still visible around it.

Confirmed clean boundaries:

- `docs/domain/cdc_reference.md` and `docs/domain/cdc_domain_map.md` place handler orchestration, DTO shaping, persistence entities, compatibility adapters, and UI consumers outside the CDC slices
- `docs/audits/cdc_usage_coverage.md` shows the audited handlers consuming CDC services instead of re-owning the formulas
- `docs/audits/test_cleanup_step1.md` and `docs/audits/test_ownership_normalization.md` confirm that higher-layer tests no longer act as semantic owners
- repository spot-checks in the audited paths show CDC services in `PoTool.Core.Domain` free of EF Core, HTTP, SignalR, controller, and UI framework dependencies

Boundary findings:

- no presentation wording was found inside the documented CDC slice ownership
- no UI logic was found inside the CDC slices
- no infrastructure dependency was found inside the CDC slice implementations that were spot-checked for this audit
- direct DTO shaping remains outside the CDC in handlers and mappers, which matches the intended architecture

Known boundary caveat:

- `PoTool.Core.Domain/Domain/EffortPlanning/EffortEstimationSuggestionService.cs` still depends on `PoTool.Shared.Settings.EffortEstimationSettingsDto`
- this is not a semantic duplication problem, but it is a compatibility-shaped contract leaking into a CDC slice boundary
- that seam is best classified as compatibility debt rather than as a reason to reopen the CDC semantics

## Compatibility Debt Still Present

The remaining debt is compatibility-oriented and sits at or around application boundaries rather than in the CDC formulas themselves.

Confirmed debt still present:

- legacy `*Effort` DTO names still carry story-point semantics in forecast and portfolio responses
- backlog-quality compatibility aliases such as `RC-2` and `EFF` remain visible at application and presentation seams
- some portfolio handlers and pages still preserve older response shapes while canonical stock, inflow, throughput, and remaining-scope semantics are already CDC-owned
- `docs/audits/cdc_usage_coverage.md` still identifies client-side roadmap scope replay as the main remaining non-CDC path in the audited scope
- the EffortPlanning suggestion service still accepts `EffortEstimationSettingsDto`, which leaves a DTO-shaped settings contract inside a CDC-facing service boundary

Debt conclusion:

- the remaining debt is real
- it is compatibility and contract cleanup debt, not missing semantic ownership
- it can be addressed incrementally without reopening CDC slice definitions

## Freeze Decision

CDC classification: **frozen with known compatibility debt**

Reasoning:

- semantic ownership is complete for the audited delivery analytics scope
- test ownership is normalized around CDC/domain, handlers, projections, and adapters
- replay and invariant validation confirm deterministic existing behavior without requiring semantic changes
- the remaining issues are compatibility aliases, transport naming, response-shape preservation, client-side duplication cleanup, and one DTO-shaped settings seam

The CDC should not be classified as `frozen and ready` because the compatibility debt is still present.

The CDC should not be classified as `not yet frozen` because the audited evidence no longer shows missing slice ownership or handler-side semantic reimplementation.

## Next Phase Recommendation

Proceed to persistence and source-abstraction work with the CDC treated as a fixed semantic boundary.

Recommended next-step discipline:

- do not reopen CDC formulas unless a real semantic defect is found
- keep future cleanup focused on application adapters, transport contracts, and client compatibility layers
- remove remaining compatibility-only aliases and legacy field shapes incrementally where consumers can adopt canonical naming
- isolate the `EffortEstimationSettingsDto` seam behind a CDC-native settings contract when boundary cleanup work is scheduled
- preserve the current ownership split so new handlers and tests do not reintroduce semantic duplication
