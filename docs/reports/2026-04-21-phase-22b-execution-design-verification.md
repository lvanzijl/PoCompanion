# Phase 22b execution design verification

## 1. Summary

- VERIFIED: this phase reviews the Phase 22 design only. No planning logic, CDC implementation, persistence, API contracts, or UI were changed.
- VERIFIED: the Phase 22 design is documented in `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-22-execution-reality-check-design.md`.
- VERIFIED: the repository does provide stable historical execution facts for throughput and spillover, plus adjacent PR and pipeline analytics. Evidence: `docs/architecture/sprint-commitment-domain-model.md`, `PoTool.Shared/Metrics/SprintTrendDtos.cs`, `PoTool.Shared/Metrics/SprintExecutionDtos.cs`, `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`.
- WEAK: the Phase 22 design is directionally sound as an advisory layer, but the anomaly definitions are not yet precise enough for Phase 23 CDC extraction.
- INVALID: `Execution variability high` is not implementation-safe as currently defined because the design permits materially different input series (`delivered story points and/or commitment completion`) without defining one canonical denominator.
- WEAK: `Spillover increasing` is useful as a narrow indicator, but it does not yet align tightly enough with real carry-over behavior because canonical spillover excludes unfinished work that stays in the same sprint path and excludes backlog round-trips.
- WEAK: routing is partially valid for symptom exploration, but several routes are weak for root-cause diagnosis.
- NO-GO: Phase 23 should not start until the design-level adjustments in Section 7 are accepted.

## 2. Broken scenarios per anomaly

### 2.1 Delivery below typical range

- VERIFIED: the current design defines this anomaly as persistently low recent delivered scope, using delivered story points per completed sprint, optionally supported by completed PBI count.

#### Triggers but should NOT

1. WEAK: **Intentional scope reduction after a product enters a maintenance period.** Delivered story points drop, but execution is healthy because the team is intentionally carrying less scope. The current definition would still treat this as abnormal because it compares only to recent historical output, not to an explicit change in operating mode.
2. WEAK: **A team spends multiple sprints on bug fixing or task-heavy stabilization work.** The canonical model says bugs and tasks do not contribute to story points or velocity (`docs/architecture/domain-model.md`), so delivery can look abnormally low even when the team is executing as expected.
3. WEAK: **A product intentionally stops adding scope while finishing only a few high-value PBIs.** Low PBI count and low delivered story points could trigger, even though the outcome is a planned narrowing of scope rather than execution degradation.

#### Does NOT trigger but should

1. WEAK: **A sprint delivers large amounts of added work while committed work underperforms.** Canonical throughput includes delivered committed work and delivered added work (`docs/architecture/sprint-commitment-domain-model.md`), so the anomaly may stay quiet even though execution against planned commitments is poor.
2. WEAK: **A team keeps delivered story points near the median by splitting work into fewer, larger PBIs while completion reliability deteriorates.** The anomaly can miss a meaningful execution problem because it looks only at output volume.
3. WEAK: **A team repeatedly delivers unestimated or derived-estimate work.** `CompletedPbiStoryPoints` excludes derived or missing estimates in sprint metrics, so real delivery effort can disappear from the throughput series and distort both baseline and anomaly detection in opposite directions.

### 2.2 Execution variability high

- VERIFIED: the current design defines this anomaly as unusually variable delivered outcomes over the recent window, using per-sprint delivered story points and/or commitment completion interpreted with shared median/percentile or variance semantics.

#### Triggers but should NOT

1. INVALID: **Alternating sprint sizes caused by a known release cadence.** If one team deliberately alternates “integration” and “delivery” sprints, the series is variable by design, not by execution instability. The current anomaly has no guardrail for intentional cadence patterns.
2. WEAK: **Estimate-size variance, not execution variance.** A team that keeps execution rhythm stable but changes PBI sizing can produce a volatile story-point series. Because the design does not define a canonical denominator, estimate noise can masquerade as execution instability.
3. WEAK: **Mixed denominator variance.** A product can have stable delivered story points but volatile `CommitmentCompletion`, or the reverse, depending on commitment size and churn. The current design allows both series, so it could trigger from denominator changes rather than actual execution instability.

#### Does NOT trigger but should

1. INVALID: **Chronic execution instability hidden by stable averages.** If delivered story points remain near-median while spillover, churn, or completion order become erratic, the anomaly may not trigger because the current design does not define which variability dimension actually matters.
2. WEAK: **Operational instability concentrated inside a sprint.** Pipeline failures, PR review delays, and recovery spikes can create chaotic delivery behavior, but a single final delivered-story-point value per sprint can smooth that away.
3. WEAK: **Instability in products with many bug-only or task-heavy sprints.** Since bugs and tasks are excluded from canonical delivery units, execution can become highly erratic in practice while the PBI-based series remains too thin to expose it.

### 2.3 Spillover increasing

- VERIFIED: the current design defines this anomaly as committed work being carried into the next sprint more often, or in larger scope, across recent completed sprints.
- VERIFIED: canonical spillover is narrower than generic carry-over: it counts committed work not Done at sprint end whose first post-sprint move is directly into the next sprint (`docs/architecture/sprint-commitment-domain-model.md`).

#### Triggers but should NOT

1. WEAK: **A team intentionally re-sequences committed work into the next sprint for strategic batching.** Direct next-sprint moves would count as spillover even when the move is deliberate and healthy.
2. WEAK: **A release-train team always moves a small unfinished integration tail forward.** The anomaly could trigger on a stable operational pattern that is accepted and bounded.
3. WEAK: **A team keeps commitments intentionally tight but always moves one large PBI forward for dependency timing reasons.** Story-point-based spillover can look sharply worse than the underlying execution reality.

#### Does NOT trigger but should

1. INVALID: **Unfinished committed work remains in the same sprint path at sprint end and is only moved later.** Canonical spillover explicitly excludes this case, even though the work has effectively carried over.
2. INVALID: **Committed work goes back to the backlog and then enters the next sprint.** Canonical spillover explicitly excludes backlog round-trips, so the anomaly misses a real planning/execution failure pattern.
3. WEAK: **Committed work is replaced by added work that gets delivered in the same sprint.** Spillover can stay flat while the team is repeatedly failing to finish what it committed to, because the metric is sensitive only to the direct carry-over subset.

## 3. Temporal model weaknesses

- VERIFIED: the Phase 22 model uses an 8 completed sprint window, 3 consecutive sprint trigger rule, 2 sprint clear rule, and insufficient evidence below 6 completed sprints.

### Reacts too late

1. WEAK: **Sudden severe execution collapse.** A clear three-sprint degradation can remain labeled non-anomalous for two completed sprints, which is too slow for a “reality check” if the change is large and obvious.
2. WEAK: **Acute spillover step-change.** If spillover jumps sharply after a reorganization or dependency change, the 3-sprint persistence rule delays escalation until the problem is already entrenched.
3. WEAK: **Products with quarterly planning rhythm.** An 8-sprint window can anchor too heavily on a previous operating mode and react late after a genuine delivery regime change.

### Reacts too early

1. WEAK: **Normal post-release recovery dip.** Three quieter sprints after a major release could be enough to trigger even if the new pattern is intentional and healthy.
2. WEAK: **Temporary dependency cluster.** A three-sprint period with one external blocker repeated across products can trip the anomaly even if execution recovers naturally afterward.
3. WEAK: **Small sample products near the minimum threshold.** With only 6–8 usable sprints, three consecutive low points can dominate the entire baseline too easily.

### Never triggers but should

1. INVALID: **Alternating bad-good-bad-good instability.** The 3 consecutive sprint rule misses persistent instability that never stays consecutive, even though the execution pattern is clearly unreliable.
2. WEAK: **Long noisy decline with intermittent recovery.** A product can deteriorate over six or seven sprints without ever satisfying three consecutive anomalous sprints.
3. WEAK: **Sparse-history teams.** A team with exactly six usable sprints and one missing sprint slice may never reach enough consecutive evidence even when the available signals are strongly negative.

## 4. Interpretation weaknesses

### Stable / Watch / Investigate

- VERIFIED: the current design maps no sustained anomaly to `Stable`, one anomaly to `Watch`, two or more to `Investigate`, and low evidence to `Insufficient evidence`.

### Watch is too weak

1. WEAK: **One anomaly can be severe enough to deserve immediate investigation.** A major sustained delivery collapse would still only map to `Watch`.
2. WEAK: **One anomaly can already have direct consequence.** A strong spillover increase can materially affect trust in commitments even if no second anomaly is active.
3. WEAK: **Watch gives no distinction between mild and strong persistence.** Three barely abnormal sprints and three sharply abnormal sprints collapse into the same state.

### Investigate is too strong

1. WEAK: **Two correlated anomalies can be the same problem counted twice.** Low throughput and higher spillover often co-occur; mapping them automatically to `Investigate` may overstate confidence.
2. WEAK: **Two mild anomalies can outrank one major anomaly.** The current state logic counts anomaly quantity, not explanatory weight.
3. WEAK: **Intentional cadence shifts can produce two anomalies without root-cause uncertainty.** The state becomes too strong when the user already knows the operating-mode change.

### Ambiguity between states

1. INVALID: **No canonical severity model exists.** The state machine distinguishes count of active anomalies, not seriousness, so `Watch` and `Investigate` are not semantically stable.
2. WEAK: **Stable can be misleading during slow deterioration.** Two weakly negative sprints and one recovery sprint can still present as `Stable` even though the user should be cautious.
3. WEAK: **Insufficient evidence boundary is too blunt.** A team with six noisy sprints and a team with two clean sprints both collapse to evidence gating choices without a design rule for partial confidence.

## 5. Routing weaknesses

### Delivery below typical range

- VERIFIED: Phase 22 routes this anomaly to Trends, PR Insights, and Pipeline Insights.
- WEAK: **Trends explains the symptom, not the cause.** `Delivery Trends` shows throughput/completion movement, but it does not explain whether the drop came from scope starvation, review friction, pipeline instability, or intentional product-mode change.
- WEAK: **PR Insights is team/date scoped, not product-cause scoped.** The workspace is strong for workflow friction, but weak when the throughput drop is product-specific or backlog-specific.
- WEAK: **Pipeline Insights can be a dead end.** The route helps only when build instability is actually relevant; it is weak for backlog, dependency, or release-policy causes.

### Execution variability high

- VERIFIED: Phase 22 routes this anomaly to Trends, PR Insights, and Pipeline Insights.
- INVALID: **The destination set does not match the under-specified anomaly.** Because the anomaly itself is not canonically defined, the routes are not provably diagnostic.
- WEAK: **Trends still shows only the same variable symptom.** It helps confirm instability but not isolate what kind of instability is present.
- WEAK: **PR and pipeline routes are correlation routes, not causal routes.** They are useful only if workflow friction or build instability is involved.

### Spillover increasing

- VERIFIED: Phase 22 routes this anomaly to Trends and Backlog Health.
- INVALID: **Backlog Health is a current-state readiness surface, not a historical carry-over diagnostic surface.** Its purpose is refinement/readiness/structural maintenance, not analysis of why committed work carried over across recent sprints (`docs/architecture/navigation-map.md`).
- WEAK: **Trends can confirm spillover but not explain it.** The user still cannot tell whether carry-over came from poor refinement, churn, dependency blocking, or deliberate re-sequencing.
- WEAK: **No direct route exists to Sprint Execution.** The repository already documents `/home/delivery/execution` as the internal sprint diagnostics page, which is a more natural destination for committed-work carry-over analysis than Backlog Health.

## 6. Data reliability risks

### Incomplete sprint history

- VERIFIED: the design already acknowledges low-history risk.
- WEAK: **The current insufficient-evidence rule is too narrow.** It checks total completed sprint count, but not continuity, missing slices, or abrupt operating-mode resets.
- RISK: a product with 6 nominal sprints but one missing history segment can appear evidence-ready while the anomaly baseline is not stable.

### Mixed effort units

- VERIFIED: current repository metrics mix counts, effort, and story points in the same surfaces. Evidence: `SprintTrendDtos.cs` and `SprintMetricsProjectionEntity.cs` store `CompletedPbiCount`, `CompletedPbiEffort`, and `CompletedPbiStoryPoints`; `navigation-map.md` describes `Delivery Trends` with PBI throughput, effort throughput, and progress percentage.
- INVALID: the design assumes a clean execution signal layer while adjacent repo surfaces still expose mixed units. That makes diagnosis and user interpretation unstable unless Phase 23 states explicitly which unit the reality-check layer owns.
- RISK: users may compare the reality-check state against effort-based trend charts and infer contradictions that are only unit mismatches.

### Inconsistent backlog practices

- VERIFIED: canonical delivery units are PBIs only; bugs and tasks do not contribute to story points or velocity (`docs/architecture/domain-model.md`).
- WEAK: anomaly reliability drops for teams whose real delivery happens disproportionately through bugs, tasks, or unestimated PBIs.
- WEAK: teams with heavy in-sprint scope addition can look execution-healthy on throughput while committed-work follow-through is weak.
- RISK: the layer should degrade to `Insufficient evidence` when authoritative PBI story-point coverage is too low, when too much delivered scope is unestimated, or when a large share of execution occurs outside canonical PBI delivery units.

## 7. Required adjustments (design-level only)

1. INVALID: **Define one canonical series for each anomaly.**
   - `Delivery below typical range` must define whether it is based on delivered story points, delivered PBIs, or a paired rule.
   - `Execution variability high` must choose one canonical denominator and one canonical spread rule.
   - `Spillover increasing` must explicitly state whether it is narrow canonical spillover only, or a broader committed carry-over concept.

2. WEAK: **Separate “known operating-mode shift” from anomaly.**
   - The design needs an explicit rule for intentional maintenance mode, release hardening periods, or planned cadence changes, otherwise false positives remain too easy.

3. WEAK: **Strengthen evidence-gating rules.**
   - Evidence should degrade not only on sprint count, but also on missing continuity, low authoritative story-point coverage, high unestimated delivery share, and bug/task-dominant execution periods.

4. WEAK: **Redefine interpretation states using severity plus breadth, not anomaly count alone.**
   - One severe sustained anomaly should be able to outrank two weak correlated anomalies.

5. WEAK: **Adjust routing to match likely diagnosis paths.**
   - Spillover-related investigation should route primarily toward Sprint Execution or another historical execution surface, not Backlog Health alone.
   - Product-specific execution anomalies need a product-relevant drill-down path, not only team-scoped PR or pipeline workspaces.

6. WEAK: **Clarify what the layer is allowed to say when routes are only weakly causal.**
   - If PR Insights and Pipeline Insights are correlation checks rather than root-cause destinations, the design should say so explicitly.

## Final section

### VERIFIED

- VERIFIED: the repository has enough stable historical delivery and spillover facts to support an advisory execution layer in principle.
- VERIFIED: separating this layer from planning heat and planning stability remains the right boundary.

### WEAK

- WEAK: `Delivery below typical range` is meaningful but too permissive and too sensitive to operating-mode shifts.
- WEAK: `Spillover increasing` is meaningful but too narrow to stand in for general committed carry-over.
- WEAK: the 8/3/2 temporal model is conservative but can be both late and blind to alternating instability.
- WEAK: state interpretation and routing are not yet precise enough for reliable user trust.

### INVALID

- INVALID: `Execution variability high` is not yet canonically defined well enough for CDC extraction.
- INVALID: the current `Investigate = two or more anomalies` rule is not semantically robust enough to survive edge cases.
- INVALID: routing `Spillover increasing` primarily to Backlog Health is not a valid historical root-cause route.

### RISKS

- RISK: proceeding now would likely encode false positives for maintenance-mode teams and false negatives for bug-heavy or backlog-round-trip teams.
- RISK: mixed-unit interpretation could cause Phase 23 to harden a concept users cannot reconcile with existing trend surfaces.
- RISK: under-specified variability semantics could create unstable or non-repeatable anomaly outcomes.

### GO / NO-GO for Phase 23

- NO-GO: do not start Phase 23 CDC slice design until the anomaly semantics, evidence gating, state model, and routing rules above are tightened at design level.
