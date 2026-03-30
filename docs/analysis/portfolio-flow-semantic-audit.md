# PortfolioFlow Semantic Audit

_Generated: 2026-03-16_

Reference documents:

- `docs/analysis/portfolio-flow-domain-exploration.md`
- `docs/architecture/domain-model.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/propagation-rules.md`
- `docs/rules/source-rules.md`

Files analyzed:

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgress.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicProgress.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`
- `PoTool.Shared/Metrics/SprintMetricsDto.cs`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`

## Unit Consistency

| Metric | Actual unit | Displayed unit | Naming suggests | Risk |
| --- | --- | --- | --- | --- |
| `CompletedEffort` | **Effort-hours** in `PortfolioDeliveryDto` because `GetPortfolioDeliveryQueryHandler` sums `SprintMetricsProjectionEntity.CompletedPbiEffort`, which is populated from `workItem.Effort` in `SprintDeliveryProjectionService` | Portfolio delivery page renders `pts` and tooltips talk about delivered effort/contribution as if it were a points-like delivery unit | Effort-hours | **High** — computation is effort-hours, UI shows points, and nearby feature cards use story-point values |
| `ThroughputEffort` | **Effort-hours** per sprint because portfolio progress sums `CompletedPbiEffort` | Portfolio progress charts and cards render `pts` | Effort-hours | **High** — outflow is effort-based but visually presented like story points |
| `RemainingEffort` | **Effort-hours** derived from reconstructed total effort minus cumulative selected-range throughput effort | Portfolio progress page shows `pts` and a derived remaining `%` | Effort-hours | **Medium-High** — unit is effort-hours, but the page visually projects it as generic points |
| `AddedEffort` | **Effort-hours of sprint commitment** from `PlannedEffort`, not observed backlog additions | Portfolio progress page shows `pts` and the legend `Added` | Effort-hours added to scope | **High** — unit is effort-hours, but semantics are sprint commitment, not backlog inflow |
| `PlannedEffort` | **Effort-hours of committed sprint items** in sprint projections | Not rendered directly; implied as `AddedEffort` and therefore effectively shown as `pts` in portfolio progress | Effort-hours planned | **High** — the same value is repurposed as inflow even though it is a commitment snapshot |
| `CompletedStoryPoints` | **Canonical PBI story points** from first-Done delivery | Usually treated as points/velocity in sprint metrics and forecasting | Story points | **Low** — unit, naming, and intended usage are aligned |
| `DeliveredScope` | **Canonical story-point scope** in DeliveryTrends (`DoneEffort`, `SprintCompletedEffort`, `DeliveredStoryPoints`) | Usually displayed as `pts`, but often travels through legacy `*Effort` transport names | Generic scope, unit not explicit | **Medium** — canonical meaning is stable, but transport/property names still look effort-based |

### Canonical conclusions from the unit audit

1. Portfolio progress is currently an **effort-hour model rendered as generic points**.
2. DeliveryTrends uses **story-point scope** as its canonical delivery unit.
3. Portfolio delivery mixes both families on one surface:
   - product totals use `CompletedPbiEffort` (**effort-hours**)
   - top feature contributors use `SprintCompletedEffort` from `FeatureProgressDto` (**story-point scope**)
4. `CompletedEffort` is therefore not one stable portfolio unit today.

## Flow Model Analysis

### What currently represents throughput

`ThroughputEffort` in `GetPortfolioProgressTrendQueryHandler` is the summed `CompletedPbiEffort` per sprint.

- This is an **outflow proxy**
- unit = effort-hours
- event basis = first-Done delivery inside the sprint window, inherited from sprint projection logic

So the current throughput concept is:

> **effort-hours delivered per sprint**

That is deterministic, but it is not aligned with the domain model's canonical delivery unit, which is PBI story points.

### What currently represents inflow

`AddedEffort` is derived from `SprintMetricsProjectionEntity.PlannedEffort`.

That means it represents:

> **effort-hours committed to a sprint backlog**

It does **not** represent:

- new PBIs entering the product backlog
- true backlog inflow after sprint start
- explicit scope-change events

So the current inflow concept is not a stock/flow event stream. It is a **commitment snapshot reused as inflow**.

### What currently represents scope stock

`TotalScopeEffort` is reconstructed historically by replaying effort and state changes for resolved PBIs/Bugs at each sprint end.

That makes it the closest current stock concept:

> **reconstructed total effort-hours in the resolved portfolio set at sprint end**

Important limitations:

- it is effort-hours, not story-point scope
- it depends on event-ledger coverage
- it excludes `Removed` items but includes done items
- it is reconstructed from current resolved items plus history, not from an authoritative portfolio stock table

### What currently represents remaining scope

`RemainingEffort` is calculated as:

> `TotalScopeEffort - cumulative ThroughputEffort`

This is not a direct measurement of currently open backlog scope.

It is a **derived residual** that depends on:

- reconstructed total effort stock
- cumulative throughput inside the selected range

Because cumulative throughput starts at zero for the selected range, `RemainingEffort` is range-relative rather than a true universal "remaining backlog" quantity.

### Does the implementation truly model stock, inflow, and outflow?

- **Stock:** approximately yes, via `TotalScopeEffort`
- **Outflow:** yes, via `ThroughputEffort`
- **Inflow:** no, only approximately, via `AddedEffort`

So the current implementation is best described as:

> **a partial stock/outflow model with a commitment-based inflow approximation**

It does not yet provide a canonical portfolio flow model with one stable meaning for:

- stock
- inflow
- outflow

`NetFlow = ThroughputEffort - AddedEffort` is therefore deterministic arithmetic, but only an approximate flow semantic.

## Added Scope Analysis

### What `AddedEffort` actually represents today

Today `AddedEffort` is:

> **the sum of `PlannedEffort` from sprint projections for the selected sprint**

That is a sprint commitment metric, not a backlog event metric.

### How it is computed

1. `SprintDeliveryProjectionService` computes `PlannedEffort` as effort-hours of planned sprint items
2. `SprintTrendProjectionService` persists that into `SprintMetricsProjectionEntity`
3. `GetPortfolioProgressTrendQueryHandler` groups projections by sprint
4. `AddedEffort` is the per-sprint sum of those `PlannedEffort` values

### Which semantic category it fits

`AddedEffort` most closely corresponds to:

- **commitment changes / sprint commitment volume**: **yes**
- **backlog inflow**: **no**
- **estimation drift**: **partially**, because re-estimation is embedded in the effort totals
- **projection artifact**: **yes**, because the value is borrowed from stored sprint projections rather than produced by explicit inflow events

### Acceptability as a flow proxy

`AddedEffort` is acceptable only as a **temporary proxy for planned sprint intake**, not as canonical inflow.

Why it is not a safe canonical flow proxy:

1. it measures commitment, not backlog entry
2. it can re-count already-existing work that is merely committed this sprint
3. it can move because of effort re-estimation, not only because of scope change
4. it is effort-hour based, while adjacent delivery semantics increasingly use story-point scope

Canonical finding:

> `AddedEffort` should be treated as a **commitment proxy**, not as portfolio backlog inflow.

## Progress Semantics

| Term | Current meaning | Unit base | Notes |
| --- | --- | --- | --- |
| `PercentDone` | `cumulative selected-range ThroughputEffort / TotalScopeEffort * 100` in portfolio progress | effort-hours | Scope-completion-style ratio, but range-relative and effort-based |
| `ProgressPercent` | `DeliveredStoryPoints / TotalScopeStoryPoints * 100`, capped at 90 until parent state is canonically Done | story points | Canonical DeliveryTrends completion ratio for features and epics |
| `CompletionPercent` | No matching production symbol was found in the inspected portfolio-flow files | n/a | The term is currently undefined in this slice and would introduce a new ambiguity if used without definition |
| `RemainingRatio` | `RemainingEffort / TotalScopeEffort * 100` computed in `PortfolioProgressPage.razor` | effort-hours | UI-only inverse-style ratio derived from the same effort model as `PercentDone` |

### Inconsistencies

1. `PercentDone` and `RemainingRatio` are both derived from the **portfolio effort reconstruction model**.
2. `ProgressPercent` is a **DeliveryTrends story-point completion ratio** with explicit parent-state capping rules.
3. `CompletionPercent` is not an established term in the inspected portfolio surface, so it has no canonical meaning today.
4. The portfolio delivery summary introduces an additional semantic collision:
   - `AverageProgressPercent` is not a completion ratio
   - it is the average of aggregated `ProgressionDelta` values
   - `ProgressionDelta` means sprint movement, not current completion state

Canonical conclusions:

- `PercentDone` = **effort-based portfolio scope digestion across the selected range**
- `ProgressPercent` = **story-point completion of feature/epic scope**
- `RemainingRatio` = **effort-based remaining share of the same reconstructed stock**
- `CompletionPercent` = **currently undefined in this slice**

These terms are not interchangeable.

## Aggregation Rules

### Portfolio progress

Portfolio progress is **not** a pure sum of upstream product metrics.

It combines:

1. **summed projection values** for:
   - `ThroughputEffort`
   - `AddedEffort`
2. **reconstructed cross-product stock** for:
   - `TotalScopeEffort`
   - `RemainingEffort`
3. **derived summary formulas** for:
   - `CumulativeNetFlow`
   - `TotalScopeChangePts`
   - `RemainingEffortChangePts`

So portfolio progress is a **reconstructed stock/flow model built partly from projections and partly from replayed history**.

### Portfolio delivery

Portfolio delivery is mostly a **sum of product metrics plus presentation aggregation**.

It:

- sums `SprintMetricsProjectionEntity` rows by product
- computes product shares from those sums
- averages per-product `ProgressionDelta` values into `AverageProgressPercent`
- pulls top feature contributors from `SprintTrendProjectionService.ComputeFeatureProgressAsync`

That means the delivery page is not one reconstructed portfolio model. It is a **composition view over upstream delivery outputs**.

### Does portfolio logic duplicate upstream DeliveryTrends logic?

Yes, partially.

What is already owned upstream:

- feature progress semantics
- epic progress semantics
- delivered scope rollups
- product progress summarization logic in `DeliveryProgressSummaryCalculator`

What portfolio delivery adds locally:

- grouping persisted sprint projections by product
- portfolio summary card totals
- contribution percentages for products and features

The duplication risk is highest where portfolio delivery recomputes portfolio-level summaries instead of reusing one canonical upstream product summary.

### Most important aggregation contradiction

`PortfolioDelivery` mixes product effort totals with feature story-point totals.

- product contribution percentages use `CompletedEffort` sourced from `CompletedPbiEffort` (**effort-hours**)
- top feature contribution percentages use `SprintCompletedEffort` from `FeatureProgressDto` (**story-point scope**)
- feature `EffortShare` divides story-point numerators by an effort-hour denominator

That means the current feature share percentages are not based on one canonical unit.

Canonical conclusion:

> Portfolio metrics do **not** currently represent one clean weighted aggregation. They are a blend of summed product metrics, reconstructed stock logic, and application-level presentation aggregation.

## CDC Readiness

### Consistent units

No.

- portfolio progress uses effort-hours
- DeliveryTrends canonical delivery uses story points
- portfolio delivery mixes both on the same page

### Stable flow semantics

No.

- outflow is reasonably stable
- stock is approximate but understandable
- inflow is only a commitment proxy
- remaining scope is derived from selected-range throughput rather than directly observed open stock

### Deterministic formulas

Mostly yes.

The arithmetic is deterministic, but deterministic formulas alone are not enough for CDC extraction when the domain meanings are unstable.

### Classification

Classification: **Needs semantic correction**

Reason:

1. units are not consistent across portfolio progress and portfolio delivery
2. `AddedEffort` is not canonical inflow
3. progress terms are overloaded across effort-based digestion, story-point completion, and sprint progression
4. portfolio delivery still reads primarily as application aggregation layered on top of DeliveryTrends

### Final readiness statement

A canonical `PortfolioFlow` model could likely be defined in the future, but the current implementation is **not ready for CDC extraction as-is**.

Recommended interpretation:

- **portfolio stock/flow** is the only promising semantic core
- it first needs explicit correction of:
  - unit choice (effort-hours vs story points)
  - inflow definition
  - remaining-scope definition
- **portfolio delivery distribution** should remain application logic until a single canonical aggregation contract exists

Final recommendation:

> The current portfolio surface should be classified as **Needs semantic correction** before any CDC extraction. A narrower future PortfolioFlow CDC may be viable, but the present portfolio delivery aggregation should remain application logic.
