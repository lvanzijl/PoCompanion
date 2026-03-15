# Statistical Helper Audit

## Summary
- Duplicated helper families still exist after the stable EffortDiagnostics core was introduced in `PoTool.Core/Metrics/EffortDiagnostics`.
- The most important exact-duplicate pure-math family is **population variance** in the estimation handlers; the most important broader duplication family is **percentile/median** logic spread across metrics, PR, pipeline, and client calculators.
- Semantically different helpers were also found:
  - `confidence` means three different things depending on the slice
  - `accuracy` sometimes means statistical consistency and sometimes means data completeness
  - `percentile` uses both **linear interpolation** and **nearest-rank** algorithms
  - `utilization` is sometimes descriptive context and sometimes a status-driving decision metric
- The safest candidates for a broader shared statistical core are reusable pure math helpers only:
  - percentile/quantile (after one repository-wide semantic decision)
  - population variance / standard deviation
  - a single agreed median contract for sorted vs unsorted inputs and empty-sample behavior
- The following logic should stay slice-specific unless a later audit proves otherwise:
  - EffortDiagnostics weighted deviation and concentration-index rules
  - confidence scoring families
  - utilization status bands
  - trend regression and slope interpretation

## Inventory of Statistical Logic

### Stable reference baseline

These are the current reference implementations for the stable EffortDiagnostics subset and are the baseline used for A/B/C/D classification below.

| File | Class | Method | Concept | Local or centralized | Classification | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs` | `EffortDiagnosticsStatistics` | `Mean`, `DeviationFromMean`, `ShareOfTotal`, `Median`, `Variance`, `CoefficientOfVariation`, `HHI` | Stable EffortDiagnostics statistical primitives | Centralized stable core | Reference baseline | Repository issue statement identifies this folder as the stable statistical core. |
| `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs` | `EffortDiagnosticsStatistics` | `CalculateWeightedDeviationScore` | Weighted deviation score | Centralized stable-slice helper | Reference baseline | Stable EffortDiagnostics-only portfolio scoring helper. |
| `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs` | `EffortDiagnosticsStatistics` | `CalculateNormalizedHerfindahlIndex` | Normalized concentration index | Centralized stable-slice helper | Reference baseline | Stable EffortDiagnostics-only HHI convenience helper. |
| `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsAnalyzer.cs` | `EffortDiagnosticsAnalyzer` | `AnalyzeImbalance`, `AnalyzeConcentration` | Centralized consumption of stable math | Centralized stable consumer | Reference baseline | Uses the stable helpers instead of re-implementing formulas in API handlers. |

### Classified occurrences

| File | Class | Method | Concept | Local or centralized | Classification | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsStatistics.cs` | `CanonicalEffortDiagnosticsStatistics` | `Mean`, `DeviationFromMean`, `ShareOfTotal`, `Median`, `Variance` | Parallel EffortDiagnostics primitives | Parallel centralized implementation | B | Same formulas as the stable core, but duplicated in another layer. |
| `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsStatistics.cs` | `CanonicalEffortDiagnosticsStatistics` | `CoefficientOfVariation`, `Hhi` | Coefficient of variation / HHI | Parallel centralized implementation | B | Same concepts, but the contracts differ: `CoefficientOfVariation` takes `(variance, mean)` and `Hhi` returns raw `Σ(share²)` before later scaling. |
| `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsCanonicalRules.cs` | `EffortImbalanceCanonicalRules` | `ComputeImbalanceScore` | Weighted deviation score | Slice-specific canonical rule | C | Stable EffortDiagnostics domain math, not a broad repository-wide statistic. |
| `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsCanonicalRules.cs` | `EffortConcentrationCanonicalRules` | `ComputeConcentrationIndex` | Normalized HHI concentration index | Slice-specific canonical rule | C | Stable EffortDiagnostics domain math over normalized shares. |
| `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs` | `GetEffortEstimationQualityQueryHandler` | `CalculateVariance` | Population variance | Local helper | A | Same population-variance formula as the stable EffortDiagnostics core. |
| `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs` | `GetEffortEstimationQualityQueryHandler` | inline in `CalculateQualityByType`, `CalculateTrendOverTime`, `CalculateOverallAccuracy` | Coefficient of variation | Local inline formula | B | Same spread formula as the stable core, but it is re-labeled as estimation `accuracy` / consistency. |
| `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` | `GetEffortEstimationSuggestionsQueryHandler` | `CalculateMedian` | Median | Local helper | B | Similar to the stable median, but integer return type truncates even-sample medians. |
| `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` | `GetEffortEstimationSuggestionsQueryHandler` | `CalculateVariance` | Population variance | Local helper | A | Same population-variance formula as the stable EffortDiagnostics core. |
| `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` | `GetEffortEstimationSuggestionsQueryHandler` | `CalculateConfidence` | Confidence scoring | Local helper | C | Sample-size-plus-variance heuristic for suggestion confidence; not general-purpose repository math. |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | `GetEffortDistributionTrendQueryHandler` | `CalculateStandardDeviation` | Population standard deviation | Local helper | D | Pure math helper derived from variance; good future shared-core candidate if semantics are standardized. |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | `GetEffortDistributionTrendQueryHandler` | inline in `GenerateForecasts` | Confidence scoring | Local inline formula | C | Maps coefficient of variation to forecast confidence levels; trend-specific heuristic. |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | `GetEffortDistributionTrendQueryHandler` | inline in `DetermineEffortTrendDirectionFromSlope` | Coefficient of variation / volatility | Local inline formula | B | Same relative-spread idea, but used to classify volatility rather than to report statistical spread directly. |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | `GetEffortDistributionTrendQueryHandler` | `CalculateLinearRegressionSlope` | Trend regression | Local helper | C | Slice-specific forecasting math, not a general reusable repository statistic today. |
| `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` | `GetCapacityCalibrationQueryHandler` | `Percentile` | Percentile / quantile | Local helper | D | Pure reusable percentile math, but there is no single repository-wide percentile contract yet. |
| `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | `GetEpicCompletionForecastQueryHandler` | `DetermineConfidence` | Confidence scoring | Local helper | C | Confidence is driven only by historical sprint-count depth. |
| `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs` | `GetSprintCapacityPlanQueryHandler` | `CalculateTeamCapacities`, `DetermineCapacityStatus` | Utilization percentage and utilization bands | Local helper | C | Operational planning logic tied to sprint-capacity semantics. |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs` | `GetEffortDistributionQueryHandler` | inline in `CalculateEffortByIteration` | Utilization percentage | Local inline formula | C | Uses `totalEffort / defaultCapacity * 100` as descriptive capacity context. |
| `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | `GetPipelineInsightsQueryHandler` | `Median` | Median | Local helper | B | Same median formula family, but assumes pre-sorted doubles and a different empty-input contract. |
| `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | `GetPipelineInsightsQueryHandler` | `Percentile` | Percentile / quantile | Local helper | D | Linear interpolation percentile; duplicated with other handlers and client calculators. |
| `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs` | `GetPrSprintTrendsQueryHandler` | `Median` | Median | Local helper | B | Same median family, but assumes pre-sorted doubles and a PR-specific empty-input contract. |
| `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs` | `GetPrSprintTrendsQueryHandler` | `Percentile` | Percentile / quantile | Local helper | D | Linear interpolation percentile; same family as capacity/pipeline/client calculators. |
| `PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs` | `GetPullRequestInsightsQueryHandler` | `Median` | Median | Local helper | B | Same median family, but nullable/no-data behavior differs from the stable core. |
| `PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs` | `GetPullRequestInsightsQueryHandler` | `Percentile` | Percentile / quantile | Local helper | B | Uses nearest-rank percentile, not the linear interpolation percentile used elsewhere. |
| `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs` | `GetPrDeliveryInsightsQueryHandler` | `Median` | Median | Local helper | B | Same median family, but nullable/no-data behavior differs from the stable core. |
| `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs` | `GetPrDeliveryInsightsQueryHandler` | `Percentile` | Percentile / quantile | Local helper | B | Uses nearest-rank percentile, not the linear interpolation percentile used elsewhere. |
| `PoTool.Client/Services/PullRequestInsightsCalculator.cs` | `PullRequestInsightsCalculator` | `CalculateMedian` | Median | Local helper | B | Client-side duplicate of the PR median family with nullable/no-data semantics. |
| `PoTool.Client/Services/PullRequestInsightsCalculator.cs` | `PullRequestInsightsCalculator` | `CalculatePercentile` | Percentile / quantile | Local helper | D | Linear interpolation percentile duplicated from server-side families. |
| `PoTool.Client/Services/PipelineInsightsCalculator.cs` | `PipelineInsightsCalculator` | `CalculateMedian` | Median | Local helper | B | Client-side duplicate of the pipeline median family with nullable/no-data semantics. |
| `PoTool.Client/Services/PipelineInsightsCalculator.cs` | `PipelineInsightsCalculator` | `CalculatePercentile` | Percentile / quantile | Local helper | D | Linear interpolation percentile duplicated from server-side families. |
| `PoTool.Client/Services/BugInsightsCalculator.cs` | `BugInsightsCalculator` | `CalculateMedian` | Median | Local helper | B | Client-side duplicate of the nullable median family. |
| `PoTool.Client/Services/BugInsightsCalculator.cs` | `BugInsightsCalculator` | `CalculatePercentile` | Percentile / quantile | Local helper | D | Linear interpolation percentile duplicated from server-side families. |

## Semantic Drift

### Confidence
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` computes confidence as a blend of **sample size** and **variance dampening**.
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` computes confidence from **coefficient-of-variation bands**.
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` computes confidence from **historical sprint count only**.
- `PoTool.Shared/Metrics/CapacityCalibrationDto.cs` uses “high-confidence capacity” as planning shorthand for **P25 velocity**, which is not a confidence score at all.

Result: `confidence` is not a shared statistical term in this repository today; it is slice-specific vocabulary reused across unrelated formulas.

### Accuracy
- `PoTool.Core/Metrics/Queries/GetEffortEstimationQualityQuery.cs` and `PoTool.Shared/Metrics/EffortEstimationQualityDto.cs` describe estimate-vs-actual `accuracy`.
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs` actually computes **inverse coefficient of variation**, which measures **consistency/stability** rather than estimate-vs-actual correctness.
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs` also uses `Accuracy`, but there it means **historical data completeness / best-effort approximation quality**, not a statistical measure.

Result: `accuracy` currently has at least two meanings: statistical consistency and data-fidelity confidence.

### Variance and coefficient of variation
- In the stable EffortDiagnostics core, variance and coefficient of variation are pure spread helpers.
- In `GetEffortEstimationQualityQueryHandler`, the same spread formula is converted into `accuracy`.
- In `GetEffortDistributionTrendQueryHandler`, the same spread formula drives **forecast confidence** and **volatile/stable** direction classification.

Result: the underlying math is similar, but the domain meaning attached to it changes by slice.

### Percentile
- `GetCapacityCalibrationQueryHandler`, `GetPrSprintTrendsQueryHandler`, `GetPipelineInsightsQueryHandler`, and the client calculators use **linear interpolation** percentiles.
- `GetPullRequestInsightsQueryHandler` and `GetPrDeliveryInsightsQueryHandler` use **nearest-rank** percentiles.
- `PoTool.Shared/PullRequests/*.cs`, `PoTool.Shared/Pipelines/PipelineInsightsDto.cs`, and `PoTool.Shared/Metrics/CapacityCalibrationDto.cs` all expose percentile-based metrics without one shared percentile definition.

Result: percentile semantics are unstable across the repository and need a separate contract decision before consolidation.

### Utilization
- `GetEffortDistributionQueryHandler` and `GetEffortImbalanceQueryHandler` use utilization as **descriptive context** relative to a default capacity.
- `GetSprintCapacityPlanQueryHandler` uses utilization as a **decision-driving operational status** with explicit under/near/over-capacity bands.
- `PoTool.Shared/Metrics/EffortDistributionTrendDto.cs` and `PoTool.Shared/Metrics/SprintCapacityPlanDto.cs` expose utilization percentages without a single shared meaning beyond “effort ÷ capacity”.

Result: utilization is a reused ratio, but not a reusable statistical helper with one repository-wide interpretation.

## Consolidation Opportunities

### Reusable pure math
- **Population variance** can safely move to a broader shared statistics core now.
  - Current duplicates: `GetEffortEstimationQualityQueryHandler.CalculateVariance`, `GetEffortEstimationSuggestionsQueryHandler.CalculateVariance`.
- **Population standard deviation** is a natural follow-on candidate once variance ownership is settled.
  - Current local copy: `GetEffortDistributionTrendQueryHandler.CalculateStandardDeviation`.
- **Median** is a future candidate only after one contract is chosen for:
  - sorted vs unsorted input
  - empty sample handling (`0`, `null`, or exception)
  - integer vs double even-sample behavior
- **Percentile / quantile** is the largest reusable-math opportunity, but only after a semantic choice is made between:
  - linear interpolation
  - nearest-rank
  - empty/tiny-sample behavior

### Reusable domain math
- A later consolidation prompt could align the duplicated EffortDiagnostics math now split between:
  - `PoTool.Core/Metrics/EffortDiagnostics`
  - `PoTool.Core.Domain/Domain/EffortDiagnostics`
- That consolidation should stay inside an **EffortDiagnostics-owned** domain surface, because:
  - deviation-from-mean
  - share-of-total concentration bands
  - weighted imbalance score
  - normalized concentration index
  are not generic repository-wide math once their risk semantics are attached.

### Intentionally slice-specific formulas
- Suggestion confidence heuristics
- Trend confidence and volatility heuristics
- Forecast confidence by sprint-count depth
- Utilization status bands in sprint-capacity planning
- Linear regression slope usage in trend forecasting

These should not be pulled into a broad shared statistical core unless a later semantic audit proves that multiple slices truly mean the same thing.

## Keep Local
- `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs` convenience helpers for weighted deviation and normalized concentration index should remain owned by the EffortDiagnostics slice unless broader reuse appears.
- `PoTool.Core.Domain/Domain/EffortDiagnostics/EffortDiagnosticsCanonicalRules.cs` should remain EffortDiagnostics-specific domain math.
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs`
  - `CalculateConfidence`
  - title similarity scoring
  - heuristic rationale generation
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs`
  - confidence-level bands
  - volatility thresholds
  - linear-regression interpretation
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - sprint-history confidence bands
- `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs`
  - utilization status thresholds
  - placeholder team-capacity operational logic

## Recommended Next Step
- **Next step: targeted consolidation prompt**
- Scope that prompt narrowly to semantically aligned pure-math duplication only:
  - choose one owner for the stable EffortDiagnostics primitive helpers duplicated across `PoTool.Core/Metrics/EffortDiagnostics` and `PoTool.Core.Domain/Domain/EffortDiagnostics`
  - consolidate the population-variance family
  - decide whether standard deviation should join that same shared surface
- Explicitly exclude percentile and confidence families from that prompt until a separate semantic decision is made for those unstable terms.
