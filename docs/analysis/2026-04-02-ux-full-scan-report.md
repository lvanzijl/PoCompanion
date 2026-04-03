# Full UX Scan Report

## 1. Summary
- Total pages analyzed: 42
- Overall UX rating: 2.8/5
- Scope note: planning routes that require a project alias in the URL (for example `/planning/{alias}/overview`) were not reachable in the loaded mock data and were excluded.
- Key strengths:
  - Workspace hub pages are usually clear, compact, and easy to route through.
  - Configuration/admin surfaces generally use readable list and form layouts.
  - Bug and validation pages expose concrete counts and actionable next steps.
- Key weaknesses:
  - Too many detail pages open in broken or contextless states.
  - Several high-value analytical pages leak raw deserialization/build-quality errors into the UI.
  - Filter-heavy pages often default to empty canvases with weak onboarding.
- Strongest pages:
  - Validation Triage (/home/validation-triage) — 4/5
  - Bug Insights (/home/bugs) — 4/5
  - Manage Teams (/settings/teams) — 4/5
- Weakest pages:
  - All 1/5 pages should be tracked as follow-up implementation bugs, not only as UX findings.
  - Legacy Analysis Workspace (/workspace/analysis) — 1/5
  - Health Overview (/home/health/overview) — 1/5
  - Product Roadmaps (/planning/product-roadmaps) — 1/5

## 2. Page-by-Page Analysis

#### Home / /home
- UX Rating: 3/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home.png`
- Purpose: Primary workspace dashboard for choosing product context and navigating into the four main workspaces.

**Improvements:**
- Replace the repeated “Signal unavailable” tile state with concrete recovery actions or fallback summaries.
- Surface context metrics above the workspace grid once sync completes so the dashboard feels informative on first view.
- Make the active scope and sync status visually stronger than the surrounding helper text.

---

#### Sync Gate / /sync-gate
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/sync-gate.png`
- Purpose: Blocking pre-workspace loading screen that prepares cached data before the user enters the app.

**Improvements:**
- Show real progress, elapsed time, and next-step information instead of a generic loading message.
- Add a safe back or skip path so users are not trapped behind a full-page gate.
- Explain which data is being prepared and why the gate exists.

---

#### Onboarding / /onboarding
- UX Rating: 3/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/onboarding.png`
- Purpose: First-run wizard for Azure DevOps connection setup and importing existing configuration.

**Improvements:**
- Split connection setup and import into clearer steps so the page is less text-heavy.
- Replace the project lookup fallback with actionable retry/help guidance.
- Reduce supporting copy so the primary form and next action dominate the screen.

---

#### Profiles Home / /profiles
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/profiles.png`
- Purpose: Profile selection landing page for choosing or adding a Product Owner.

**Improvements:**
- Add profile metadata such as product count and last sync to help users choose faster.
- Clarify the action hierarchy between profile tiles and secondary management links.
- Highlight the active profile more strongly on the selected tile.

---

#### Settings / /settings
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/settings.png`
- Purpose: Configuration hub for cache management, TFS settings, import/export, triage tags, and setup help.

**Improvements:**
- Demote the live sync panel so the section navigation remains the primary visual focus.
- Group settings into clearer cards with short purpose statements and next actions.
- Add sticky in-page navigation or section summaries to reduce scanning cost.

---

#### Manage Teams / /settings/teams
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/settings-teams.png`
- Purpose: Administration page for browsing and maintaining configured teams.

**Improvements:**
- Add search and sorting for longer team lists.
- Show linked product counts and sync freshness directly on each team row.
- Expose row-level actions more clearly without relying on extra navigation.

---

#### Manage Products / /settings/products
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/settings-products.png`
- Purpose: Administration page for managing products across Product Owners.

**Improvements:**
- Show backlog root titles instead of raw work item IDs.
- Add search/sort by owner and linked team count.
- Promote edit/team/repository actions inside each product card.

---

#### Work Item State Classification / /settings/workitem-states
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/settings-workitem-states.png`
- Purpose: Configuration page for mapping raw work item states to canonical lifecycle states.

**Improvements:**
- Use stronger grouping or sticky headers to improve long-table scanning.
- Explain default vs custom mappings inline instead of relying on dense intro copy.
- Highlight incomplete or risky mappings before the save action.

---

#### Manage Product Owner / /settings/productowner/1
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/settings-manage-product-owner.png`
- Purpose: Profile detail page that summarizes one Product Owner and their products.

**Improvements:**
- Show linked repositories, teams, and sync freshness on each product card.
- Differentiate edit-profile and add-product actions more clearly.
- Add quick jumps from products into planning or analytics pages.

---

#### Edit Product Owner / /settings/productowner/edit/1
- UX Rating: 3/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/settings-edit-product-owner.png`
- Purpose: Form for editing Product Owner metadata, goals, and avatar.

**Improvements:**
- Replace the opaque goal picker prompt with selected goal chips and a visible count.
- Collapse the image gallery behind a clearer avatar picker to reduce noise.
- Surface validation/help closer to required fields and the submit action.

---

#### Work Item Explorer / /workitems
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/workitems.png`
- Purpose: Tree-based explorer for browsing synced work items and validation context.

**Improvements:**
- Explain whether the empty result is caused by filters, missing cache data, or sync scope.
- Promote the recovery CTA above the empty table so users know what to do next.
- Separate filter controls from the results area more clearly.

---

#### Bugs Triage / /bugs-triage
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/bugs-triage.png`
- Purpose: Operational table for triaging bugs by state and status.

**Improvements:**
- Keep key columns sticky so long titles remain readable while scanning.
- Add severity and age badges to support prioritization.
- Make bulk triage actions more visible near the table header.

---

#### Classic Landing / /legacy
- UX Rating: 3/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/legacy.png`
- Purpose: Legacy intent-based landing page with classic navigation and cache status.

**Improvements:**
- Reduce the competing cache/status panel so the intent cards dominate.
- Clarify which navigation model is current versus legacy.
- Remove or demote duplicated paths now covered by the newer workspace hubs.

---

#### Legacy Product Workspace / /workspace/product
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/workspace-product.png`
- Purpose: Legacy product workspace hub for jumping to analysis, planning, and team views.

**Improvements:**
- Replace jargon-heavy quick links with clearer outcome-oriented labels.
- Surface current product metrics near the header so the page feels grounded in data.
- Reduce overlap with the newer Home workspace hubs.

---

#### Legacy Team Workspace / /workspace/team
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/workspace-team.png`
- Purpose: Legacy team-focused workspace for sprint navigation and team actions.

**Improvements:**
- Strengthen sprint context by showing selected sprint details near the header.
- Reduce the number of competing action groups on first view.
- Use clearer hierarchy between current-sprint actions and secondary historical views.

---

#### Legacy Analysis Workspace / /workspace/analysis
- UX Rating: 1/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/workspace-analysis.png`
- Purpose: Legacy analysis workspace that currently fails on load.

**Improvements:**
- Fix the fatal 404 state; the page is currently unusable.
- Replace raw technical error text with a guided recovery panel.
- Auto-route users back to a valid analysis destination when context is missing.

---

#### Legacy Communication Workspace / /workspace/communication
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/workspace-communication.png`
- Purpose: Legacy workspace for generating status-report style communication output.

**Improvements:**
- Make the generated preview the primary focal area and controls secondary.
- Compress the report-template controls into tabs or a smaller stepper.
- Move share/export actions closer to the preview.

---

#### Health Hub / /home/health
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-health.png`
- Purpose: Hub page for choosing between overview, validation, and backlog health views.

**Improvements:**
- Show live counts or quality signals on each destination card.
- Differentiate the three destinations more strongly by scope and outcome.
- Surface refresh state near the card grid, not only in global chrome.

---

#### Health Overview / /home/health/overview
- UX Rating: 1/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-health-overview.png`
- Purpose: Build Quality overview for the active Product Owner.

**Improvements:**
- Fix the broken data contract so the primary overview can render.
- Replace raw deserialization text with a controlled error/empty state and retry action.
- Preserve the page scaffold even when data fails so navigation still feels stable.

---

#### Backlog Health / /home/health/backlog-health
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-backlog-health.png`
- Purpose: Product-level backlog quality page.

**Improvements:**
- Auto-select the active product or show a visible selector on the page itself.
- Add a summary scaffold so the page does not feel like a dead end on first load.
- Explain the difference between product scope and all-products mode.

---

#### Validation Triage / /home/validation-triage
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-validation-triage.png`
- Purpose: Grouped validation dashboard that prioritizes structural, readiness, and effort issues.

**Improvements:**
- Use more consistent visual scales so category cards are easier to compare.
- Show affected product/team context before users open each queue.
- Make “Open queue” the single unmistakable primary action on each card.

---

#### Validation Queue / /home/validation-queue
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-validation-queue.png`
- Purpose: Queue page for reviewing validation items in a selected category.

**Improvements:**
- Carry the selected category in the URL or auto-default from the triage page.
- Show a preview list or picker instead of a hard stop when context is missing.
- Use a stronger recovery pattern than a single fallback button.

---

#### Fix Session / /home/validation-fix
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-validation-fix.png`
- Purpose: Guided fix flow for one validation item.

**Improvements:**
- Preserve queue context before navigation so the page rarely opens blank.
- Show selected issue summary and progress when no item is loaded yet.
- Offer a direct return path to the last queue state instead of generic fallback copy.

---

#### Dependency Overview / /home/dependencies
- UX Rating: 3/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-dependencies.png`
- Purpose: Read-only dependency insight page for product/area-path filtering.

**Improvements:**
- Reduce filter chrome so the dependency view remains the focal point.
- Clarify the relationship between this page and the full dependency graph.
- Provide a stronger empty-state explanation when no dependencies match the filters.

---

#### Delivery Hub / /home/delivery
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-delivery.png`
- Purpose: Hub page for choosing sprint, portfolio, and execution delivery views.

**Improvements:**
- Add live counts or date ranges on each destination card.
- Emphasize the recommended next step for first-time users.
- Show current team/product context next to the card group.

---

#### Sprint Execution / /home/delivery/execution
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-delivery-execution.png`
- Purpose: Internal sprint diagnostics page for scope changes and work starvation.

**Improvements:**
- Auto-select the most relevant team/sprint when only one valid option exists.
- Replace “No sprints found” with configuration-aware recovery guidance.
- Use the empty space for diagnostic definitions or recent examples.

---

#### Portfolio Delivery / /home/delivery/portfolio
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-delivery-portfolio.png`
- Purpose: Cross-team delivery comparison page.

**Improvements:**
- Make the required team/sprint setup row more explicit on first load.
- Show a scaffold or sample layout before data is selected.
- Explain what will appear once a team is chosen.

---

#### Sprint Delivery / /home/delivery/sprint
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-delivery-sprint.png`
- Purpose: Stakeholder sprint delivery report for the latest sprint.

**Improvements:**
- Fix the broken Build Quality panel so the primary summary can render.
- Reduce the number of competing lower-page diagnostics.
- Make sprint navigation and summary outcomes more prominent than secondary sections.

---

#### What’s New Since Last Sync / /home/changes
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-changes.png`
- Purpose: Change summary page comparing the latest sync window to the previous one.

**Improvements:**
- Keep zero-state panels shorter so the summary metrics stay above the fold.
- Add drill-down links from each metric card for faster follow-up.
- Clarify whether zero means “no change” or “data not available yet.”

---

#### Bug Insights / /home/bugs
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-bugs.png`
- Purpose: Bug analytics dashboard with triage and severity metrics.

**Improvements:**
- Strengthen the next action path from insight cards into triage/detail views.
- Group related metrics so percentile context is easier to scan.
- Add clearer feedback when team/product filters change the scope.

---

#### Bug Detail / /home/bugs/detail
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-bugs-detail.png`
- Purpose: Bug drilldown page for a single bug.

**Improvements:**
- Preserve selected bug context in navigation so the page rarely opens blank.
- Show a recent-bugs list or fallback picker instead of a hard stop.
- Reuse the Bug Insights filter bar for continuity.

---

#### Pull Request Insights / /home/pull-requests
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-pull-requests.png`
- Purpose: PR analytics page with scatter plot and summary metrics.

**Improvements:**
- Auto-select a meaningful repository/team default after sync.
- Replace empty chart space with onboarding guidance or annotated placeholders.
- Explain filter dependencies more clearly when no PR data exists.

---

#### PR Delivery Insights / /home/pr-delivery-insights
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-pr-delivery-insights.png`
- Purpose: PR classification dashboard for delivery, bug, disturbance, and unmapped PRs.

**Improvements:**
- Explain which repositories/products feed this view before the empty summary cards.
- Use stronger empty-state guidance instead of blank analytical surfaces.
- Connect each classification bucket to drill-down actions.

---

#### Pipeline Insights / /home/pipeline-insights
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-pipeline-insights.png`
- Purpose: Pipeline health analytics page with sprint and SLO filters.

**Improvements:**
- Preselect the latest sprint for the chosen team when possible.
- Move advanced toggles behind progressive disclosure to reduce filter density.
- Fill the empty state with data-source and recovery guidance.

---

#### Trends Hub / /home/trends
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-trends.png`
- Purpose: Hub page for past-oriented analytical trend pages.

**Improvements:**
- Differentiate pages with data from read-only or empty states more clearly.
- Add preview metrics on each card so the hub feels evidence-based.
- Reduce repeated navigation language around the hub cards.

---

#### Delivery Trends / /home/trends/delivery
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-trends-delivery.png`
- Purpose: Delivery throughput trend page across recent sprints.

**Improvements:**
- Handle load failures with a structured error panel instead of inline raw text.
- Make sprint-range selection the obvious first action.
- Preserve a useful chart scaffold so the page still explains itself when empty.

---

#### Portfolio Flow Trend / /home/portfolio-progress
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-portfolio-progress.png`
- Purpose: Portfolio-level trend page for progress over a sprint range.

**Improvements:**
- Collapse advanced controls until a sprint range is chosen.
- Add a stronger primary visualization placeholder so the page is not control-heavy.
- Explain when CDC history matters and why users would enable it.

---

#### Planning Hub / /home/planning
- UX Rating: 4/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/home-planning.png`
- Purpose: Hub page for roadmap and sprint-planning destinations.

**Improvements:**
- Add live status badges to roadmap and plan-board cards.
- Promote a default next action for first-run users.
- Reduce repeated breadcrumb copy so the option cards dominate.

---

#### Plan Board / /planning/plan-board
- UX Rating: 3/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/planning-plan-board.png`
- Purpose: Operational planning board for assigning PBIs and bugs to upcoming sprints.

**Improvements:**
- Auto-select the first configured product when only a small set is available.
- Show a lightweight board scaffold before selection to set expectations.
- Clarify the difference between refreshing from TFS and planning from cached data.

---

#### Multi-Product Planning / /planning/multi-product
- UX Rating: 2/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/planning-multi-product.png`
- Purpose: Cross-product planning timeline aligned to a shared time axis.

**Improvements:**
- Replace the empty center canvas with a stronger onboarding illustration and defaults.
- Explain planning pressure and capacity collisions near the toggles that control them.
- Auto-load configured products or a saved selection instead of starting at zero.

---

#### Product Roadmaps / /planning/product-roadmaps
- UX Rating: 1/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/planning-product-roadmaps.png`
- Purpose: Read-only product roadmap comparison page.

**Improvements:**
- Fix the failing epic-load contract; the landing page currently errors for every product.
- Replace raw error text with per-product recovery guidance and retry actions.
- Keep unaffected product lanes visible even if one product fails.

---

#### Product Roadmap Editor / /planning/product-roadmaps/1
- UX Rating: 1/5
- Screenshot: `docs/analysis/assets/2026-04-02-ux-full-scan/planning-product-roadmap-editor.png`
- Purpose: Per-product roadmap editing workspace.

**Improvements:**
- Fix the failing roadmap data load so editing can start from valid data.
- Separate error messaging from the editor chrome so the main task remains clear.
- Add a concise summary of selected product, scope, and last saved state above the canvas.

---

## 3. Cross-Page Patterns

- Repeated UX issues:
  - Context is often not preserved into deep links, so queue/detail pages load blank and tell the user to go back.
  - Backend/data-contract failures surface as raw technical text instead of resilient data-state panels.
  - Analytical pages frequently lead with filters and empty chart space rather than a clear default story.
  - Many pages depend on manual team/product/sprint selection even when the app already has an active profile and recent sync.
- Inconsistencies between pages:
  - Hub pages are polished and instructive, while their detail pages often collapse into sparse or broken states.
  - Legacy workspaces and newer Home hubs both exist, but they use different labeling and navigation models.
  - Some admin pages show strong compact lists, while several analytics pages use oversized empty areas with little guidance.
- Navigation problems:
  - Blank-state pages rely on “go back home” patterns instead of keeping enough context to continue.
  - Several routes require hidden selection state that is not visible or restorable from the URL.
  - Global top navigation stays consistent, but local forward/backward movement is uneven across drilldown pages.

## 4. Priority Fixes
- Replace raw backend/deserialization failures with stable data-state panels on Health Overview, Delivery Trends, Product Roadmaps, and the roadmap editor.
- Persist category, bug, product, team, and sprint context into deep-link URLs and auto-default the most likely choice when only one valid option exists.
- Reduce filter-first empty pages by preloading useful defaults and showing meaningful chart/table scaffolds before manual selection.
