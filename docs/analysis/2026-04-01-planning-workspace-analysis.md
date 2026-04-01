# Planning Workspace Analysis

Date: 2026-04-01

## Scope

This report describes how the current planning workspace works in practice across the planning hub, the operational sprint planning board, and the strategic roadmap surfaces. Findings are based on the implemented UI, client services, API endpoints, handlers, and existing architecture documentation.

## 1. Planning-related pages, components, and routes

### 1.1 Workspace and routes

The planning area currently consists of one navigation hub and two concrete planning experiences:

| Route | File | Role |
| --- | --- | --- |
| `/home/planning` | `PoTool.Client/Pages/Home/PlanningWorkspace.razor` | Lightweight planning hub that links to the roadmap and plan-board tools. |
| `/planning/product-roadmaps` | `PoTool.Client/Pages/Home/ProductRoadmaps.razor` | Read-only cross-product roadmap overview. |
| `/planning/product-roadmaps/{productId}` | `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor` | Editable per-product roadmap planning surface for epics. |
| `/planning/plan-board` | `PoTool.Client/Pages/Home/PlanBoard.razor` | Operational sprint planning board for PBIs and bugs. |

The route constants are centralized in `PoTool.Client/Models/WorkspaceRoutes.cs:118-132`, and the planning hub itself is intentionally static: it does not load planning data before the user chooses a destination (`PlanningWorkspace.razor:32-60`; `docs/architecture/navigation-map.md:357-370`).

### 1.2 Key UI components and helpers

The current planning implementation is split between direct page logic and shared client helpers:

- `PoTool.Client/Services/PlanBoardWorkItemRules.cs`
  - defines what can appear on the plan board
  - builds the candidate hierarchy
  - decides whether an item is already assigned to a visible sprint
- `PoTool.Client/Services/RoadmapWorkItemRules.cs`
  - identifies epics/objectives
  - interprets the `roadmap` tag used for roadmap membership
- `PoTool.Client/Services/RoadmapEpicPriorityReorderPlanner.cs`
  - computes priority-preserving reorder writes for roadmap epics

The roadmap page also exposes reporting and snapshot features, but those are read-only extras around the roadmap overview rather than alternative planning models (`ProductRoadmaps.razor:39-97`, `docs/architecture/navigation-map.md:523-526`).

## 2. What objects are being planned

### 2.1 Strategic planning: epics on the roadmap

The roadmap editor plans **epics**. Roadmap membership is determined by the presence of the lowercase `roadmap` tag on epic work items (`RoadmapWorkItemRules.cs:11-25`; `docs/architecture/navigation-map.md:534-548`).

In practice, the editable roadmap model is:

- **product-scoped**
- **epic-only**
- **ordered by `BacklogPriority`**
- **membership controlled by tags**

The overview page is read-only, while the editor page allows:

- adding an epic to the roadmap by appending the `roadmap` tag
- removing an epic by removing that tag
- reordering roadmap epics by changing `BacklogPriority`
- editing epic title and description

Relevant implementation:

- `ProductRoadmapEditor.razor:674-714`
- `ProductRoadmapEditor.razor:921-1007`
- `ProductRoadmapEditor.razor:1011-1200`

### 2.2 Operational planning: PBIs and bugs into sprints

The plan board plans **PBIs and bugs** into sprints. The page loads the product hierarchy but only PBIs and bugs are actual sprint-plannable leaves (`PlanBoardWorkItemRules.cs:8-15`, `57-60`).

The left panel is not a flat backlog. It is a derived hierarchy:

- Epic
  - Feature
    - PBI / Bug

Epics and features are visible parents when they are not Done or Removed, but dragging them really means “plan all eligible descendant PBIs/bugs” through `EligiblePbiIds` (`PlanBoardWorkItemRules.cs:33-39`, `124-183`, `264-268`).

### 2.3 Planning dimensions

Current planning uses the following dimensions:

#### Roadmap planning

- **Product**
- **Epic membership** (`roadmap` tag)
- **Relative ordering** via `BacklogPriority`

There is no additional time-axis enforcement in the editor itself; the roadmap is an ordered list, not a dated schedule.

#### Sprint planning

- **Product** (`PlanBoard.razor:533-561`)
- **Team-derived sprint list** using the product's first team only (`PlanBoard.razor:564-575`)
- **Iteration path / sprint path** as the persisted assignment field (`PlanBoard.razor:734`, `792`; `UpdateWorkItemIterationPathCommand.cs:5-11`)
- **Work-item type** (only PBIs and bugs are assignable)
- **State classification** (Done and Removed are excluded) (`PlanBoard.razor:541-547`)
- **Effort/story points** for display and capacity indication (`PlanBoard.razor:171-203`, `875-900`)

Only the next three future sprints are surfaced in the board (`PlanBoard.razor:569-574`; `docs/architecture/navigation-map.md:575-576`).

## 3. How planning behavior works

### 3.1 Planning hub behavior

`/home/planning` is a pure navigation workspace. It provides two entry points:

- **Product Roadmaps**
- **Plan Board**

It also includes links outward to health, trends, backlog health, and delivery, but it does not perform any planning logic itself (`PlanningWorkspace.razor:32-95`).

### 3.2 How items are assigned to sprints

#### Board loading

When the plan board loads a product, it:

1. retrieves the product
2. loads Done/Removed state classifications
3. loads the work-item hierarchy from the product's `BacklogRootWorkItemIds`
4. loads sprints for `product.TeamIds.First()`
5. keeps only future sprints
6. shows only the first three upcoming sprints
7. splits data into:
   - left-side candidate tree for unplanned PBIs/bugs
   - right-side sprint columns for already assigned PBIs/bugs

Implementation:

- `PlanBoard.razor:526-593`
- `PlanBoard.razor:636-675`
- `PlanBoardWorkItemRules.cs:46-121`

#### Assignment interaction

Sprint assignment is explicit drag-and-drop or drag-move only:

- dragging a PBI/bug leaf onto a sprint assigns that work item
- dragging a feature or epic assigns all eligible descendant PBIs/bugs
- dragging an already assigned sprint item to another sprint reassigns it

The actual persisted value is the target sprint's `Path`, written as the work item's `IterationPath` (`PlanBoard.razor:729-767`, `785-815`).

There is no intermediate draft model. The board writes immediately and then reloads.

### 3.3 How roadmap changes are persisted

Roadmap edits are also immediate-write operations:

- **add to roadmap** = add `roadmap` tag, then assign a `BacklogPriority`
- **remove from roadmap** = remove `roadmap` tag
- **reorder roadmap** = update one or more `BacklogPriority` values
- **edit epic** = update title and/or description

Key implementation paths:

- add/remove by drag: `ProductRoadmapEditor.razor:674-753`
- add/remove by button: `ProductRoadmapEditor.razor:921-1007`
- reorder: `ProductRoadmapEditor.razor:755-813`, `1011-1200`
- edit drawer save: `ProductRoadmapEditor.razor:1098-1142`

If reordering cannot safely reuse existing priorities because priorities are missing, invalid, or duplicated, the editor first normalizes priorities to deterministic `1000.0` spacing and then retries (`ProductRoadmapEditor.razor:1061-1078`, `1144-1169`; `RoadmapEpicPriorityReorderPlanner.cs:21-91`).

### 3.4 How changes are persisted to TFS

The client does not write directly from components with ad hoc `HttpClient` code. It goes through typed services:

- `WorkItemService.UpdateIterationPathAsync` (`WorkItemService.cs:327-344`)
- `WorkItemService.UpdateBacklogPriorityAsync` (`WorkItemService.cs:308-325`)
- `WorkItemService.UpdateTagsAsync` (`WorkItemService.cs:346-358`)
- `WorkItemService.UpdateTitleDescriptionAsync` (`WorkItemService.cs:360-372`)

The API exposes matching work-item endpoints:

- `POST /api/workitems/{tfsId}/iteration-path` (`WorkItemsController.cs:339-362`)
- `POST /api/workitems/{tfsId}/backlog-priority` (`WorkItemsController.cs:314-337`)
- `POST /api/workitems/{tfsId}/tags` (`WorkItemsController.cs:274-292`)
- `POST /api/workitems/{tfsId}/title-description` (`WorkItemsController.cs:294-312`)

On the server, the write pattern is consistent:

1. write to TFS through `ITfsClient`
2. refresh or reconstruct the updated work item
3. upsert the local cache/repository

Examples:

- iteration path: `UpdateWorkItemIterationPathCommandHandler.cs:29-68`
- backlog priority: `UpdateWorkItemBacklogPriorityCommandHandler.cs:29-68`
- tags: `UpdateWorkItemTagsCommandHandler.cs:30-47`
- title/description: `UpdateWorkItemTitleDescriptionCommandHandler.cs:30-47`

For `IterationPath` and `BacklogPriority`, the handlers explicitly override the refreshed entity with the just-written value so the cache still reflects the intended new state even if TFS re-reads are temporarily stale (`UpdateWorkItemIterationPathCommandHandler.cs:41-66`, `UpdateWorkItemBacklogPriorityCommandHandler.cs:41-66`). The test coverage for iteration-path updates documents this behavior directly (`PoTool.Tests.Unit/Handlers/UpdateWorkItemIterationPathCommandHandlerTests.cs:49-155`).

### 3.5 Manual or assisted planning

The current planning model is predominantly **manual with lightweight indicators**.

#### What is manual

- Users choose the product.
- Users drag items into sprints manually.
- Users drag/reorder roadmap epics manually.
- Users decide whether an epic belongs on the roadmap by tag membership.
- Users must refresh from TFS manually when they want a fresh board snapshot (`PlanBoard.razor:36-42`, `606-633`).

#### What assistance exists

- capacity visualization based on historical velocity (`PlanBoard.razor:168-207`, `1001-1030`)
- warnings for overcommitment and missing estimates (`PlanBoard.razor:196-203`, `255-259`, `885-890`)
- hierarchy aggregation so parent drag operations can place all eligible descendants (`PlanBoardWorkItemRules.cs:124-183`)
- roadmap reorder recovery when priorities are malformed (`ProductRoadmapEditor.razor:1144-1169`)

#### What assistance does not exist

There is no evidence of:

- auto-assignment to sprints
- recommendation of the “best” sprint
- optimization across teams
- capacity-based blocking
- sequence validation that prevents unrealistic plans
- dependency-aware sprint planning

Capacity is advisory only; the UI warns when assigned points exceed historical capacity but still allows the plan (`PlanBoard.razor:169-203`).

## 4. Limitations in the current planning model

### 4.1 What the current planning cannot express well

#### Multi-team planning

The plan board loads sprints from `product.TeamIds.First()` only (`PlanBoard.razor:564-575`). That means:

- products with multiple teams do not get a combined or parallel sprint view
- team-specific planning trade-offs are hidden
- the first team effectively becomes the planning authority for sprint selection

#### More than three planning horizons

The board deliberately limits the planning horizon to the next three future sprints (`PlanBoard.razor:570-574`). Longer-range sprint sequencing is not visible in this surface.

#### Rich roadmap structure

The roadmap editor is an ordered epic list, not a full release model. It does not model:

- explicit capacity per roadmap slot
- dependency-constrained sequencing rules
- milestone enforcement
- alternative scenarios or draft plans
- multiple parallel lanes per product beyond membership/order

#### Detailed intra-sprint planning

Within a sprint column, the board shows assigned items but does not provide a richer model for:

- ordering within the sprint
- day-by-day sequence
- explicit dependency chains
- splitting work across multiple sprints/teams

#### Drafting or approval workflows

Both roadmap and sprint planning write directly to TFS-backed state. There is no explicit concept of:

- save-as-draft
- compare two alternative plans
- review/approve before publish
- undo beyond manual reverse edits

Snapshots exist for the roadmap overview, but not as a general planning draft workflow (`ProductRoadmaps.razor:77-97`; `docs/architecture/navigation-map.md:524-526`).

### 4.2 Where users are likely to struggle

#### Products with more than one team

Because sprint planning uses the first team only, users in multi-team products are likely to struggle to understand:

- which team's sprints they are actually planning against
- how to represent cross-team parallel execution
- whether another team's capacity matters

#### Unestimated work

The board allows items with no estimate to be planned and only flags them with a warning (`PlanBoard.razor:255-259`, `885-890`). Users can therefore create a plan that appears valid structurally but weakens the capacity signal.

#### Hidden assignment scope on parent drag

Dragging an epic or feature plans all eligible descendant PBIs/bugs via `EligiblePbiIds` (`PlanBoardWorkItemRules.cs:141-143`, `166-167`, `265-267`). That is efficient, but the UI model can make it easy to underestimate how many underlying items are about to be reassigned.

#### Immediate-write behavior

Because edits persist immediately, users do not get a safe staging area for experimentation. Mistakes must be corrected by further writes rather than by discarding a draft.

#### Priority recovery and ordering edge cases

Roadmap reordering depends on existing `BacklogPriority` quality. The editor contains recovery logic for missing/duplicate/invalid priorities, which is helpful, but it also reveals that roadmap ordering can become hard to reason about when TFS priorities are already inconsistent (`RoadmapEpicPriorityReorderPlanner.cs:48-69`; `ProductRoadmapEditor.razor:1144-1169`).

## 5. Implicit assumptions in the current design

### 5.1 Single team over multiple teams

The implementation assumes sprint planning is effectively **single-team per product** because it always pulls sprints from the first team attached to the product (`PlanBoard.razor:567-569`).

### 5.2 Sequential over parallel work

Roadmap planning assumes a single ordered backlog for epics inside a product. Sprint planning assumes one visible sequence of up to three sprint buckets. Neither surface expresses a richer model of concurrent streams, shared ownership, or parallel team tracks.

### 5.3 Capacity awareness is present but lightweight

Capacity awareness exists, but only as an advisory overlay:

- six historical sprints are used for calibration (`PlanBoard.razor:426`, `988-1007`)
- a median historical capacity is shown
- overcommitment generates warnings, not blocking rules

So capacity awareness is **present**, but it is **not a governing planner**.

### 5.4 TFS as the source of truth

Both planning experiences assume TFS-backed fields are canonical:

- roadmap membership from tags
- roadmap order from `BacklogPriority`
- sprint assignment from `IterationPath`

The application cache is deliberately kept aligned with those TFS writes, but not treated as the primary source of truth (`UpdateWorkItemIterationPathCommandHandler.cs:41-66`; `UpdateWorkItemBacklogPriorityCommandHandler.cs:41-66`; `UpdateWorkItemTagsCommandHandler.cs:34-47`).

### 5.5 Product-scoped planning

Both planning surfaces center planning around a selected product. There is no current cross-product planning model for balancing a shared team or portfolio-wide sprint allocation from this workspace.

## 6. Practical conclusion

In practice, the planning workspace is not one unified planning engine. It is a small planning hub that routes to two distinct tools:

1. **Product Roadmap planning** for curating and ordering epics per product using tags and `BacklogPriority`
2. **Plan Board sprint planning** for assigning PBIs and bugs into the next three sprints using `IterationPath`

The model is direct, understandable, and operationally simple, but it is also narrow:

- mostly manual
- immediate-write
- product-scoped
- weak on multi-team and parallel planning
- advisory rather than constraint-driven on capacity

That makes it effective for explicit PO-driven curation, but limited as a broader planning system for scenario analysis, team balancing, or capacity-aware portfolio coordination.
