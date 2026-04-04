# Product Context Implementation

## Summary of global Product implementation
- Product authority now comes from the shared global filter state instead of page-local defaults or route-owned product parameters.
- Shared filter resolution no longer treats `/planning/product-roadmaps/{productId}` as authoritative Product context.
- Global defaults keep Product at the explicit `all products` state instead of silently selecting the first owned product for Plan Board.
- Product-scoped pages now resolve their query scope from the shared Product state:
  - `Home`
  - `Backlog Health`
  - `Plan Board`
  - `Product Roadmaps`
  - `Product Roadmap Editor`
  - `Bugs Triage`
  - `Work Item Explorer`
- The page catalog now exposes shared Product filter behavior on `bugs-triage` and `workitems` so Product is globally available there as well.

## Per-page changes applied

| Page | Changes applied |
|---|---|
| `PoTool.Client/Pages/Home/HomePage.razor` | Removed local Product ownership of the chip bar, bound chip state to the global filter store, and refreshed dashboard metrics/signals from global Product state only. |
| `PoTool.Client/Pages/Home/BacklogOverviewPage.razor` | Removed first-product fallback, bound the page selector to the global filter store, and reloaded backlog health only for the explicit globally selected Product. |
| `PoTool.Client/Pages/Home/PlanBoard.razor` | Removed local Product fallback state, stopped auto-selecting the first filtered Product, and reloaded the board only when the shared global Product resolves to one valid Product in scope. |
| `PoTool.Client/Pages/Home/ProductRoadmaps.razor` | Applied global Product scoping when building product lanes and reloaded roadmap lanes when the shared Product filter changes. |
| `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor` | Stopped loading by route parameter authority, loaded editor data from the shared global Product selection, and showed a controlled state when no single Product is selected globally. |
| `PoTool.Client/Pages/BugsTriage.razor` | Replaced implicit profile-owned Product scoping with explicit Product IDs resolved from the shared global filter state. |
| `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor` | Replaced implicit profile/all-products fallback scoping with explicit Product IDs resolved from the shared global filter state. |
| `PoTool.Client/Services/GlobalFilterPageCatalog.cs` | Added `bugs-triage` and `workitems` to the global filter catalog and marked Product Roadmaps as Product-aware. |
| `PoTool.Client/Services/FilterStateResolver.cs` | Changed Product route handling so route Product IDs are treated as lookup hints only, not authoritative selection. |
| `PoTool.Client/Services/GlobalFilterRouteService.cs` | Stopped stripping Product from editor routes and made roadmap-editor paths follow the shared Product state when one Product is selected globally. |
| `PoTool.Client/Services/GlobalFilterDefaultsService.cs` | Removed the Plan Board first-owned-product default so `all products` remains the explicit default Product state. |

## Removed implicit behaviors
- Removed Plan Board automatic selection of the first owned Product.
- Removed Backlog Health automatic selection of the first available Product.
- Removed Home page-local Product state as a separate source of truth.
- Removed Bugs Triage and Work Item Explorer hidden Product scoping based only on profile ownership.
- Removed roadmap-editor route Product authority as a page override.

## Route conflicts and resolutions
- **Conflict:** `/planning/product-roadmaps/{productId}` previously acted as authoritative Product context.
- **Resolution:** route Product IDs are now treated as lookup hints only; shared global Product state remains authoritative.
- **Runtime behavior:**
  - if a single global Product is selected, the editor route now syncs to that Product
  - if no single global Product is selected, the editor stays in a controlled no-selection state instead of silently adopting the route Product

## Validation results (before vs after behavior)

| Scenario | Before | After |
|---|---|---|
| Plan Board with no Product selected | silently picked first owned Product | stays on explicit no-selection state |
| Backlog Health with no Product selected | silently picked first available Product | waits for explicit global Product |
| Product Roadmap Editor opened from route only | route Product overrode global state | route Product is hint-only; global selection wins |
| Bugs Triage | used profile-owned Products implicitly | sends explicit Product IDs from shared global state |
| Work Item Explorer | used profile-owned/all Products implicitly | sends explicit Product IDs from shared global state |
| Shared Product navigation | page-local Product state could diverge from shared filter state | Product state stays aligned through the shared global filter store |

### Automated validation
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~GlobalFilterRouteServiceTests|FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~GlobalFilterDefaultsServiceTests"`

## Risks and follow-up actions
- Multi-product analytical pages still need a follow-up review if they must expose arbitrary multi-select Product subsets through the shared global filter UX instead of their existing page-specific controls.
- The roadmap editor now intentionally refuses to infer Product context from the route alone; old deep links still open, but users must have a single Product selected globally to edit.
- Product-scoped pages now rely more heavily on the shared filter lifecycle, so future changes to global filter routing/defaulting should be validated against these pages together.
