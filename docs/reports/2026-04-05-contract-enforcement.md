# Contract enforcement report — 2026-04-05

## Per-page deviations

| Page | Contract deviation before change | Client/API source | Status |
|---|---|---|---|
| `PortfolioDelivery` | Product-aware page ignored selected product scope and always requested all products | `PoTool.Client/Pages/Home/PortfolioDelivery.razor` | fixed |
| `PipelineInsights` | Product-aware page ignored selected product scope; API endpoint could not accept product IDs for insights requests | `PoTool.Client/Pages/Home/PipelineInsights.razor`, `PoTool.Client/Services/PipelineStateService.cs`, `PoTool.Client/ApiClient/ApiClient.PipelineFilters.cs`, `PoTool.Api/Controllers/PipelinesController.cs` | fixed |
| `ProductRoadmapEditor` | Contract report identified ambiguity between route product and shared global product state | `PoTool.Client/Services/FilterStateResolver.cs`, `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor` | unchanged; tracked as remaining edge case |

## Changes applied

### `PortfolioDelivery`
- Forwarded the shared global product selection to `MetricsStateService.GetPortfolioDeliveryStateAsync`.
- Preserved all-products mode by sending `null` only when no product is selected.

### `PipelineInsights`
- Forwarded the shared global product selection from the page into `PipelineStateService`.
- Extended the client API wrapper and governed generated client contract to include pipeline `productIds`.
- Extended `PipelinesController.GetInsights` to accept `productIds` and include them in canonical filter resolution.

### Validation coverage
- Added controller tests for:
  - pipeline insights canonical product filtering
  - portfolio delivery canonical product filtering
- Updated generated-client state service tests for the new pipeline signature.
- Added source-level audit coverage to prevent both pages from regressing to ignored product scope.

## Contract compliance status

| Page | Compliance after change | Notes |
|---|---|---|
| `PortfolioDelivery` | compliant | Selected product scope now reaches the API; all-products mode remains explicit |
| `PipelineInsights` | compliant | Selected product scope now reaches canonical pipeline filter resolution |
| `ProductRoadmapEditor` | partially aligned | Existing route/global-state ambiguity remains unchanged in this task |

## Remaining edge cases

### `ProductRoadmapEditor`
- The current implementation still keeps shared global product state authoritative even on `/planning/product-roadmaps/{productId}` routes.
- This task did not change that behavior because the current repository semantics and recent release notes already describe roadmap-editor routes as non-authoritative for product selection.
- If the editor should become truly route-owned in a future change, the page contract and route-alignment rule must be reconciled first.

### Route alignment scope
- Project-scoped planning routes remain the only explicit route-owned multi-product mode.
- This task did not broaden route authority beyond those existing project-scoped cases.
