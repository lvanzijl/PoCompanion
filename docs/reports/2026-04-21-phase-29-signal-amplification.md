# Phase 29 signal amplification

## Scope

- IMPLEMENTATION phase
- UX refinement only
- No CDC slice changes
- No interpretation logic changes
- No routing-definition changes
- No new anomaly types
- No additional signals
- Single-hint constraint preserved

---

## 1. Early rendering implementation

### IMPLEMENTED

- The Plan Board now starts project-summary and board loading in parallel instead of waiting for the summary request to finish before starting the board request.
- Added a reusable `PlanBoardExecutionHintSection` so the sprint-heat title, explanatory copy, and hint can render before the full board region finishes loading.
- Added an early preview path that shows the existing execution hint in the sprint-heat section while the heavy board region is still resolving.

### IMPROVED

- In anomaly context, the hint now becomes visible in the same visible phase as the summary region instead of appearing only after the full sprint grid completes.
- The hint is no longer visually tied to the slowest board region.

### REGRESSION

- None observed in the validated anomaly and non-anomaly flows.

### RISK

- Early visibility still depends on the planning-board API returning successfully; this phase improves client perception, not backend latency itself.

---

## 2. Updated wording

### IMPLEMENTED

- Updated surfaced hint copy:
  - completion below typical → `Execution signal: committed work was not delivered as expected (recent sprint)`
  - completion variability → `Execution signal: delivery was less steady than expected (recent sprints)`
  - spillover increase → `Execution signal: committed work kept carrying into the next sprint (recent sprint)`

### IMPROVED

- The wording is less abstract and easier to understand without hover.
- The required `Execution signal:` prefix remains intact.
- No metrics, percentages, thresholds, or alarmist wording were added.

### REGRESSION

- None observed in routing or anomaly selection.

### RISK

- The variability wording is still necessarily more abstract than the Sprint Execution destination because it points to a trend pattern rather than one sprint failure.

---

## 3. Context anchoring decision

### IMPLEMENTED

- Added a compact recent-sprint anchor directly in the single-line hint text:
  - `(recent sprint)` for Sprint Execution destinations
  - `(recent sprints)` for Delivery Trends destinations

### IMPROVED

- Users get immediate scope context without a second label, badge, or line of helper text.

### REGRESSION

- None observed in layout density or overlap.

### RISK

- The anchor is intentionally generic; it improves speed of interpretation but does not replace the destination’s precise team/sprint context.

---

## 4. Hover refinement

### IMPLEMENTED

- Updated hover copy to one short, actionable sentence per hint:
  - `Recent sprint delivered less than committed. Open Sprint Execution to see unfinished work.`
  - `Recent sprints delivered unevenly. Open Delivery Trends to see when this changed.`
  - `Recent sprint carried more committed work forward. Open Sprint Execution to see unfinished work.`

### IMPROVED

- Hover now states both:
  - what happened
  - what the user should do next

### REGRESSION

- None observed; hover remains secondary and optional.

### RISK

- Hover is still inaccessible on purely scan-only behavior, so the no-hover wording remains important.

---

## 5. Stability measures

### IMPLEMENTED

- Reduced hint show debounce from `200ms` to `75ms` to shorten perceived delay once the hint is available.
- Preserved minimum-visible timing in `ExecutionRealityHint` to avoid show/hide flicker.
- Strengthened the hint context key to include sprint identity as well as product/team, reducing remount instability when context changes.
- Added a short early-preview hold (`250ms`) so the preview state does not flash away immediately before the loaded board state replaces it.
- Kept the hint single-line with truncation-safe styling for stable height.

### IMPROVED

- Hint mounting is steadier during board load.
- Context changes are less likely to reuse stale visible state.

### REGRESSION

- None observed in repeated anomaly-board loads during manual validation.

### RISK

- The preview-to-loaded transition still swaps sections once the full board is ready; it is materially steadier than before, but not architected as one permanently mounted DOM node.

---

## 6. Regression validation

### IMPLEMENTED

- Added focused audit coverage for:
  - reusable hint-section markup
  - early preview placement before the slow board data-state region
  - loaded-state placement above sprint heat grid and before track markup
  - early and loaded markup snapshots
- Updated service/navigation/mock-scenario tests for the refined wording and anchor expectations.

### IMPROVED

- The implementation is now guarded against regressions in:
  - early-vs-loaded placement
  - single-line copy structure
  - preserved routing behavior
  - silent non-anomaly handling

### REGRESSION

- None observed in validated tests or manual walkthroughs.

### RISK

- Snapshot-style audit coverage is markup-governance focused; it does not replace a full browser-image diff system.

---

## Validation

### Automated

- `dotnet build PoTool.sln --configuration Release --no-restore`
- `./.github/scripts/check-sync-over-async.sh`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --configuration Release --no-build --filter "ProductPlanningBoardExecutionHintServiceTests|ProductPlanningBoardClientUiTests"`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~PoTool.Tests.Unit.Audits.PlanBoardExecutionHintMarkupAuditTests|FullyQualifiedName~PoTool.Tests.Unit.Models.ProductPlanningExecutionHintNavigationTests|Name~BattleshipMockScenario_ProducesExecutionAnomaliesAndSinglePlanningBoardHint"`

### Manual

- IMPLEMENTED: verified anomaly context now shows the hint above the still-loading sprint-grid area before full board resolution.
- IMPROVED: verified hint wording is clearer without hover.
- IMPROVED: verified hover immediately explains what happened and where to go.
- IMPLEMENTED: verified click destination remains unchanged and still lands on Sprint Execution with the expected team/sprint context.
- IMPLEMENTED: verified non-anomaly context remains silent in both API output and rendered board.

---

## Final section

### IMPLEMENTED

- IMPLEMENTED: earlier Plan Board execution-hint rendering path
- IMPLEMENTED: clearer single-line wording with compact recent-sprint context anchors
- IMPLEMENTED: improved short hover guidance
- IMPLEMENTED: timing-stability refinements for hint visibility
- IMPLEMENTED: regression tests and release-note update

### IMPROVED

- IMPROVED: hint becomes visible before full sprint-grid resolution
- IMPROVED: first-read comprehension without hover
- IMPROVED: perceived readiness of anomaly context

### REGRESSION

- REGRESSION: none found in validated automated or manual coverage

### RISK

- RISK: if board-response latency increases materially, early preview can still only begin after the board DTO returns
- RISK: preview and loaded states are intentionally separate render phases, so future refactors must preserve the no-duplicate and no-flicker behavior

---

## Next step

Phase 30 — behavior re-validation after amplification
