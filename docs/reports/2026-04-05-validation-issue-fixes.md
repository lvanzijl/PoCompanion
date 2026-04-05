# Validation issue fixes — 2026-04-05

## Scope
This report documents the sequential analysis, fixes, and revalidation for the 4 open issues from `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-04-validation-report.md`.

Context used during verification:
- mock profile: `Commander Elena Marquez`
- `productId=1`: `Incident Response Control`
- `productId=2`: `Crew Safety Operations`
- `teamId=4`: `Crew Safety`
- `sprintId=2`: `Sprint 11`
- project route alias: `battleship-systems`

Validation runtime:
- API: `http://localhost:5291`
- Client: `http://localhost:5292`

Build/test validation used during this task:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~BattleshipWorkItemGeneratorTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~GetProductPlanningProjectionsQueryHandlerTests|FullyQualifiedName~GlobalFilterDefaultsServiceTests|FullyQualifiedName~GlobalFilterRouteServiceTests"`

## Issue 1 — Sprint Execution returns no execution data for the current sprint

### Repro status
Reproduced on `/home/delivery/execution?productId=1&teamId=4&sprintId=2&timeMode=Sprint` before the fix.

### Verwachte data
Sprint 11 execution data for product 1 should show committed/unfinished/completed scope, because the page is explicitly scoped to product 1 and Sprint 11.

### Mockdata aanwezig
Before fix: **nee** for the required current sprint path alignment.
After fix: **ja**.

Evidence:
- Before fix, product 1 PBI/Bug work items were still assigned to legacy iteration paths like `\Battleship Systems\2025\Q1\Sprint 3` and `\Battleship Systems\2025\Q2\Sprint 9`.
- The current seeded sprint entities for teams 4/5/6 use `\Battleship Systems\Sprint 10..14`.
- After fix, product 1 has Sprint 11 work items and Sprint Execution returned non-empty data (`initialScopeCount=47`, `unfinishedCount=204`, non-empty rows in the UI/API result).

### Root cause categorie
**data**

### Concrete root cause
Battleship mock work items used legacy quarter-based sprint paths that no longer matched the currently seeded Battleship sprint entities. `GetSprintExecutionQueryHandler` correctly filtered by sprint path, but the required mockdata for Sprint 11 did not exist under the active seeded sprint scheme.

### Aangepaste bestanden
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipWorkItemGenerator.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/MockData/BattleshipWorkItemGeneratorTests.cs`

### Beschrijving van fix
- Aligned generated planning/delivery sprint paths with the seeded Battleship sprint window (`\Battleship Systems\Sprint 10..14`).
- Weighted mock delivery work toward the active/current planning window so Sprint 11 receives real data.
- Added a regression test to ensure generated delivery work includes Sprint 11 and no longer uses the legacy `\2025\Q*\Sprint *` paths for delivery items.

### Resultaat na hertest
**Fixed**
- API: `GET /api/metrics/sprint-execution?productOwnerId=1&sprintId=2&productId=1` now returns `state=2` with non-empty execution data.
- UI: Sprint Execution now renders committed story points and an `Unfinished PBIs (204)` table instead of the previous empty-state message.

---

## Issue 2 — Project Planning Overview loses product/project data on the project route

### Repro status
Reproduced on `/planning/battleship-systems/overview?productId=1` before the fix.

### Verwachte data
The project route should resolve `battleship-systems` to the Battleship project and render the read-only summary across the project’s products.

### Mockdata aanwezig
**Ja**

Evidence:
- API returned valid project summary data for `battleship-systems`.
- API returned 6 project products and non-zero planning totals.
- The page still rendered `0 products` and a blank project alias before the fix.

### Root cause categorie
**frontend**

### Concrete root cause
`ProjectsClient` was missing the repository’s required case-insensitive JSON serializer configuration. The API response body was valid camelCase JSON, but the generated client deserialized the shared DTO into default values on the browser side.

### Aangepaste bestanden
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/ApiClient.Extensions.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/ProjectServiceTests.cs`

### Beschrijving van fix
- Added `ProjectsClient` to the generated-client JSON settings partials so it uses case-insensitive deserialization like the other NSwag clients.
- Added a regression test asserting `ProjectsClient` exposes case-insensitive serializer settings.

### Resultaat na hertest
**Fixed**
- API already returned correct data; after the client fix the page renders:
  - `battleship-systems`
  - `Read-only summary across 6 products`
  - non-zero planned PBI/effort values
- The stale `0 products` state no longer occurs.

---

## Issue 3 — Multi-Product Planning selection/projection mismatch

### Repro status
Reproduced on `/planning/multi-product` before the fixes.

### Verwachte data
With products 1 and 2 selected, the page should use the same product set for selection state, sprint cadence resolution, projection requests, and rendering.

### Mockdata aanwezig
**Ja**, but it was partly unusable before the fixes.

Evidence:
- Planning projection endpoints returned roadmap epics for products 1 and 2.
- Before the fixes, many projections had no usable completion date and the page resolved sprint cadence from an empty sprint list.
- The UI could show selected products while still building zero visible lanes.

### Root cause categorie
**frontend / backend**

### Concrete root cause
Two problems combined:
1. The page displayed the selected-product count from `_productLanes.Count` instead of the actual selected product state.
2. The page resolved sprint cadence from `[]`, so no product lane could become timeline-capable even when team sprint data existed.
3. Completed forecasts with `SprintsRemaining <= 0` were returned with `EstimatedCompletionDate = null`, leaving timeline bars without an authoritative end date.

### Aangepaste bestanden
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetProductPlanningProjectionsQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipWorkItemGenerator.cs`

### Beschrijving van fix
- Made the selection label reflect `_selectedProductIds.Count` instead of the rendered lane count.
- Loaded actual team sprint history for the selected products and passed it into `SprintCadenceResolver`.
- Added a backend fallback so persisted completed forecasts with no explicit completion date reuse `LastUpdated` as the authoritative end date.
- The mockdata sprint-path alignment from issue 1 also ensured forecast materialization could use the seeded sprint window consistently.

### Resultaat na hertest
**Fixed**
- API: product 1 projections now expose completion dates for all 14 roadmap epics.
- UI: `/planning/multi-product` now renders:
  - `2 selected product(s)`
  - a visible global forecast axis
  - lanes for `Incident Response Control` and `Crew Safety Operations`
  - projected epic rows for both products

---

## Issue 4 — Plan Board still blocks sprint columns after product selection

### Repro status
Reproduced on `/planning/plan-board?productId=1` before the fix.

### Verwachte data
Once a concrete product is selected, Plan Board should use that product’s team context to resolve usable sprint columns instead of staying permanently column-less.

### Mockdata aanwezig
**Ja**

Evidence:
- Product 1 already had team IDs `[4, 5, 6]`.
- Teams 4/5/6 already had seeded sprints for `Sprint 10..14`.
- The page still hardcoded `var sprints = new List<SprintDto>();` and emitted the “no longer derives sprint columns” message.

### Root cause categorie
**frontend / state-routing**

### Concrete root cause
The page no longer derived sprint columns from the selected product’s teams, but it also had no alternative explicit sprint-input UI. That left the board with valid product context and valid sprint data available, yet no columns could ever be created.

### Aangepaste bestanden
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`

### Beschrijving van fix
- Restored sprint-column resolution from the selected product’s team sprint data.
- Loaded sprint lists for the product’s teams, deduplicated them by normalized path, and selected current/future sprint columns (with a bounded fallback to the latest dated sprints).
- Replaced the hardcoded “columns unavailable” behavior with a real empty-state only when no team sprint data exists.

### Resultaat na hertest
**Fixed**
- UI now shows sprint columns on Plan Board for product 1.
- Validation snapshot confirms columns including `Sprint 11`, `Sprint 12`, and `Sprint 13` are present.

---

## End summary

| Issue | Status | Root cause | Data issue or logic issue | Remaining risk |
| --- | --- | --- | --- | --- |
| 1. Sprint Execution | **Fixed** | Battleship mock work items used legacy sprint paths that did not match the seeded Sprint 10..14 entities | **Data issue** | Low — future mockdata changes must keep work-item iteration paths aligned with seeded sprint paths |
| 2. Project Planning Overview | **Fixed** | `ProjectsClient` missed case-insensitive JSON settings and deserialized valid project summaries into default values | **Logic issue** | Low — any new NSwag client still needs to be added to `ApiClient.Extensions.cs` |
| 3. Multi-Product Planning | **Fixed** | Selection count used rendered lanes, sprint cadence resolved from an empty sprint list, and completed forecasts lacked fallback end dates | **Logic issue** | Low/medium — future forecast semantics should preserve usable completion dates for completed projections |
| 4. Plan Board | **Fixed** | Product-selected board stopped deriving sprint columns from product team sprint data without providing any replacement input path | **Logic issue** | Low — if Plan Board UX changes again, sprint-column source must remain explicit and functional |

## Final status
All 4 issues were analyzed sequentially, verified against code/mockdata, fixed, and revalidated.
