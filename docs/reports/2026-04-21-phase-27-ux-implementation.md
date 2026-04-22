# Phase 27 UX implementation

## 1. Summary

- IMPLEMENTED: the planning board now surfaces the Phase 26 execution hint as one reusable board-level component.
- IMPLEMENTED: the hint renders only above the sprint heat grid, below the sprint-heat explanatory text, and never inside sprint cards.
- IMPLEMENTED: the API now composes at most one execution hint onto the planning-board DTO without changing planning heat logic, Phase 23c CDC semantics, Phase 24 interpretation semantics, or Phase 25 route mapping.
- VERIFIED: the implementation preserves the single-line `Execution signal:` phrasing, one-hint maximum, direct navigation, and silent handling for `Stable` / `InsufficientEvidence`.
- REGRESSION: none found in the validated build and test suites listed below.
- RISK: if the board is opened without an explicit team in the shared filter state, the hint falls back to the product’s first linked team and its current sprint context.

## 2. Implemented component

### `PoTool.Client/Components/Planning/ExecutionRealityHint.razor`

- IMPLEMENTED:
  - one reusable component
  - single-line strip rendering only
  - direct click navigation
  - optional tooltip sentence
  - debounce + minimum-visible anti-flicker behavior

- IMPLEMENTED:
  - parameters:
    - `Hint`
    - `ProductId`
    - `ContextTeamId`

- VERIFIED:
  - no child chip/badge rendering exists inside the component
  - no list rendering exists
  - the component displays at most one hint

### `PoTool.Client/Components/Planning/ExecutionRealityHint.razor.css`

- IMPLEMENTED:
  - subtle left-edge accent
  - semibold text
  - light background tint
  - single horizontal strip styling

- VERIFIED:
  - no filled red / warning block styling
  - no anomaly-specific color palette
  - no pill/chip visual treatment

## 3. Integration location

### `PoTool.Client/Pages/Home/PlanBoard.razor`

- IMPLEMENTED: inserted `<ExecutionRealityHint />`:
  - below the sprint-heat explanatory text
  - above the `planning-board-sprint-signal-grid`

- VERIFIED:
  - the sprint grid markup itself was not restructured
  - the hint is not rendered inside the sprint-card loop
  - the hint is not rendered as a page-top banner

## 4. API and DTO composition

### `PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`

- IMPLEMENTED: new optional `ProductPlanningExecutionHintDto` on `ProductPlanningBoardDto`

### `PoTool.Api/Services/ProductPlanningBoardExecutionHintService.cs`

- IMPLEMENTED:
  - execution hint composition on top of the existing board response
  - gating to `Watch` / `Investigate` only
  - single visible hint selection
  - strong-over-weak arbitration
  - Phase 26 tie-break order:
    1. `spillover-increase`
    2. `completion-below-typical`
    3. `completion-variability`

- IMPLEMENTED: exact surfaced texts:
  - `Execution signal: committed delivery below typical range`
  - `Execution signal: delivery consistency outside typical range`
  - `Execution signal: direct spillover increasing`

- VERIFIED:
  - no new anomaly keys were introduced
  - no additional routes were introduced
  - `Stable` and `InsufficientEvidence` return no hint

### `PoTool.Api/Controllers/ProductPlanningBoardController.cs`

- IMPLEMENTED:
  - controller responses now enrich returned planning boards with the optional execution hint
  - existing endpoint paths remain unchanged
  - optional query context (`teamId`, `sprintId`) is accepted to preserve direct-navigation context for the hint

### `PoTool.Client/Services/ProductPlanningBoardClientService.cs`

- IMPLEMENTED:
  - board GET/reset/mutation requests now carry optional `teamId` and `sprintId`
  - query strings are appended without changing endpoint paths

## 5. Routing behavior

### `PoTool.Client/Models/ProductPlanningExecutionHintNavigation.cs`

- IMPLEMENTED: exact Phase 25 route mapping:
  - `completion-below-typical` → `/home/delivery/execution`
  - `completion-variability` → `/home/trends/delivery`
  - `spillover-increase` → `/home/delivery/execution`

- IMPLEMENTED:
  - Sprint Execution navigation uses sprint-mode filter context from the hint team/sprint
  - Delivery Trends uses the current resolved range when available; otherwise it falls back to a valid single-sprint range using the hinted sprint

- VERIFIED:
  - no intermediate chooser or multi-step UI was added
  - no alternate route options were added

## 6. Regression safeguards

### Automated safeguards

- IMPLEMENTED:
  - `PoTool.Api.Tests/ProductPlanningBoardExecutionHintServiceTests.cs`
  - `PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`
  - `PoTool.Tests.Unit/Models/ProductPlanningExecutionHintNavigationTests.cs`
  - `PoTool.Tests.Unit/Audits/PlanBoardExecutionHintMarkupAuditTests.cs`

- VERIFIED:
  - rendering contract is source-audited for exactly one board-level hint instance
  - markup audit ensures the hint stays above the sprint heat grid and outside track/epic markup
  - API tests verify trigger gating, arbitration, fallback context, and exact anomaly-to-text mapping
  - client tests verify navigation state construction and query-string propagation

### Manual verification checklist

- VERIFIED:
  - checklist added for reviewer/operator use

- [ ] Heat remains the dominant visual element
- [ ] Scan order still reads explanatory text → execution hint → sprint heat grid
- [ ] No duplicate execution hints appear across sprints
- [ ] Hint styling remains weaker than high-strain red heat
- [ ] Clicking the hint routes directly to the expected delivery/trend page

## 7. Test coverage and validation

- VERIFIED: build and test commands run after implementation
  - `dotnet build PoTool.sln`
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`
  - `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`

- VERIFIED: results
  - `PoTool.Api.Tests`: 41 passed
  - `PoTool.Tests.Unit`: 2117 passed
  - `PoTool.Core.Domain.Tests`: 48 passed

## Final section

### IMPLEMENTED

- IMPLEMENTED: reusable `ExecutionRealityHint` component
- IMPLEMENTED: PlanBoard placement above sprint heat
- IMPLEMENTED: exact Phase 26 phrasing and one-hint rendering
- IMPLEMENTED: API-side trigger gating and arbitration
- IMPLEMENTED: direct Phase 25 routing integration
- IMPLEMENTED: debounce + minimum-visible anti-flicker behavior
- IMPLEMENTED: automated routing/markup/regression tests
- IMPLEMENTED: release note entry for the user-visible planning-board change

### VERIFIED

- VERIFIED: no planning heat or planning signal calculations were changed
- VERIFIED: no Phase 23c CDC logic was changed
- VERIFIED: no Phase 24 interpretation logic was changed
- VERIFIED: no Phase 25 route definitions were changed
- VERIFIED: the hint is absent for `Stable` and `InsufficientEvidence`
- VERIFIED: validated build and test suites passed

### REGRESSIONS

- REGRESSION: none found in validated build/test coverage

### RISKS

- RISK: when the board has no explicit team selection in the shared filter state, the hint currently falls back to the product’s first linked team
- RISK: `completion-variability` navigation falls back to a one-sprint range if no resolved range already exists on the current board context
- RISK: final visual dominance still requires reviewer confirmation in the live UI because automated tests validate placement and structure, not pixel output

### GO / NO-GO for Phase 28 (real usage validation)

- GO: Phase 28 may proceed because the Phase 26 contract is now implemented with one board-level hint, direct routing, deterministic arbitration, anti-flicker rules, regression safeguards, and passing build/test validation.
