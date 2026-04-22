# Phase 28 real-usage validation — final rerun

## Scope

- FINAL REAL-USAGE VALIDATION only
- No code changes
- No UX changes
- No routing changes
- No CDC / interpretation changes
- Validation executed against:
  - Battleship anomaly data from Phase 28b
  - fixed sync-gate behavior from Phase 28c
  - repaired Sprint Execution destination from Phase 28d

---

## 1. Full flow validation

### OBSERVED

1. Application startup at `http://localhost:5291/` entered sync-gate and completed successfully to `/home`.
2. Sync-gate advanced through the expected working stages, including:
   - `SyncWorkItemRelationships`
   - `ComputeSprintTrends`
   - `ComputeForecastProjections`
   - `SyncPipelines`
3. Natural navigation worked:
   - `/home`
   - Planning workspace
   - Plan Board
4. Anomaly product context was opened naturally by selecting:
   - `Incident Response Control` (`productId=1`)
5. The anomaly board initially showed planning summary tiles plus loading/progress indicators.
6. After the board fully resolved, the live UI surfaced a visible hint button above sprint heat:
   - `Execution signal: committed delivery below typical range`
7. Clicking that hint navigated to:
   - `/home/delivery/execution?productId=1&teamId=4&sprintId=9&timeMode=Sprint`

### CONFIRMED

- Sync-gate completes.
- The planning board does load in anomaly context.
- The hint is visible in the live UI once the board finishes rendering.
- The repaired Sprint Execution destination is reachable from the hint itself.

### UNCLEAR

- The anomaly board did not feel fast or immediately ready; the hint appeared only after a materially longer wait than the first visible shell.

### RISK

- The feature is end-to-end functional, but its real value depends on users waiting long enough for the anomaly board to finish rendering.

---

## 2. Visibility findings

### OBSERVED

- During the first normal scan of the anomaly board, the visible UI showed:
  - planning header
  - project planning summary
  - product imbalance
  - progress indicators in the board area
- During that initial 5–10 second scan, the hint was not yet visible.
- After the board completed rendering, the hint appeared as a distinct button directly above sprint heat.
- The live planning-board API for the same context returned:
  - `anomalyKey = completion-below-typical`
  - `message = Execution signal: committed delivery below typical range`
  - `teamId = 4`
  - `sprintId = 9`

### CONFIRMED

- The hint is naturally noticeable once the board has fully loaded because it sits immediately above the primary sprint-heat region.

### UNCLEAR

- Users who treat the earlier summary-plus-progress state as “good enough” may never wait to see the hint.

### MISLEADING

- The first visible board state suggests “still loading” rather than “execution issue detected.”

### RISK

- Visibility is real, but delayed visibility weakens the signal in rushed usage.

---

## 3. First impression

### OBSERVED

- In the first 5–10 seconds without interaction, the page impression was:
  - planning summary tiles
  - progress/loading activity
  - no visible execution cue yet
- Once the board finished, the hint appeared before sprint heat and became part of the page hierarchy.

### CONFIRMED

- The immediate first impression is not the hint.
- Once the board resolves, the hint feels relevant because it sits at the transition into sprint heat.

### UNCLEAR

- Whether typical users wait long enough to form that second, more accurate impression remains uncertain.

### MISLEADING

- Early rendering emphasizes board readiness problems more than execution insight.

### RISK

- A delayed first impression reduces the chance that the signal changes behavior in fast planning reviews.

---

## 4. Comprehension analysis

### OBSERVED

- Visible hint copy:
  - `Execution signal: committed delivery below typical range`
- Hover text:
  - `Open Sprint Execution to inspect what happened to committed work inside the sprint.`

### CONFIRMED

- The hint is clearly execution-related rather than planning-related.
- The hover clarifies meaning effectively and makes the destination purpose obvious.

### UNCLEAR

- Without hover, the phrase `below typical range` is somewhat abstract and does not immediately say whether the issue is completion, spillover, or delivery collapse.

### MISLEADING

- The wording is not misleading, but it is less concrete than the destination page itself.

---

## 5. Click behavior

### OBSERVED

- The visible hint rendered as a button and was obviously clickable.
- Clicking it navigated directly to Sprint Execution with the expected product/team/sprint context.
- I did not observe meaningful hesitation before clicking once the hint was visible.

### CONFIRMED

- Destination matches expectation.
- Navigation feels obvious after hover and still reasonable without hover.

### UNCLEAR

- The largest hesitation point is not the click itself; it is whether users wait for the hint to appear.

### RISK

- Click-path value is gated by delayed board completion, not by bad affordance.

---

## 6. Destination effectiveness

### OBSERVED

- Sprint Execution loaded successfully from the actual hint click.
- The destination immediately showed:
  - `Team: Crew Safety`
  - `Time: Sprint 11`
  - `Applied filter scope differs from the request`
  - `Applied: \Battleship Systems\Sprint 11`
  - `Sprint Execution Summary`
  - `Committed Story Points = 669`
  - `Delivered Story Points = 0`
  - `Unfinished PBIs (208)`
- The page quickly explained what went wrong: large committed scope, effectively no delivery, and a very large unfinished set.

### CONFIRMED

- The destination provides immediate explanatory value.
- A user can understand the anomaly quickly without hunting across multiple widgets.

### UNCLEAR

- The filter-scope banner is still slightly abstract and may require a moment of interpretation.

### MISLEADING

- Nothing on the destination felt misleading in this rerun.

### RISK

- The destination is strong enough that the remaining risk is source-timing, not destination quality.

---

## 7. Decision impact

### OBSERVED

- Returning mentally from Sprint Execution to the plan, the anomaly meaning is actionable:
  - heavy committed scope
  - zero delivered points
  - very large unfinished backlog
- This materially lowers confidence that the current plan is healthy for that product context.

### CONFIRMED

- The signal has real decision-making value.
- It can trigger a sensible planning action such as revisiting sequencing, commitment level, or execution risk discussion.

### UNCLEAR

- The exact action still depends on the planner’s workflow; the hint signals risk well, but it does not prescribe a single remediation.

---

## 8. Multi-anomaly behavior

### OBSERVED

- Network activity during board loading evaluated multiple anomaly candidates across products and sprints:
  - sprint execution calls for products 1 and 2
  - sprint ids `9`, `21`, and `33`
- The anomaly product planning-board API returned exactly one surfaced hint:
  - `completion-below-typical`
- In the live anomaly board UI, only one hint button was shown.

### CONFIRMED

- One-hint arbitration works.
- Only one hint is surfaced even when multiple anomaly evaluations are happening behind the scenes.

### UNCLEAR

- It remains possible that some users would want awareness of additional hidden anomalies elsewhere, but that is not necessary for this entry-point signal to be useful.

### MISLEADING

- The single-hint behavior did not feel misleading in the rendered UI.

### RISK

- Hiding secondary anomalies may feel incomplete for power users, but it keeps the planning board from becoming noisy.

---

## 9. Negative findings

### OBSERVED

- Ignoring the hint is easy because the board can still be used as a planning surface without clicking it.
- The strongest weakness is timing:
  - early state looks partially loaded
  - the useful signal appears later
- Without hover, the wording is good but still less concrete than the destination explanation.

### MISLEADING

- The main misleading behavior is temporal, not semantic:
  - the board first reads as “loading” instead of “important execution signal available.”

### RISK

- In short or impatient sessions, users may ignore the hint simply because the board has not fully resolved yet.

---

## 10. Silence validation

### OBSERVED

- In non-anomaly comparison context (`productId=2`), the planning-board API returned:
  - `executionHint = null`
- The non-anomaly board loaded fully with sprint heat and no execution hint.

### CONFIRMED

- No hint appears in non-anomaly context.
- Silence feels correct when compared against the anomaly context that eventually surfaces a real hint.

### UNCLEAR

- None that block the conclusion.

---

## 11. Trust evaluation

### OBSERVED

- Trust was strengthened by three facts in sequence:
  - sync-gate completed
  - the hint appeared in the live anomaly board
  - the hint click landed on a destination that immediately explained the issue
- The only trust-reducing factor was the delay before the board exposed the hint.

### CONFIRMED

- A user can trust this signal.
- A user could reasonably rely on it as a prompt to inspect execution problems from planning context.

### UNCLEAR

- Trust will be weaker for users who expect the board to be decision-ready immediately after the first shell render.

### RISK

- If load timing regresses, trust will erode before the signal itself is judged on merit.

---

## Final section

### CONFIRMED VALUE

- CONFIRMED: sync-gate completes and allows the full flow to reach planning naturally.
- CONFIRMED: the Battleship anomaly data produces a real execution hint in the working end-to-end flow.
- CONFIRMED: the hint becomes visible in the anomaly plan board and is naturally positioned above sprint heat.
- CONFIRMED: hover clarifies intent effectively.
- CONFIRMED: clicking the hint opens a repaired Sprint Execution page that explains what went wrong quickly.
- CONFIRMED: non-anomaly context stays silent.
- CONFIRMED: the signal provides real decision-making value once surfaced.

### UNCLEAR VALUE

- UNCLEAR: how many users will wait through the anomaly board’s slower initial loading state before the hint appears.
- UNCLEAR: whether some users would prefer awareness of secondary anomalies beyond the single surfaced hint.

### MISLEADING BEHAVIOR

- MISLEADING: the first visible anomaly-board state looks like generic loading rather than immediate execution guidance.

### RISKS

- RISK: the signal’s usefulness is sensitive to board-render timing.
- RISK: rushed users may leave or decide too early, before the hint appears.

---

## Decision

**GO → Phase 29**

The execution hint provides real decision-making value in a fully working end-to-end system: it surfaces in the anomaly planning context, reads as execution-related, clarifies on hover, navigates correctly on click, and lands on a destination that quickly explains what went wrong. The remaining issue is not usefulness but timing risk during the anomaly board’s initial render.
