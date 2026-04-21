# Summary

- VERIFIED: the original Phase 15 signal implementation in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningSprintSignals.cs` behaved more like a colored metric system than a judgment system because it mapped raw counts directly into risk and confidence buckets without board-relative normalization.
- IMPLEMENTED: calibrated the existing heuristics in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningSprintSignals.cs` so risk now uses board-relative baselines plus weighted structural guardrails, and confidence now uses gradual horizon decay with smaller change penalties.
- IMPLEMENTED: hardened `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs` to cover stability, gradual confidence decay, risk/confidence separation, and non-regression away from raw metric exposure/global-score language.
- VERIFIED: targeted regression commands passed after calibration.

# Current signal formulas

## Phase 1 — signal nature

### Pre-calibration evidence

- VERIFIED: command used to inspect the previous implementation:
  - `git --no-pager show HEAD^:PoTool.Client/Models/ProductPlanningSprintSignals.cs | sed -n '72,185p'`
- VERIFIED: before calibration, risk was the direct sum of four raw threshold buckets:
  - active Epics: `>=4 => +2`, `>=3 => +1`
  - active tracks: `>=3 => +2`, `>=2 => +1`
  - forward shifts: `>=2 => +2`, `1 => +1`
  - overlap pairs: `>=3 => +2`, `>=1 => +1`
  - final classes: `>=5 => High`, `>=2 => Medium`, else `Low`
- VERIFIED: before calibration, confidence used a stepped raw penalty:
  - distance bucket: sprint index `>=6 => +3`, `>=4 => +2`, `>=2 => +1`
  - changed/affected Epics: `>=2 => +2`, `1 => +1`
  - structure/forward change: both parallel+overlap => `+2`, any one of parallel/overlap/forward => `+1`
  - final classes: `<=1 => High`, `<=2 => Medium`, else `Low`
- VERIFIED: pre-calibration logic used no board-relative baselines and no gradual horizon normalization.

### Judgment answer

- VERIFIED: before calibration, this was a **colored metric system**, not a true interpreted judgment system.

### Post-calibration evidence

- VERIFIED: calibrated logic lives in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningSprintSignals.cs` lines 72-223.
- IMPLEMENTED: current risk logic now has two stages:
  1. collect raw per-sprint counts
     - active Epic count
     - active track count
     - overlap pair count
     - changed/affected Epic count
     - forward-shift count
  2. calibrate against board-relative baselines
     - `loadBaseline = average active Epic count over active sprints`
     - `trackBaseline = average active track count over active sprints`
     - `overlapBaseline = average overlap-pair count over active sprints`
- IMPLEMENTED: current risk score uses weighted contributions:
  - load:
    - `>= max(5, ceil(loadBaseline + 1.25)) => +1.55`
    - `>= 4 => +1.10`
    - `>= max(2, ceil(loadBaseline + 0.5)) => +0.80`
  - parallel structure:
    - `activeTrackCount >= 3 => +1.10`
    - `activeTrackCount >= 2 && trackBaseline < 1.5 => +0.55`
    - `activeTrackCount >= 2 && activeEpicCount >= 3 && activeTrackCount > trackBaseline => +0.65`
  - forward pull-in:
    - `forwardShiftCount >= max(2, ceil(activeEpicCount * 0.5)) => +0.95`
    - `forwardShiftCount > 0 => +0.40`
  - overlap pressure:
    - `overlapPairCount >= 3` or `>= ceil(overlapBaseline + 1) + 1 => +0.95`
    - `overlapPairCount >= ceil(overlapBaseline + 1) => +0.45`
    - `overlapPairCount > 0 && activeTrackCount >= 3 => +0.30`
  - final classes:
    - `>= 3.00 => High`
    - `>= 1.25 => Medium`
    - otherwise `Low`
- IMPLEMENTED: current confidence logic now uses gradual horizon decay plus smaller instability penalties:
  - `distanceRatio = sprintIndex / (sprintCount - 1)` and base decay is `distanceRatio * 1.9`
  - changed/affected Epics:
    - `>= max(3, ceil(activeEpicCount * 0.75)) => +1.00`
    - `>= 2 => +0.55`
    - `1 => +0.30`
  - structure change:
    - parallel+overlap changed => `+0.75`
    - one of parallel/overlap changed => `+0.45`
  - forward shift:
    - `>= max(2, ceil(activeEpicCount * 0.5)) => +0.55`
    - `> 0 => +0.30`
  - final classes:
    - `>= 2.75 => Low`
    - `>= 1.15 => Medium`
    - otherwise `High`

### Judgment answer after calibration

- VERIFIED: after calibration, this is now a **judgment system** built from normalized heuristics rather than a thin color wrapper around raw counts.

# Issues found

## Phase 2 — stability verification

- VERIFIED: the old implementation could flip far-future confidence too aggressively because a single changed Epic on a distant sprint stacked a large raw distance bucket and a full raw change point.
- VERIFIED: the old implementation treated raw crowding counts as universally meaningful even when the same density was normal for the whole board.
- VERIFIED: the old implementation used stepped distance buckets (`0/1/2/3`) instead of gradual decay, which made confidence more binary than intended.
- VERIFIED: the calibration tests added in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs` now exercise those exact stability risks.

# Calibration changes

## Phase 3 — heuristic calibration

- IMPLEMENTED: introduced board-relative baselines for load, active tracks, and overlap pressure without adding any new data source.
- IMPLEMENTED: converted risk from equal raw threshold points into weighted contributions so small changes do not jump as sharply while large structural compression still reaches `High`.
- IMPLEMENTED: replaced stepped confidence distance buckets with gradual horizon decay using `distanceRatio * 1.9`.
- IMPLEMENTED: reduced confidence sensitivity for single changed Epics so minor far-future movement does not collapse directly to `Low`.
- IMPLEMENTED: kept risk and confidence separate:
  - risk still reflects crowding/parallelism/pull-in/overlap
  - confidence still reflects horizon distance plus recent instability
- VERIFIED: no backend planning logic, persistence, TFS integration, or external data source was changed.

# Before/after behavior

- VERIFIED: before calibration, the inspected implementation from `git show HEAD^:PoTool.Client/Models/ProductPlanningSprintSignals.cs` was a raw-threshold color system.
- VERIFIED: after calibration, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs` now proves:
  - typical dense boards can remain `Risk low` when that density is normal for the board
  - a minor far-future change stays `Confidence medium` instead of dropping straight to `Low`
  - stable horizons decay confidence gradually instead of oscillating
  - near-term high-risk crowding can still coexist with `Confidence high`
  - public sprint-signal output does not expose a global score field or raw score/ratio wording

# Test/governance results

- VERIFIED: command:
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningSprintSignalFactoryTests|FullyQualifiedName~PlanningBoardImpactSummaryBuilderTests" --no-restore`
  - result: Passed `10`, Failed `0`
- VERIFIED: command:
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningBoardClientUiTests" --no-build`
  - result: Passed `16`, Failed `0`
- VERIFIED: command:
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build -v minimal`
  - result: Passed `2100`, Failed `0`, Skipped `0`
- VERIFIED: command:
  - `python -m json.tool docs/release-notes.json`
  - result: valid JSON

# Remaining risks

- NOT IMPLEMENTED: no dependency-aware confidence model; the phase explicitly forbade adding new data sources.
- NOT IMPLEMENTED: no velocity or delivery-history input; calibration remains intentionally board-local.
- VERIFIED: the heuristics are now more stable, but they remain heuristic classifications rather than mathematically learned forecasts.

# Recommendation

- GO: keep the calibrated heuristic model.
- Recommendation: if future review still finds noisy boards, tune the existing weights and thresholds first before considering any architectural redesign or external inputs.

# Final section

## IMPLEMENTED

- Board-relative normalization for risk heuristics.
- Gradual confidence decay across the sprint horizon.
- Reduced sensitivity for minor far-future changes.
- Stability/regression tests for risk-confidence calibration.

## NOT IMPLEMENTED

- New data sources.
- Velocity-based or dependency-based modeling.
- Any redesign of the planning board signal architecture.

## BLOCKERS

- None.

## Evidence (files/tests/commands)

- Files:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`
- Commands:
  - `git --no-pager show HEAD^:PoTool.Client/Models/ProductPlanningSprintSignals.cs | sed -n '72,185p'`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningSprintSignalFactoryTests|FullyQualifiedName~PlanningBoardImpactSummaryBuilderTests" --no-restore`
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~ProductPlanningBoardClientUiTests" --no-build`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build -v minimal`
  - `python -m json.tool docs/release-notes.json`

## GO/NO-GO for returning to normal development

- GO
