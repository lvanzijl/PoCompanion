# Phase 22c execution design correction

## 1. Summary

- FIXED: the Phase 22 execution reality-check design is narrowed to strict, single-source, single-denominator anomaly definitions.
- FIXED: the ambiguous `and/or` anomaly inputs are removed.
- FIXED: the state model is changed from anomaly-count interpretation to severity-based interpretation.
- FIXED: routing is reduced to one primary diagnostic destination per anomaly.
- FIXED: evidence gating is hardened around history depth, continuity, and estimation coverage.
- REMOVED: the original `Delivery below typical range` definition.
- REMOVED: the broad, ambiguous interpretation of `Spillover increasing`.
- UNRESOLVED: this phase stays at design level only. No CDC extraction, persistence, API, or UI work was implemented.

## 2. Corrected anomaly definitions (strict)

### 2.1 Commitment completion below typical range

- FIXED: this **replaces** the original `Delivery below typical range` anomaly.
- FIXED: **Canonical data source:** `CommitmentCompletion` from sprint execution metrics (`PoTool.Shared/Metrics/SprintExecutionDtos.cs`).
- FIXED: **Canonical denominator:** `CommittedSP - RemovedSP` only, because `CommitmentCompletion` is defined as `DeliveredSP / (CommittedSP - RemovedSP)`.
- FIXED: **Strict meaning:** the product is finishing a persistently smaller share of its originally committed, still-in-scope story-point work than is typical for that product’s own recent completed-sprint history.
- FIXED: **Why this replaces the original anomaly:** the old delivery anomaly could be masked by added work because canonical throughput includes committed and added delivery. `CommitmentCompletion` isolates execution against committed scope and removes that ambiguity.

### 2.2 Commitment completion variability high

- FIXED: `Execution variability high` is retained only by redefining it onto one metric.
- FIXED: **Canonical data source:** `CommitmentCompletion` from sprint execution metrics (`PoTool.Shared/Metrics/SprintExecutionDtos.cs`).
- FIXED: **Canonical denominator:** `CommittedSP - RemovedSP` only.
- FIXED: **Variability definition:** variability means the recent 8-sprint `CommitmentCompletion` series swings outside the product’s own recent normal spread around its median-centered baseline.
- FIXED: **How variability is measured conceptually:** use one median-centered spread interpretation on the `CommitmentCompletion` series only; do not mix story-point throughput, PBI counts, or alternate completion ratios.
- FIXED: **Why this metric reflects execution instability:** it measures how consistently the team finishes its committed scope after removals, independent of raw sprint size. Stable execution should produce a stable completion pattern even when commitment size changes.

### 2.3 Direct next-sprint spillover rate increasing

- FIXED: this **replaces** the original `Spillover increasing` anomaly name.
- FIXED: **Chosen option:** **A) keep canonical spillover (strict)**.
- FIXED: **Canonical data source:** `SpilloverRate` from sprint execution metrics (`PoTool.Shared/Metrics/SprintExecutionDtos.cs`).
- FIXED: **Canonical denominator:** `CommittedSP - RemovedSP` only, because `SpilloverRate` is defined as `SpilloverSP / (CommittedSP - RemovedSP)`.
- FIXED: **Strict meaning:** a persistently larger share of committed, still-in-scope story-point work is moving directly into the next sprint unfinished.
- FIXED: **Justification for keeping the strict definition:** the broad “commitment failure” interpretation overlaps too heavily with commitment completion and reintroduces ambiguity about backlog round-trips, same-sprint-path leftovers, and added work. The narrow canonical spillover metric is stricter, cleaner, and already defined in the repository domain model.

## 3. Removed or replaced anomalies

- REMOVED: **`Delivery below typical range`**
  - REPLACED WITH: **`Commitment completion below typical range`**
  - REMOVED because throughput-based delivery can be inflated by added work and undercut by estimation gaps.

- REMOVED: **`Spillover increasing`** as a broad carry-over phrase
  - REPLACED WITH: **`Direct next-sprint spillover rate increasing`**
  - REMOVED because the old name implied a broader carry-over concept than the repository actually defines.

- FIXED: **`Execution variability high`** is not removed, but it is narrowed to **commitment completion variability** only.

## 4. New state model (severity-based)

### 4.1 Per-anomaly severity

- FIXED: each anomaly has exactly three internal statuses:
  1. **Inactive**
  2. **Weak**
  3. **Strong**

- FIXED: **Weak anomaly**
  - the anomaly condition is present in **3 consecutive completed sprints**

- FIXED: **Strong anomaly**
  - the anomaly condition is present in **4 or more consecutive completed sprints**

- FIXED: **Clear rule**
  - an active anomaly clears only after **2 consecutive completed sprints** return to normal range
  - after the **first** normal sprint, the anomaly remains active but is treated as **Weak pending clear**

### 4.2 User-facing state mapping

- FIXED: the user-facing states remain:
  1. **Stable**
  2. **Watch**
  3. **Investigate**
  4. **Insufficient evidence**

- FIXED: hidden severity weights:
  - **Weak = 1**
  - **Strong = 2**

| Condition | User-facing state |
| --- | --- |
| Evidence gate failed | Insufficient evidence |
| Total active severity = 0 | Stable |
| Total active severity = 1 | Watch |
| Total active severity >= 2 | Investigate |

- FIXED: **What makes an anomaly strong:** persistence into a fourth consecutive completed sprint or longer.
- FIXED: **How multiple weak anomalies combine:** two weak anomalies combine to total severity 2, which escalates to `Investigate`.
- FIXED: **How a single strong anomaly escalates state:** one strong anomaly alone has severity 2, which escalates to `Investigate`.
- REMOVED: direct state mapping based only on “number of active anomalies.”

## 5. Corrected routing map

| Anomaly | Primary diagnostic destination | Reason |
| --- | --- | --- |
| Commitment completion below typical range | `/home/delivery/execution` | The likely diagnostic question is whether originally committed work was actually finished, displaced, starved, or replaced by added work. Sprint Execution is the repository surface built for that question. |
| Commitment completion variability high | `/home/trends/delivery` | The likely first diagnostic step is to locate when the instability started and whether it is persistent across sprints. Delivery Trends is the repository’s historical multi-sprint surface. |
| Direct next-sprint spillover rate increasing | `/home/delivery/execution` | The likely diagnostic question is which committed items stayed unfinished and what happened to them inside the sprint. Sprint Execution directly exposes unfinished, added, removed, and starved scope. |

### Removed routes

- REMOVED: automatic routing from these anomalies to `/home/pull-requests`
- REMOVED: automatic routing from these anomalies to `/home/pipeline-insights`
- REMOVED: automatic routing from spillover to `/home/health/backlog-health`
- REMOVED: multi-route “nice to have” drill-down bundles

- FIXED: PR Insights and Pipeline Insights are no longer part of the primary routing contract because they are correlation checks, not root-cause routes.

## 6. Evidence gating rules

The execution reality-check signal becomes **Insufficient evidence** when **any** of the following is true.

### 6.1 History depth

- FIXED: fewer than **8 completed sprints** with valid execution metrics for the product/team view

### 6.2 Data continuity

- FIXED: the latest 8 completed sprints are not a continuous ordered sequence
- FIXED: any sprint inside the latest 8-sprint window is missing the required execution metric record
- FIXED: sprint ordering is not reliable enough to identify the next sprint for canonical spillover interpretation

### 6.3 Estimation coverage

- FIXED: for any sprint in the latest 8-sprint window, the canonical denominator `CommittedSP - RemovedSP` is zero, negative, or unavailable
- FIXED: for any sprint in the latest 8-sprint window, `CommitmentCompletion` or `SpilloverRate` cannot be computed from authoritative committed story-point scope
- FIXED: for any of the latest 3 completed sprints, delivered committed work includes unestimated delivery that prevents `CommitmentCompletion` from being treated as authoritative

### 6.4 Gating consequence

- FIXED: if the evidence gate fails, **all three anomalies are suppressed**
- FIXED: the surface shows **Insufficient evidence**
- REMOVED: partial anomaly evaluation when the baseline window is incomplete or estimation coverage is non-authoritative

## 7. Remaining risks

- RISK: the stricter evidence gate will reduce coverage for teams with weak estimation hygiene.
- RISK: `Commitment completion variability high` is implementation-ready conceptually, but user explanation text must stay disciplined so it does not drift back into generic “delivery volatility.”
- RISK: keeping strict canonical spillover means some real carry-over behavior remains intentionally out of scope; that is acceptable only if the name stays narrow.
- RISK: using `Sprint Execution` as the primary diagnostic route assumes the eventual surface can support a product/team context handoff cleanly.

## Final section

### FIXED

- FIXED: all anomalies now use exactly one canonical source and exactly one denominator.
- FIXED: the ambiguous throughput-based delivery anomaly is replaced by a commitment-based anomaly.
- FIXED: variability is narrowed to one metric only: `CommitmentCompletion`.
- FIXED: spillover is explicitly kept narrow and renamed to match its strict meaning.
- FIXED: the state model is severity-based.
- FIXED: routing is reduced to one primary destination per anomaly.
- FIXED: evidence gating now depends on history depth, continuity, and estimation coverage.

### REMOVED

- REMOVED: `Delivery below typical range`
- REMOVED: the broad interpretation hidden inside `Spillover increasing`
- REMOVED: anomaly-count-only state semantics
- REMOVED: indirect PR/Pipeline/Backlog Health routes from the primary routing contract

### UNRESOLVED

- UNRESOLVED: no implementation exists yet for the corrected design.
- UNRESOLVED: UI wording and user-facing explanation text still need to be authored in Phase 23 without reintroducing ambiguity.

### RISKS

- RISK: strict evidence gating may leave some products without a signal.
- RISK: the corrected model intentionally favors reliability over coverage.
- RISK: any future attempt to broaden spillover or reintroduce raw throughput semantics would reopen the ambiguity fixed here.

### GO / NO-GO for Phase 23

- GO: Phase 23 may proceed **only** with the corrected definitions, routing, severity model, and evidence gates in this report.
