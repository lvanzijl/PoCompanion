# Phase 9 UI Integration — Planning Board

## 1. Summary

- **IMPLEMENTED:** The existing `/planning/plan-board` workspace now loads and renders the real product planning board from the completed planning API instead of the old drag-based backlog/sprint board.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor.css`.

- **IMPLEMENTED:** The client now has a typed planning-board API service for all existing planning endpoints without changing backend contracts or reusing `ReleasePlanningBoard`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Program.cs`.

- **IMPLEMENTED:** The UI now exposes explicit operations for move, adjust spacing, run in parallel, return to main, reorder, shift plan, reload, and reset, while surfacing changed/affected highlights, validation issues, and session-ready state on the board itself.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

## 2. Chosen UI surface and rationale

- **VERIFIED:** The correct UI surface is the existing `Plan Board` route (`/planning/plan-board`), not a new route or experimental page. The planning workspace already advertises this surface as the operational planning entry point.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanningWorkspace.razor`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/WorkspaceRoutes.cs`.

- **VERIFIED:** This location fits the current planning workflow because it already participates in the shared project/product scope routing and global filter workflow, so the new board remains product-scoped without inventing a second planning entry path.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

- **VERIFIED:** `ReleasePlanningBoard` was not used because it is an alternate model and is explicitly out of scope for this phase; the new UI is wired to the existing `ProductPlanningBoardController` endpoints instead.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; no `ReleasePlanningBoard` references were added in the changed files.

## 3. Files added/changed

- **IMPLEMENTED:** Added
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor.css`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-9-ui-integration-planning-board.md`

- **IMPLEMENTED:** Updated
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Program.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## 4. Client API wiring added/changed

- **IMPLEMENTED:** Added a dedicated `ProductPlanningBoardClientService` that calls the existing planning-board API endpoints for:
  - get board
  - reset
  - move
  - adjust spacing
  - run in parallel
  - return to main
  - reorder
  - shift plan  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`.

- **IMPLEMENTED:** Registered the service in the Blazor DI container so pages/components use one typed client service instead of duplicating `HttpClient` calls.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Program.cs`.

## 5. Board rendering implemented

- **IMPLEMENTED:** The page now renders:
  - product board header/state
  - sprint axis
  - main lane
  - derived parallel tracks
  - epic bars/cards positioned by computed sprint span
  - planned/computed start, duration, derived end, track position
  - changed and affected markers
  - board-level and epic-level validation issues  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor.css`.

- **IMPLEMENTED:** Added a small render-model factory to keep grouping/order/status derivation out of the Razor markup and avoid copying backend planning logic into the page.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`.

## 6. Controls/actions implemented

- **IMPLEMENTED:** Every epic card now exposes explicit controls for:
  - move by sprint
  - adjust spacing before
  - shift plan
  - reorder
  - run in parallel
  - return to main  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

- **IMPLEMENTED:** The board header now exposes explicit reload and reset actions for iterative planning sessions.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

## 7. UX rules implemented

- **IMPLEMENTED:** Cause/effect visibility
  - changed epic = stronger visual treatment
  - affected epic = lighter visual treatment  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor.css`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

- **IMPLEMENTED:** Main vs parallel clarity
  - main lane uses the dominant product color treatment
  - parallel tracks use the same hue family with lower intensity  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor.css`.

- **IMPLEMENTED:** No silent failures
  - board-level feedback is shown inline
  - per-epic invalid input explanations are shown inline
  - server-returned validation issues remain visible on the board  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

- **IMPLEMENTED:** Spacing visibility
  - sprint-axis grid placement leaves empty spans visible without separate gap entities  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor.css`.

- **IMPLEMENTED:** Track minimization awareness
  - the UI explicitly states that tracks are derived automatically and does not ask the user to choose arbitrary track numbers  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

- **IMPLEMENTED:** Stable-state indicator
  - stable / changed / warning status is derived from changed ids and validation issues and shown in the board header  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.

## 8. Tests added

- **IMPLEMENTED:** Added runnable client-service and render-model tests covering:
  - board load endpoint wiring
  - reset endpoint wiring
  - all explicit mutation endpoints
  - not-found handling
  - main/parallel track render-model grouping
  - changed/warning status derivation  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`.

- **VERIFIED:** Executed tests:
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build --no-restore`  
  **Evidence:** successful local test run during implementation.

## 9. Verified preserved backend semantics

- **VERIFIED:** No planning engine rules changed.  
  **Evidence:** no changed files under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/**`.

- **VERIFIED:** No persistence, recovery, TFS write-back, API contract, or session model changes were made in this phase.  
  **Evidence:** changed files are confined to `PoTool.Client/**`, one client-facing test file, release notes, and this report.

- **VERIFIED:** The page consumes the existing `ProductPlanningBoardDto` and request contracts only.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`.

## 10. Known gaps intentionally left for later phases

- **NOT IMPLEMENTED:** Drag & drop.
- **NOT IMPLEMENTED:** New planning endpoints or backend redesign.
- **NOT IMPLEMENTED:** Browser-level component rendering harness; current automated coverage is service/render-helper level under existing MSTest conventions.
- **NOT IMPLEMENTED:** Calendar/date labels beyond the sprint-index axis already exposed by the current planning-board DTO contract.

## 11. Risks or blockers

- **VERIFIED:** No blocker was hit that required backend redesign or `ReleasePlanningBoard` reuse.
- **VERIFIED:** Parallel builds can still hit transient Blazor output file-lock errors; sequential client builds succeeded.  
  **Evidence:** sequential `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --no-restore` passed.

## 12. Recommendation for next phase

- **RECOMMENDATION:** Add richer browser-level interaction tests once the repository adopts an approved Blazor component/browser test harness, and consider exposing canonical sprint/date labels from the backend read model if product owners need named sprint windows instead of indexed sprint positions.

## Final section

### IMPLEMENTED

- Real product planning board UI on the existing `/planning/plan-board` route
- Typed client API wiring for all existing planning-board endpoints
- Main lane + derived parallel track rendering
- Explicit controls for all locked planning actions
- Inline changed/affected/validation/session-state feedback
- Reload and reset session actions
- Release note entry for the user-visible change
- Runnable MSTest coverage for client endpoint wiring and render-model behavior

### NOT IMPLEMENTED

- Drag & drop
- Backend, engine, persistence, or API redesign
- ReleasePlanningBoard reuse
- Browser-level UI automation harness

### BLOCKERS

- None for this phase’s locked scope

### Evidence (files/tests)

- **Files:** all files listed in sections 3–8 above
- **Builds/tests passed:**
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --no-restore`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build --no-restore`

### GO/NO-GO for next phase

- **GO:** The planning UI is now integrated into the real client workflow without changing the locked backend semantics.
