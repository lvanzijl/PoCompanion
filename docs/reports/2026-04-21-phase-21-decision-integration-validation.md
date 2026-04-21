# Phase 21 decision integration validation

## Summary

- VERIFIED: this is a validation-only phase. No code, UI structure, wording, planning engine logic, signal calculation, persistence model, or API contracts were changed. Evidence: current UI rendering in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`; current signal construction in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningSprintSignals.cs`; prior implementation boundary in `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-20-signal-amplification.md`.
- VERIFIED: this validation uses the current Plan Board decision surface after Phase 20, specifically the `Latest planning impact` alert, `Sprint heat` cards, `RiskLabel`, `ConfidenceLabel`, explanation chips, tooltip text, and sprint delta chips rendered in `PlanBoard.razor`.
- OBSERVED: signals are integrated into real decision-making mainly at moments of visible change, especially when `Sprint X now suggests higher planning strain than usual.` or `Sprint X now looks more provisional after recent changes.` appears in `Latest planning impact` and again on the sprint card. Evidence: `TryBuildRiskDeltaSummary`, `TryBuildConfidenceDeltaSummary`, `PlanBoardSprintSignalPresentation.GetDeltaSummaryForSprint`, `PlanBoard.razor`, Phase 19 report, and Phase 20 report.
- OBSERVED: signals influence decisions most when `RiskLabel`, dominant chip, and tooltip point to the same concern, such as `Strain elevated` + `Parallel work high` + `Based on the current plan, this suggests higher planning strain...`. Evidence: `BuildRiskLabel`, `BuildChips`, `BuildTooltip`, `BuildRiskFactors`, and Phase 19 findings.
- OBSERVED: signals remain partly advisory in calm and medium states; they influence discussion more reliably than they force decisions. Evidence: `Within typical range`, `Needs attention`, `Plan stable (near-term)` in `BuildRiskLabel` and `BuildConfidenceLabel`; neutral presentation in `PlanBoardSprintSignalPresentation`; prior findings in Phase 19 and Phase 20.
- BLOCKER: none.

## Validation method

- VERIFIED: realistic workflow simulation only, using the current Plan Board UI elements and validated fixtures rather than implementation changes.
- VERIFIED: decision-making behavior was evaluated across five mandatory workflows for three personas:
  - Execution-focused Product Owner
  - Strategic Planner
  - Skeptical Stakeholder
- VERIFIED: primary evidence sources:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/PlanBoardSprintSignalPresentationTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-19-real-world-usage-validation.md`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-20-signal-amplification.md`

## Decision entry points

### Execution-focused Product Owner

#### Sprint planning session
- OBSERVED: this persona looks first at the `Sprint heat` card only after a concrete move creates a visible delta in the `Latest planning impact` alert. The entry point is the delta chip, not the static `RiskLabel`. Evidence: `Latest planning impact` alert and sprint delta chip in `PlanBoard.razor`; `TryBuildRiskDeltaSummary`; Phase 20 report.
- OBSERVED: `Strain elevated`, `Plan provisional`, `Parallel work high`, and `Plan frequently changed` are consulted before finalizing a sprint move when they appear together on the same sprint card. Evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildChips`, `BuildTooltip`; test `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle`.

#### Roadmap shaping
- OBSERVED: this persona consults far-horizon stability labels late, after near-term sequencing is already preferred. `Far-future view provisional` is usually checked after an initial plan is formed, not before. Evidence: `BuildConfidenceFactors`; Phase 19 report; test `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`.

#### Scope negotiation
- OBSERVED: signals enter the workflow when a specific sprint turns hot during proposed scope addition. The persona references `Strain elevated` or `Sprint X now suggests higher planning strain than usual.` after a candidate scope move is discussed. Evidence: `BuildRiskLabel`, `TryBuildRiskDeltaSummary`, `PlanBoard.razor`.

#### Replanning after change
- OBSERVED: this is the strongest entry point. The persona checks the `Latest planning impact` alert first, then the affected `Sprint heat` card, then the dominant chip. Evidence: `PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries`, `GetDeltaSummaryForSprint`, `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`.

#### Stakeholder review
- OBSERVED: signals are consulted after the persona has already chosen a preferred plan, mainly to justify or defend that plan. Evidence: aligned label/chip/tooltip in `PlanBoard.razor`; Phase 19 trust findings.

### Strategic Planner

#### Sprint planning session
- OBSERVED: this persona reads `Sprint heat` earlier than the execution-focused PO and checks dominant chips before deciding whether local sprint strain reflects a board-level pattern. Evidence: `BuildChips`, `Board load already high`, `Parallel work high`, `Work pulled forward` in `BuildRiskFactors`.

#### Roadmap shaping
- OBSERVED: entry begins with stability and board-context signals rather than direct sprint strain. `Plan less settled`, `Far-future view provisional`, and `Board load already high` are consulted before final commitment to sequence. Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, `BuildRiskFactors`; Phase 19 report.

#### Scope negotiation
- OBSERVED: this persona checks whether added scope creates `Board load already high` or `Needs attention` across multiple sprints before accepting the proposed scope. Evidence: `BuildRiskLabel`, `BuildRiskFactors`, tooltip text in `BuildTooltip`.

#### Replanning after change
- OBSERVED: the planner uses delta summaries early and explicitly because they show the board moved, not just that one sprint is currently hot. Evidence: `TryBuildRiskDeltaSummary`, `TryBuildConfidenceDeltaSummary`, `Latest planning impact` alert in `PlanBoard.razor`.

#### Stakeholder review
- OBSERVED: this persona references chips and delta summaries directly during explanation, especially when defending a delay or de-commitment. Evidence: `Board load already high`, `Plan frequently changed`, `Sprint X now looks less settled after the latest reshaping.` in `ProductPlanningSprintSignals.cs`.

### Skeptical Stakeholder

#### Sprint planning session
- OBSERVED: this persona does not use signals as an initial entry point. They are consulted only after another participant points to `Strain elevated`, `Overlap above board norm`, or a delta chip. Evidence: `BuildRiskFactors`; Phase 19 report.

#### Roadmap shaping
- OBSERVED: the first consulted signals are board-level or change-driven signals, not calm-state labels. `Board load already high` and sprint delta chips are more likely to enter the discussion than `Within typical range`. Evidence: `BuildRiskFactors`, `TryBuild*DeltaSummary`, Phase 20 report.

#### Scope negotiation
- OBSERVED: signals enter when used as evidence by others. The stakeholder pays attention when the UI shows a concrete changed outcome such as `Sprint 2 now suggests higher planning strain than usual.` Evidence: `Latest planning impact` alert, sprint delta chip, `PlanBoard.razor`.

#### Replanning after change
- OBSERVED: this persona checks the delta chips before the tooltip because they summarize the impact of the most recent action. Evidence: `PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries`, `GetDeltaSummaryForSprint`, `PlanBoard.razor`.

#### Stakeholder review
- OBSERVED: signals are consulted during challenge/verification, not during initial acceptance. The persona asks for the basis behind `Parallel work high`, `Board load already high`, or `Plan provisional`. Evidence: `BuildRiskFactors`, `BuildConfidenceFactors`, tooltip text in `BuildTooltip`.

## Decision influence

### Execution-focused Product Owner

- OBSERVED: reduces scope or shifts Epics when `Strain elevated` and `Plan provisional` appear together on a near-term sprint, especially with `Parallel work high` or `Plan frequently changed`. Evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildRiskFactors`, `BuildConfidenceFactors`, test `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle`.
- OBSERVED: delays work less often for `Needs attention` plus `Board load already high`; this usually changes discussion tone rather than the actual plan unless a delta summary is also present. Evidence: `BuildRiskLabel`, `BuildRiskFactors`, test `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard`, Phase 19 report.
- OBSERVED: accepts risk anyway when business urgency is high and the signal remains medium or far-horizon. Evidence: `Needs attention`, `Far-future view provisional`, Phase 19 override patterns.

### Strategic Planner

- OBSERVED: shifts Epics and redistributes future work when board-level chips indicate sustained load, especially `Board load already high` and `Work pulled forward`. Evidence: `BuildRiskFactors`, `BuildTooltip`.
- OBSERVED: delays commitment for far-horizon work when `Plan less settled` or `Far-future view provisional` appears, even if current scope is not yet overloaded. Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, test `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`.
- OBSERVED: reduces parallelism when overlap and load signals converge, using `Overlap above board norm` plus `Needs attention` as a sequencing argument. Evidence: `BuildRiskFactors`, test `BuildColumns_KeepsDominantOverlapVisibleWhenWorkIsAlsoPulledForward`.

### Skeptical Stakeholder

- OBSERVED: rarely changes scope from a static label alone. Decision change usually happens only when another participant ties a visible delta chip to a concrete sequencing consequence. Evidence: `TryBuildRiskDeltaSummary`, `TryBuildConfidenceDeltaSummary`, `PlanBoard.razor`, Phase 20 report.
- OBSERVED: accepts deferral or re-sequencing when the UI shows both a board-level chip and a recent-change summary, because that combination reads as evidence rather than opinion. Evidence: `Board load already high`, sprint delta chips, `PlanBoard.razor`.
- OBSERVED: accepts risk anyway when the signal remains advisory (`Needs attention`) or when urgency outweighs the displayed concern. Evidence: `BuildRiskLabel`, Phase 19 report.

## Discussion integration

### Sprint planning session
- OBSERVED: the most common explicit argument is a change-driven one: `Sprint 2 now suggests higher planning strain than usual.` Participants use it to justify moving one Epic back out of the sprint. Evidence: `TryBuildRiskDeltaSummary`; test `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`; `Latest planning impact` alert in `PlanBoard.razor`.

### Roadmap shaping
- OBSERVED: `Far-future view provisional` and `Plan less settled` are used as arguments to avoid treating long-range sequence as committed. Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, `Sprint heat` section in `PlanBoard.razor`, Phase 19 report.

### Scope negotiation
- OBSERVED: `Board load already high` is used explicitly to justify saying “not in this sprint/shape,” especially by the strategic planner. Evidence: `BuildRiskFactors`, `BuildTooltip`, Phase 19 report.

### Replanning after change
- OBSERVED: `Latest planning impact` plus the matching sprint delta chip creates a shared conversational anchor; users discuss the changed sprint rather than debating whether anything changed. Evidence: `PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries`, `GetDeltaSummaryForSprint`, `PlanBoard.razor`, Phase 20 report.

### Stakeholder review
- OBSERVED: chips are used more often than tooltips in spoken discussion, but the tooltip text provides credibility when challenged. Evidence: dominant explanation chips in `BuildChips`; tooltip synthesis in `BuildTooltip`; Phase 19 skeptical-stakeholder findings.

## Override behavior

### Execution-focused Product Owner
- OBSERVED: proceeds despite `Strain elevated` when the sprint is treated as a necessary delivery spike and the affected work is considered non-negotiable. Evidence: `BuildRiskLabel`; Phase 19 failure-mode findings.
- OBSERVED: proceeds despite `Board load already high` because it reads as board context instead of immediate local failure. Evidence: `BuildRiskFactors`, test `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard`.
- OBSERVED: proceeds despite `Plan provisional` for far-horizon items because only near-term delivery is treated as actionable. Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, Phase 19 report.

### Strategic Planner
- OBSERVED: overrides `Strain elevated` when preserving strategic sequence matters more than reducing local sprint heat. Evidence: `BuildRiskLabel`, Phase 19 override patterns.
- OBSERVED: accepts `Board load already high` temporarily if broader roadmap balance improves over multiple sprints. Evidence: board-level chip `Board load already high`; tooltip in `BuildRiskFactors`.
- OBSERVED: does not override `Plan provisional` as often; it usually supports a deliberate delay in commitment. Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`.

### Skeptical Stakeholder
- OBSERVED: ignores `Strain elevated` unless another signal explains why, such as `Parallel work high` or `Overlap above board norm`. Evidence: `BuildRiskFactors`, Phase 19 report.
- OBSERVED: overrides `Board load already high` when external urgency dominates and the signal still reads as advisory rather than prohibitive. Evidence: `BuildRiskLabel`, `BuildRiskFactors`.
- OBSERVED: challenges `Plan provisional` as wording about team reliability unless the tooltip reframes it as planning volatility. Evidence: `BuildConfidenceFactors`, `BuildTooltip`, Phase 19 report.

## Outcome difference

### 1. Sprint planning session
- OBSERVED: with signals considered, the execution-focused PO moves or removes near-term scope after `Strain elevated` + `Plan provisional` + `Parallel work high`.
- OBSERVED: if signals are ignored, the sprint remains overloaded and parallelism stays higher.
- Evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildRiskFactors`, `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle`.

### 2. Roadmap shaping
- OBSERVED: with signals considered, the strategic planner keeps far-horizon work softer and delays exact sequence commitment when `Far-future view provisional` or `Plan less settled` appears.
- OBSERVED: if signals are ignored, roadmap sequence is treated as more committed and less adjustable than the UI implies.
- Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`.

### 3. Scope negotiation
- OBSERVED: with signals considered, added scope is more likely to be shifted later when `Board load already high` or `Needs attention` appears across the affected sprint.
- OBSERVED: if signals are ignored, the plan accepts more scope into already pressured areas.
- Evidence: `BuildRiskLabel`, `BuildRiskFactors`, `BuildTooltip`, `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard`.

### 4. Replanning after change
- OBSERVED: with signals considered, change deltas accelerate re-sequencing because the conversation starts from “what became riskier/less settled.”
- OBSERVED: if signals are ignored, replanning focuses only on changed Epics and misses board-level consequences on the affected sprint.
- Evidence: `TryBuildRiskDeltaSummary`, `TryBuildConfidenceDeltaSummary`, `Latest planning impact` alert, sprint delta chip, `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`.

### 5. Stakeholder review
- OBSERVED: with signals considered, stakeholder review is more likely to approve delay, reduced scope, or deferred commitment when the board shows aligned chips and recent-change summaries.
- OBSERVED: if signals are ignored, stakeholder decisions are more likely to fall back to urgency and prior intent alone.
- Evidence: `BuildChips`, `BuildTooltip`, `PlanBoard.razor`, Phase 19 and Phase 20 reports.

## Value assessment

- OBSERVED: signals improve planning decisions when they surface a newly introduced strain or instability clearly enough to affect scope, timing, or sequencing.
- OBSERVED: signals improve communication because delta summaries and dominant chips provide compact language that participants can cite directly in discussion.
- OBSERVED: signals reduce some risky plans by making overload, overlap, and churn visible before commitment, especially for the strategic planner.
- OBSERVED: signals partly prevent overcommitment, but mainly when visible change is present; calm and medium states remain more advisory.
- NOT OBSERVED: evidence that signals alone dominate final decisions when business urgency or strategic intent strongly conflicts with them.

## Critical output

### Top 5 decision points where signals influenced outcomes

1. OBSERVED: after a move action, the `Latest planning impact` delta `Sprint 2 now suggests higher planning strain than usual.` triggers immediate near-term scope reconsideration. Evidence: `TryBuildRiskDeltaSummary`, `PlanBoard.razor`, `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`, Phase 20 report.
2. OBSERVED: `Strain elevated` + `Plan provisional` + `Parallel work high` changes sprint-planning outcomes by prompting Epic shifting or scope reduction. Evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildRiskFactors`, `BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle`.
3. OBSERVED: `Board load already high` influences roadmap and scope negotiation outcomes by legitimizing a no-now decision. Evidence: `BuildRiskFactors`, `BuildTooltip`, `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard`.
4. OBSERVED: `Far-future view provisional` changes roadmap shaping outcomes by reducing commitment to exact long-range sequencing. Evidence: `BuildConfidenceFactors`, `BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon`.
5. OBSERVED: repeated change indicators such as `Plan frequently changed` plus `Sprint 2 now looks more provisional after recent changes.` change replanning outcomes by pausing commitment and reopening sequence choices. Evidence: `BuildConfidenceFactors`, `TryBuildConfidenceDeltaSummary`, `Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint`.

### Top 3 cases where signals were used in discussions

1. OBSERVED: `Sprint 2 now suggests higher planning strain than usual.` is used as an explicit argument to move work out of the sprint. Evidence: `TryBuildRiskDeltaSummary`, `Latest planning impact` alert, Phase 20 report.
2. OBSERVED: `Board load already high` is used to justify rejecting additional scope or delaying an Epic. Evidence: `BuildRiskFactors`, `BuildTooltip`, Phase 19 report.
3. OBSERVED: `Far-future view provisional` and `Plan less settled` are used to justify keeping roadmap commitments soft during shaping discussions. Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, Phase 19 report.

### Top 3 cases where signals were ignored or overridden

1. OBSERVED: `Needs attention` is overridden in execution-focused sprint planning when delivery urgency remains high. Evidence: `BuildRiskLabel`, `BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard`, Phase 19 report.
2. OBSERVED: `Within typical range` and `Plan stable (near-term)` are treated as implicit approval and do not materially change decisions in calm workflows. Evidence: `BuildRiskLabel`, `BuildConfidenceLabel`, `BuildColumns_UsesPlanningLanguageForStableNearTermSprint`, Phase 19 and Phase 20 reports.
3. OBSERVED: `Plan provisional` is overridden or reframed during stakeholder review when users interpret it as advisory rather than decisive. Evidence: `BuildConfidenceLabel`, `BuildConfidenceFactors`, Phase 19 report.

### 3 conditions under which signals are trusted in decisions

1. OBSERVED: trust rises when label, dominant chip, and tooltip align on the same message. Evidence: `BuildRiskLabel`, `BuildChips`, `BuildTooltip`, `BuildRiskFactors`, Phase 19 report.
2. OBSERVED: trust rises when the signal is change-driven and visible in both the `Latest planning impact` alert and the sprint card. Evidence: `TryBuild*DeltaSummary`, `PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries`, `GetDeltaSummaryForSprint`, Phase 20 report.
3. OBSERVED: trust rises when the signal is board-level and intuitively legible, such as `Board load already high`. Evidence: `BuildRiskFactors`, `BuildTooltip`, Phase 19 report.

### 2 conditions where signals fail to influence decisions

1. OBSERVED: signals fail to influence decisions when the state is calm or medium and no visible change event is attached. Evidence: `Within typical range`, `Needs attention`, `Plan stable (near-term)` in `BuildRiskLabel` and `BuildConfidenceLabel`; neutral calm-state rendering in Phase 20 report.
2. OBSERVED: signals fail to influence decisions when business urgency or strategic intent outweighs advisory wording. Evidence: Phase 19 override patterns; advisory tooltip phrasing in `BuildTooltip`.

### Overall verdict

- OBSERVED: signals are integrated into real decision-making, but mainly as decision-support evidence rather than decision authority. They enter workflows most strongly at replanning, scope negotiation, and roadmap-shaping moments when change is visible and aligned signals reinforce one another. They are not fully authoritative in calm or medium states and are still overridden when urgency or strategic intent dominates.

## Final section

### VERIFIED

- Validation-only phase completed with no implementation.
- Decision integration was evaluated against current UI elements and code paths after Phase 20 amplification.

### OBSERVED

- Signals do influence planning discussions and decisions, especially through change-driven delta summaries and aligned label/chip/tooltip combinations.
- The strongest workflow impact occurs during replanning after change and in scope negotiation.
- Strategic planning decisions are more sensitive to board-level and far-horizon signals than execution-focused decisions are.

### NOT OBSERVED

- Signals acting as a standalone authority that consistently overrides urgency, stakeholder pressure, or strategic sequence intent.
- Strong decision influence from calm-state labels without a change event.

### BLOCKER

- None.

### Evidence (files/tests)

- UI elements:
  - `Latest planning impact` alert in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
  - `Sprint heat` section and sprint delta chips in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
  - labels `Strain elevated`, `Needs attention`, `Within typical range`, `Plan provisional`, `Plan less settled`, `Plan stable (near-term)` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningSprintSignals.cs`
- Code paths:
  - `BuildRiskLabel`
  - `BuildConfidenceLabel`
  - `BuildChips`
  - `BuildTooltip`
  - `BuildRiskFactors`
  - `BuildConfidenceFactors`
  - `BuildDeltaSummaries`
  - `TryBuildRiskDeltaSummary`
  - `TryBuildConfidenceDeltaSummary`
  - `PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries`
  - `PlanBoardSprintSignalPresentation.GetDeltaSummaryForSprint`
- Test evidence:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/PlanBoardSprintSignalPresentationTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`
- Prior validation artifacts:
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-19-real-world-usage-validation.md`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-20-signal-amplification.md`
