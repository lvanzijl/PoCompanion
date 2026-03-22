# BuildQuality UI Compliance Audit Report

## 1. Summary

- Scope handling: **PASS**
- No recomputation: **FAIL**

The current `PoTool.Client` BuildQuality integration passes scope parameters to backend queries and does not locally slice BuildQuality raw facts. However, the UI layer does recompute and reinterpret BuildQuality presentation states in several places, which violates the no-recomputation invariant.

---

## 2. Scope handling analysis

### `PoTool.Client/Pages/Home/HealthWorkspace.razor`

- **Location:** `PoTool.Client/Pages/Home/HealthWorkspace.razor:155-201`
- **How scope is passed:** the page computes a rolling window (`_windowEndUtc = DateTimeOffset.UtcNow`, `_windowStartUtc = _windowEndUtc.AddDays(-DefaultRollingWindowDays)`) and passes those values into `BuildQualityService.GetRollingWindowAsync(activeProfile.Id, _windowStartUtc, _windowEndUtc, ...)`.
- **UI filtering exists:** no filtering of builds, test runs, coverage, or BuildQuality evidence/metrics. The only local collection handling is product display ordering in `GetProductsInDisplayOrder()` (`PoTool.Client/Pages/Home/HealthWorkspace.razor:234-244`), which reorders already returned products but does not change BuildQuality scope.
- **Verdict:** **PASS**

### `PoTool.Client/Pages/Home/SprintTrend.razor`

- **Location:** `PoTool.Client/Pages/Home/SprintTrend.razor:1073-1098`
- **How scope is passed:** the page passes the selected sprint directly into `BuildQualityService.GetSprintAsync(_profileId.Value, _currentSprint.Id, ...)`.
- **UI filtering exists:** no filtering of BuildQuality facts or DTO collections. The only local collection handling is product ordering for display in `_sprintBuildQuality.Products.OrderBy(product => product.ProductName, ...)` (`PoTool.Client/Pages/Home/SprintTrend.razor:169-180`), which does not change scope.
- **Verdict:** **PASS**

### `PoTool.Client/Pages/Home/PipelineInsights.razor`

- **Location:** `PoTool.Client/Pages/Home/PipelineInsights.razor:924-1003`
- **How scope is passed:** the page derives pipeline identifiers from the existing Pipeline Insights DTO and passes `productOwnerId`, `sprintId`, and `pipelineDefinitionId` into `BuildQualityService.GetPipelineAsync(...)`.
- **UI filtering exists:** no filtering of BuildQuality builds, tests, coverage, or BuildQuality DTO collections after retrieval. The page caches returned `PipelineBuildQualityDto` instances in `_pipelineBuildQualityByDefinition` and reuses them.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor`

- **Location:** `PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor:1-132`
- **How scope is passed:** no scope selection; receives a single `BuildQualityResultDto` as a parameter.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityCompactComponent.razor`

- **Location:** `PoTool.Client/Components/Common/BuildQualityCompactComponent.razor:1-119`
- **How scope is passed:** no scope selection; receives a single `BuildQualityResultDto` as a parameter.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor`

- **Location:** `PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor:1-85`
- **How scope is passed:** no scope selection; receives a single `BuildQualityResultDto` as a parameter.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityPresentation.cs`

- **Location:** `PoTool.Client/Components/Common/BuildQualityPresentation.cs:1-212`
- **How scope is passed:** no scope selection; consumes already returned DTO values.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

---

## 3. Recomputation analysis

### Finding 1 — UI defines new quality thresholds and derives visual health states

- **Location:** `PoTool.Client/Components/Common/BuildQualityPresentation.cs:32-36,64-95,161-179`
- **Code pattern:** hardcoded thresholds `MinimumBuilds = 3`, `MinimumTests = 20`, `GoodThreshold = 0.90d`, `WarningThreshold = 0.70d`; local comparisons in `GetRateState`, `GetTestState`, and `GetOverallState`.
- **Why it is a violation:** the UI is introducing its own interpretation layer for "good / warning / bad / unknown" using metric values and duplicated thresholds. This is not present in the DTO contract and is not a simple display of DTO values.

### Finding 2 — UI reinterprets confidence into new labels and border states

- **Location:** `PoTool.Client/Components/Common/BuildQualityPresentation.cs:86-122`
- **Code pattern:** `result.Metrics.Confidence < 2` and `GetConfidenceState(...)` map numeric confidence into `"High confidence"`, `"Low confidence"`, and `"Insufficient confidence"`.
- **Why it is a violation:** the UI is transforming the canonical confidence value into a new client-side scoring interpretation instead of displaying the DTO value directly.

### Finding 3 — UI infers Unknown from `null` instead of using explicit DTO flags only

- **Location:** `PoTool.Client/Components/Common/BuildQualityPresentation.cs:38-43,161-166`
- **Code pattern:** `return isUnknown || !value.HasValue ? "Unknown" : ...`
- **Why it is a violation:** the helper derives Unknown from the absence of a value (`!value.HasValue`) in addition to the explicit DTO unknown flags. The audit rule requires the UI to avoid inferring Unknown and to rely on transported semantics.

### Finding 4 — UI duplicates threshold explanation constants

- **Location:** `PoTool.Client/Components/Common/BuildQualityPresentation.cs:32-33,136-144`
- **Code pattern:** `GetConfidenceSummary(...)` embeds `MinimumBuilds (3)` and `MinimumTests (20)` from UI-local constants.
- **Why it is a violation:** the UI repeats threshold semantics locally instead of only displaying backend-provided data. This duplicates canonical threshold knowledge in the client.

### Finding 5 — UI aggregates evidence counts into a derived failed total

- **Location:** `PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor:52-55`
- **Code pattern:** `Result.Evidence.FailedBuilds + Result.Evidence.PartiallySucceededBuilds`
- **Why it is a violation:** the tooltip creates a new combined "Failed" number in the UI rather than displaying the evidence fields exactly as transported.

### Finding 6 — Pipeline Insights drawer repeats the failed-count aggregation

- **Location:** `PoTool.Client/Pages/Home/PipelineInsights.razor:492-495`
- **Code pattern:** `Failed @(_drawerBuildQuality.Result.Evidence.FailedBuilds + _drawerBuildQuality.Result.Evidence.PartiallySucceededBuilds)`
- **Why it is a violation:** the build drawer derives a new failed count locally instead of showing the evidence fields directly.

### Finding 7 — Pipeline Insights border state is derived locally from the DTO

- **Location:** `PoTool.Client/Pages/Home/PipelineInsights.razor:1016-1020`
- **Code pattern:** `BuildQualityPresentation.GetOverallState(detail.Result)`
- **Why it is a violation:** the page computes a new overall BuildQuality state for each pipeline/build border using client-side threshold and confidence logic instead of rendering a backend-provided state.

---

## 4. Build-level (Pipeline Insights) analysis

- **Whether build-level recomputation exists:** **Yes**
- **Whether DTO is used correctly:** **Partially**

`PipelineInsights.razor` does not compute build-level pass rates, coverage percentages, or confidence formulas from raw build facts. It does, however, derive a client-side overall quality border state by calling `BuildQualityPresentation.GetOverallState(detail.Result)` (`PoTool.Client/Pages/Home/PipelineInsights.razor:1016-1020`) and derives a combined failed count in the drawer (`PoTool.Client/Pages/Home/PipelineInsights.razor:492-495`).

So the page uses backend DTOs as the source, but it does not use them directly for build-level presentation. It layers additional UI-side BuildQuality interpretation on top.

---

## 5. Rolling window handling

- **Location:** `PoTool.Client/Pages/Home/HealthWorkspace.razor:155-201`
- **Whether UI slices data or passes parameter:** the page computes a rolling 30-day window locally and passes it into `GetRollingWindowAsync(...)`. It does **not** fetch a broader BuildQuality dataset and slice it locally afterward.
- **Verdict:** **PASS**

---

## 6. Unknown handling

- **Whether UI alters Unknown semantics:** **Yes**
- **Details:**
  - The UI does **not** convert null to `0`.
  - The UI does display `"Unknown"` explicitly.
  - However, `BuildQualityPresentation.FormatPercent(...)` treats either an explicit unknown flag **or** a `null` value as `"Unknown"` (`PoTool.Client/Components/Common/BuildQualityPresentation.cs:38-43`), which means the UI is inferring Unknown from missing data rather than relying only on explicit transported semantics.
- **Verdict:** **FAIL**

---

## 7. Violations

1. `PoTool.Client/Components/Common/BuildQualityPresentation.cs:32-36,64-95,161-179`  
   UI defines and applies client-side thresholds (`0.90`, `0.70`, `3`, `20`) to derive BuildQuality health states.

2. `PoTool.Client/Components/Common/BuildQualityPresentation.cs:86-122`  
   UI reinterprets numeric confidence into new qualitative states.

3. `PoTool.Client/Components/Common/BuildQualityPresentation.cs:38-43`  
   UI infers Unknown from `null`.

4. `PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor:52-55`  
   UI aggregates `FailedBuilds + PartiallySucceededBuilds`.

5. `PoTool.Client/Pages/Home/PipelineInsights.razor:492-495`  
   UI aggregates `FailedBuilds + PartiallySucceededBuilds` in the drawer.

6. `PoTool.Client/Pages/Home/PipelineInsights.razor:1016-1020`  
   UI derives a new overall BuildQuality state for pipeline/build borders.

---

## 8. Final verdict

**FAIL**

Scope selection is correctly delegated to backend BuildQuality queries, but the UI does recompute and reinterpret BuildQuality semantics in shared presentation helpers and Pipeline Insights rendering. The no-recomputation invariant is therefore not satisfied.
