# Phase 12b — Action Consolidation

Date: 2026-04-20

## 1. Summary

- **IMPLEMENTED:** The Plan Board now promotes one primary scheduling action: **Move Epic**.
- **IMPLEMENTED:** The default path now uses the existing move-and-shift behavior so users can reshape the plan without choosing a strategy first.
- **IMPLEMENTED:** Precision controls remain available, but they now sit behind **Advanced options** instead of competing for attention by default.
- **VERIFIED:** Planning engine logic, persistence behavior, recovery behavior, TFS integration, and API contracts were not changed.

## 2. Action consolidation changes

- **IMPLEMENTED:** Added one primary action card per Epic with a single visible default action:
  - **Move Epic**
- **IMPLEMENTED:** The default action keeps numeric sprint delta input, but reduces the visible decision surface to one main choice.
- **IMPLEMENTED:** Moved secondary schedule-shaping actions behind collapsed **Advanced options**:
  - Move only this Epic
  - Move everything after this
  - Change priority order
- **VERIFIED:** Existing parallel-work and TFS-date actions remain available and explicit.

## 3. Default behavior defined and implemented

- **IMPLEMENTED:** Default behavior is now explicit in the UI:
  - moving an Epic shifts following work automatically
- **IMPLEMENTED:** The primary button uses the existing adjust-spacing mutation path, preserving backend behavior while changing the interaction hierarchy.
- **VERIFIED:** Alternative behaviors are still discoverable on demand as overrides instead of mandatory upfront choices.

## 4. UI hierarchy changes

- **IMPLEMENTED:** Added a prominent primary action panel with a visible **Default** chip.
- **IMPLEMENTED:** Reduced default visual branching by collapsing secondary controls under **Advanced options**.
- **IMPLEMENTED:** Demoted override buttons from primary action emphasis to utility emphasis.
- **VERIFIED:** Explicit operations remain in place; drag & drop was not introduced.

## 5. Consequence visibility improvements

- **IMPLEMENTED:** Added a board-level latest-change summary that states:
  - what changed directly
  - what shifted after it
  - what stayed in place
- **IMPLEMENTED:** Added per-Epic change alerts so changed and affected cards explain their status without requiring users to infer it from chips alone.
- **IMPLEMENTED:** Renamed change chips from generic system wording to clearer outcome wording:
  - `Changed` → `Updated`
  - `Affected` → `Shifted`

## 6. Dual time model clarification

- **IMPLEMENTED:** Reframed the time model into two visible groups:
  - **Your plan**
  - **Calculated schedule**
- **IMPLEMENTED:** Renamed the planned-side label to **Start you picked** to reduce interpretation overhead.
- **VERIFIED:** No schedule computation changed; only the visual distinction between authored timing and calculated timing changed.

## 7. Tests added/updated

- **IMPLEMENTED:** Updated `ProductPlanningBoardClientUiTests` to verify:
  - the new latest-change summary on the render model
  - unchanged endpoint usage for existing board mutations
  - operational-diagnostics summaries still take precedence correctly
- **IMPLEMENTED:** Updated `PlanBoardReconciliationAuditTests` to verify:
  - reconcile remains explicit and conditional
  - the primary move action uses the existing default spacing/move path
  - advanced overrides remain present
  - the dual time model labels render in the page markup
- **VERIFIED:** Relevant automated test suites pass after the change.

Validation run:

- **VERIFIED:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --no-restore`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`

## 8. Verification of unchanged backend behavior

- **VERIFIED:** No planning engine logic changed.
- **VERIFIED:** No persistence model changed.
- **VERIFIED:** No recovery model changed.
- **VERIFIED:** No TFS integration behavior changed.
- **VERIFIED:** No planning-board API contract changed.
- **VERIFIED:** The default move action reuses the existing adjust-spacing endpoint rather than introducing a new backend behavior.

## 9. Remaining UX gaps

- **NOT IMPLEMENTED:** A richer visual diff of exactly which downstream Epics shifted by how many sprints.
- **NOT IMPLEMENTED:** A faster direct-manipulation interaction model; explicit numeric input remains required for precision.
- **NOT IMPLEMENTED:** Post-action summaries specialized by action type; the new summary is generic so it stays correct across multiple explicit operations.

## Final section

### IMPLEMENTED

- One default **Move Epic** action that shifts following work automatically
- Collapsed **Advanced options** for secondary schedule-editing overrides
- Board-level and Epic-level consequence summaries
- Clearer **Your plan** vs **Calculated schedule** time grouping
- Updated release notes and automated tests for the new hierarchy

### NOT IMPLEMENTED

- Any planning engine, persistence, recovery, TFS, or API behavior change
- Drag & drop
- Removal of precision controls
- Action-type-specific downstream diff visualization

### BLOCKER

- None

### GO/NO-GO

- **GO:** This phase is complete within the locked scope. Interaction speed is improved by reducing upfront micro-decisions while preserving existing precision and backend behavior.
