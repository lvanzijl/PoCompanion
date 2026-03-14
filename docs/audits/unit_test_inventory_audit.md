# PoTool Unit Test Inventory Audit

## Summary
- Baseline verification on 2026-03-14:
  - `dotnet restore PoTool.sln` ✅
  - `dotnet build PoTool.sln --no-restore` ✅
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build -v minimal` ❌ — the suite currently discovers **1,176 runnable tests** and finishes with **36 existing failures** outside this audit task. The failures include `GetSprintTrendMetricsQueryHandlerTests`, `RealTfsClientVerificationTests`, `WorkItemAncestorCompletionTests`, `WorkItemSelectionServiceTests`, and `MockDataValidatorTests`.
- Source inventory used for classification: **133 test files**, **1,160 explicit test methods**, **36,900 lines** of unit-test code.
- Tests per architectural layer by primary file intent (source-method counts):
  - **CDC/domain service tests:** 23
  - **handler/orchestration tests:** 304
  - **projection/regression tests:** 106
  - **mapper tests:** 7
  - **utility/helper tests:** 720
- Tests per business importance category by primary file intent (source-method counts):
  - **critical domain semantics:** 154
  - **important orchestration:** 701
  - **low-risk plumbing:** 196
  - **cosmetic or low-value assertions:** 109

### Audit readout
The suite is broad, but it is **not centered on isolated CDC tests**. Only about two dozen test methods directly target the new CDC/domain services, while the strongest protection for sprint analytics currently comes from **handler** and **projection regression** coverage—especially `SprintTrendProjectionServiceTests`, `GetSprintMetricsQueryHandlerTests`, `GetSprintExecutionQueryHandlerTests`, and `GetEpicCompletionForecastQueryHandlerTests`.

That distribution is acceptable for regression safety, but it means the most important rules are still more protected by **consuming-path tests** than by small direct CDC tests. The biggest audit signal is therefore not “missing coverage everywhere”; it is **coverage concentrated one layer too high**.

## Critical Domain Coverage

### Covered well
- **Sprint commitment reconstruction**
  - Direct lookup coverage exists in `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`.
  - Consuming-path regression exists in `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`, `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`, and `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`.
- **First-done delivery**
  - Direct lookup coverage exists in `HistoricalSprintLookupTests.cs`.
  - Reopen/second-Done scenarios are covered in `GetSprintMetricsQueryHandlerTests.cs`, `GetSprintExecutionQueryHandlerTests.cs`, and `SprintTrendProjectionServiceTests.cs`.
- **Spillover detection**
  - Direct lookup coverage exists in `HistoricalSprintLookupTests.cs`.
  - Regression coverage exists in `GetSprintExecutionQueryHandlerTests.cs` and `SprintTrendProjectionServiceTests.cs`, including “directly to next sprint” and “backlog round-trip is not spillover”.
- **Story point resolution**
  - `PoTool.Tests.Unit/Services/CanonicalStoryPointResolutionServiceTests.cs` directly covers real story points, `BusinessValue` fallback, missing estimates, zero-on-Done, zero-on-non-Done, derived estimates, and parent fallback.
- **BusinessValue fallback**
  - Directly covered in `CanonicalStoryPointResolutionServiceTests.cs`.
  - Also exercised through `GetSprintMetricsQueryHandlerTests.cs` and `GetEpicCompletionForecastQueryHandlerTests.cs`.
- **Zero-on-Done rule**
  - Directly covered in `CanonicalStoryPointResolutionServiceTests.cs`.
  - Also exercised at handler level in `GetSprintMetricsQueryHandlerTests.cs`.
- **Sprint execution formulas**
  - Directly covered in `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`.
  - Reinforced through `GetSprintExecutionQueryHandlerTests.cs`.
- **Hierarchy rollups**
  - Directly covered in `PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs`.
  - Reinforced through `GetEpicCompletionForecastQueryHandlerTests.cs` and `SprintTrendProjectionServiceTests.cs`.
- **Bug/task exclusion**
  - Directly covered in `HierarchyRollupServiceTests.cs` and `CanonicalStoryPointResolutionServiceTests.cs`.
  - Reinforced in `GetSprintMetricsQueryHandlerTests.cs`, `GetSprintExecutionQueryHandlerTests.cs`, and `GetEpicCompletionForecastQueryHandlerTests.cs`.

### Partially covered
- **Canonical state classification**
  - There is direct lookup coverage in `HistoricalSprintLookupTests.cs` and boundary mapping coverage in `StateClassificationInputMapperTests.cs`.
  - `WorkItemStateClassificationServiceTests.cs` is much more focused on caching and invalidation than on classification semantics breadth, so the canonical-state rule is covered, but not deeply stress-tested.
- **Derived estimates**
  - Covered for the normal sibling-average path and fractional preservation.
  - Missing edge cases include “all siblings missing”, “only bugs/tasks under the feature”, and other malformed-hierarchy cases.
- **Timeline reconstruction edge behavior**
  - The historical lookup tests prove the main happy paths.
  - There is little direct evidence for tie-break ordering, duplicate updates, whitespace-normalization oddities, or timestamp-collision cases.

### Missing
- **No listed critical rule is completely untested.**
- The missing protection is mostly **edge-case depth**, not outright absence:
  - event ordering collisions in historical replay
  - broader canonical-state default/negative cases
  - ingestion edge cases before replay feeds downstream metrics

## Layer Distribution

### CDC/domain
**Current weight: 23 direct tests across 4 focused files**

Primary files:
- `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`
- `PoTool.Tests.Unit/Services/CanonicalStoryPointResolutionServiceTests.cs`
- `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`
- `PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs`

Assessment:
- The direct CDC tests are **high-value and well targeted**.
- The main weakness is volume: this layer is small relative to the business importance of the rules it owns.
- Result: CDC correctness is protected more by downstream regression tests than by a large isolated domain suite.

### Handlers
**Current weight: 304 tests**

Most relevant files:
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`

Assessment:
- This is the strongest dedicated bucket after helpers.
- The handler layer is doing a large share of the semantic protection work for sprint analytics.
- That is good for regression safety, but it also means handler tests are compensating for a relatively thin direct CDC layer.
- One important caveat: `GetSprintTrendMetricsQueryHandlerTests` currently fail during setup, so that class is present as intended coverage but currently weakened as an executable guardrail.

### Projections
**Current weight: 106 tests**

Primary files:
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` (dominant concentration)
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
- `PoTool.Tests.Unit/Services/BacklogStateComputationServiceTests.cs`

Assessment:
- Projection coverage is the most meaningful regression shield for CDC semantics in the current suite.
- `SprintTrendProjectionServiceTests.cs` is especially valuable because it covers delivery attribution, spillover, canonical Done mapping, derived-story-point diagnostics, feature progress, and hierarchy propagation through realistic consuming paths.
- This area is **well invested** and should stay that way.

### Mappers
**Current weight: 7 tests**

Files:
- `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`
- `PoTool.Tests.Unit/Services/StateClassificationInputMapperTests.cs`

Assessment:
- Mapper coverage is thin, but the mapper layer is intentionally small.
- The tests hit the most important boundary concerns: trimming/normalization, timestamp fallback, and transport-to-domain conversion.
- This is sufficient for now unless the boundary contracts change again.

### Helpers
**Current weight: 720 tests**

Representative groups:
- high-value helper services: `WorkItemStateClassificationServiceTests.cs`, `ActivityEventIngestionServiceTests.cs`, `PullRequestMetricsServiceTests.cs`, `PipelineInsightsCalculatorTests.cs`, `RoadmapAnalyticsServiceTests.cs`, `BugInsightsCalculatorTests.cs`
- plumbing-heavy helpers: cache/read-provider tests, repository tests, TFS client tests, configuration tests, middleware tests, UI helper tests

Assessment:
- This is by far the largest bucket.
- Some of that is justified because many helper services contain orchestration and diagnostics logic.
- But a noticeable share of total test effort is also concentrated in lower-risk infrastructure and presentation-adjacent utilities, while the CDC itself remains comparatively lean.

## Risk Map

| Component | Importance | Current test strength | Recommendation |
| --- | --- | --- | --- |
| Sprint history lookups (`SprintCommitmentLookup`, `FirstDoneDeliveryLookup`, `SprintSpilloverLookup`, `StateReconstructionLookup`, `StateClassificationLookup`) | Critical | **Moderate** — direct coverage exists in `HistoricalSprintLookupTests.cs`, plus strong handler/projection regression, but edge cases are sparse | **Expand** |
| `CanonicalStoryPointResolutionService` | Critical | **Strong** — direct tests cover precedence, `BusinessValue` fallback, zero-on-Done, derived estimates, and missing estimates | **Keep** |
| `SprintExecutionMetricsCalculator` | Critical | **Strong** — formulas are directly tested and reinforced by handler coverage | **Keep** |
| `HierarchyRollupService` | Critical | **Strong** — direct coverage plus forecast/projection consuming-path coverage | **Keep** |
| `SprintTrendProjectionService` | Critical | **Very strong** — this is the main semantic regression net for CDC behavior | **Keep** |
| `GetSprintMetricsQueryHandler` | High | **Strong** — good orchestration tests for historical commitment, first-done semantics, fallback estimates, and bug/task exclusion | **Keep** |
| `GetSprintExecutionQueryHandler` | High | **Strong** — good orchestration tests for added/removed scope, spillover, canonical rates, and exclusions | **Keep** |
| `GetSprintTrendMetricsQueryHandler` | High | **Weak in practice** — tests exist, but the current suite shows setup failures, so executable protection is degraded | **Review** |
| `GetEpicCompletionForecastQueryHandler` | High | **Strong** — good forecast coverage around fallback, derived estimates, hierarchy, and bug/task exclusion | **Keep** |
| Mapper adapters (`HistoricalSprintInputMapper`, `StateClassificationInputMapper`) | High boundary importance | **Adequate** — thin but precise | **Keep** |
| `WorkItemStateClassificationService` | High | **Moderate** — coverage is real but skewed toward caching behavior more than canonical-semantic breadth | **Review** |
| `ActivityEventIngestionService` | High | **Weak** — only 2 tests for a feed that influences replay-driven analytics | **Expand** |
| Generic infrastructure helpers (cache, TFS client, repositories, config, middleware, UI helpers) | Low to medium | **Generally sufficient** — broad coverage already exists | **Reduce** further growth unless bug-driven |

## Recommended Next Actions

### Expand
- Add direct edge-case tests for the sprint-history replay helpers:
  - timestamp collisions
  - duplicate update IDs / event IDs
  - missing-history fallback to current snapshot values
  - whitespace/case normalization edge cases
- Expand `ActivityEventIngestionServiceTests.cs` beyond idempotency/backfill to cover ordering, duplicate updates, and multi-field update behavior.
- Add a small second wave of `CanonicalStoryPointResolutionServiceTests.cs` for malformed estimate scenarios:
  - all siblings missing
  - only bug/task siblings
  - parent fallback after excluded children

### Keep
- Keep the current direct CDC tests.
- Keep the heavy regression investment in `SprintTrendProjectionServiceTests.cs`.
- Keep the sprint metrics/execution/forecast handler tests, because they are currently the strongest protection for end-to-end CDC consumption.
- Keep the mapper tests as a thin boundary contract suite.

### Reduce
- Avoid adding more low-risk tests in already saturated areas unless a bug justifies them:
  - cache/read-provider infrastructure
  - generic TFS client request wiring
  - simple UI helper or route/assertion tests
- The suite already spends much more test effort on helper/plumbing inventory than on isolated CDC rules.

### Review
- Review and repair the currently failing `GetSprintTrendMetricsQueryHandlerTests` so the intended handler coverage is executable again.
- Review `WorkItemStateClassificationServiceTests.cs` to decide whether more semantic assertions should replace part of the current cache-heavy emphasis.
- Review whether some lower-value helper/UI assertions are still buying enough business protection to justify future expansion.
