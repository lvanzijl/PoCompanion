# Phase 14 — Impact Visibility

Date: 2026-04-20

## 1. Summary

- **IMPLEMENTED:** Added explicit post-action impact summaries on the Plan Board so the latest change explains what moved, how much moved, and whether the plan structure changed.
- **IMPLEMENTED:** Added per-Epic inline impact messages so changed and affected cards explain cause instead of forcing users to infer it by scanning the board.
- **IMPLEMENTED:** Separated TFS reporting maintenance from planning actions on each Epic card.
- **VERIFIED:** Planning engine logic, persistence behavior, recovery behavior, TFS integration behavior, and planning-board API contracts were not changed.

## 2. Impact visibility features implemented

- **IMPLEMENTED:** Added a client-side impact-summary builder that compares the previous visible board with the latest returned board after an action.
- **IMPLEMENTED:** Surfaced a structured `Latest planning impact` / `Latest reporting update` summary area immediately after successful actions.
- **IMPLEMENTED:** Quantified direct and downstream change counts using explicit summaries such as direct Epic changes and additional shifted Epics.
- **IMPLEMENTED:** Reused the same planning terminology across board-level and Epic-level feedback: shifted, parallel work, main plan, overlap, priority order, and reporting maintenance.

## 3. Summary examples

- **IMPLEMENTED:** Board-level summaries now support messages such as:
  - `1 Epic changed directly; 2 more Epics shifted.`
  - `Epic A moved +1 sprint.`
  - `2 Epics shifted by +1 sprint.`
  - `Epic B now runs in parallel.`
  - `Overlap introduced between Epic A and Epic B.`
- **VERIFIED:** The summaries are derived from actual before/after board state, not from hardcoded action assumptions.

## 4. Structural change detection

- **IMPLEMENTED:** Detects when an Epic moves into parallel work.
- **IMPLEMENTED:** Detects when an Epic returns to the main plan.
- **IMPLEMENTED:** Detects overlap introduction and overlap removal between Epics based on before/after computed schedule ranges.
- **IMPLEMENTED:** Detects priority-order movement for the acted Epic and reports the resulting relative position when possible.

## 5. Per-Epic feedback improvements

- **IMPLEMENTED:** Changed Epic cards now show explicit inline impact messages instead of only generic changed/affected labeling when detailed impact is available.
- **IMPLEMENTED:** Directly affected Epics can now explain why they moved, for example:
  - `Shifted +1 sprint due to upstream change.`
  - `Moved to parallel work.`
  - `Returned to the main plan.`
  - `Now overlaps with Epic A.`
- **VERIFIED:** Generic fallback changed/affected messaging remains available when no detailed comparison can be produced.

## 6. TFS separation approach

- **IMPLEMENTED:** Moved the TFS write-back action into a distinct `Reporting maintenance` section on Epic cards.
- **IMPLEMENTED:** Relabeled the user-facing maintenance action to `Update reporting data`.
- **IMPLEMENTED:** Kept the explanatory text explicit that this refreshes reported dates from the plan without changing the plan itself.
- **VERIFIED:** The maintenance action still uses the existing reconcile-projection client path.

## 7. Tests added/updated

- **IMPLEMENTED:** Added `PlanningBoardImpactSummaryBuilderTests` to verify:
  - quantified direct and affected change summaries
  - shift magnitude reporting
  - structural change reporting for parallel work
  - overlap change reporting
  - reporting-maintenance summaries with no planning change
- **IMPLEMENTED:** Updated `PlanBoardReconciliationAuditTests` to verify:
  - reporting maintenance is rendered separately
  - the new reporting-maintenance labels are present
  - the Plan Board uses latest-impact summary state and Epic-level impact messages
- **VERIFIED:** Existing planning-board client endpoint coverage remains unchanged.

Validation run:

- **VERIFIED:** `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
- **VERIFIED:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --no-restore`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`

## 8. Verification of unchanged backend behavior

- **VERIFIED:** No planning engine logic changed.
- **VERIFIED:** No persistence model changed.
- **VERIFIED:** No recovery model changed.
- **VERIFIED:** No TFS integration behavior changed.
- **VERIFIED:** No planning-board API contract changed.
- **VERIFIED:** All new impact visibility behavior is derived in the client from existing board data and returned action results.

## 9. Remaining UX gaps

- **NOT IMPLEMENTED:** Multi-action history beyond the latest visible action.
- **NOT IMPLEMENTED:** Bulk impact summaries across multiple independent Epic actions in one operation.
- **NOT IMPLEMENTED:** Predictive impact previews before an action is executed.

## Final section

### IMPLEMENTED

- Latest impact summary area for planning and reporting actions
- Quantified direct and downstream change summaries
- Structural change detection for parallel work, overlap introduction/removal, and priority-order movement
- Per-Epic inline impact explanations for changed and affected Epics
- Separate reporting-maintenance section and clearer reporting-maintenance labeling
- Unit and audit coverage for the new impact visibility behavior
- Release notes update for the user-visible Plan Board change

### NOT IMPLEMENTED

- Planning engine changes
- Persistence, recovery, TFS integration, or API contract changes
- New planning semantics
- Predictive or multi-step impact history

### BLOCKER

- None

### GO/NO-GO

- **GO:** This phase is complete within the locked scope. The Plan Board now explains latest impact more explicitly without changing planning behavior or backend contracts.
