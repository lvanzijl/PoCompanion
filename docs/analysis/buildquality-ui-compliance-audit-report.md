# BuildQuality UI Compliance Audit Report

## 1. Summary

- Scope handling: **PASS**
- No recomputation: **PASS**

This audit was refreshed against the current `PoTool.Client` implementation after the final chart-state cleanup documented in `docs/analysis/buildquality-chart-state-cleanup-report.md`.

Current repository truth:

- BuildQuality scope is selected only by passing parameters into typed backend BuildQuality service calls.
- The client displays DTO values, explicit DTO flags, and explicit unknown-reason codes without reintroducing local BuildQuality formulas, thresholds, confidence reinterpretation, or chart-state drift.
- No current UI compliance violations were found in the audited files.

---

## 2. Scope handling analysis

### `PoTool.Client/Pages/Home/HealthWorkspace.razor`

- **Location:** `PoTool.Client/Pages/Home/HealthWorkspace.razor:155-201`
- **How scope is passed:** the page computes a rolling window once (`_windowStartUtc`, `_windowEndUtc`) and passes those values into `BuildQualityService.GetRollingWindowAsync(...)`.
- **UI filtering exists:** none. `GetProductsInDisplayOrder()` only reorders already-returned products for display.
- **Verdict:** **PASS**

### `PoTool.Client/Pages/Home/SprintTrend.razor`

- **Location:** `PoTool.Client/Pages/Home/SprintTrend.razor:1073-1098`
- **How scope is passed:** the page passes the selected sprint identifier directly into `BuildQualityService.GetSprintAsync(...)`.
- **UI filtering exists:** none. Product ordering remains presentation-only.
- **Verdict:** **PASS**

### `PoTool.Client/Pages/Home/PipelineInsights.razor`

- **Location:** `PoTool.Client/Pages/Home/PipelineInsights.razor:920-999`
- **How scope is passed:** the page derives pipeline definition identifiers from the existing Pipeline Insights DTO and passes `productOwnerId`, `sprintId`, and `pipelineDefinitionId` into `BuildQualityService.GetPipelineAsync(...)`.
- **UI filtering exists:** none. The page caches returned `PipelineBuildQualityDto` instances but does not slice broader BuildQuality datasets after retrieval.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor`

- **Location:** `PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor:1-124`
- **How scope is passed:** no scope selection; receives a single `BuildQualityResultDto`.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityCompactComponent.razor`

- **Location:** `PoTool.Client/Components/Common/BuildQualityCompactComponent.razor:1-123`
- **How scope is passed:** no scope selection; receives a single `BuildQualityResultDto`.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor`

- **Location:** `PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor:1-86`
- **How scope is passed:** no scope selection; receives a single `BuildQualityResultDto`.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityPresentation.cs`

- **Location:** `PoTool.Client/Components/Common/BuildQualityPresentation.cs:13-48`
- **How scope is passed:** no scope selection; consumes already-returned DTO values and explicit unknown-reason codes.
- **UI filtering exists:** none.
- **Verdict:** **PASS**

---

## 3. Recomputation analysis

### `PoTool.Client/Components/Common/BuildQualityPresentation.cs`

- The helper now contains only:
  - `FormatPercent(double? value, bool isUnknown)`
  - `GetUnknownReasonText(string? reason)`
- `FormatPercent(...)` formats backend-provided percentages for display, returns `"Unknown"` only when the explicit DTO unknown flag is set, and returns an em dash when the value is absent without an unknown flag.
- `GetUnknownReasonText(...)` maps backend-provided unknown reason codes into user-facing text and does not derive new metric semantics.
- **Verified absent:** client-side thresholds, rate-state derivation, confidence-state derivation, overall-state derivation, and null-driven Unknown inference.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor`

- Displays `Result.Metrics.SuccessRate`, `Result.Metrics.TestPassRate`, `Result.Metrics.Coverage`, `Result.Metrics.Confidence`, and explicit evidence counts/flags.
- Does not calculate percentages, ratios, confidence, Unknown, or overall health states.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityCompactComponent.razor`

- Displays DTO values directly, including the raw numeric `Result.Metrics.Confidence` and explicit `BuildThresholdMet` / `TestThresholdMet` flags.
- Does not reinterpret confidence into qualitative client states.
- **Verdict:** **PASS**

### `PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor`

- Displays evidence fields exactly as transported:
  - `FailedBuilds`
  - `PartiallySucceededBuilds`
  - `PassedTests`
  - `NotApplicableTests`
  - `CoveredLines`
  - `TotalLines`
- Does not aggregate `FailedBuilds + PartiallySucceededBuilds` into a new UI-only number.
- Uses explicit unknown-reason fields from the DTO.
- **Verdict:** **PASS**

### `PoTool.Client/Pages/Home/PipelineInsights.razor`

- Displays pipeline BuildQuality detail returned by `BuildQualityService.GetPipelineAsync(...)`.
- The drawer now renders `FailedBuilds` and `PartiallySucceededBuilds` separately.
- No `BuildQualityPresentation.GetOverallState(...)` call or equivalent border-state derivation remains.
- **Verdict:** **PASS**

### Previously reported violations re-audited

The previously documented violations are no longer present in the current client code:

- client thresholds in `BuildQualityPresentation`
- client confidence mapping
- Unknown inference from `null`
- `FailedBuilds + PartiallySucceededBuilds` aggregation
- pipeline/build border-state derivation from client-side logic
- dormant chart-state identifiers and rendering logic
- remaining strict drift matches in shared chart code

Overall recomputation verdict: **PASS**

---

## 4. Build-level (Pipeline Insights) analysis

- **Whether build-level recomputation exists:** **No**
- **Whether DTO is used correctly:** **Yes**

`PipelineInsights.razor` loads pipeline-scoped BuildQuality detail through `BuildQualityService.GetPipelineAsync(...)` and renders the returned DTO values via `BuildQualityCompactComponent` plus direct evidence-field display in the drawer.

Current audited behavior:

- no client-side pass-rate or coverage recomputation from raw build facts
- no client-side confidence reinterpretation
- no aggregated failed count
- no pipeline/build border-state derivation from DTO metrics

**Verdict:** **PASS**

---

## 5. Rolling window handling

- **Location:** `PoTool.Client/Pages/Home/HealthWorkspace.razor:155-201`
- **Whether UI slices data or passes parameter:** the page computes a rolling 30-day window once and passes it into `BuildQualityService.GetRollingWindowAsync(...)`.
- **Whether broader BuildQuality data is sliced after retrieval:** **No**
- **Verdict:** **PASS**

---

## 6. Unknown handling

- **Whether UI alters Unknown semantics:** **No**
- **Details:**
  - `BuildQualityPresentation.FormatPercent(...)` returns `"Unknown"` only when the explicit DTO unknown flag is true.
  - When a metric value is absent without an unknown flag, the UI renders `"—"` instead of inferring Unknown.
  - `BuildQualityTooltipComponent` uses the explicit DTO unknown-reason fields and maps them to text through `GetUnknownReasonText(...)`.
- **Verdict:** **PASS**

---

## 7. Drift detection results

Repository-wide verification after the final cleanup found no remaining strict BuildQuality UI drift matches in the shared chart surface or presentation helpers.

Search-based results:

- `GoodThreshold`, `WarningThreshold`, `MinimumBuilds`, `MinimumTests` in `PoTool.Client`: **no matches found**
- `GetRateState`, `GetTestState`, `GetConfidenceState`, `GetOverallState` in `PoTool.Client`: **no matches found**
- `FailedBuilds + PartiallySucceededBuilds` in `PoTool.Client`: **no matches found**
- `QualityStateLabel` in `PoTool.Client`: **no matches found**
- `QualityStrokeColor` in `PoTool.Client`: **no matches found**

Current chart code state:

- `PoTool.Client/Components/Charts/TimeScatterPoint.cs` now exposes only standard scatter-point fields plus metadata convenience accessors.
- `PoTool.Client/Components/Charts/TimeScatterSvg.razor` renders hover and category styling from `Category` only, with no dormant BuildQuality state label or stroke override.

**Verdict:** **PASS**

---

## 8. Violations

None.

No current scope-handling violations or no-recomputation violations were found in the audited UI files.

---

## 9. Final verdict

**PASS — SAFE TO MERGE**

The current `PoTool.Client` BuildQuality UI complies with the audited scope-handling and no-recomputation rules. The previous FAIL findings were stale and are no longer supported by the code after the final presentation cleanup and chart-state cleanup.

## Reviewer-ready notes

### What changed

- refreshed `buildquality-ui-compliance-audit-report.md` to match current UI implementation after the final cleanup
- updated the matching MSTest document audit to enforce the current result

### What was intentionally not changed

- no backend/provider/query/DTO changes
- no UI redesign beyond the already completed compliance fix and chart cleanup
- no formula/threshold/Unknown semantic changes

### Known limitations / follow-up

- None.
