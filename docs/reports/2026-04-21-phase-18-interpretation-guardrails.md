# Phase 18 interpretation guardrails

## Summary

- VERIFIED: current Plan Board signal language could still be misread as certainty, safety, or delivery probability in `PoTool.Client/Models/ProductPlanningSprintSignals.cs` and `PoTool.Client/Pages/Home/PlanBoard.razor`.
- IMPLEMENTED: wording-only guardrails for labels, chips, tooltips, page hint text, and signal delta summaries.
- IMPLEMENTED: regression coverage in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs` and `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`.
- IMPLEMENTED: release note update in `docs/release-notes.json`.
- NOT IMPLEMENTED: no signal calculations, planning logic, persistence model, API contracts, new signals, or new UI elements were changed.
- BLOCKER: none.

## Current language analysis

### Relevant code locations

- `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `BuildRiskLabel`
  - `BuildConfidenceLabel`
  - `BuildChips`
  - `BuildTooltip`
  - `BuildRiskFactors`
  - `BuildConfidenceFactors`
  - `TryBuildRiskDeltaSummary`
  - `TryBuildConfidenceDeltaSummary`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
  - Sprint heat hint above the cards
- `PoTool.Client/Models/PlanningBoardImpactSummary.cs`
  - latest-impact summary path through `ProductPlanningSprintSignalFactory.BuildDeltaSummaries`

### Current phrases reviewed

#### Labels

- VERIFIED:
  - previous risk label text came from `BuildRiskLabel`: `Risk high`, `Risk medium`, `Risk low`
  - previous confidence label text came from `BuildConfidenceLabel`: `Confidence high`, `Confidence medium`, `Confidence low`

#### Chips

- VERIFIED:
  - previous chip text included `Load in range`, `Confidence steady`, `Low confidence (far future)`, `Far horizon limits confidence`
  - chip construction path: `BuildChips` → `BuildRiskFactors` / `BuildConfidenceFactors`

#### Tooltips

- VERIFIED:
  - previous tooltip text included phrases such as:
    - `This sprint looks manageable...`
    - `Confidence is high because this sprint is near-term...`
    - `Confidence is low because this sprint sits far enough out...`
  - tooltip path: `BuildTooltip`

#### Page hint

- VERIFIED:
  - previous page hint in `PlanBoard.razor` said:
    - `Background color shows sprint risk. Color strength shows how confident the current plan still is.`

#### Impact summaries

- VERIFIED:
  - previous summary text included:
    - `Sprint N now above normal load.`
    - `Confidence decreased for Sprint N after recent changes.`
    - `Confidence increased for Sprint N because the plan is steadier there.`
  - summary path: `PlanningBoardImpactSummaryBuilder.Build` → `ProductPlanningSprintSignalFactory.BuildDeltaSummaries`

### Misinterpretation risk per phrase

- VERIFIED:
  - `Risk low`
    - risk: users can read it as safe overall, not just low interpreted sprint strain
  - `Confidence high`
    - risk: users can read it as delivery likelihood, not plan stability
  - `This sprint looks manageable`
    - risk: users can read absence of warning as endorsement of the plan
  - `Confidence increased/decreased`
    - risk: users can read confidence as predictive certainty instead of a stability signal
  - page hint `shows sprint risk` / `shows how confident`
    - risk: the hint did not explicitly frame the UI as advisory and current-plan-based

## Misinterpretation risks

### 1. “High confidence” read as “we will deliver”

- VERIFIED
- UI elements:
  - confidence label from `BuildConfidenceLabel`
  - confidence tooltip text from `BuildConfidenceFactors`
  - page hint in `PlanBoard.razor`
  - delta summaries from `TryBuildConfidenceDeltaSummary`
- Evidence:
  - direct `Confidence high` / `Confidence decreased` phrasing in `ProductPlanningSprintSignals.cs`
  - tests before this phase asserted `Confidence high` in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`

### 2. “Low risk” read as “safe plan”

- VERIFIED
- UI elements:
  - risk label from `BuildRiskLabel`
  - low-risk chip from `BuildRiskFactors`
  - low-risk tooltip from `BuildRiskFactors`
- Evidence:
  - direct `Risk low`
  - low-risk tooltip text `This sprint looks manageable...`

### 3. Absence of warning read as “no problem”

- VERIFIED
- UI elements:
  - low-risk chip and tooltip
  - page hint above sprint heat
- Evidence:
  - `Load in range`
  - `This sprint looks manageable...`
  - no short hint that the visualization is only based on the current plan state

## Guardrail design

### Design rules followed

- IMPLEMENTED:
  - wording-only changes
  - no new UI component
  - no additional visual clutter
  - no signal logic change

### Exact text replacements

#### Labels

- IMPLEMENTED in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `Risk high` → `Strain elevated`
  - `Risk medium` → `Needs attention`
  - `Risk low` → `Within typical range`
  - `Confidence high` → `Plan stable (near-term)`
  - `Confidence medium` → `Plan less settled`
  - `Confidence low` → `Plan provisional`

#### Chips

- IMPLEMENTED in `BuildRiskFactors`, `BuildConfidenceFactors`, and `BuildChips`
  - `Load in range` → `Load within board norm`
  - `Confidence steady` → `Near-term plan stable`
  - `Low confidence (far future)` → `Far-future plan provisional`
  - `Far horizon limits confidence` → `Far-future view provisional`

#### Tooltips

- IMPLEMENTED in `BuildRiskFactors` and `BuildConfidenceFactors`
  - changed from direct state language such as `Confidence is high because...`
  - to interpretive wording such as:
    - `Based on the current plan...`
    - `...suggests higher planning strain...`
    - `...looks relatively stable...`
    - `...stays provisional...`

#### Page hint

- IMPLEMENTED in `PoTool.Client/Pages/Home/PlanBoard.razor`
  - previous: `Background color shows sprint risk. Color strength shows how confident the current plan still is.`
  - new: `Background color suggests sprint planning strain in the current plan. Color strength suggests how settled that sprint still looks, not delivery certainty.`

#### Impact summaries

- IMPLEMENTED in `TryBuildRiskDeltaSummary` and `TryBuildConfidenceDeltaSummary`
  - `Sprint N now above normal load.` → `Sprint N now suggests higher planning strain than usual.`
  - `Confidence decreased for Sprint N after recent changes.` → `Sprint N now looks more provisional after recent changes.`
  - `Confidence increased for Sprint N because the plan is steadier there.` → `Sprint N now looks more settled in the current plan.`

### Justification

- VERIFIED:
  - `Within typical range` preserves the low-risk meaning while removing the “safe plan” implication.
  - `Plan stable (near-term)` reframes confidence as present-plan stability, not delivery probability.
  - `Based on the current plan` anchors all tooltip claims to the current snapshot.
  - `suggests` reduces certainty tone.
  - `not delivery certainty` directly guards the most likely confidence misread without adding a new UI element.

## Changes implemented (with file references)

### `PoTool.Client/Models/ProductPlanningSprintSignals.cs`

- IMPLEMENTED:
  - updated user-facing risk labels
  - updated user-facing confidence labels
  - updated low-risk / high-confidence fallback chips
  - updated all tooltip sentences to current-plan interpretive wording
  - updated impact summary wording to stability/strain language instead of raw confidence language

### `PoTool.Client/Pages/Home/PlanBoard.razor`

- IMPLEMENTED:
  - updated the sprint-heat hint to define the UI as interpretive and explicitly not delivery certainty

### `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`

- IMPLEMENTED:
  - updated existing assertions for new labels/chips/tooltips
  - added a regression test that forbids `guarantee`, `will deliver`, `safe plan`, `Confidence high`, and `Risk low`

### `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`

- IMPLEMENTED:
  - updated summary assertions to new interpretive phrasing
  - added assertions that summary items do not drift back to `Confidence` wording or delivery-certainty language

### `docs/release-notes.json`

- IMPLEMENTED:
  - added a release note for the wording-only interpretation guardrails

## Before/after wording comparison

| UI element | Before | After |
|---|---|---|
| Risk label | `Risk low` | `Within typical range` |
| Confidence label | `Confidence high` | `Plan stable (near-term)` |
| Low-risk chip | `Load in range` | `Load within board norm` |
| Stable chip | `Confidence steady` | `Near-term plan stable` |
| Far-horizon chip | `Far horizon limits confidence` | `Far-future view provisional` |
| Low-risk tooltip | `This sprint looks manageable...` | `Based on the current plan, this sprint sits within the board's usual load and shape.` |
| Stable tooltip | `Confidence is high because...` | `Based on the current plan, this sprint looks relatively stable because...` |
| Page hint | `shows sprint risk... shows how confident...` | `suggests sprint planning strain... not delivery certainty` |
| Impact summary | `Confidence decreased for Sprint 2...` | `Sprint 2 now looks more provisional after recent changes.` |

## Tests added/updated

- IMPLEMENTED:
  - updated `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
    - `BuildColumns_UsesPlanningLanguageForStableNearTermSprint`
    - `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`
    - `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle`
    - added `BuildColumns_UsesInterpretiveLanguageInsteadOfDeliveryCertainty`
  - updated `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`
    - `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`

### Test evidence

- VERIFIED:
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningSprintSignalFactoryTests|FullyQualifiedName~PlanningBoardImpactSummaryBuilderTests" --no-restore`
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningBoardClientUiTests" --no-restore`

## Remaining risks

- NOT IMPLEMENTED:
  - the visualization still uses color and short labels, so users can still overread the signals if they skip the hint and tooltip text entirely.
  - latest-impact summaries remain short by design and do not explain the full signal model.
- VERIFIED:
  - minimal guardrails were sufficient; no new signal or heavy UI element was needed.

## Final section

### IMPLEMENTED

- Label guardrails
- Chip guardrails
- Tooltip guardrails
- Sprint heat hint guardrail
- Impact summary guardrails
- Regression tests for interpretive wording
- Release note update

### NOT IMPLEMENTED

- Any signal calculation change
- Any planning engine change
- Any persistence or API change
- Any new signal
- Any new UI component

### BLOCKERS

- None

### Evidence (files/tests)

- Files:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `PoTool.Client/Pages/Home/PlanBoard.razor`
  - `PoTool.Client/Models/PlanningBoardImpactSummary.cs`
  - `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`
  - `docs/release-notes.json`
- Tests:
  - targeted signal wording tests passed
  - targeted Plan Board UI tests passed
  - full solution test run passed after the wording changes

### GO/NO-GO for Phase 18 acceptance

- GO, contingent on final post-change solution validation staying green.
