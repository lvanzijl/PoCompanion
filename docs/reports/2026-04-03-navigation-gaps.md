# Navigation structural gaps

Sources:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-03-route-inventory.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-03-route-classification.md`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`

This report reviews the classified route set and highlights structural problems only. It does not propose implementation details.

## Orphan routes (no entry)

### `/not-found` (`NotFound`)
- **Problem:** The route is only entered through router not-found handling. There is no app-owned navigation path into it and the classification report records no explicit exit path. That makes it structurally orphaned from the intentional navigation model.
- **Resolution options:**
  1. Keep it framework-only and explicitly document it as a system recovery route outside the regular route taxonomy.
  2. Add a single explicit recovery path back to `/home` or `/profiles` and treat it as a utility system route.
  3. Remove the dedicated route surface and let the router render the not-found experience without exposing `/not-found` as a first-class destination.

## Ambiguous ownership

### `/home/bugs` (`BugOverview`)
- **Problem:** The route is owned as `Health workspace` in the classification report, but the inventory shows its inbound navigation comes from `TrendsWorkspace`, while `WorkspaceNavigationCatalog` groups it under the Health active route set. The route is therefore positioned as a Trends destination while being owned as Health.
- **Resolution options:**
  1. Reassign ownership to Trends and keep it as a historical/quality trend page.
  2. Move its visible entry point into Health and keep Health ownership.
  3. Split the concept into separate Health and Trends bug views with distinct routes and responsibilities.

### `/home/changes` (`HomeChanges`)
- **Problem:** The route is owned as `Home system`, but its exits lead into Delivery and Health flows and its content is change-oriented rather than hub-oriented. It currently sits between workspace navigation, sync diagnostics, and cross-workspace activity review without a clear owning workspace.
- **Resolution options:**
  1. Keep it owned by Home and define it explicitly as the post-sync landing utility for cross-workspace change review.
  2. Re-home it under Trends or Delivery if its purpose is primarily historical change inspection.
  3. Break the page into workspace-specific change views and leave Home with only a summary link-out.

## Conflicting classification

### `/planning/{ProjectAliasRoute}/overview` (`ProjectPlanningOverview`)
- **Problem:** The route is classified as `DeepLinkOnly`, but the inventory shows direct inbound navigation from `PlanningWorkspace` plus return navigation from `ProductRoadmaps` and `PlanBoard`. That behavior matches a planning child route more than a deep-link-only route.
- **Resolution options:**
  1. Reclassify it as `WorkspaceChild` under Planning.
  2. Keep it deep-link-only and remove/avoid direct planning workspace entry links.
  3. Convert it into a generated detail route that is only reachable from one parent planning page.

### `/planning/product-roadmaps/{ProductId:int}` (`ProductRoadmapEditor`)
- **Problem:** The route is classified as `DeepLinkOnly`, but it has explicit inbound navigation from `ProductRoadmaps`, breadcrumbs, and return links. That makes it an in-flow child editor rather than a pure deep link.
- **Resolution options:**
  1. Reclassify it as `WorkspaceChild` under Planning.
  2. Keep it deep-link-only and remove breadcrumb-style round trips so it is clearly an isolated editor entry.
  3. Fold the editor into the roadmaps page so the concept no longer requires its own route class.

### `/home/delivery/sprint/activity/{WorkItemId:int}` and `/home/sprint-trend/activity/{WorkItemId:int}` (`SprintTrendActivity`)
- **Problem:** The route is classified as `DeepLinkOnly`, yet it is a standard drill-down from `SprintTrend`, is included in the Delivery active route prefix set, and has a defined return path to the sprint delivery page. Structurally it behaves like a delivery detail child.
- **Resolution options:**
  1. Reclassify it as `WorkspaceChild` under Delivery.
  2. Keep it deep-link-only and remove it from the Delivery active route prefix set.
  3. Replace the dedicated route with an in-page drawer/dialog drill-down so it no longer competes with workspace-child semantics.

## Deep-link-only without justification

### `/workitems` (`WorkItemExplorer`)
- **Problem:** The route is marked `DeepLinkOnly`, but the inventory shows multiple explicit inbound links from backlog and release-planning surfaces plus local UI navigation inside the page. The current documentation does not explain why it is excluded from a workspace despite repeated first-party entry points.
- **Resolution options:**
  1. Document it as a shared system explorer intentionally used across multiple workspaces.
  2. Assign it to one owning workspace and treat the other references as cross-workspace deep links.
  3. Split the route into workspace-specific explorers if backlog and release-planning use cases are materially different.

### `/planning/{ProjectAliasRoute}/overview` and `/planning/product-roadmaps/{ProductId:int}`
- **Problem:** Both routes are treated as `DeepLinkOnly`, but both participate in stable Planning flows with breadcrumbs and return paths. The classification looks driven by absence from top-level navigation rather than by actual navigation behavior.
- **Resolution options:**
  1. Reclassify both as Planning workspace children.
  2. Add explicit documentation that Planning supports nested detail routes that remain deep-link-only by policy.
  3. Reduce route surface so only one of these remains as a routed detail page and the other becomes embedded UI.

## Missing exit paths

### `/not-found` (`NotFound`)
- **Problem:** The classification report records `none found` for exit path. A dead-end recovery page is a structural navigation risk because the user cannot follow an explicit app-owned path back to a safe entry point.
- **Resolution options:**
  1. Add one deterministic recovery destination such as `/home`.
  2. Redirect automatically to a safe hub after presenting the not-found message.
  3. Keep it as-is but document that browser back is the only supported exit behavior.

### `/workitems` (`WorkItemExplorer`)
- **Problem:** The classification report records `none found` for exit path even though the inventory says the page has local UI navigation. The route therefore lacks a documented canonical way back to its parent context.
- **Resolution options:**
  1. Define a canonical return destination based on the caller context and document it.
  2. Add a neutral fallback exit such as `/home/health/backlog-health`.
  3. Treat it as a utility explorer with a mandatory caller-provided return URL contract.

## Duplicated concepts

### Backlog health dual routes: `/home/health/backlog-health` and `/home/backlog-overview`
- **Problem:** Both routes resolve to `BacklogOverviewPage`. The classification report treats them as one workspace child, but the duplicate paths preserve two conceptual entry points for the same page, which weakens canonical navigation and complicates ownership reasoning.
- **Resolution options:**
  1. Keep only `/home/health/backlog-health` as canonical and redirect the legacy path.
  2. Keep both temporarily but document one as legacy-only with a retirement plan.
  3. Separate the concepts if the legacy path is meant to represent a materially different user mental model.

### Sprint delivery dual routes: `/home/delivery/sprint` and `/home/sprint-trend`
- **Problem:** Both routes resolve to `SprintTrend`. One path presents the page as a Delivery child, while the other presents it as a legacy trend page. That duplicates the concept and blurs whether the page is current delivery inspection or legacy trend analysis.
- **Resolution options:**
  1. Keep `/home/delivery/sprint` as canonical and redirect `/home/sprint-trend`.
  2. Keep both temporarily but label one as legacy-only everywhere.
  3. Split trend analysis from sprint delivery inspection into two distinct routes if both concepts are still needed.

### Scoped and unscoped planning route pairs
- **Problem:** `ProductRoadmaps` and `PlanBoard` each expose a scoped and unscoped route for the same page concept. That duplicates addressability for identical UI and makes context ownership depend on URL shape rather than on a single canonical planning entry model.
- **Resolution options:**
  1. Standardize on one canonical route shape and redirect the other.
  2. Keep both shapes but declare one as an alias generated only for backward compatibility.
  3. Make the scoped and unscoped variants intentionally different experiences so the duplication is removed at the concept level.

## Summary

Primary structural gaps:
- framework-only orphan recovery route (`/not-found`)
- ownership split between Health, Trends, and Home-system concepts
- several routes classified as `DeepLinkOnly` despite behaving like normal workspace children
- undocumented dead-end or caller-dependent exits
- multiple duplicated canonical-vs-legacy route concepts

The largest consistency issue is that the current classification model is stricter than the actual navigation graph: several routes already participate in ordinary workspace flows but are still documented as deep-link-only or system-owned.
