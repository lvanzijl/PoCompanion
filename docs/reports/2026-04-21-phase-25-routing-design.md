# Phase 25 routing design

## 1. Summary

- VERIFIED: this phase stays in design scope only and does not modify the Phase 23c CDC slice, the Phase 24 interpretation layer, planning logic, UI, or routing implementation.
- VERIFIED: the routing design follows the corrected Phase 22c anomaly definitions exactly:
  - `completion-below-typical`
  - `completion-variability`
  - `spillover-increase`
- JUSTIFIED: each anomaly is mapped to exactly one primary diagnostic destination.
- WEAK: PR and pipeline views remain useful secondary correlation surfaces, but they are not chosen as primary routes because they do not answer the first execution-diagnostic question directly.
- RISK: some true causes may live outside the first-route workspace, so the routing layer must remain explicit that it selects the best first investigation point, not a guaranteed root-cause destination.

## 2. Root cause analysis per anomaly

### 2.1 Commitment completion below typical range

- VERIFIED: this anomaly means the team is finishing a persistently smaller share of originally committed, still-in-scope story-point work than is typical for its own recent execution history.

- JUSTIFIED: most likely execution-related root causes are:
  1. committed work remained unfinished inside the sprint
  2. later-added work displaced originally planned work
  3. initially committed work starved while other work completed first

- VERIFIED: these are execution causes, not planning causes, because they concern what happened to committed work after sprint start.

### 2.2 Commitment completion variability high

- VERIFIED: this anomaly means the recent commitment-completion series swings outside the team’s recent normal spread around its own median-centered baseline.

- JUSTIFIED: most likely execution-related root causes are:
  1. completion behavior is unstable from sprint to sprint even when commitment exists
  2. added-vs-planned execution behavior changes materially across adjacent sprints
  3. unfinished or starved committed work appears intermittently rather than consistently

- VERIFIED: these are execution causes, not planning causes, because the anomaly is defined on historical commitment completion behavior, not forecast or backlog-readiness semantics.

### 2.3 Direct next-sprint spillover rate increasing

- VERIFIED: this anomaly means a persistently larger share of committed, still-in-scope work is moving directly into the next sprint unfinished.

- JUSTIFIED: most likely execution-related root causes are:
  1. committed PBIs are not finishing within the sprint window
  2. work added during the sprint completes while originally committed work carries over
  3. execution order leaves initial scope unfinished at sprint close

- VERIFIED: these are execution causes, not planning causes, because the anomaly is defined on direct carry-over of committed scope after sprint execution begins.

## 3. Final routing table

| AnomalyKey | Primary route |
| --- | --- |
| `completion-below-typical` | `/home/delivery/execution` |
| `completion-variability` | `/home/trends/delivery` |
| `spillover-increase` | `/home/delivery/execution` |

- VERIFIED: the table is strict 1:1.
- VERIFIED: no secondary routes are part of the routing contract.

## 4. Route justification

### 4.1 `completion-below-typical` → `/home/delivery/execution`

- JUSTIFIED: Sprint Execution is the best first step because it directly answers whether originally committed work was finished, displaced, starved, removed, or left unfinished.
- JUSTIFIED: the user is expected to learn:
  - how much initial scope completed
  - which committed PBIs stayed unfinished
  - whether later-added work finished ahead of planned work
  - whether starvation signals are present

- JUSTIFIED: other destinations are not primary because:
  - `/home/trends/delivery` shows history shape, but not the per-sprint execution mechanics that explain a current completion drop
  - `/home/pull-requests` shows workflow friction correlation, not committed-scope execution outcome
  - `/home/pipeline-insights` shows build stability correlation, not committed-scope disposition
  - `/home/health/backlog-health` is planning/readiness-oriented and is outside the execution-first diagnostic question

### 4.2 `completion-variability` → `/home/trends/delivery`

- JUSTIFIED: Delivery Trends is the best first step because the first diagnostic question is when the instability started and whether it is persistent or episodic across multiple sprints.
- JUSTIFIED: the user is expected to learn:
  - whether the abnormal completion swings are clustered or sustained
  - whether the instability is recent or longstanding
  - whether the problem is visible as a multi-sprint historical pattern rather than a single-sprint event

- JUSTIFIED: other destinations are not primary because:
  - `/home/delivery/execution` is optimized for one sprint’s execution details, not the onset and persistence pattern of a multi-sprint anomaly
  - `/home/pull-requests` and `/home/pipeline-insights` may explain correlation later, but they are not the first place to confirm the historical shape of the anomaly
  - `/home/health/backlog-health` is not an execution-history surface

### 4.3 `spillover-increase` → `/home/delivery/execution`

- JUSTIFIED: Sprint Execution is the best first step because the first diagnostic question is which committed work rolled forward unfinished and what happened to that work inside the sprint.
- JUSTIFIED: the user is expected to learn:
  - which PBIs remained unfinished at sprint close
  - whether carry-over coincided with added work or starvation
  - whether the spillover behavior is rooted in execution order or unfinished initial scope

- JUSTIFIED: other destinations are not primary because:
  - `/home/trends/delivery` can confirm persistence later, but it does not expose the specific unfinished and displaced work needed for first diagnosis
  - `/home/pull-requests` and `/home/pipeline-insights` are indirect technical-friction views rather than direct carry-over diagnostics
  - `/home/health/backlog-health` addresses backlog maturity and refinement, not direct next-sprint spillover mechanics

## 5. Failure analysis

### 5.1 `completion-below-typical` route failure cases

- RISK: `/home/delivery/execution` will not help much when the true driver is external delivery friction that is not visible in sprint scope movement, such as review or build delays without obvious starvation or added-scope evidence.
- RISK: likely misclassification occurs when a persistent completion drop is actually the visible symptom of PR or pipeline friction rather than sprint-execution displacement mechanics.

### 5.2 `completion-variability` route failure cases

- RISK: `/home/trends/delivery` will not help much when the variability is already known and the missing answer is the within-sprint mechanism for the latest affected sprint.
- RISK: likely misclassification occurs when a variability signal is technically correct but operationally dominated by one acute execution breakdown that would be faster to inspect in Sprint Execution.

### 5.3 `spillover-increase` route failure cases

- RISK: `/home/delivery/execution` will not help much when carry-over is obvious but the true driver lives in deeper technical workflow friction that the page does not expose directly.
- RISK: likely misclassification occurs when strict canonical spillover captures persistent carry-over, but the most actionable explanation is outside sprint-scope mechanics and instead sits in PR or pipeline behavior.

## 6. Minimal routing contract

- VERIFIED: the minimal internal contract is:

```text
completion-below-typical -> /home/delivery/execution
completion-variability   -> /home/trends/delivery
spillover-increase       -> /home/delivery/execution
```

- VERIFIED: the contract contains only `AnomalyKey -> Route`.
- VERIFIED: no UI labels are part of the contract.
- VERIFIED: no navigation logic is part of the contract.
- VERIFIED: no deep-link parameters are part of the contract yet.

## Final section

### VERIFIED

- VERIFIED: the routing design stays within routing-only scope
- VERIFIED: each anomaly maps to exactly one primary destination
- VERIFIED: the selected routes match the corrected Phase 22c anomaly definitions
- VERIFIED: the contract is minimal and internal-only

### JUSTIFIED

- JUSTIFIED: `completion-below-typical` routes first to Sprint Execution because the first question is what happened to committed scope inside the sprint
- JUSTIFIED: `completion-variability` routes first to Delivery Trends because the first question is when and how the instability appears across sprints
- JUSTIFIED: `spillover-increase` routes first to Sprint Execution because the first question is which committed work carried over and why

### WEAK

- WEAK: the design assumes current workspace capabilities remain aligned with their documented purposes
- WEAK: some teams may need PR or pipeline investigation immediately after the primary route, especially when execution symptoms are downstream of technical workflow friction
- WEAK: variability is the least single-surface-friendly anomaly because confirming the pattern and explaining the latest sprint can require different views

### RISKS

- RISK: users may overread the primary route as a guaranteed root-cause answer rather than a best-first investigation path
- RISK: if Sprint Execution cannot hand off context cleanly, the two execution-first mappings lose diagnostic efficiency
- RISK: if later phases broaden anomaly semantics, this routing table will become invalid and must be redesigned rather than patched

### GO / NO-GO for Phase 26 (UX integration)

- GO: Phase 26 may proceed because the routing layer now has a strict 1:1 anomaly-to-route contract with documented justification, failure cases, and no added UI or routing implementation semantics.
