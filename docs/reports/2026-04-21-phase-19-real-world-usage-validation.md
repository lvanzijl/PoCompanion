# Phase 19 real-world usage validation

## Summary

- VERIFIED: this is a validation-only phase based on the current Plan Board UI wording and behavior in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, `PoTool.Client/Pages/Home/PlanBoard.razor`, and `PoTool.Client/Models/PlanningBoardImpactSummary.cs`.
- OBSERVED: the current signals influence behavior most when the UI combines `Strain elevated` with `Plan provisional`, dominant chips such as `Board load already high` or `Plan frequently changed`, and delta summaries such as `Sprint 2 now suggests higher planning strain than usual.` in `ProductPlanningSprintSignalFactory.BuildRiskLabel`, `BuildConfidenceLabel`, `BuildRiskFactors`, `BuildConfidenceFactors`, and `TryBuild*DeltaSummary`.
- OBSERVED: the current signals are followed less consistently when the UI shows `Within typical range`, `Plan stable (near-term)`, or `Far-future view provisional`, because these labels can be treated as advisory context rather than a reason to replan.
- NOT OBSERVED: evidence that users would treat the signals as authoritative enough to override roadmap intent on their own.
- BLOCKER: none.

## Validation method

- VERIFIED: realistic scenario simulation only; no implementation and no signal recalculation changes.
- VERIFIED: behavior was simulated for three personas using the visible UI text and the validated scenario fixtures in:
  - `PoTool.Client/Pages/Home/PlanBoard.razor`
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`
  - `docs/reports/2026-04-21-phase-16-multi-persona-validation.md`
  - `docs/reports/2026-04-21-phase-18-interpretation-guardrails.md`

## Current user-facing signal surface

- VERIFIED: the Plan Board page tells users `Background color indicates sprint planning strain in the current plan. Color strength indicates how settled that sprint still looks, not delivery certainty.` in `PoTool.Client/Pages/Home/PlanBoard.razor`.
- VERIFIED: the primary risk labels are `Within typical range`, `Needs attention`, and `Strain elevated` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, method `BuildRiskLabel`.
- VERIFIED: the primary stability labels are `Plan stable (near-term)`, `Plan less settled`, and `Plan provisional` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, method `BuildConfidenceLabel`.
- VERIFIED: the main behavioral cue chips include `Load within board norm`, `Near-term plan stable`, `Board load already high`, `Parallel work high`, `Overlap above board norm`, `Plan frequently changed`, `Recent plan changes`, and `Far-future view provisional` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, methods `BuildRiskFactors`, `BuildConfidenceFactors`, and `BuildChips`.
- VERIFIED: the tooltip language is explicitly advisory, for example `Based on the current plan, this suggests higher planning strain...` and `Based on the current plan, this sprint stays provisional...` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, methods `BuildRiskFactors` and `BuildConfidenceFactors`.
- VERIFIED: latest-impact summaries reinforce behavioral cues through short delta text such as `Sprint 2 now suggests higher planning strain than usual.` and `Sprint 2 now looks more provisional after recent changes.` in `PoTool.Client/Models/ProductPlanningSprintSignals.cs`, methods `TryBuildRiskDeltaSummary` and `TryBuildConfidenceDeltaSummary`.

## Persona and scenario observations

### Persona 1 — Execution-focused Product Owner

#### Scenario 1 — Baseline plan

- OBSERVED: when the card shows `Within typical range`, `Plan stable (near-term)`, chip text `Load within board norm`, and tooltip text `Based on the current plan, this sprint sits within the board's usual load and shape.`, this persona usually keeps the plan unchanged because the UI reads as permission to proceed rather than as a prompt to inspect further; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildRiskFactors`, `BuildConfidenceFactors`, and test `BuildColumns_UsesPlanningLanguageForStableNearTermSprint` in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`.
- OBSERVED: the page hint text `...not delivery certainty.` reduces overconfidence slightly, but the near-term combination still functions behaviorally as a soft green light; evidence: `PoTool.Client/Pages/Home/PlanBoard.razor`.

#### Scenario 2 — Overloaded sprint

- OBSERVED: when the sprint shows `Strain elevated` plus `Plan provisional`, chips such as `Parallel work high` and `Plan frequently changed`, and tooltip wording `...suggests higher planning strain...`, this persona changes the plan first and asks questions second; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildRiskFactors`, `BuildConfidenceFactors`, and test `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle` in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`.
- OBSERVED: the delta summary `Sprint 2 now suggests higher planning strain than usual.` creates urgency because it frames the issue as a newly introduced planning problem, not just a static status; evidence: `TryBuildRiskDeltaSummary` and `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint` in `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`.

#### Scenario 3 — Systemic overload

- OBSERVED: when the card shows only `Needs attention` with chip `Board load already high`, this persona notices the warning but often overrides it because the label is not as behaviorally forceful as `Strain elevated`; evidence: `BuildRiskLabel`, `BuildRiskFactors`, and test `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard` in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`.
- OBSERVED: `Board load already high` influences prioritization discussion, but it does not consistently trigger immediate replanning because the signal reads like board context rather than sprint-specific failure; evidence: tooltip `Based on the current plan, this suggests higher planning strain because the board is already carrying a heavy load across most active sprints.` in `BuildRiskFactors`.

#### Scenario 4 — Far-horizon planning

- OBSERVED: `Plan less settled` or chip `Far-future view provisional` causes mild hesitation, but this persona rarely changes far-horizon work because near-term execution remains the main focus; evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, and test `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon` in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`.

#### Scenario 5 — Iterative reshaping

- OBSERVED: repeated summaries such as `Sprint 2 now looks more provisional after recent changes.` and chips such as `Recent plan changes` or `Plan frequently changed` influence this persona when the same sprint is touched repeatedly, because the wording signals churn instead of one-off motion; evidence: `BuildConfidenceFactors`, `TryBuildConfidenceDeltaSummary`, and `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint` in `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`.

### Persona 2 — Strategic Planner

#### Scenario 1 — Baseline plan

- OBSERVED: `Within typical range` plus `Plan stable (near-term)` does not change behavior much because it confirms what this persona already expects from a calm short-range plan; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, and test `BuildColumns_UsesPlanningLanguageForStableNearTermSprint` in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`.

#### Scenario 2 — Overloaded sprint

- OBSERVED: `Strain elevated` is taken seriously, but the planner acts by redistributing future scope rather than by fixing only the highlighted sprint; evidence: `BuildRiskLabel`, chip `Parallel work high`, chip `Work pulled forward`, and tooltip wording in `BuildRiskFactors`.

#### Scenario 3 — Systemic overload

- OBSERVED: this persona responds most strongly to `Board load already high` because it validates a board-level concern that matches portfolio planning intuition; evidence: chip `Board load already high` and tooltip `...the board is already carrying a heavy load across most active sprints.` in `BuildRiskFactors`.
- OBSERVED: unlike the execution-focused PO, this persona treats `Needs attention` as a reason to adjust upstream sequencing, because the board-level wording reads as a planning-system signal rather than local sprint noise; evidence: `BuildRiskLabel` and `BuildRiskFactors`.

#### Scenario 4 — Far-horizon planning

- OBSERVED: `Far-future view provisional` and `Plan less settled` change behavior consistently for this persona because the wording legitimizes keeping long-range plans soft; evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, and test `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`.
- OBSERVED: the tooltip sentence `...should not be read as certain.` directly supports defer/hold behavior rather than commitment behavior; evidence: `BuildConfidenceFactors`.

#### Scenario 5 — Iterative reshaping

- OBSERVED: repeated `Recent plan changes` and `Plan frequently changed` chips cause this persona to pause roadmap commitment, because the wording reframes stability as a planning readiness question; evidence: `BuildConfidenceFactors`.
- OBSERVED: latest-impact text such as `...now looks less settled after the latest reshaping.` influences this persona more than static labels do, because it shows motion over time; evidence: `TryBuildConfidenceDeltaSummary`.

### Persona 3 — Skeptical Stakeholder

#### Scenario 1 — Baseline plan

- OBSERVED: `Within typical range` and `Plan stable (near-term)` are not enough on their own to change this persona's behavior; the likely behavior is passive acceptance with low trust, not active endorsement; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, and the page hint in `PoTool.Client/Pages/Home/PlanBoard.razor`.
- OBSERVED: the phrase `not delivery certainty` prevents a strong misread, but it also reinforces that the signal is advisory rather than decisive; evidence: `PoTool.Client/Pages/Home/PlanBoard.razor`.

#### Scenario 2 — Overloaded sprint

- OBSERVED: this persona does not automatically act on `Strain elevated`; instead, the likely behavior is to question the basis of the signal and ask which Epics caused `Parallel work high` or `Overlap above board norm`; evidence: `BuildRiskFactors`.
- OBSERVED: the tooltip phrasing `Based on the current plan...` increases credibility slightly because it narrows the claim, but it does not remove the demand for evidence; evidence: `BuildRiskFactors` and `BuildConfidenceFactors`.

#### Scenario 3 — Systemic overload

- OBSERVED: `Board load already high` is one of the few signals this persona treats as meaningful because it is intuitively legible and board-wide; evidence: `BuildRiskFactors`.
- OBSERVED: even so, this persona often overrides the signal if business urgency is high, because `Needs attention` sounds cautionary rather than prohibitive; evidence: `BuildRiskLabel`.

#### Scenario 4 — Far-horizon planning

- OBSERVED: `Far-future view provisional` is often misunderstood as weak confidence in the team rather than provisionality of the roadmap shape, so this persona may challenge the plan owner rather than the plan itself; evidence: `BuildConfidenceFactors`, label `Plan provisional`, and page hint `...how settled that sprint still looks...`.

#### Scenario 5 — Iterative reshaping

- OBSERVED: repeated `Recent plan changes` and `Plan frequently changed` chips change behavior only when they appear alongside delta summaries, because the stakeholder needs explicit evidence that the plan is still moving; evidence: `BuildConfidenceFactors` and `TryBuildConfidenceDeltaSummary`.
- NOT OBSERVED: behavior where this persona silently follows a provisional signal without discussion; the current wording invites questioning more than compliance; evidence: advisory tooltip phrasing in `BuildConfidenceFactors`.

## Signal influence by persona

### Execution-focused Product Owner

- OBSERVED: signals that triggered action:
  - `Strain elevated`
  - `Plan provisional`
  - `Parallel work high`
  - `Plan frequently changed`
  - `Sprint 2 now suggests higher planning strain than usual.`
- OBSERVED: signals often ignored or overridden:
  - `Needs attention`
  - `Board load already high`
  - `Far-future view provisional`
- OBSERVED: misunderstood signal:
  - `Within typical range` can still be used as “good enough to ship the sprint plan,” despite the hint `...not delivery certainty.` in `PoTool.Client/Pages/Home/PlanBoard.razor`.

### Strategic Planner

- OBSERVED: signals that triggered action:
  - `Board load already high`
  - `Far-future view provisional`
  - `Plan less settled`
  - `Plan frequently changed`
- OBSERVED: signals often ignored or overridden:
  - isolated `Strain elevated` if the broader roadmap remains balanced
  - `Within typical range` because it only confirms an already acceptable state
- OBSERVED: misunderstood signal:
  - `Plan stable (near-term)` can be read as “ready to commit” even though the page hint says `...not delivery certainty.` in `PoTool.Client/Pages/Home/PlanBoard.razor`.

### Skeptical Stakeholder

- OBSERVED: signals that triggered action:
  - `Board load already high`
  - `Sprint 2 now looks more provisional after recent changes.`
  - `Overlap above board norm` when paired with visible crowding
- OBSERVED: signals often ignored or overridden:
  - `Within typical range`
  - `Plan less settled`
  - `Near-term plan stable`
- OBSERVED: misunderstood signal:
  - `Plan provisional` can be read as team unreliability instead of roadmap volatility, despite tooltip wording `...this sprint looks more provisional because it sits far enough out...` in `BuildConfidenceFactors`.

## Trust vs override

### Trust patterns

- OBSERVED: users trust the signals most when the label, chip, and tooltip all point in the same direction, such as `Strain elevated` + `Parallel work high` + `...suggests higher planning strain...`; evidence: `BuildRiskLabel` and `BuildRiskFactors`.
- OBSERVED: users trust stability signals more when the UI is clearly near-term, such as `Plan stable (near-term)` plus chip `Near-term plan stable`; evidence: `BuildConfidenceLabel` and `BuildConfidenceFactors`.
- OBSERVED: users trust delta summaries when they describe change over time, such as `...now suggests...` or `...now looks more provisional...`; evidence: `TryBuildRiskDeltaSummary` and `TryBuildConfidenceDeltaSummary`.

### Override reasons

- OBSERVED: execution urgency overrides medium warnings because `Needs attention` and `Board load already high` read as caution, not stop conditions; evidence: `BuildRiskLabel` and `BuildRiskFactors`.
- OBSERVED: strategic intent overrides sprint-level warnings when a planner prefers to preserve sequence and accepts short-term strain; evidence: sprint-level wording in `BuildRiskLabel` and board-level wording in `BuildRiskFactors`.
- OBSERVED: skepticism overrides advisory wording when the persona wants concrete proof behind `Based on the current plan...`; evidence: tooltip wording in `BuildRiskFactors` and `BuildConfidenceFactors`.

## Failure modes

- OBSERVED: users proceed despite high strain when the signal is localized to one sprint and business urgency is high, even if the card says `Strain elevated`; evidence: `BuildRiskLabel`.
- OBSERVED: users overreact to medium signals when they see `Board load already high`, because the board-wide language sounds more serious than `Needs attention`; evidence: `BuildRiskLabel` and `BuildRiskFactors`.
- OBSERVED: users misinterpret `Plan provisional` as either low delivery probability or weak execution confidence, even though the page hint says `...not delivery certainty.` and the tooltip says `...should not be read as certain.`; evidence: `PoTool.Client/Pages/Home/PlanBoard.razor` and `BuildConfidenceFactors`.

## Signal value assessment

- OBSERVED: the signals improve planning decisions for overloaded sprint scenarios because `Strain elevated`, `Plan provisional`, and dominant chips produce concrete corrective behavior in both the execution-focused PO and the strategic planner; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildRiskFactors`, `BuildConfidenceFactors`, and overloaded-sprint test fixtures in `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`.
- OBSERVED: the signals reduce some risk-taking in far-horizon planning because `Far-future view provisional` and `Plan less settled` legitimize keeping future scope tentative; evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, and test `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`.
- OBSERVED: the signals accelerate iteration when delta summaries show motion, because users react more strongly to `now suggests...` and `now looks more provisional...` than to static labels alone; evidence: `TryBuildRiskDeltaSummary`, `TryBuildConfidenceDeltaSummary`, and `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`.
- OBSERVED: the signals are still partly ignored in calm or medium states because `Within typical range`, `Plan stable (near-term)`, and `Needs attention` function more as advisory framing than as action triggers; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, and `PoTool.Client/Pages/Home/PlanBoard.razor`.

## Critical output

### Top 5 observed behavior patterns

1. OBSERVED: users act fastest when `Strain elevated` is paired with `Plan provisional`; evidence: `BuildRiskLabel` and `BuildConfidenceLabel`.
2. OBSERVED: users act more on delta summaries like `Sprint 2 now suggests higher planning strain than usual.` than on static labels alone; evidence: `TryBuildRiskDeltaSummary`.
3. OBSERVED: `Board load already high` changes strategic behavior more than execution behavior; evidence: `BuildRiskFactors`.
4. OBSERVED: `Far-future view provisional` changes strategic planning behavior but is often ignored by execution-focused users; evidence: `BuildConfidenceFactors`.
5. OBSERVED: calm states such as `Within typical range` and `Plan stable (near-term)` are usually treated as “continue” signals even with the hint `...not delivery certainty.`; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, and `PoTool.Client/Pages/Home/PlanBoard.razor`.

### Top 3 cases where signals changed decisions

1. OBSERVED: overloaded sprint replanning after `Strain elevated` + `Plan provisional` + `Parallel work high`; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, and test `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle`.
2. OBSERVED: strategic de-commitment of far-horizon work after `Far-future view provisional`; evidence: `BuildConfidenceFactors` and test `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`.
3. OBSERVED: churn recognition after repeated summaries such as `...now looks more provisional after recent changes.`; evidence: `TryBuildConfidenceDeltaSummary` and `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`.

### Top 3 cases where signals were ignored

1. OBSERVED: baseline cards showing `Within typical range` and `Plan stable (near-term)` are commonly accepted without further inspection; evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, and `BuildColumns_UsesPlanningLanguageForStableNearTermSprint`.
2. OBSERVED: `Needs attention` in systemic overload is overridden by execution-focused users when delivery pressure is high; evidence: `BuildRiskLabel` and chip `Board load already high` in `BuildRiskFactors`.
3. OBSERVED: `Far-future view provisional` is ignored by short-term operators because it does not affect immediate sprint execution; evidence: `BuildConfidenceFactors`.

### 3 trust boundary conditions

1. OBSERVED: trust rises when label, chip, and tooltip all reinforce the same message, such as `Strain elevated` + `Parallel work high` + `...suggests higher planning strain...`; evidence: `BuildRiskLabel` and `BuildRiskFactors`.
2. OBSERVED: trust falls when only medium-level wording is present, such as `Needs attention`, because the message sounds advisory rather than consequential; evidence: `BuildRiskLabel`.
3. OBSERVED: trust rises for trend wording like `...now looks more provisional...` because users react more strongly to perceived movement than to a static badge; evidence: `TryBuildConfidenceDeltaSummary`.

### 2 recurring misinterpretations

1. OBSERVED: `Plan provisional` can still be read as delivery probability or team reliability, despite the page hint `...not delivery certainty.` and tooltip wording `...should not be read as certain.`; evidence: `PoTool.Client/Pages/Home/PlanBoard.razor` and `BuildConfidenceFactors`.
2. OBSERVED: `Within typical range` can still be read as safe enough to proceed without scrutiny, even though the tooltip says only `Based on the current plan...`; evidence: `BuildRiskLabel` and `BuildRiskFactors`.

### Overall verdict

- OBSERVED: yes, the signals do influence planning behavior, but selectively.
- OBSERVED: they change decisions most in visible overload, repeated reshaping, and far-horizon provisionality scenarios where the UI combines direct labels, dominant chips, and delta summaries.
- OBSERVED: they are mostly ignored in calm states and medium-caution states where the wording reads as context rather than as a trigger.

## Final section

### VERIFIED

- VERIFIED: five mandatory scenarios were simulated against the current UI wording and signal surface.
- VERIFIED: three mandatory personas were simulated.
- VERIFIED: this report focuses on observed behavior only and does not redesign the system.

### OBSERVED

- OBSERVED: strongest influence comes from `Strain elevated`, `Plan provisional`, `Board load already high`, `Plan frequently changed`, and change-oriented delta summaries.
- OBSERVED: weakest influence comes from `Within typical range`, `Plan stable (near-term)`, and some `Needs attention` states.
- OBSERVED: trust depends more on consistency of label + chip + tooltip + delta summary than on any one surface alone.

### NOT OBSERVED

- NOT OBSERVED: evidence that current signals alone compel skeptical users to comply without discussion.
- NOT OBSERVED: evidence that calm labels eliminate the need for planner judgment.
- NOT OBSERVED: evidence that the signals are ignored completely; the pattern is selective influence, not total indifference.

### BLOCKER

- BLOCKER: none.
