# Phase 20 signal amplification

## 1. Summary

- VERIFIED: Phase 20 stayed inside the allowed scope. No planning engine logic, signal calculation, persistence model, or API contracts changed. Evidence: `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, `PoTool.Client/Models/PlanningBoardImpactSummary.cs`, `PoTool.Client/Pages/Home/PlanBoard.razor`.
- IMPLEMENTED: latest impact now elevates sprint delta summaries ahead of static change details and re-renders those deltas as distinct chips in the `Latest planning impact` alert. Evidence: `PoTool.Client/Models/PlanningBoardImpactSummary.cs`, `PoTool.Client/Models/PlanBoardSprintSignalPresentation.cs`, `PoTool.Client/Pages/Home/PlanBoard.razor`, test `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`.
- IMPLEMENTED: sprint cards now emphasize the dominant chip first, keep medium/high attention states visually stronger, and de-emphasize calm low/high combinations so they read as neutral context rather than approval. Evidence: `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, `PoTool.Client/Models/PlanBoardSprintSignalPresentation.cs`, `PoTool.Client/Pages/Home/PlanBoard.razor`, `PoTool.Client/Pages/Home/PlanBoard.razor.css`, tests in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/PlanBoardSprintSignalPresentationTests.cs`.
- NOT IMPLEMENTED: no new signals, no new data sources, no threshold changes, no velocity/dependency modeling, and no interaction redesign. Evidence: unchanged signal classification methods `ClassifyRisk`, `ClassifyConfidence`, `TryBuildRiskDeltaSummary`, and `TryBuildConfidenceDeltaSummary` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`.
- BLOCKER: none.

## 2. Current visibility analysis

- VERIFIED: the latest-impact surface existed only as a single outlined `MudAlert` in `PoTool.Client/Pages/Home/PlanBoard.razor`, where sprint delta summaries were rendered in the same list as impact counts, move details, and overlap notes through `_latestImpactSummary.SummaryItems`.
- VERIFIED: delta summaries were produced in `ProductPlanningSprintSignalFactory.BuildDeltaSummaries` and appended late in `PlanningBoardImpactSummaryBuilder.Build`, which made change-over-time messages visible but not consistently first-class in the `Latest planning impact` alert.
- VERIFIED: sprint heat cards in the `Sprint heat` grid rendered a static hierarchy of `RiskLabel`, `ConfidenceLabel`, and equal-weight explanation chips in `PoTool.Client/Pages/Home/PlanBoard.razor`, so dominant signal convergence depended mostly on text interpretation rather than presentation.
- VERIFIED: calm states used a filled low-risk chip and a success-colored confidence chip, which made `Within typical range` plus `Plan stable (near-term)` visually stronger than the Phase 19 findings supported. Evidence: previous rendering path in `PlanBoard.razor`; preserved label text in `BuildRiskLabel` and `BuildConfidenceLabel`.

## 3. Amplification opportunities

- VERIFIED: delta summaries were the strongest behavior trigger from Phase 19, but they were visually weaker than static sprint labels because they appeared only inside the mixed summary list. Evidence: Phase 19 findings in `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-19-real-world-usage-validation.md` and `BuildDeltaSummaries` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`.
- VERIFIED: label + chip + tooltip convergence already existed semantically, but the UI did not reinforce the dominant chip over secondary chips. Evidence: `BuildChips`, `BuildTooltip`, and sprint-card rendering in `PoTool.Client/Models/ProductPlanningSprintSignals.cs` and `PoTool.Client/Pages/Home/PlanBoard.razor`.
- VERIFIED: medium signals such as `Needs attention` were easier to ignore because they were presented close to calm styling, especially when secondary calm chips diluted the first visible explanation. Evidence: `BuildRiskLabel`, `BuildChips`, and Phase 19 observations.
- VERIFIED: calm states were visually stronger than necessary because low risk still used a filled status chip and high confidence still used positive-success styling. Evidence: old risk/confidence chip rendering in `PoTool.Client/Pages/Home/PlanBoard.razor`.

## 4. Design decisions

- IMPLEMENTED: moved sprint delta summaries immediately after the impact-count summary inside `PlanningBoardImpactSummaryBuilder.Build` so recent change signals surface before move mechanics and overlap details.
- IMPLEMENTED: split `Latest planning impact` into two presentation layers in `PoTool.Client/Pages/Home/PlanBoard.razor`:
  - signal-delta chips rendered first through `PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries`
  - remaining contextual summaries rendered below through `GetNonSignalSummaries`
- IMPLEMENTED: sorted explanation chips by factor weight across risk and confidence in `BuildChips`, so attention-driving change or instability chips surface before calm filler chips.
- IMPLEMENTED: introduced `PlanBoardSprintSignalPresentation` to keep sprint-card emphasis consistent:
  - low risk renders outlined/default instead of filled/success
  - high confidence renders text/default instead of affirmative success emphasis
  - only the dominant explanation chip is visually promoted for attention states
  - matching sprint delta summaries can render directly on the affected sprint card
- NOT IMPLEMENTED: no wording changes to the existing labels `Within typical range`, `Needs attention`, `Strain elevated`, `Plan stable (near-term)`, `Plan less settled`, or `Plan provisional`.

## 5. Changes implemented (with file references)

- IMPLEMENTED: `PoTool.Client/Models/PlanningBoardImpactSummary.cs`
  - reordered `BuildDeltaSummaries` insertion so change-driven summaries appear near the top of `_latestImpactSummary.SummaryItems`
- IMPLEMENTED: `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - updated `BuildChips` to prioritize dominant weighted explanations before calmer secondary chips
- IMPLEMENTED: `PoTool.Client/Models/PlanBoardSprintSignalPresentation.cs`
  - added presentation rules for:
    - signal-delta extraction
    - sprint-specific delta lookup
    - neutral calm-state chip styling
    - dominant-chip emphasis
    - delta-summary color selection
- IMPLEMENTED: `PoTool.Client/Pages/Home/PlanBoard.razor`
  - grouped `Latest planning impact` signal deltas into distinct chips
  - kept non-signal summary items in the existing list below
  - rendered matching delta summaries directly on affected sprint cards
  - switched risk/confidence chip styling to the new calm-vs-attention presentation rules
  - promoted only the first dominant explanation chip visually
- IMPLEMENTED: `PoTool.Client/Pages/Home/PlanBoard.razor.css`
  - added compact styling for delta chips and dominant/secondary explanation chips
  - increased sprint-card minimum height slightly to preserve readability without adding a new layout region

## 6. Before/after behavior comparison

| Area | Before | After | Evidence |
| --- | --- | --- | --- |
| Latest change visibility | Sprint delta summaries were buried in the same list as count and move summaries | Delta summaries render first as distinct chips in `Latest planning impact` and remain visible on the affected sprint card | `PlanningBoardImpactSummaryBuilder.Build`, `PlanBoard.razor`, `PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries` |
| Change over static state | Static `RiskLabel` and `ConfidenceLabel` dominated the sprint card | Matching delta summary is visible on the sprint card, so recent change competes directly with static state | `PlanBoard.razor`, `PlanBoardSprintSignalPresentation.GetDeltaSummaryForSprint` |
| Medium signal emphasis | `Needs attention` could be diluted by calmer chips and close-to-green presentation | medium/high attention keeps stronger chip styling and dominant chip ordering | `BuildChips`, `PlanBoardSprintSignalPresentation.GetRiskChipVariant`, `GetExplanationChipVariant` |
| Calm-state interpretation | `Within typical range` + `Plan stable (near-term)` still looked affirmative | low risk and high confidence now render neutrally while preserving wording | `PlanBoardSprintSignalPresentation.GetRiskChipColor`, `GetConfidenceChipColor` |

## 7. Tests added/updated

- IMPLEMENTED: updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `BuildColumns_KeepsMinorFarFutureChangeFromDroppingStraightToLowConfidence` now asserts the dominant confidence chip appears before calm filler chips
  - `BuildColumns_PutsHighRiskAndLowConfidenceSignalsAheadOfNeutralChips` asserts dominant signal chips stay first in convergence cases
- IMPLEMENTED: added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/PlanBoardSprintSignalPresentationTests.cs`
  - verifies calm-state neutrality for low risk and high confidence
  - verifies signal-delta extraction and sprint matching
  - verifies only the dominant explanation chip gets emphasis in attention states
- IMPLEMENTED: updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`
  - asserts impact summary ordering keeps sprint deltas immediately after the impact-count summary
- VERIFIED: validation run succeeded:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build`

## 8. Remaining risks

- NOT IMPLEMENTED: no visual severity escalation beyond the existing labels and categories, so medium states remain advisory by design even though they are harder to miss.
- NOT IMPLEMENTED: no new tooltip copy, no new summary language, and no explicit “signals align” badge were added, because that would have introduced extra wording and potential UI clutter outside the allowed scope.
- VERIFIED: the amplification depends on `_latestImpactSummary` being present after a planning action; baseline board loads without a latest change still rely on the static sprint-card hierarchy, now with calmer neutral styling for low/high states.

## Final section

### IMPLEMENTED

- Sprint delta summaries now lead the latest-impact presentation and stay visible on the affected sprint card.
- Dominant explanation chips now surface before calmer secondary chips.
- Medium/high attention states now carry stronger presentation than calm states without changing labels or thresholds.
- Calm low-risk/high-confidence states now read as neutral context instead of soft approval.

### NOT IMPLEMENTED

- No new signals
- No new data sources
- No signal logic or threshold changes
- No API, persistence, or planning-engine changes
- No interaction redesign

### BLOCKERS

- None.

### Evidence (files/tests)

- UI elements:
  - `Latest planning impact` alert in `PoTool.Client/Pages/Home/PlanBoard.razor`
  - sprint heat cards in `PoTool.Client/Pages/Home/PlanBoard.razor`
  - dominant/secondary chip styling in `PoTool.Client/Pages/Home/PlanBoard.razor.css`
- Code locations:
  - `ProductPlanningSprintSignalFactory.BuildChips` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `PlanningBoardImpactSummaryBuilder.Build` in `PoTool.Client/Models/PlanningBoardImpactSummary.cs`
  - `PlanBoardSprintSignalPresentation` in `PoTool.Client/Models/PlanBoardSprintSignalPresentation.cs`
- Test evidence:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/PlanBoardSprintSignalPresentationTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`

### GO/NO-GO for Phase 20 acceptance

- GO: Phase 20 meets the stated goal of increasing behavioral impact through signal amplification, change emphasis, and calmer default presentation without changing signal logic, labels, categories, persistence, or API contracts.
