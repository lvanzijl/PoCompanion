# UX Full Scan v2

## Overview
- **Data source:** Battleship mock data only (`UseMockClient=true` in Development)
- **Profile used:** Commander Elena Marquez (`productOwnerId=1`)
- **Project route used:** `battleship-systems`
- **Primary valid contexts used:**
  - single product: `productId=1` (`Incident Response Control`)
  - alternate product: `productId=2` (`Crew Safety Operations`) when needed for comparison-only reasoning
  - team: `teamId=4`
  - sprint: `sprintId=2` (`Sprint 11`)
  - range: `fromSprintId=1`, `toSprintId=3` (`Sprint 10` → `Sprint 12`)
  - rolling: `rollingWindow=30`, `rollingUnit=Days`
- **Total pages scanned:** 29
- **Successful loads:** 28
- **Failed loads:** 1
- **Average UX score:** 7.0 / 10

Notes:
- Validation was executed against the current local app run on `http://localhost:5292` with the mock API on `http://localhost:5291`.
- No page in the successful set triggered the **invalid-context** state when opened with a valid Battleship context.
- Remaining browser noise was limited to the blocked Google Fonts request and the existing preload warning; these were not counted as page failures.

## Page Results

### 1. Home (`/home`)
- **Context used:** defaults active, all-products home dashboard
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Replace raw filter-state labels with readable names instead of numeric IDs when filters become active.
  - Explain why each workspace tile is recommended right now, not just what it links to.
  - Make the quick product selector feel more persistent across workspace navigation.

### 2. Health workspace (`/home/health`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Surface one dominant health signal above the navigation tiles.
  - Shorten tile subtitles so scanning the hub is faster.
  - Reduce the visual distance between the purpose statement and the available actions.

### 3. Health overview (`/home/health/overview`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Call out the weakest product first instead of leaving comparison interpretation entirely to the user.
  - Explain `Confidence` inline instead of relying on domain knowledge.
  - Compress the product cards slightly so the full comparison fits higher on the page.

### 4. Backlog Health (`/home/health/backlog-health?productId=1`)
- **Context used:** single product `Incident Response Control`
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Make the current product scope more prominent before the readiness content starts.
  - Emphasize one lead readiness signal before the rest of the page detail.
  - Translate the page from “status display” to clearer next actions for the PO.

### 5. Bug overview (`/home/bugs?productId=1`)
- **Context used:** single product `Incident Response Control`
- **Load status:** success
- **UX score:** 6/10
- **3 improvements:**
  - Promote the most urgent bug signal before secondary summary cards.
  - Make the connection to Bugs Triage more explicit and more immediate.
  - Reduce low-value summary chrome when the page is already product-scoped.

### 6. Validation triage (`/home/validation-triage`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Put the highest-volume category first visually every time.
  - Add one-line action guidance under each category card.
  - Reduce explanatory copy above the actionable cards.

### 7. Validation queue (`/home/validation-queue?category=SI`)
- **Context used:** category `SI`
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Bring the selected category into the hero area more strongly.
  - Show likely remediation effort beside each rule group.
  - Differentiate queue size from business priority more clearly.

### 8. Validation fix (`/home/validation-fix?category=SI&ruleId=SI-3`)
- **Context used:** category `SI`, rule `SI-3`
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Increase the visual priority of the current item over the surrounding chrome.
  - Show stronger progress cues across the full session, not just item count.
  - Reduce the density of long rule-explanation text without losing traceability.

### 9. Delivery workspace (`/home/delivery`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Show one clear “best next page” recommendation instead of equally weighted tiles.
  - Surface current team/sprint expectations before route selection.
  - Make unavailable or weak workspace signals more informative than “signal unavailable.”

### 10. Sprint Execution (`/home/delivery/execution?productId=1&teamId=4&sprintId=2&timeMode=Sprint`)
- **Context used:** product 1, team 4, Sprint 11
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Explain the **Requested ≠ applied** state in plain language instead of system language.
  - Highlight the single most urgent execution risk above the long table.
  - Tighten the visual hierarchy between summary metrics and the detailed work-item list.

### 11. Sprint Delivery (`/home/delivery/sprint?productId=1&teamId=4&sprintId=2&timeMode=Sprint`)
- **Context used:** product 1, team 4, Sprint 11
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Strengthen the primary chart’s dominance over support panels.
  - Clarify how build quality contributes to the sprint story.
  - Reduce filter/setup explanation once a valid sprint is already applied.

### 12. Portfolio Delivery (`/home/delivery/portfolio?productId=1&teamId=4&fromSprintId=1&toSprintId=3&timeMode=Range`)
- **Context used:** team 4, Sprint 10–12 range, requested product 1
- **Load status:** success
- **UX score:** 6/10
- **3 improvements:**
  - Explain more clearly that product scope is not actually applied here, because the page silently broadens to products 1 and 2.
  - Promote one dominant portfolio conclusion ahead of the summary grid.
  - Avoid showing a long loading phase before enough visible content appears.

### 13. Pull Requests (`/home/pull-requests?teamId=4&timeMode=Rolling&rollingWindow=30&rollingUnit=Days`)
- **Context used:** team 4, rolling 30-day window
- **Load status:** success
- **UX score:** 6/10
- **3 improvements:**
  - Lead with the biggest actionable PR bottleneck instead of an even-weight dashboard.
  - Make the current rolling window more visually obvious.
  - Reduce the amount of filter chrome relative to insight content.

### 14. PR Delivery Insights (`/home/pr-delivery-insights?productId=1&teamId=4&sprintId=2&timeMode=Sprint`)
- **Context used:** product 1, team 4, Sprint 11
- **Load status:** success
- **UX score:** 6/10
- **3 improvements:**
  - Promote one key insight before the surrounding supporting detail.
  - Clarify the relationship between PR flow and sprint delivery outcomes.
  - Reduce first-view reliance on knowing the metric vocabulary already.

### 15. Trends workspace (`/home/trends`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Show the currently relevant trend window immediately in the header.
  - Clarify the difference between trend pages in one short sentence per tile.
  - Reduce the sense that the user must know the model before picking a route.

### 16. Delivery Trends (`/home/trends/delivery?productId=1&teamId=4&fromSprintId=1&toSprintId=3&timeMode=Range`)
- **Context used:** product 1, team 4, Sprint 10–12 range
- **Load status:** success
- **UX score:** 6/10
- **3 improvements:**
  - Call out that the selected range currently shows mostly zero delivery, because the user otherwise has to infer it from several charts.
  - Emphasize the most decision-relevant trend rather than treating all four panels equally.
  - Reduce setup friction by offering one named preset range.

### 17. Portfolio Progress (`/home/portfolio-progress`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Make the main visualization even more dominant over supporting text.
  - Shorten the interpretation guidance around the CDC metric.
  - Add a faster jump from summary signal to product-level drill-down.

### 18. Pipeline Insights (`/home/pipeline-insights?productId=1&teamId=4&sprintId=2&timeMode=Sprint`)
- **Context used:** product 1, team 4, Sprint 11
- **Load status:** failed
- **UX score:** 4/10
- **3 improvements:**
  - Replace the generic `Data unavailable` block with a categorized explanation and mock-mode guidance.
  - Preserve more of the page’s intended insight layout when only one data slice fails.
  - Show the recovery path closer to the failing panel rather than as a generic retry.

### 19. What’s New Since Sync (`/home/changes`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Group changes by impact before chronology.
  - Highlight the most important change cluster above the rest.
  - Improve separation between pipeline, PR, and work-item change types.

### 20. Planning workspace (`/home/planning`)
- **Context used:** defaults active
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Add one compact planning summary above the route tiles.
  - Clarify which route is best for planning review versus planning action.
  - Reduce competition between navigation chrome and planning content.

### 21. Product Roadmaps — global (`/planning/product-roadmaps`)
- **Context used:** all products in active profile
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Elevate the highest-risk roadmap lane instead of giving all lanes equal emphasis.
  - Reduce repeated epic metadata so more of the roadmap fits above the fold.
  - Make forecast confidence easier to compare at a glance across lanes.

### 22. Product Roadmaps — project scoped (`/planning/battleship-systems/product-roadmaps`)
- **Context used:** route project `battleship-systems`
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Make it more obvious that the project route constrains the visible universe without changing global product selection.
  - Replace raw project identifiers in the filter summary with more user-facing wording.
  - Distinguish project-level planning context from the per-product lanes more clearly.

### 23. Product Roadmap Editor (`/planning/product-roadmaps/1`)
- **Context used:** route product bootstrap for product 1
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Surface why each epic is a candidate before the drawer is opened.
  - Keep save-state feedback closer to the interaction location.
  - Show one stronger cue for what a “good” roadmap mix looks like.

### 24. Plan Board — global (`/planning/plan-board?productId=1`)
- **Context used:** product 1
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Explain the relationship between the left candidate tree and the sprint columns more quickly.
  - Reduce the cognitive load of the tree before the user starts dragging.
  - Clarify how sprint columns were chosen for the visible product teams.

### 25. Plan Board — project scoped (`/planning/battleship-systems/plan-board?productId=1`)
- **Context used:** route project plus valid product 1
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Make the route-scoped project context more visible in the header, not just in filters.
  - Explain when project scope matters versus product scope for planning moves.
  - Reduce non-essential summary chrome above the board itself.

### 26. Project Planning Overview (`/planning/battleship-systems/overview?productId=1`)
- **Context used:** route project `battleship-systems`, valid product query retained globally
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Remove the confusing “Product filter is active globally but not applied on this page” warning from the main reading flow.
  - Replace raw project ID display in the filter summary with a user-facing label.
  - Promote one lead project risk instead of making the table and risk chips share equal attention.

### 27. Multi-Product Planning (`/planning/multi-product`)
- **Context used:** defaults active; page resolved visible products 1 and 2
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Make the main timeline even more dominant over the setup controls.
  - Explain what “clusters” and “capacity collisions” mean inline.
  - Add a clearer summary sentence describing the cross-product story on first load.

### 28. Bugs Triage (`/bugs-triage?productId=1`)
- **Context used:** product 1
- **Load status:** success
- **UX score:** 8/10
- **3 improvements:**
  - Make tagged versus untagged bugs more visually distinct in the tree.
  - Surface selected tag filters closer to the selected bug details.
  - Highlight likely hotfix candidates before the long default list.

### 29. Work Item Explorer (`/workitems?productId=1`)
- **Context used:** product 1
- **Load status:** success
- **UX score:** 7/10
- **3 improvements:**
  - Reduce the amount of up-front metadata competing with the hierarchy itself.
  - Make the validation-fix path feel more directly connected to the affected items.
  - Improve the readability of the first visible tree rows before the user expands further.

## Failures

| Page | Failure type | Description |
| --- | --- | --- |
| `/home/pipeline-insights?productId=1&teamId=4&sprintId=2&timeMode=Sprint` | other — backend/data retrieval failure | Valid Battleship context did not trigger invalid-context handling, but the page rendered a `Data unavailable` state with `Error retrieving pipeline insights` instead of usable insight content. |

## Cross-page Findings

### Inconsistencies
- **Requested vs applied filter semantics are inconsistent.** Sprint Execution and Portfolio Delivery both surfaced `Requested ≠ applied`, but the explanation and visual treatment were weak and page-specific.
- **Some pages ignore a globally active product while still surfacing product-related filter chrome.** Project Planning Overview and Portfolio Delivery expose this most clearly.
- **Filter summary labels are too raw.** Numeric product IDs and internal project IDs appear in places where users need product/project names.
- **Action-heavy pages vary too much in first-view hierarchy.** Validation pages are clear immediately; delivery/trend pages often need more interpretation before the user knows what to do.

### Redundancies
- **Home hub + workspace hubs overlap heavily.** The route-to-route mental model is repeated several times before the user reaches content.
- **Bug Overview and Bugs Triage overlap in intent.** One is diagnostic, one is operational, but the distinction is not visually strong enough.
- **Validation Triage, Queue, and Fix form a useful flow, but the first two repeat context and explanation more than necessary.**

### Structural UX issues
- **Pages requiring deep context setup still ask the user to do too much work.** Delivery Trends, PR surfaces, Sprint Delivery, Sprint Execution, Pipeline Insights, and Portfolio Delivery all benefit from valid query context but still feel setup-heavy.
- **Several analytical pages do not clearly establish one primary visualization or lead takeaway.** The user must infer the main story from multiple similarly weighted panels.
- **Filter chrome remains visually persistent even when it adds little value.** This is especially noticeable on hubs and pages that ignore some global filters entirely.
- **Project-scoped planning pages are now functionally valid, but the UX still exposes contract complexity.** The new context model is correct; the page language is not yet simple.

## Priority Fixes
1. **Make filter application semantics explicit and human-readable** on pages that request one scope but apply another.
2. **Fix Pipeline Insights for valid Battleship sprint context** so the page returns insight content instead of a generic unavailable state.
3. **Replace raw IDs in filter summaries and alerts with friendly names** across product/project-aware pages.
4. **Promote one primary takeaway per analytical page** and demote secondary chrome and controls.
5. **Reduce context-setup friction on delivery and trend pages** with named valid presets or stronger guided defaults.
