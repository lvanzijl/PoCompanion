# Phase 17 calibration refinement

## Summary

- VERIFIED: the current Phase 15/15.1 signal code still produced all four Phase 16 failures in the client-only signal path.
- IMPLEMENTED: minimal client-side refinements in `PoTool.Client/Models/ProductPlanningSprintSignals.cs` to improve board-wide overload visibility, far-horizon confidence tone, dominant-factor explanations, and chip prioritization.
- IMPLEMENTED: regression coverage in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs` for the verified failures.
- IMPLEMENTED: user-visible release notes update in `docs/release-notes.json`.
- NOT IMPLEMENTED: no backend changes, no new data sources, no velocity metrics, no dependency modeling, no planning engine/persistence/TFS/API changes.
- BLOCKER: none.

## Implementation mapping (files + flow)

### Relevant files

- `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - risk classification: `ClassifyRisk`, `GetLoadRiskContribution`, `GetSystemicLoadRiskContribution`, `GetTrackRiskContribution`, `GetForwardShiftRiskContribution`, `GetOverlapRiskContribution`
  - confidence classification: `ClassifyConfidence`, `GetChangedEpicConfidencePenalty`, `GetStructureConfidencePenalty`, `GetForwardShiftConfidencePenalty`, `IsFarHorizonHighConfidenceCapped`
  - explanation output: `BuildChips`, `BuildTooltip`, `BuildRiskFactors`, `BuildConfidenceFactors`
- `PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`
  - UI data path: `ProductPlanningBoardRenderModelFactory.Create` → `ProductPlanningSprintSignalFactory.BuildColumns`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
  - UI output path: sprint heat card binds `RiskLabel`, `ConfidenceLabel`, `ExplanationChips`, and `Tooltip`
- `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - signal calibration regression coverage
- `docs/release-notes.json`
  - user-visible release note for the refinement
- `docs/reports/2026-04-21-phase-16-multi-persona-validation.md`
  - mandatory validation input for this phase

### Data flow

1. `ProductPlanningBoardRenderModelFactory.Create` in `PoTool.Client/Models/ProductPlanningBoardRenderModel.cs` builds `SprintColumns` through `ProductPlanningSprintSignalFactory.BuildColumns`.
2. `BuildColumns` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs` maps:
   - raw board state
   - previous-board deltas
   - computed baselines
   - risk/confidence classification
   - chips/tooltips
3. `PlanBoard.razor` renders the final signal labels, heat style, chips, and tooltip without additional interpretation.

### Control points for each failure

- Systemic overload masking
  - baseline computation: `BuildMetrics`
  - risk thresholds: `ClassifyRisk`, `GetLoadRiskContribution`
- Far-future confidence optimism
  - distance decay: `ClassifyConfidence`
- Weak explanation credibility
  - tooltip construction: `BuildTooltip`
  - chip construction: `BuildChips`
- Dominant factor hidden by chip prioritization
  - chip ordering and truncation: `BuildChips`
  - factor selection: `BuildRiskFactors`, `BuildConfidenceFactors`

## Verification of each Phase 16 failure

### 1. Systemic overload masking

- VERIFIED
- Evidence:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
    - `BuildMetrics` computes `loadBaseline` from the board’s own active-window average.
    - pre-refinement `ClassifyRisk` added only per-sprint structural contributions.
    - pre-refinement `GetLoadRiskContribution` could still leave a chronically dense board below the `>= 1.25` medium threshold.
- Minimal reproducible scenario:
  - four Epics active in every sprint across a four-sprint board
  - board baseline stays high because every sprint is dense
  - pre-refinement result: the sprint could stay `Risk low`
  - post-refinement regression: `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard`
- Reasoning:
  - board-relative normalization muted the signal exactly where the whole board was already aggressive.

### 2. Far-future confidence optimism

- VERIFIED
- Evidence:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
    - pre-refinement `ClassifyConfidence` used `distanceRatio * 1.9`
    - that allowed stable mid-horizon sprints to remain `High`
  - Phase 16 validation identified that “much of the horizon can still look High.”
- Minimal reproducible scenario:
  - one stable Epic spanning seven sprints
  - pre-refinement result: the midpoint sprint still classified as `Confidence high`
  - post-refinement regression: `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`
- Reasoning:
  - gradual decay existed, but the tone remained too optimistic for the middle and far horizon.

### 3. Weak explanation credibility

- VERIFIED
- Evidence:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
    - pre-refinement `BuildTooltip` used broad category sentences such as “several Epics land together” or “some recent changes still affect”
    - those sentences did not reference board-relative context or the strongest active cause
- Minimal reproducible scenario:
  - any medium/high sprint with multiple possible contributors
  - pre-refinement tooltip restated a generic bucket rather than the leading factor
  - post-refinement tooltips now pull from ordered factor builders
- Reasoning:
  - the UI sounded interpretive, but not evidential.

### 4. Dominant factor hidden by chip prioritization

- VERIFIED
- Evidence:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
    - pre-refinement `BuildChips` added chips in fixed order
    - `ForwardShiftCount > 0` short-circuited overlap with `else if`
    - final chip list was capped with `.Take(MaxExplanationChips)`
- Minimal reproducible scenario:
  - overlap and forward pull-in present in the same sprint
  - pre-refinement result: `Work pulled forward` could hide `Overlap pressure`
  - post-refinement regression: `BuildColumns_KeepsDominantOverlapVisibleWhenWorkIsAlsoPulledForward`
- Reasoning:
  - chip ordering was presence-based, not dominance-based.

## Refinement design per issue

### Systemic overload masking

- IMPLEMENTED design:
  - add one small systemic risk contribution when the board baseline itself is already high
- Exact code location:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `SystemicLoadBaselineThreshold`
  - `SystemicLoadContribution`
  - `GetSystemicLoadRiskContribution`
  - inclusion in `ClassifyRisk`
- Scope control:
  - no new signal type
  - no backend or DTO contract change

### Far-future confidence optimism

- IMPLEMENTED design:
  - keep existing gradual penalty
  - add a horizon-based cap so a sprint deep enough into the horizon cannot remain `High`
- Exact code location:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `FarHorizonHighConfidenceBoundary`
  - `IsFarHorizonHighConfidenceCapped`
  - cap applied in `ClassifyConfidence`

### Weak explanation credibility

- IMPLEMENTED design:
  - derive ordered risk/confidence explanation factors from actual contribution weights
  - generate tooltips from the strongest factor instead of generic label templates
- Exact code location:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `BuildRiskFactors`
  - `BuildConfidenceFactors`
  - `BuildTooltip`

### Dominant factor hidden by chip prioritization

- IMPLEMENTED design:
  - replace fixed chip order with factor ordering by contribution weight
  - reserve chip slots for the strongest risk and confidence drivers
- Exact code location:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `BuildChips`

## Changes implemented (with file references)

### `PoTool.Client/Models/ProductPlanningSprintSignals.cs`

- IMPLEMENTED:
  - cached risk/confidence classification inside `BuildColumns` so explanation/render output reuses the same classification result
  - systemic overload contribution via `GetSystemicLoadRiskContribution`
  - extracted confidence sub-penalty helpers for reuse in classification and explanation
  - far-horizon cap via `IsFarHorizonHighConfidenceCapped`
  - factor-based explanation generation via `BuildRiskFactors` and `BuildConfidenceFactors`
  - dominant-cause tooltip generation via `BuildTooltip`
  - contribution-ordered chips via `BuildChips`

### `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`

- IMPLEMENTED:
  - updated existing assertions to reflect dominant-factor explanations
  - added coverage for:
    - systemic overload visibility
    - mid-horizon confidence cap
    - dominant overlap visibility when other changes exist

### `docs/release-notes.json`

- IMPLEMENTED:
  - added a new Plan Board note describing the user-visible refinement

## Before/after behavior comparison

### Systemic overload

- Before:
  - a chronically dense board could still render a sprint as `Risk low`
- After:
  - the same board now surfaces at least `Risk medium`
  - explanation now calls out that the board load is already high

### Far-horizon confidence

- Before:
  - stable midpoint/far-horizon sprints could remain `Confidence high`
- After:
  - midpoint/far-horizon steady sprints are capped to `Confidence medium`
  - explanation now states that horizon distance is limiting confidence

### Explanation credibility

- Before:
  - tooltips used broad heuristic summaries
- After:
  - tooltips cite the strongest active factor, such as overlap above board norm, board-wide load, or far-horizon provisionality

### Dominant factor clarity

- Before:
  - chip order depended on fixed presence checks
- After:
  - chips are ordered by contribution weight
  - dominant factors appear first

## Tests added/updated

- IMPLEMENTED:
  - `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard`
  - `BuildColumns_KeepsDominantOverlapVisibleWhenWorkIsAlsoPulledForward`
  - expanded `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`
  - updated `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle`
  - updated `BuildColumns_UsesPlanningLanguageForStableNearTermSprint`

## Remaining risks

- NOT IMPLEMENTED:
  - Phase 16’s broader “parallel work realism” concern remains only indirectly addressed; this phase was limited to the four mandatory failures.
  - latest-impact delta summaries still prioritize the strongest changed sprint rather than describing distributed plan reshaping.
- VERIFIED:
  - the refinement remains heuristic and client-local by design.
  - no new predictive model was introduced.

## Final section

### IMPLEMENTED

- Client-only systemic overload visibility refinement
- Far-horizon high-confidence cap
- Dominant-factor risk/confidence explanation builders
- Contribution-ordered chip generation
- Regression test expansion for the verified failures
- User-facing release note update

### NOT IMPLEMENTED

- New data sources
- Velocity metrics
- Dependency modeling
- Backend logic or API contract changes
- Planning engine, persistence, or TFS integration changes

### BLOCKERS

- None

### Evidence (files/tests/commands)

- Files:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`
  - `PoTool.Client/Pages/Home/PlanBoard.razor`
  - `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `docs/release-notes.json`
  - `docs/reports/2026-04-21-phase-16-multi-persona-validation.md`
- Commands:
  - `dotnet test PoTool.sln --configuration Release -v minimal`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningSprintSignalFactoryTests|FullyQualifiedName~PlanningBoardImpactSummaryBuilderTests" --no-restore`
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningBoardClientUiTests" --no-restore`
  - `python -m json.tool docs/release-notes.json`
- Test evidence:
  - targeted unit tests passed after refinement
  - targeted Plan Board UI tests passed after refinement
  - full solution test run passed after refinement

### GO/NO-GO for Phase 17 acceptance

- GO, contingent on final post-change validation staying green.
