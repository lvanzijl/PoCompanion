# Effort Diagnostics Semantic Audit

## Summary

The effort-diagnostics area does not currently present one stable semantic model.

The strongest contradictions are:
- `GetEffortConcentrationRiskQuery` exposes `ConcentrationThreshold`, and the feature spec documents it as caller-configurable, but the handler ignores it and uses fixed `25/40/60/80` bands instead (`PoTool.Core/Metrics/Queries/GetEffortConcentrationRiskQuery.cs:11-15`, `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs:39-42`, `:213-223`, `features/effort_distribution_analytics.md:67-72`, `:401-407`).
- `GetEffortEstimationQualityQuery` and `EffortEstimationQualityDto` describe estimate-vs-actual accuracy, but the handler never compares an estimate with an actual. It filters completed items with `Effort > 0` and converts coefficient of variation into an "accuracy" score, so the implemented meaning is effort consistency, not estimate accuracy (`PoTool.Core/Metrics/Queries/GetEffortEstimationQualityQuery.cs:7-13`, `PoTool.Shared/Metrics/EffortEstimationQualityDto.cs:3-35`, `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs:82-117`, `:144-150`, `:180-183`, `:216-218`).
- Domain rules define effort as estimated implementation hours, but multiple effort-diagnostics handlers, DTO-facing strings, and settings comments describe the same field as "points" or use Fibonacci defaults that read like story-point scales (`docs/rules/estimation-rules.md:70-80`, `features/effort_distribution_analytics.md:59-60`, `:260-260`, `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs:287`, `:307`, `:327`, `:358`, `:373`, `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs:147`, `:189`, `:279`, `:299`, `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:320`, `:324`, `PoTool.Shared/Settings/EffortEstimationSettingsDto.cs:18-29`).
- `GetSprintCapacityPlanQueryHandler` returns a DTO that looks like real per-person sprint capacity planning, but the implementation groups every item under the literal placeholder `"Team Member"`, leaves sprint dates null, and applies a default capacity to that placeholder rather than to actual assignees (`PoTool.Shared/Metrics/SprintCapacityPlanDto.cs:3-38`, `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs:80-103`, `:106-130`).

Overall, only effort imbalance and most of effort concentration look close to stable rule families. Estimation quality, estimation suggestions, and sprint capacity planning are still heuristic or operational, and capacity calibration is stable but belongs to sprint/story-point analytics rather than effort-hour diagnostics.

## Contract vs Implementation Drift

### GetEffortImbalanceQuery

**Contract meaning**  
Detect effort imbalance across teams and sprints, with configurable area filtering, iteration depth, default capacity context, and a caller-provided imbalance threshold (`PoTool.Core/Metrics/Queries/GetEffortImbalanceQuery.cs:7-16`). The feature spec presents this as imbalance detection based on mean and "standard deviation," with risk bands of `<30 / 30-50 / 50-80 / >80` (`features/effort_distribution_analytics.md:288-305`).

**Actual implementation**  
The handler filters to `Effort > 0`, limits iterations by lexicographically descending `IterationPath`, calculates deviation as `abs(actual - average) / average`, and classifies per-group risk with threshold multipliers (`threshold`, `1.5x`, `2.5x`) rather than the fixed bands documented in the DTO enum comments and feature file (`PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs:78-129`, `:141-176`, `:179-221`, `:224-234`). `DefaultCapacityPerIteration` is only used to append utilization text to sprint descriptions, not to change imbalance classification (`:203-207`, `:366-383`).

**Mismatch description**  
- The query parameters are all used, but `DefaultCapacityPerIteration` is descriptive only.
- The feature spec says imbalance uses standard deviation; the handler does not use standard deviation anywhere in this calculation (`features/effort_distribution_analytics.md:288-305`).
- `ImbalanceRiskLevel` XML comments document fixed bands `30/50/80`, but per-group handler logic uses threshold-relative bands, which with the default threshold become `30/45/75` instead (`PoTool.Shared/Metrics/EffortImbalanceDto.cs:41-50`, `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs:224-234`).
- Overall risk/score are calculated only from already-flagged medium+ groups because low-risk rows are filtered out before aggregation, so "overall" is really "overall among flagged imbalances," not the full distribution (`PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs:174-176`, `:219-221`, `:236-265`).

**Severity**  
Medium.

### GetEffortConcentrationRiskQuery

**Contract meaning**  
Identify concentration risk in effort distribution with caller-provided area filtering, iteration depth, and a configurable `ConcentrationThreshold` defaulting to `0.5` (`PoTool.Core/Metrics/Queries/GetEffortConcentrationRiskQuery.cs:7-15`). The feature spec repeats the threshold as a public query parameter (`features/effort_distribution_analytics.md:67-72`, `:401-407`).

**Actual implementation**  
The handler logs `ConcentrationThreshold` but never uses it in either area-level or iteration-level analysis. Risk classification is hard-coded to `25/40/60/80`, and the overall concentration index is described as HHI but is computed only from already-flagged groups because `None` rows are removed before the HHI-style calculation runs (`PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs:39-42`, `:97-121`, `:133-169`, `:171-211`, `:213-257`).

**Mismatch description**  
- `ConcentrationThreshold` is a false public contract today: callers can send it, but it has no effect.
- DTO comments and feature documentation imply a whole-distribution concentration index, but the handler excludes every `<25%` group before the HHI-style calculation, so the index is not a true portfolio-wide HHI (`PoTool.Shared/Metrics/EffortConcentrationRiskDto.cs:3-39`, `features/effort_distribution_analytics.md:329-346`, `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs:166-168`, `:208-210`, `:225-257`).
- `TopWorkItems` always contains up to five formatted strings, while the feature spec presents this family primarily as bucket-level concentration analysis; that is presentation shaping, not part of the query contract.

**Severity**  
High.

### GetEffortEstimationQualityQuery

**Contract meaning**  
Get effort estimation quality by comparing historical estimates vs actuals to measure estimation accuracy (`PoTool.Core/Metrics/Queries/GetEffortEstimationQualityQuery.cs:7-13`, `PoTool.Shared/Metrics/EffortEstimationQualityDto.cs:3-35`).

**Actual implementation**  
The handler filters completed items with `Effort > 0`, groups them by work item type and iteration, calculates variance and coefficient of variation over the observed effort values, and converts low variation into higher "accuracy" (`PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs:82-123`, `:125-165`, `:167-200`, `:202-235`). The time trend uses `RetrievedAt` as its date range, not estimate date, actual effort date, sprint dates, or completion dates (`:91-100`, `:185-193`).

**Mismatch description**  
- No estimate-vs-actual comparison exists anywhere in the implementation; only one numeric field (`Effort`) is analyzed.
- `AverageEstimationAccuracy`, `AverageAccuracy`, and `WorkItemsWithEstimates` are semantically misleading. In practice, the code returns effort consistency of completed items, and `WorkItemsWithEstimates` always equals `TotalCompletedWorkItems` because the handler prefilters to `Effort > 0` before constructing the DTO (`PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs:82-91`, `:111-116`).
- `MaxIterations` does not mean "last N sprint windows"; it means the last N iteration groups by max `RetrievedAt`, which is operational cache timing rather than canonical sprint chronology.
- No feature-level documentation in `features/` defines this rule family, so the misleading XML comments are currently the main semantic source.

**Severity**  
High.

### GetEffortEstimationSuggestionsQuery

**Contract meaning**  
Provide ML-based or heuristic-based effort suggestions for work items without effort estimates, filtered optionally by iteration, area, and in-progress status (`PoTool.Core/Metrics/Queries/GetEffortEstimationSuggestionsQuery.cs:7-15`). Settings supply default estimates per work item type (`PoTool.Shared/Settings/EffortEstimationSettingsDto.cs:3-49`).

**Actual implementation**  
The handler uses purely local heuristics: it finds unestimated work items, optionally filters by exact iteration and area prefix, narrows to literal `"In Progress"` or `"Active"` states when requested, finds completed historical items with `Effort > 0`, scores similarity by type/area/title, takes the top five matches, returns the median effort, and computes confidence from sample size plus variance (`PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:82-139`, `:141-234`, `:281-314`).

**Mismatch description**  
- All query parameters are used.
- The "ML" part of the contract is overstated; the implementation is a deterministic heuristic scorer with hard-coded weights and no learning model.
- `OnlyInProgressItems` uses literal workflow states for inclusion, while the same handler uses the state-classification service for completed historical samples. That splits workflow semantics inside one query (`PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:102-108`, `:112-120`).
- Default values and rationale strings describe effort as "points," which conflicts with the domain rule that effort is implementation hours (`docs/rules/estimation-rules.md:70-80`, `PoTool.Shared/Settings/EffortEstimationSettingsDto.cs:18-29`, `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:320-324`).

**Severity**  
Medium.

### GetSprintCapacityPlanQuery

**Contract meaning**  
Get sprint capacity planning analysis for a specific iteration, including total planned effort, total capacity, utilization, status, team capacities, warnings, and sprint timing (`PoTool.Core/Metrics/Queries/GetSprintCapacityPlanQuery.cs:7-13`, `PoTool.Shared/Metrics/SprintCapacityPlanDto.cs:3-38`, `PoTool.Api/Controllers/MetricsController.cs:177-218`).

**Actual implementation**  
The handler filters work items to one `IterationPath`, sums `Effort`, and then creates "team capacities" by grouping every item under the literal key `"Team Member"`. It uses `defaultCapacity ?? 40`, leaves `StartDate` and `EndDate` null, and raises warnings from the synthetic aggregate (`PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs:67-103`, `:106-130`, `:133-201`).

**Mismatch description**  
- The query parameter is used, but the DTO shape promises more realism than the handler provides.
- `TeamCapacities` looks like per-member data, yet the implementation intentionally collapses everything to one placeholder entry.
- Controller text says `defaultCapacity` is "per person," but the effort-distribution feature spec talks about default capacity per iteration. Capacity semantics are therefore split even before considering the placeholder grouping (`features/effort_distribution_analytics.md:67-72`, `PoTool.Api/Controllers/MetricsController.cs:181-203`).
- No feature documentation or focused tests in scope define what "capacity plan" should mean here, so the current handler reads as provisional application logic rather than a canonical domain contract.

**Severity**  
High.

## Terminology Conflicts

- **effort vs story points**  
  Domain rules say effort is estimated implementation hours (`docs/rules/estimation-rules.md:70-80`). The effort-distribution feature spec collapses the concept into `Effort/Story Points` (`features/effort_distribution_analytics.md:59-60`, `:260-260`), and multiple handlers format effort as `points` in user-facing descriptions (`PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs:287`, `:307`, `:327`, `:358`, `:373`, `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs:147`, `:189`, `:279`, `:299`, `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:320`, `:324`).

- **estimate vs actual vs accuracy**  
  The estimation-quality query and DTO say "estimate vs actual" and "accuracy," but the handler has no actual-value field. It computes consistency of existing effort values, so `accuracy` currently means low coefficient of variation, not closeness to any ground truth (`PoTool.Core/Metrics/Queries/GetEffortEstimationQualityQuery.cs:7-13`, `PoTool.Shared/Metrics/EffortEstimationQualityDto.cs:3-35`, `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs:144-150`).

- **capacity**  
  The same word refers to at least three different things:
  1. descriptive utilization context in effort imbalance (`DefaultCapacityPerIteration`) (`PoTool.Core/Metrics/Queries/GetEffortImbalanceQuery.cs:11-16`, `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs:366-383`)
  2. synthetic per-person sprint effort capacity in `GetSprintCapacityPlanQuery` (`PoTool.Core/Metrics/Queries/GetSprintCapacityPlanQuery.cs:10-13`, `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs:106-130`)
  3. story-point velocity and predictability calibration in `GetCapacityCalibrationQueryHandler` (`PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs:13-27`, `:100-178`).

- **utilization**  
  In the imbalance flow, utilization is optional descriptive text only. In sprint capacity planning, utilization drives the main status classification. In trend/distribution views, utilization is grid/chart decoration. The same label therefore spans descriptive, diagnostic, and decision-making roles.

- **variance / confidence**  
  Suggestion confidence is sample-size-plus-variance based (`PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:305-314`). Trend confidence is coefficient-of-variation based (`PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs:251-264`). Epic forecast confidence is only sprint-count based (`PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs:228-237`). The same word does not map to one shared semantic model.

## Duplicate Statistical Logic

- **Variance**  
  The same population-variance formula appears in:
  - `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs:227-235`
  - `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:295-303`

- **Coefficient of variation**  
  The same `sqrt(variance) / mean` idea appears in:
  - estimation quality, where it becomes `accuracy` (`PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs:146-150`, `:181-183`, `:216-218`)
  - effort distribution trend, where it becomes `confidence` and `volatile` classification (`PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs:256-264`, `:298-303`)

- **Median / percentile central-tendency logic**  
  Literal median exists in estimation suggestions (`PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:178-180`, `:281-293`). Adjacent capacity calibration computes P50 through a generic percentile helper (`PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs:145-151`, `:181-196`). This is not copy-paste duplication, but it is overlapping statistical ownership.

- **Confidence scoring**  
  Confidence is separately implemented in at least three incompatible ways:
  - suggestions: sample size + variance average (`PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs:305-314`)
  - trend forecasts: coefficient-of-variation buckets (`PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs:251-264`)
  - epic forecast: sprint-count buckets (`PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs:228-237`)

## Rule Stability Assessment

- **EffortImbalance** — **Stable domain rule with some presentation/documentation drift**  
  The core calculation is explicit and repeatable: bucket effort, compute deviation from mean, classify severity, and generate rebalance advice. The main instability is not the rule itself but the surrounding wording and band documentation.

- **EffortConcentration** — **Stable domain rule with contract drift**  
  Share-of-total concentration and HHI-style indexing are coherent domain ideas. The instability comes from the unused `ConcentrationThreshold` and from computing the concentration index on already-filtered groups instead of the full distribution.

- **EstimationQuality** — **Heuristic rule**  
  The current implementation is a statistical consistency heuristic over completed effort values. Because the public contract calls it estimate-vs-actual accuracy, the family is not semantically stable yet.

- **EstimationSuggestions** — **Heuristic rule**  
  The whole family is hard-coded similarity scoring, median selection, and fallback defaults. It is valuable, but it is not currently justified by canonical domain rules.

- **SprintCapacityPlanning** — **Operational / workflow rule**  
  Today it is mostly application-side workflow logic over one iteration, synthetic team-member grouping, and warning thresholds. It is not grounded in a real assignee/capacity domain model yet.

- **CapacityCalibration** — **Stable domain rule, but outside this effort-diagnostics slice**  
  It is built on canonical sprint projections, delivered story points, committed story points, percentile bands, and hours-per-story-point diagnostics (`PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs:13-27`, `:98-178`). That makes it stable, but semantically it belongs to sprint analytics and planning calibration rather than raw effort-distribution diagnostics.

## Recommended Canonical Semantics

- **EffortImbalance**  
  Canonical meaning should be: _distribution imbalance of observed effort across area path buckets and iteration buckets, measured as deviation from the mean_. Capacity is optional explanatory context, not part of the imbalance formula.

- **EffortConcentration**  
  Canonical meaning should be: _share-of-total effort concentration by area path and iteration, with a concentration index derived from the full portfolio distribution_. If thresholds remain configurable, they must actually drive classification; otherwise the parameter should be removed.

- **EstimationQuality**  
  Canonical meaning today is not estimate-vs-actual accuracy. The implemented meaning is: _consistency of completed effort values within work-item-type and iteration groupings_. That should be the canonical description unless a separate actuals model is introduced later.

- **EstimationSuggestions**  
  Canonical meaning should be: _heuristic effort suggestions for currently unestimated items, based on similar completed items plus configured defaults when history is absent_. This should stay explicitly heuristic.

- **SprintCapacityPlanning**  
  Canonical meaning today is only: _iteration-level effort load versus assumed capacity thresholds_. It is not yet true team-member capacity planning because no assignee-based capacity model is present.

- **CapacityCalibration**  
  Canonical meaning should remain: _story-point capacity calibration from historical sprint delivery, predictability, and hours-per-story-point diagnostics_. It should be treated as a separate sprint analytics family.

## Extraction Readiness

Pieces that could safely move into a CDC slice today:
- **EffortImbalance core formulas**: mean-based deviation, threshold classification, and overall score logic.
- **EffortConcentration core formulas**: share-of-total calculation and concentration classification, but only after resolving the unused threshold parameter and deciding whether the index must use the full distribution.
- **CapacityCalibration** could move into a CDC-style slice today, but it should move with sprint analytics/planning semantics, not with effort diagnostics.

Pieces that should not move yet:
- **EstimationQuality** until its name/comments/DTO semantics are aligned with what the code actually computes.
- **EstimationSuggestions** until the team explicitly accepts the similarity weights, median strategy, variance/confidence rules, and hours-vs-points terminology.
- **SprintCapacityPlanning** until a real assignee/capacity model exists and the DTO no longer implies per-member precision that the handler does not have.

Net assessment: a narrow CDC extraction for **EffortImbalance + EffortConcentration** is feasible after a semantic cleanup pass. The rest of the effort-diagnostics area still needs naming and rule stabilization before extraction.
