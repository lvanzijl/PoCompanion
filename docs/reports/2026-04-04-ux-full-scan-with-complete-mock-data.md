# Coverage Audit Summary

## Pages scanned
- `/` — Index
- `/profiles` — ProfilesHome
- `/onboarding` — Onboarding
- `/sync-gate` — SyncGate
- `/startup-blocked` — StartupBlocked
- `/home` — HomePage
- `/home/planning` — PlanningWorkspace
- `/home/delivery` — DeliveryWorkspace
- `/home/health` — HealthWorkspace
- `/home/trends` — TrendsWorkspace
- `/home/health/overview` — HealthOverviewPage
- `/home/health/backlog-health` and `/home/backlog-overview` — BacklogOverviewPage + legacy redirect
- `/home/bugs` — BugOverview
- `/home/pipeline-insights` — PipelineInsights
- `/home/delivery/execution` — SprintExecution
- `/home/delivery/sprint` and `/home/sprint-trend` — SprintTrend + legacy redirect
- `/home/delivery/sprint/activity/{workItemId}` and `/home/sprint-trend/activity/{workItemId}` — SprintTrendActivity + legacy redirect
- `/home/trends/delivery` — DeliveryTrends
- `/home/delivery/portfolio` — PortfolioDelivery
- `/home/pull-requests` — PrOverview
- `/home/pr-delivery-insights` — PrDeliveryInsights
- `/home/portfolio-progress` — PortfolioProgressPage
- `/home/changes` — HomeChanges
- `/home/validation-queue` — ValidationQueuePage
- `/home/validation-triage` — ValidationTriagePage
- `/home/validation-fix` — ValidationFixPage
- `/bugs-triage` — BugsTriage
- `/planning/product-roadmaps` and `/planning/{projectAlias}/product-roadmaps` — ProductRoadmaps
- `/planning/product-roadmaps/{productId}` — ProductRoadmapEditor
- `/planning/plan-board` and `/planning/{projectAlias}/plan-board` — PlanBoard
- `/planning/multi-product` — MultiProductPlanning
- `/planning/{projectAlias}/overview` — ProjectPlanningOverview
- `/settings` and `/settings/{selectedTopic}` — SettingsPage topics (`cache`, `cache-management`, `tfs`, `import-export`, `workitem-states`, `triage-tags`, `getting-started`)
- `/settings/teams` — ManageTeams
- `/settings/productowner/{profileId}` — ManageProductOwner
- `/settings/productowner/edit/{profileId?}` — EditProductOwner
- `/settings/workitem-states` — WorkItemStates
- `/not-found` — NotFound

## Missing data per page (before fix)
- **Critical**
  - `HomePage`, `PlanningWorkspace`, and other guarded routes: mock startup seeded a saved TFS config, but `StartupReadinessDto` still reported `HasTestedConnectionSuccessfully=false` and `HasVerifiedTfsApiSuccessfully=false`, so mock mode looked incomplete until manual validation was triggered.
  - `HealthOverviewPage`: `/api/buildquality/rolling` returned HTTP 200, but the client treated the cache-backed build-quality payload as unavailable and rendered a hard data-unavailable panel.
  - `ProjectPlanningOverview` direct route: `/planning/battleship-systems/overview` was corrected away from the page before the route could render.
- **Degraded**
  - `ProductRoadmaps`: battleship epics lacked canonical `roadmap` tags, so the roadmap overview rendered visible products with `0 roadmap epics`.
  - `BugsTriage`: mock mode seeded zero enabled triage tags, so the tag filter bar was empty and the page could not demonstrate tag-assisted triage.
  - `HealthOverviewPage`, `ProductRoadmaps`, `BugsTriage`: mock work-item tags were comma-separated instead of TFS-style semicolon-separated, so tag-driven semantics were inconsistent with the rest of the application.
  - `PlanBoard`, `PipelineInsights`, `SprintExecution`, `SprintTrend`, `DeliveryTrends`, `PortfolioDelivery`, `PrOverview`, and `PrDeliveryInsights`: pages rendered, but default filter state still lands on an incomplete or empty first view until team/sprint/product context is chosen.
- **Enhancement**
  - `ProductRoadmapEditor`: available epics existed, but seeded roadmap order remained empty, so the editor demonstrated setup rather than a fully populated roadmap lane.
  - `SettingsPage` triage-tag topic: the page structure rendered, but mock mode did not previously exercise an enabled tag catalog.

## Classification
- **Critical:** startup readiness flags, build-quality envelope handling, project-scoped planning route correction.
- **Degraded:** roadmap-tag coverage, triage-tag catalog coverage, TFS tag formatting, filter-default clarity on action pages.
- **Enhancement:** richer saved roadmap order and more preselected context on task-oriented pages.

# Mock Data Additions

## Exact fields/entities added
- Set mock `TfsConfigEntity.HasTestedConnectionSuccessfully` and `TfsConfigEntity.HasVerifiedTfsApiSuccessfully` to `true` during deterministic mock seeding.
- Seeded six enabled `TriageTagEntity` records: `Needs Investigation`, `Regression`, `Customer Reported`, `Operational Risk`, `Hotfix Candidate`, and `Needs Repro`.
- Converted generated mock `WorkItemDto.Tags` values from comma-delimited strings to TFS-style semicolon-delimited strings.
- Added deterministic `roadmap`, `priority-high`, `priority-medium`, and `priority-low` tags to seeded epic records.
- Added deterministic triage tags plus `Needs Investigation`/`Regression` edge cases to seeded bug records.
- Switched build-quality rolling and sprint reads to shared data-state HTTP parsing so the UI consumes the actual mock payload instead of falling back to a false unavailable state.

## New relationships introduced
- Roadmap views now recognize a stable subset of seeded epic items as roadmap epics through canonical tags rather than empty fallback lanes.
- Bug-triage filters now have a populated tag catalog that matches the tags emitted on seeded mock bugs.
- Mock startup readiness now aligns the saved TFS configuration entity with the intended mock-mode contract, so the seeded profile/configuration relationship is immediately usable.

## Edge cases included
- Roadmap epics still include mixed priority tags to preserve realistic imbalance and sequencing discussion.
- Bug tags include overlaps such as `Needs Investigation` + `Regression` so bug-triage filters can exercise combined-tag cases.
- Existing removed items, missing effort values, partially completed work, and multi-sprint history remain in the battleship hierarchy.

# Validation Results

- **All pages render:** No
- **What now renders cleanly after the fixes:** guarded startup routes enter sync without manual mock validation, `HealthOverviewPage` renders real build-quality content, `ProductRoadmaps` shows roadmap epics, and `BugsTriage` shows the enabled triage-tag catalog.
- **Console results on validated routes:** no app-level console errors were observed after the fixes; remaining console noise came from the blocked Google Fonts request in the sandbox and the preload warning already present in the client shell.

## Remaining issues (if any)
- `/planning/{projectAlias}/overview` still redirects to `/home/planning` because the global filter correction flow removes the project-scoped route state before the page can render.
- Project-scoped planning summary data still depends on route/filter behavior and was not fully recoverable in this pass.
- Several action pages still open in an intentionally incomplete filter state; they render, but their first impression remains low-clarity until a team/sprint/product is selected.

# UX Ratings

- **Index (`/`)** — **6/10**
  - Explain the upcoming sync/setup decision before redirecting away.
  - Show the active profile or target workspace in the loading state.
  - Replace the blank spinner-first view with a short startup checklist.

- **ProfilesHome (`/profiles`)** — **7/10**
  - Emphasize the current active profile more strongly.
  - Surface product ownership counts inline on each profile card.
  - Add one clearer next-step action for first-time mock users.

- **Onboarding (`/onboarding`)** — **7/10**
  - Compress the initial connection form to reduce modal height.
  - Show mock-mode guidance so users know which steps are already satisfied.
  - Make skip-versus-save consequences clearer before the final action row.

- **SyncGate (`/sync-gate`)** — **7/10**
  - Show stage progress history instead of only the current stage label.
  - Surface estimated remaining work once enough progress data exists.
  - Offer a clearer explanation of what data becomes available after sync.

- **StartupBlocked (`/startup-blocked`)** — **7/10**
  - Differentiate backend-unavailable versus setup-missing states more visually.
  - Add one contextual troubleshooting summary for mock mode.
  - Reduce the visual weight of the generic blocking chrome.

- **HomePage (`/home`)** — **8/10**
  - Replace the empty `Project` chip label with a readable default.
  - Persist the quick product selection across page changes.
  - Surface why each workspace card is recommended right now.

- **PlanningWorkspace (`/home/planning`)** — **7/10**
  - Show the currently active project context more clearly.
  - Add one compact summary card above the navigation tiles.
  - Reduce the amount of secondary navigation competing with the primary choices.

- **DeliveryWorkspace (`/home/delivery`)** — **7/10**
  - Add one visible summary of selected team/sprint context before the tiles.
  - Clarify which downstream page is best for status versus diagnosis.
  - Make the default empty filter state less ambiguous.

- **HealthWorkspace (`/home/health`)** — **7/10**
  - Surface the top health issue directly in the hero area.
  - Differentiate operational health versus backlog health more clearly.
  - Tighten the tile subtitles so they scan faster.

- **TrendsWorkspace (`/home/trends`)** — **7/10**
  - Show the current trend window immediately in the header.
  - Explain the difference between trend pages in one short sentence each.
  - Reduce filter friction for the first meaningful view.

- **HealthOverviewPage (`/home/health/overview`)** — **8/10**
  - Collapse the product cards slightly so both products fit above the fold.
  - Explain `Confidence` more directly without relying on implied knowledge.
  - Highlight the weakest product first instead of alphabetical order.

- **BacklogOverviewPage (`/home/health/backlog-health`)** — **7/10**
  - Make the default product/team scope more obvious before the charts.
  - Prioritize one lead chart over the surrounding summaries.
  - Clarify what action the user should take from the top signal.

- **BugOverview (`/home/bugs`)** — **6/10**
  - Increase default context so the page is meaningful without extra filtering.
  - Promote the most urgent bug signal ahead of the supporting metrics.
  - Tighten navigation back to bug triage.

- **PipelineInsights (`/home/pipeline-insights`)** — **6/10**
  - Preselect a meaningful team/sprint in mock mode.
  - Replace the incomplete-filter panel with richer guided defaults.
  - Reduce the amount of chrome shown before actual insight content.

- **SprintExecution (`/home/delivery/execution`)** — **7/10**
  - Land on a ready-to-read team/sprint in mock mode.
  - Increase emphasis on the single biggest execution risk.
  - Simplify the filter explanation copy above the content.

- **SprintTrend (`/home/delivery/sprint`)** — **7/10**
  - Default to the most relevant sprint context automatically.
  - Tighten the hierarchy between the lead chart and its supporting panels.
  - Make advanced calibration content more obviously secondary.

- **SprintTrendActivity (`/home/delivery/sprint/activity/{workItemId}`)** — **6/10**
  - Preserve the parent sprint context more visibly in the header.
  - Add a clearer empty/not-found state for bad work-item links.
  - Surface the most recent change before the full activity list.

- **DeliveryTrends (`/home/trends/delivery`)** — **7/10**
  - Preload a standard sprint range in mock mode.
  - Reduce the amount of filter setup required before the first chart.
  - Clarify which metric is the primary story on initial load.

- **PortfolioDelivery (`/home/delivery/portfolio`)** — **6/10**
  - Preload a default team and sprint range so the page is not filter-gated.
  - Show one dominant portfolio signal before secondary details.
  - Simplify the route back to product-level diagnosis.

- **PrOverview (`/home/pull-requests`)** — **6/10**
  - Preload a meaningful rolling window and team context.
  - Surface the most actionable PR bottleneck ahead of raw counts.
  - Make the filter summary easier to scan at a glance.

- **PrDeliveryInsights (`/home/pr-delivery-insights`)** — **6/10**
  - Provide a mock-mode default team/sprint combination.
  - Highlight one lead delivery insight before the rest of the charts.
  - Reduce the first-load reliance on filter setup.

- **PortfolioProgressPage (`/home/portfolio-progress`)** — **8/10**
  - Make the primary visualization even more dominant over supporting controls.
  - Shorten supporting copy around CDC interpretation.
  - Offer a faster jump from summary signal to detailed comparison.

- **HomeChanges (`/home/changes`)** — **7/10**
  - Group changes by importance before chronology.
  - Promote the highest-risk change cluster above the rest.
  - Improve the distinction between pull-request, pipeline, and work-item changes.

- **ValidationQueuePage (`/home/validation-queue`)** — **7/10**
  - Bring the selected validation category into the hero/header area.
  - Emphasize the next recommended fix action more strongly.
  - Reduce repeated metadata around each queue item.

- **ValidationTriagePage (`/home/validation-triage`)** — **8/10**
  - Put the highest-volume validation category first visually.
  - Add one-line action guidance per validation family.
  - Tighten the amount of explanatory copy above the actionable content.

- **ValidationFixPage (`/home/validation-fix`)** — **7/10**
  - Make progress through the fix session more visible.
  - Increase contrast between current item details and navigation controls.
  - Improve the recovery state when no active fix item is selected.

- **BugsTriage (`/bugs-triage`)** — **8/10**
  - Distinguish already-tagged versus untagged bugs in the tree more clearly.
  - Surface selected tag filters closer to the bug detail panel.
  - Highlight likely hotfix candidates ahead of the generic bug list.

- **ProductRoadmaps (`/planning/product-roadmaps`)** — **8/10**
  - Resolve sprint cadence so forecast bars can render more confidently.
  - Elevate the highest-risk roadmap lane above the rest.
  - Reduce repeated per-epic metadata so more roadmap fits above the fold.

- **ProductRoadmapEditor (`/planning/product-roadmaps/{productId}`)** — **8/10**
  - Seed a small saved roadmap lane so the editor demonstrates both columns immediately.
  - Show why an epic is a roadmap candidate before the user opens the drawer.
  - Surface save-state feedback closer to the interaction point.

- **PlanBoard (`/planning/plan-board`)** — **6/10**
  - Preselect a product in mock mode so the board is populated on first load.
  - Replace the current empty-state card with a more actionable starter view.
  - Clarify what changes when the time mode stays on snapshot.

- **MultiProductPlanning (`/planning/multi-product`)** — **7/10**
  - Lead with one dominant cross-product timeline view.
  - Trim setup friction for the first meaningful comparison.
  - Emphasize dependencies or collisions more clearly.

- **ProjectPlanningOverview (`/planning/{projectAlias}/overview`)** — **3/10**
  - Stop redirecting away from valid project-scoped routes.
  - Preserve the route project as authoritative context in global filters.
  - Ensure project-wide summary data stays available without manual correction.

- **SettingsPage (`/settings`, `/settings/{selectedTopic}`)** — **7/10**
  - Show the active topic summary in the page header.
  - Reduce the visual competition between the sidebar and content panel.
  - Surface the most important mock-mode settings first.

- **ManageTeams (`/settings/teams`)** — **7/10**
  - Promote one primary team-management action above the table.
  - Clarify how team edits affect filter-driven pages.
  - Improve empty-state messaging for archived or missing teams.

- **ManageProductOwner (`/settings/productowner/{profileId}`)** — **7/10**
  - Show product ownership and goal counts earlier in the page.
  - Make navigation back to profiles/settings more obvious.
  - Tighten spacing around the editable sections.

- **EditProductOwner (`/settings/productowner/edit/{profileId?}`)** — **7/10**
  - Clarify whether the user is creating or editing at the top of the form.
  - Show validation guidance before the action row.
  - Promote the save path over secondary navigation.

- **WorkItemStates (`/settings/workitem-states`)** — **7/10**
  - Make canonical-state impact clearer near each mapping section.
  - Group related work-item types more compactly.
  - Highlight unsaved or risky state mappings more strongly.

- **NotFound (`/not-found`)** — **6/10**
  - Add clearer recovery paths back to the main workspaces.
  - Explain whether the route is obsolete, blocked, or misspelled.
  - Make the primary recovery action visually dominant.

- **Legacy redirect routes** — **6/10**
  - Explain that the route was normalized to a current destination.
  - Preserve more context when redirecting into the modern page.
  - Log a softer user-facing note instead of silently rerouting.

# Key Systemic UX Issues

- Filter-driven pages still rely too heavily on manual team/sprint/product selection before showing a meaningful first view.
- Global filter presentation still exposes raw or empty project labels such as `()` instead of readable defaults.
- Project-scoped planning routes are still fragile because shared filter correction can override the route itself.
- Several analytical pages still compete between header chrome, filter chrome, and the actual lead visualization.
- Mock mode now covers roadmap and bug-triage content better, but saved roadmap order and cadence-derived forecast confidence are still thinner than the rest of the battleship story.

# Highest Impact Improvements

1. Make route-scoped project context authoritative so project planning pages stop redirecting away.
2. Add mock-mode default filter presets for team/sprint/product on action-heavy pages so first load is meaningful.
3. Seed a small saved roadmap order and resolved sprint cadence for each visible product to strengthen planning pages simultaneously.
4. Replace raw/empty filter labels with human-readable defaults across all workspaces.
5. Promote one primary signal per analytical page and demote supporting controls/chrome below it.
