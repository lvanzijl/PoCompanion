# PoTool Unit Test Strategy

## Testing Priorities
- **highest priority:** canonical sprint analytics semantics in `PoTool.Core.Domain`, especially sprint-history reconstruction, story-point resolution, hierarchy rollups, and sprint execution formulas
- **medium priority:** API orchestration that composes CDC services with EF-backed inputs, handler flows, projection readers, and a small number of boundary-critical adapter mappings
- **low priority:** trivial DTOs, obvious records, direct property-copy mappers, DI resolvability checks, and low-risk plumbing that mostly duplicates stronger semantic tests

## What To Test Heavily
- **CDC/domain services**
  - `SprintCommitmentLookup`, `FirstDoneDeliveryLookup`, `SprintSpilloverLookup`, `StateReconstructionLookup`, and `StateClassificationLookup`
  - `CanonicalStoryPointResolutionService`
  - `HierarchyRollupService`
  - `SprintExecutionMetricsCalculator`
  - Reasoning: these classes own the canonical meaning of sprint analytics. If they drift, every handler, forecast, and projection can become wrong in the same way.
- **Sprint helpers and historical replay edge cases**
  - Commitment timestamp behavior, first-Done semantics, reopen behavior, spillover rules, state reconstruction ordering, and snapshot-vs-history reconciliation
  - Reasoning: these rules are subtle, easy to regress, and hard to diagnose once buried inside larger orchestration tests.
- **Story point resolution semantics**
  - PBI-only authority, `StoryPoints -> BusinessValue -> Missing`, zero-on-Done vs zero-on-not-Done, derived sibling estimates, parent fallback, and exclusion of bugs/tasks
  - Reasoning: this logic directly affects velocity, forecasting, and rollups across multiple features.
- **Hierarchy rollups**
  - PBI-to-feature and feature-to-epic scope rules, completed-vs-total rollup, excluded types, and fallback only when child evidence is absent
  - Reasoning: this is core sizing and forecast behavior, not transport plumbing.
- **Sprint execution formulas**
  - churn, commitment completion, spillover, added delivery, and denominator edge cases
  - Reasoning: formula regressions are cheap to catch with unit tests and expensive to detect later from UI symptoms.

## What To Test Lightly
- **Handlers**
  - `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler`, `GetEpicCompletionForecastQueryHandler`, and similar analytics handlers
  - Reasoning: handlers should prove orchestration, not re-prove every CDC rule. Keep thin tests that show the handler loads the right inputs, calls CDC services correctly, and maps outputs to the right DTO fields.
- **Projection readers and projection services**
  - especially `SprintTrendProjectionService` and SQLite-backed projection paths
  - Reasoning: projections deserve targeted regression coverage because they combine EF materialization, event sourcing inputs, and CDC consumption. Keep a curated set of realistic end-to-end regressions, plus infrastructure-specific translation checks.
- **Query composition**
  - EF queries, product scoping, sprint filtering, and historic event selection
  - Reasoning: query composition matters at boundaries, but it should be protected with a few representative integration/regression tests rather than large unit matrices.
- **Adapter boundaries**
  - `HistoricalSprintInputMapper`, `StateClassificationInputMapper`, and other transport-to-domain seams
  - Reasoning: keep only tests for normalization, trimming, null/default handling, timestamp fallback, or other boundary behavior that could silently distort CDC input.

## What To Test Minimally
- **Trivial DTOs and obvious property records**
  - transport contracts in `PoTool.Shared`, simple records, and shape-only data containers
  - Reasoning: compile-time usage and higher-level tests already protect these better than direct unit tests do.
- **Low-risk plumbing**
  - constructor-shape tests, pure DI-resolution checks, and repetitive service-registration assertions
  - Reasoning: these tests are usually brittle and mostly prove that the container can build, not that the product behaves correctly.
- **Duplicate mapper assertions**
  - property-copy tests that do not validate normalization or boundary semantics
  - Reasoning: they add maintenance cost without protecting business meaning.
- **Repeated semantic assertions in heavy tests**
  - handler and projection scenarios that restate the same first-Done, fallback-estimate, or hierarchy-rollup rule already covered directly in CDC tests
  - Reasoning: one consuming-path regression is valuable; many near-identical restatements are not.
- **Large mock-data and infrastructure-heavy “unit” tests**
  - full Battleship hierarchy validation, wide mock-data audits, TFS-client-adjacent setup-heavy tests, and other slow suites outside the canonical analytics core
  - Reasoning: these consume disproportionate time and maintenance relative to the protection they add for PoTool sprint analytics.

## Recommended Target Shape
- **CDC:** strong semantic coverage, including edge cases and malformed-input behavior
- **Sprint helpers:** direct authoritative tests for timeline replay, canonical state interpretation, and spillover/commitment reconstruction
- **Handlers:** thin orchestration coverage that proves CDC consumption, scoping, and outward contract mapping
- **Projections:** targeted regression coverage for realistic historical/event-driven paths and provider-specific failures
- **Query composition:** a few focused integration tests where EF translation or persistence behavior is the actual risk
- **Mappers:** only boundary-critical mappings such as normalization, trimming, timestamp fallback, and canonical-type conversion
- **DTOs and plumbing:** minimal or no dedicated unit tests unless a bug or compatibility contract justifies them

## Recommended Test Reduction Principles
- Remove or avoid tests that only restate CDC semantics through a heavier layer without adding a new boundary, provider, or regression signal.
- Prefer one direct CDC test plus one consuming-path regression over repeating the same rule in several handlers and projections.
- Keep handler tests small: one scenario should prove orchestration, not every permutation of domain math.
- Keep projection tests only when they protect realistic end-to-end behavior, hierarchy propagation, or provider-specific translation risks that CDC tests cannot see.
- Skip dedicated tests for direct property copying unless the mapping normalizes, trims, defaults, or otherwise transforms boundary data.
- Treat very slow mock-data or infrastructure-heavy tests as integration/regression candidates, not as the default unit-test strategy.
- Add new tests primarily when they protect canonical semantics, a known regression, or a fragile boundary. Do not add tests only because a new method exists.
- If a bug is already fully covered at the CDC layer, first ask whether a higher-layer test should be reduced to a single smoke regression instead of expanded.

## Final Guidance
PoTool should bias future testing toward a small, authoritative CDC suite with strong semantic depth, supported by a thinner layer of orchestration tests and a carefully curated set of projection/integration regressions. The goal is not maximum test count; it is maximum protection of canonical analytics meaning. Test the rules where they are defined, keep only enough higher-layer coverage to prove composition works, and stop spending unit-test budget on trivial transport shapes and slow low-signal plumbing.
