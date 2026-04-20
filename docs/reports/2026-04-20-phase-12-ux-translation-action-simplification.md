# Phase 12 — UX Translation and Action Simplification

Date: 2026-04-20

## 1. Summary

- **IMPLEMENTED:** The `/planning/plan-board` workspace now uses planning-language labels and safer trust-oriented copy without changing planning behavior, persistence behavior, recovery behavior, TFS integration, or API contracts.
- **IMPLEMENTED:** The board now states clearly that the board is the plan and TFS only receives reported dates for reporting.
- **IMPLEMENTED:** Visible action labels now describe outcome and consequence instead of mechanism, and lightweight inline hints explain impact before the user acts.
- **VERIFIED:** Automatic track derivation, changed-versus-affected highlighting, explicit controls, diagnostics, reconciliation, and existing mutation endpoints remain in place.

## 2. Terminology changes applied

- **IMPLEMENTED:** Replaced visible drift language with user-facing sync language.
  - `Drift` / `TFS drift` → `Out of sync with TFS`
- **IMPLEMENTED:** Replaced reconciliation wording with direct planning language.
  - `Reconcile TFS projection` → `Update TFS dates`
- **IMPLEMENTED:** Replaced projection wording in visible UI text.
  - `Projection` / `TFS projection` → `Reported dates` / `TFS reported dates`
- **IMPLEMENTED:** Replaced recovery wording in visible UI text.
  - `Recovered` / `Recovered + normalized` → `Imported from existing data` / `Imported from existing data and cleaned up`
- **IMPLEMENTED:** Removed visible durable-session wording from board copy and success messages.
- **VERIFIED:** Internal enum names, DTO names, endpoint names, and persistence semantics were left intact.

## 3. Action simplification changes

- **IMPLEMENTED:** Renamed action groups to explain what will happen:
  - `Move by sprint` → `Move only this Epic`
  - `Adjust spacing before` → `Move this and shift following work`
  - `Shift plan suffix` → `Move everything after this`
  - `Reorder roadmap sequence` → `Change priority order`
  - `Run in parallel` action surfaced as `Create parallel work`
  - `Return to main` surfaced as `Return to main plan`
- **IMPLEMENTED:** Renamed button labels to be intent-based:
  - `Apply` → `Move Epic`, `Move + shift`, `Shift following work`, `Update order`
- **IMPLEMENTED:** Reduced visual noise by removing the always-on “No validation issues” chip and only surfacing planning-issue chips when relevant.
- **VERIFIED:** All actions still call the same existing client-service endpoints and preserve existing mechanics.

## 4. Authority model clarification

- **IMPLEMENTED:** Added a lightweight authority banner at the top of the board:
  - `This board defines your plan. Dates are written to TFS for reporting.`
- **IMPLEMENTED:** Reworded board header copy to reinforce that planning happens on the board and TFS is a reporting target.
- **IMPLEMENTED:** Updated reload, undo, and TFS-date update messages to feel safe and unambiguous.
- **VERIFIED:** No competing-authority behavior was introduced; the board still uses the same authoritative internal planning intent and the same TFS reporting write-back model.

## 5. UI changes implemented

- **IMPLEMENTED:** Updated board header summary text to remove engine-centric wording.
- **IMPLEMENTED:** Renamed top-level actions:
  - `Reload board` → `Reload from saved plan`
  - `Reset session` → `Undo all changes`
- **IMPLEMENTED:** Renamed track labels and subtitles:
  - `Main lane` → `Main plan`
  - `Parallel track` → `Parallel lane`
- **IMPLEMENTED:** Renamed planning metrics to planning-language terms:
  - `Planned start` → `Chosen start`
  - `Computed start` → `Scheduled start`
  - `Derived end` → `Planned finish`
  - `Track position` → `Work lane`
- **IMPLEMENTED:** Added inline action hints describing consequence:
  - only this Epic moves
  - this Epic plus following work moves
  - only following work moves
  - priority changes without manual track management
  - parallel work is created or removed while tracks remain automatic
  - TFS dates are refreshed from the plan without changing the plan
- **IMPLEMENTED:** Reduced state-noise emphasis:
  - primary: changed, blocking issues
  - secondary: affected, out of sync with TFS
  - tertiary: imported from existing data
- **IMPLEMENTED:** Added UI-layer translation helpers so visible diagnostics and issue text can be presented in planning language without changing backend contracts.

## 6. Tests added/updated

- **IMPLEMENTED:** Updated `ProductPlanningBoardClientUiTests` to verify new render-model labels:
  - `Main plan`
  - `Parallel lane 1`
  - `Plan updated`
  - `Plan needs attention`
- **IMPLEMENTED:** Added UI-text translation tests for:
  - projection → reported dates
  - recovered → imported from existing data
  - internal intent → saved plan
  - durable wording removal from translated UI text
- **IMPLEMENTED:** Added drift-label test to verify `Out of sync with TFS`.
- **VERIFIED:** Existing endpoint wiring tests continue to pass unchanged.

## 7. Verification of unchanged behavior

- **VERIFIED:** No planning engine logic changed.
- **VERIFIED:** No persistence model changed.
- **VERIFIED:** No recovery model changed.
- **VERIFIED:** No TFS integration behavior changed.
- **VERIFIED:** No planning-board API contract changed.
- **VERIFIED:** Explicit control model remains in place; drag and drop was not added.
- **VERIFIED:** Automatic track derivation remains unchanged.
- **VERIFIED:** Changed and affected highlighting remains intact.

Validation run:

- **VERIFIED:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --no-restore`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`

## 8. Remaining UX gaps

- **NOT IMPLEMENTED:** No browser-level component rendering tests were added; the repository still lacks a dedicated UI component test harness for this page.
- **NOT IMPLEMENTED:** The board still exposes several separate explicit actions because functionality was required to remain unchanged.
- **NOT IMPLEMENTED:** Backend diagnostic message generation still originates from existing server-side semantics; only the UI-layer visible phrasing was translated where feasible.
- **NOT IMPLEMENTED:** The board still uses numeric delta entry for precise scheduling actions; this phase did not introduce new interaction patterns.

## Final section

### IMPLEMENTED

- Planning-language terminology across the Plan Board UI
- Clear authority messaging that the board is the plan and TFS is reporting output
- Safer trust-oriented labels for reload, undo, and TFS date update actions
- Intent-based action labels and consequence hints
- Reduced visual competition among changed, affected, sync, and imported-state indicators
- UI-layer text translation helper coverage and updated client UI tests
- Release note entry for the user-visible change

### NOT IMPLEMENTED

- Any planning-engine, persistence, recovery, TFS, or API-contract change
- Drag and drop
- Modal confirmations or heavy interaction redesign
- Removal of diagnostics or critical system-state visibility
- Browser-level component/UI automation harness

### BLOCKER

- None

### GO/NO-GO

- **GO:** This phase is complete within the locked scope. The board is more understandable and safer-feeling without changing how it works.
