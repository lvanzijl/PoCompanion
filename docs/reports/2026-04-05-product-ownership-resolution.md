# Ownership policy applied

Global Product is now the only authoritative selected product context.

- Route product parameters no longer silently replace the selected global product.
- Project-scoped planning routes now constrain only the allowed product universe.
- Single-product editor routing supports one-time bootstrap from the route only when no global product is selected yet.
- Invalid route/global product combinations now stay visible as explicit invalid-context states instead of being silently normalized.
- Hidden first-product defaults remain removed; all-products mode stays explicit.

# Sources of Product authority found

| Source | Location | Classification | Resolution |
| --- | --- | --- | --- |
| Global filter state | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterStore.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/FilterStateResolver.cs` | allowed | Kept as the authoritative selected product source. |
| Route product parameter on `/planning/product-roadmaps/{productId}` | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/FilterStateResolver.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmapEditor.razor` | bootstrap-only | Route stays a lookup/bootstrap input only; mismatches with global product are now invalid. |
| Route project alias on planning routes | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/FilterStateResolver.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProjectPlanningOverview.razor` | allowed-universe constraint only | Route project now validates the allowed product universe and never silently replaces the global selection. |
| Page-local product writes | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/BacklogOverviewPage.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/GlobalFilterControls.razor` | allowed | Page-local controls only write back into `GlobalFilterStore`; they no longer own divergent state. |
| Implicit first-product fallback | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterDefaultsService.cs` | must be removed | Confirmed absent for product selection; no new fallback was introduced. |
| Profile-owned product derivation for scoped page data | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/BacklogOverviewPage.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/BugsTriage.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/WorkItemExplorer.razor` | allowed universe only | Profile-owned products remain the visible universe for the active profile, but they no longer silently select a product. Invalid global selections now fail explicitly. |

# Changes made per page/route

## Shared filter/routing

- `FilterStateResolver` now marks two product-context failures as invalid:
  - editor route product path does not match the selected global product
  - selected global product is outside a project-scoped planning route universe
- `GlobalFilterRouteService` now preserves the current editor route path when rebuilding the URI, so global product changes do not silently rewrite `/planning/product-roadmaps/{productId}`.
- `GlobalProductSelectionHelper` now exposes unavailable selected-product detection for page-level explicit invalid-context handling.

## `ProductRoadmapEditor`

- Added one-time bootstrap from the route product when the global product is still unset.
- Added explicit invalid-context UI for:
  - route/global product mismatch
  - route product outside the current profile-visible universe
  - selected global product outside the current editor scope
- After bootstrap, the route path is preserved and later global-product changes no longer rewrite the route.

## `ProductRoadmaps`

- Project-scoped routes now use the route project only as the allowed product universe.
- Project-scoped roadmap lanes no longer collapse to the currently selected global product.
- Added explicit invalid-context UI when the selected global product conflicts with the current project scope.

## `PlanBoard`

- Plan Board still requires exactly one selected global product.
- Project routes now constrain the valid product universe only.
- Added explicit invalid-context UI when the selected global product is outside the current route scope.
- Board loading now stops instead of silently clearing to a generic “select product” state.

## `ProjectPlanningOverview`

- Added route/global context validation before loading the project summary.
- Added explicit invalid-context UI when the selected global product conflicts with the route-owned project universe.

## `BacklogOverviewPage`

- Global product remains the only selected-product source.
- Added explicit invalid-context UI when the selected global product is not available in the profile-visible backlog scope.
- Existing page-local dropdown continues to write directly into global filter state only.

## `BugsTriage`

- Added explicit invalid-context handling when the selected global product is outside the active profile scope.
- The page now stops loading triage data instead of silently behaving as empty.

## `WorkItemExplorer`

- Added explicit invalid-context handling when the selected global product is outside the active profile or fallback product universe.
- The page now reports invalid product selection through the existing error surface instead of silently loading an empty result.

# Bootstrap-only route behaviors

- `/planning/product-roadmaps/{productId}` may seed global product selection once, but only when the global product is currently unset.
- Bootstrap writes the route product into global filter state and rebuilds the URI without changing the route path.
- After bootstrap, route/global mismatches are treated as invalid context and remain visible until the user fixes the global selection or leaves the route.

# Invalid-context behaviors introduced

- Project-scoped planning routes now fail explicitly when the selected global product is not in the route project’s product set.
- Product roadmap editor now fails explicitly when the selected global product differs from the route product after initialization.
- Backlog, bug triage, and work-item explorer now fail explicitly when the selected global product is not available in the active profile-visible universe.
- Invalid states surface through page alerts and the existing global filter invalid-state messaging instead of silent route correction or silent empty data.

# Remaining edge cases

- Unknown project aliases on route-owned planning pages still remain route-authoritative placeholders; without a resolved project, the client cannot validate the allowed product universe.
- Product-aware pages outside the targeted set still rely on existing page contracts and were not widened in this change.
- Multi-product pages without a project-scoped route still use the current global all-products or selected-products behavior for the active profile universe.
