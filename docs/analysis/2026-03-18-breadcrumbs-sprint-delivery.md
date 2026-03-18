# Bug Analysis Report

## Metadata
- Bug: Sprint Delivery breadcrumbs navigate to /profiles instead of restoring in-page state
- Area: Delivery / Sprint Delivery
- Status: Analysis complete

## ROOT_CAUSE

- Sprint Delivery uses two different breadcrumb contracts, and the drill-down breadcrumb bar is wired to the wrong one. The page correctly stores drill-down position as in-page UI state (`_drillDownLevel`, `_selectedProductId`, `_selectedEpicId`, `_selectedFeatureId`) and changes that state through `DrillIntoProductAsync`, `DrillIntoEpicAsync`, `DrillIntoFeatureAsync`, `DrillUp`, and `ResetDrillDown` in `PoTool.Client/Pages/Home/SprintTrend.razor:1107-1171`. But the visible drill-down breadcrumb bar is built as a plain `List<BreadcrumbItem>` rendered by `<MudBreadcrumbs Items="@_drillDownBreadcrumbs" />` in `PoTool.Client/Pages/Home/SprintTrend.razor:127-137`, and every breadcrumb item is created with `href: null` and no click callback in `PoTool.Client/Pages/Home/SprintTrend.razor:1173-1210`. As a result, breadcrumb clicks are not mapped back into the page's drill-down state. The only implemented in-page back navigation is the separate arrow button (`OnClick="DrillUp"` at `PoTool.Client/Pages/Home/SprintTrend.razor:131-135`). The root cause is therefore a contract mismatch: Sprint Delivery drill-down is state-driven, but its breadcrumb items are constructed as generic navigation breadcrumbs instead of state-restoring breadcrumbs.

## CURRENT_BEHAVIOR
- The page-level header breadcrumb is static and route-based: `Home -> Delivery -> Sprint Delivery`, produced by `UpdateBreadcrumbs()` in `PoTool.Client/Pages/Home/SprintTrend.razor:1235-1243`. It is unrelated to product/epic drill-down state.
- The Sprint Delivery drill-down flow itself is fully in-page. Clicking a product row calls `DrillIntoProductAsync`, which sets `_selectedProductId`, `_selectedProductName`, and `_drillDownLevel = Product`; clicking an epic row calls `DrillIntoEpicAsync`, which sets `_selectedEpicId`, `_selectedEpicName`, and `_drillDownLevel = Epic`; drilling further into a feature follows the same pattern in `PoTool.Client/Pages/Home/SprintTrend.razor:1107-1135`.
- Moving back up the hierarchy is implemented only through `DrillUp()`, which mutates the same state fields back toward `Portfolio`, and through `ResetDrillDown()`, which is called when the sprint changes in `PoTool.Client/Pages/Home/SprintTrend.razor:1137-1171` and `:1250-1273`.
- The breadcrumb bar shown during drill-down is rebuilt from that state by `UpdateDrillDownBreadcrumbs()`, which creates labels `Portfolio`, selected product, selected epic, and selected feature in `PoTool.Client/Pages/Home/SprintTrend.razor:1173-1210`.
- Those breadcrumb items do not carry any state-restoration action. Non-current items are left enabled, but they are not bound to `DrillUp()`, `ResetDrillDown()`, or any item-specific callback; they are only plain `BreadcrumbItem` instances with `href: null`.
- Because the drill-down breadcrumb UI does not participate in the page's in-memory hierarchy contract, clicking `Portfolio` or the product breadcrumb does not restore `products overview -> product -> epic` within Sprint Delivery. Instead, the click escapes the intended in-page flow and the user ends up leaving the workspace, which matches the reported jump to `/profiles`.

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the full Sprint Delivery flow and the page already has the correct hierarchical state model for this feature. Product, epic, and feature drill-down are not represented in the route; they live entirely in component state and are updated by explicit methods on the page.

That is why the breadcrumb bug is not a data problem and not primarily a routing-table problem. The page already knows how to go from portfolio to product and from product to epic, and it already knows how to go back one level through `DrillUp()`. The defect is that the breadcrumb bar was built using generic `BreadcrumbItem` objects without attaching the same state transitions that the rest of the page uses.

So the strict contract for this issue is: Sprint Delivery breadcrumbs are currently presentation-only labels for an in-page hierarchy, but the UI exposes them as if they are navigable. Because they are not wired to restore component state, users leave the intended Sprint Delivery flow instead of returning to the product overview on the same page.
</comments>
