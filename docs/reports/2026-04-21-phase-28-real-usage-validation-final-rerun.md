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

1. Application startup at `http://localhost:5291/` entered sync-gate and then completed successfully to `/home`.
2. Sync-gate advanced through the previously critical stage:
   - `SyncWorkItems`
   - `ComputeSprintTrends`
   - `ComputeForecastProjections`
   - `SyncPipelines`
   - final `syncStatus = 2`
3. Natural navigation worked:
   - `/home`
   - Planning workspace
   - Plan Board
4. Anomaly product context was opened naturally by selecting:
   - `Incident Response Control` (`productId=1`)
5. The Plan Board API for that anomaly context returned a real execution hint:
   - `anomalyKey = completion-below-typical`
   - `message = Execution signal: committed delivery below typical range`
   - `teamId = 4`
   - `sprintId = 9`
6. The repaired destination route loaded successfully when opened directly:
   - `/home/delivery/execution?productId=1&teamId=4&sprintId=9&timeMode=Sprint`

### CONFIRMED

- Sync-gate completion is no longer the primary blocker in this rerun.
- The Sprint Execution destination is functionally repaired and reachable.
- The Battleship anomaly data is active and produces a valid execution hint at API level.

### UNCLEAR

- The Plan Board shell loaded, but the live board content remained visually incomplete long enough that the hint never surfaced in the browser session even though the API returned it.

### RISK

- The end-to-end system is only partially “fully working” in real usage because the source hint exists in data but does not become reliably visible in the board UI.

---

## 2. Visibility findings

### OBSERVED

- During normal scanning of the anomaly product Plan Board, the visible UI showed:
  - planning header
  - project planning summary
  - product imbalance
  - persistent progress bars in the board area
- The hint text was **not** visible in the live browser render.
- DOM inspection during the live session found:
  - no visible `Execution signal:` text in the rendered page
- API inspection at the same time confirmed that the hint payload existed.

### CONFIRMED

- In real browser usage for this rerun, the hint is **skipped entirely** because it is not surfaced visually.

### MISLEADING

- The current UI gives the impression that there is no execution hint at all, even while the API is already returning one.

### RISK

- A signal that only exists behind the API boundary has no practical decision value for a planning user.

---

## 3. First impression

### OBSERVED

- The first impression in anomaly context was not “execution warning”.
- It was:
  - planning summary tiles
  - board-level loading/progress indicators
  - no execution strip above sprint heat
- Because the hint never rendered, it did not compete with planning heat; it simply was not part of the first impression.

### CONFIRMED

- The hint does **not** feel relevant immediately in this rerun because the user never sees it.

### UNCLEAR

- If the hint had rendered in its intended location, it likely would have read as secondary to planning heat, but that was not observable in the live UI.

### MISLEADING

- The page feels like “board still loading” rather than “board ready, with an execution anomaly worth investigating.”

---

## 4. Comprehension analysis

### OBSERVED

- Hint-only comprehension could not be validated from the live board because the hint did not appear.
- API payload wording was:
  - `Execution signal: committed delivery below typical range`
  - `Open Sprint Execution to inspect what happened to committed work inside the sprint.`

### CONFIRMED

- At copy level, the hint is clearly execution-related rather than planning-related.

### UNCLEAR

- In actual board context, I could not validate:
  - whether a user immediately understands what happened
  - whether the sprint/team ambiguity is acceptable
  - whether hover clarification is enough

### MISLEADING

- In the live UI, comprehension fails before wording matters because the signal is absent.

---

## 5. Click behavior

### OBSERVED

- No visible hint was available to click in the real board session.
- Therefore:
  - no natural hesitation-before-click behavior could be observed
  - no hover behavior could be validated in-browser
- To continue the validation, I opened the exact destination route derived from the live hint payload directly.

### CONFIRMED

- Real click behavior from the source board is currently blocked by missing hint visibility.

### UNCLEAR

- Whether the hint would feel obviously clickable if it rendered remains unproven in this rerun.

### MISLEADING

- The current live experience makes the feature feel absent rather than actionable.

---

## 6. Destination effectiveness

### OBSERVED

- The repaired Sprint Execution destination loaded successfully.
- The destination immediately showed:
  - `Team: Crew Safety`
  - `Time: Sprint 11`
  - `Applied filter scope differs from the request`
  - `Applied: \Battleship Systems\Sprint 11`
  - `Sprint Execution Summary`
  - `Committed Story Points = 669`
  - `Delivered Story Points = 0`
  - `Unfinished PBIs (208)`
- This destination quickly communicates that the sprint had large committed scope and effectively no delivered scope.

### CONFIRMED

- If a user reaches the destination, it does provide real explanatory value.
- The repaired destination now explains the anomaly substantially better than in the earlier failed-load validation.

### UNCLEAR

- The filter-scope banner is useful but still slightly abstract:
  - `Requested: All`
  - `Applied: \Battleship Systems\Sprint 11`
- A user may still need a moment to interpret why the applied scope is narrower than the request.

### MISLEADING

- The destination is useful, but that usefulness is hidden behind a source hint that currently does not appear in the live board.

### RISK

- Strong destination value does not help if the user is never prompted into it by the source experience.

---

## 7. Decision impact

### OBSERVED

- In the real board experience, the hint did not change planning confidence because it was not visible.
- In the direct destination view, the evidence was strong enough to reduce confidence in the current plan:
  - heavy committed scope
  - zero delivered points
  - large unfinished backlog

### CONFIRMED

- The destination could plausibly trigger action if reached.
- The source board currently does **not** trigger that action in live usage because the hint is absent.

### UNCLEAR

- Whether users would return mentally to the plan and adjust sequencing based on this evidence remains partly inferential, because the click path was not available in the live board.

---

## 8. Multi-anomaly behavior

### OBSERVED

- Network activity during Plan Board loading showed multiple anomaly-related evaluations across products and sprints:
  - sprint execution calls for product 1 and 2
  - sprint ids `9`, `21`, and `33`
- Despite those multiple evaluations, the anomaly product planning-board API returned exactly one surfaced hint:
  - `completion-below-typical`
- Non-anomaly comparison product (`productId=2`) returned:
  - `executionHint = null`

### CONFIRMED

- One-hint arbitration is active at API level.
- Silence works at API level for the quieter comparison context.

### UNCLEAR

- Because the hint was not rendered, I could not validate whether “only one hint shown” feels correct in the actual board UI.

### MISLEADING

- Current UI behavior hides not just secondary anomalies, but also the primary surfaced anomaly.

### RISK

- Arbitration quality is irrelevant to users unless the winning hint is actually visible.

---

## 9. Negative findings

### OBSERVED

- Ignoring the hint is trivially easy in this rerun because there is no visible hint to notice.
- Misinterpretation risk shifts from wording risk to feature-presence risk:
  - users may conclude there is no execution issue surfaced at all
  - users may assume the planning board is still loading or partially incomplete
- The board area remained visually dominated by loading indicators long after the planning-board API had returned valid content.

### MISLEADING

- The current live behavior undercuts the feature more strongly than vague wording would:
  - not “weak signal”
  - but effectively “missing signal”

### RISK

- A hidden signal becomes noise only in retrospect; in-session it becomes non-existent.

---

## 10. Silence validation

### OBSERVED

- In non-anomaly comparison context (`productId=2`), the planning-board API returned no execution hint.
- In the live UI, no hint was visible in anomaly context either.

### CONFIRMED

- Silence is correct at API level for the non-anomaly product.

### UNCLEAR

- UI-level silence does not feel trustworthy because the anomaly product is also silent in this rerun.

### MISLEADING

- When anomaly and non-anomaly contexts both look silent in-browser, the user cannot distinguish “healthy silence” from “missing signal”.

---

## 11. Trust evaluation

### OBSERVED

- Trust improved in one important area:
  - the repaired Sprint Execution destination now loads and explains the anomaly
- Trust failed in the earlier part of the flow:
  - the board never surfaced the hint that should trigger the investigation

### CONFIRMED

- Users could trust the destination **if they somehow reached it**.
- Users are unlikely to trust or rely on the overall hint workflow because the board does not currently surface the source cue in real usage.

### UNCLEAR

- Whether this is a timing issue, a rendering issue, or a visibility regression in the current browser path was not resolved during validation because no implementation changes were allowed.

### RISK

- A decision-support chain is only as trustworthy as its weakest step; here the weak step is source visibility, not destination explanation.

---

## Final section

### CONFIRMED VALUE

- CONFIRMED: sync-gate completes and no longer blocks the full flow.
- CONFIRMED: the Battleship anomaly data produces a real execution hint at API level.
- CONFIRMED: the repaired Sprint Execution destination now loads successfully.
- CONFIRMED: once reached, Sprint Execution gives meaningful anomaly explanation quickly.

### UNCLEAR VALUE

- UNCLEAR: hover clarity in live UI.
- UNCLEAR: hesitation-before-click in live UI.
- UNCLEAR: whether one-hint arbitration feels right when rendered.

### MISLEADING BEHAVIOR

- MISLEADING: the anomaly board appears effectively silent even though the live API returns a valid hint.
- MISLEADING: users are more likely to interpret the board as partially loaded than as intentionally surfacing execution guidance.
- MISLEADING: non-anomaly silence cannot feel “correct” when anomaly context is also visually silent.

### RISKS

- RISK: the feature’s practical value is nullified at the source step because the hint is not visible in live browser usage.
- RISK: users may never discover the repaired destination on their own.
- RISK: trust in the workflow is broken by source invisibility even though the destination itself is now useful.

---

## Decision

**NO-GO → exact blocking reason:** in this final real-usage rerun, the execution hint provides no dependable decision-making value because the anomaly Plan Board API returns a valid hint but the live browser UI does not surface it, so users cannot naturally notice, interpret, hover, click, or rely on the signal even though the repaired destination now works.
