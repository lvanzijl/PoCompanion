# UX Intent Classification

Scope note: this report covers all routed pages with visible UI in `PoTool.Client`. Legacy redirect shims (`/home/sprint-trend`, `/home/sprint-trend/activity/{id}`, `/home/backlog-overview`) are excluded from per-page tables because they do not present stable user-facing content. Element classification is done at visible section/component level rather than atomic controls so the output stays actionable.

## Index (`/`)
- **Page name:** Index / startup redirect
- **Primary role:** First-run router
- **Primary question:** Where should the user go next based on readiness?
- **Secondary questions:** Is startup blocked? Is onboarding required?
- **Primary signal:** Full-screen loading state while readiness is resolved
- **Supporting signals:** None

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Full-screen progress spinner | Core | Keep as-is | It directly answers that startup routing is in progress. |
| Empty page shell around spinner | Supporting | Keep as-is | Minimal framing avoids competing with redirect intent. |

## Onboarding (`/onboarding`)
- **Page name:** Onboarding
- **Primary role:** First-run setup launcher
- **Primary question:** How does a new user get into a usable configured state?
- **Secondary questions:** What setup is required first? Where does the flow continue after completion?
- **Primary signal:** Modal onboarding wizard
- **Supporting signals:** Full-screen empty background

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Onboarding wizard dialog | Core | Promote | It is the entire purpose of the route and should dominate attention. |
| Empty centered container | Supporting | Keep as-is | Neutral background keeps focus on the wizard. |

## Profiles Home (`/profiles`)
- **Page name:** Profiles Home
- **Primary role:** Identity and context entry page
- **Primary question:** Which Product Owner context should the user enter with?
- **Secondary questions:** Should a new profile be created? Are teams or release notes needed first?
- **Primary signal:** Profile tile grid
- **Supporting signals:** Add Profile tile; What's New / Manage Teams actions

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| “Who’s using PO Companion?” header | Core | Keep as-is | Clarifies the route’s selection intent immediately. |
| Profile tile grid | Core | Promote | This is the key decision surface and should remain first and dominant. |
| Add Profile tile | Supporting | Keep as-is | It supports entry when no usable profile exists. |
| Empty-state alert | Supporting | Keep as-is | Helps when no profiles exist without competing in normal flow. |
| “What’s New” button | Advanced | Hide behind advanced mode | Useful, but not part of the primary selection decision. |
| “Manage Teams” button | Advanced | Demote | Administrative work is secondary to choosing a profile. |

## Sync Gate (`/sync-gate`)
- **Page name:** Preparing Workspace
- **Primary role:** Cache readiness gate
- **Primary question:** Is the workspace being prepared successfully and what should the user do if it fails?
- **Secondary questions:** Which stage is running? Can the user recover without leaving the flow?
- **Primary signal:** Current sync status title + description
- **Supporting signals:** Progress bar / stage text; retry and escape actions

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Sync status title and description | Core | Promote | This is the decision-making signal for whether the user waits, retries, or exits. |
| Progress paper with stage and progress bar | Core | Keep as-is | It makes the blocking wait meaningful. |
| Retry / Back to Profiles / Open Settings actions | Supporting | Keep as-is | They are needed only when the happy path breaks. |
| Spinning sync icon | Supporting | Keep as-is | Reinforces state, but should not outrank the status text. |

## Startup Blocked (`/startup-blocked`)
- **Page name:** Startup Blocked
- **Primary role:** Fatal startup recovery page
- **Primary question:** Why can’t startup continue, and what is the next recovery step?
- **Secondary questions:** Should the user retry? Should settings be opened?
- **Primary signal:** StartupBlockingPanel title/reason/recovery hint
- **Supporting signals:** Retry Startup; Open Settings actions

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| StartupBlockingPanel reason and hint | Core | Keep as-is | It directly answers the route’s main question. |
| Retry startup action | Supporting | Promote | Most likely next step when a transient issue caused the block. |
| Open settings action | Supporting | Keep as-is | Useful escape hatch when configuration is broken. |

## Home (`/home`)
- **Page name:** Product Owner Dashboard
- **Primary role:** Workspace entry dashboard
- **Primary question:** Which workspace needs attention right now?
- **Secondary questions:** What product context is active? Has sync completed recently?
- **Primary signal:** Workspace tile grid with subtitles/status badges
- **Supporting signals:** Product selector + metric strip; sync status row; quick actions

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Workspace tile grid | Core | Promote | It is the main decision surface for the dashboard. |
| Product selector chip row | Supporting | Keep as-is | Important context, but subordinate to workspace choice. |
| Product metric strip (team sprint / bugs / changes today) | Supporting | Group | It provides orientation but reads as context, not destination. |
| Sync status row | Supporting | Keep as-is | It supports trust in the dashboard state. |
| Quick Actions panel | Advanced | Demote | Useful shortcuts, but they compete with workspace entry if placed too high. |
| No-profile alert | Core | Keep as-is | When present it correctly blocks all other intent. |

## Home Changes (`/home/changes`)
- **Page name:** What’s New Since Last Sync
- **Primary role:** Operational delta review
- **Primary question:** What changed between the last two sync windows?
- **Secondary questions:** Are there bugs or validation issues that need immediate attention? Did a sprint complete?
- **Primary signal:** Sync-window summary alert + quick stats
- **Supporting signals:** Bugs opened table; bugs closed table; validation issues table; sprint completions table

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Sync window alert | Core | Promote | It defines the time scope of every other section. |
| Quick stats cards | Core | Keep as-is | They summarize the change window at a glance. |
| Bugs Opened section | Supporting | Keep as-is | Important secondary drill-down from the headline counts. |
| Bugs Closed section | Supporting | Keep as-is | Same as above. |
| Validation Issues section | Supporting | Keep as-is | High-value operational follow-up. |
| Operational-page info alert | Advanced | Demote | Helpful framing, but lower importance than the actual change window signal. |

## Health Workspace (`/home/health`)
- **Page name:** Health workspace
- **Primary role:** Health navigation hub
- **Primary question:** Which health view should the user open next?
- **Secondary questions:** Is the need overview, validation triage, or backlog health? Is another workspace more appropriate?
- **Primary signal:** Health navigation tile group
- **Supporting signals:** Workspace explanation copy; cross-workspace shortcuts

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Health hub tile group | Core | Promote | It is the reason the page exists. |
| Header + breadcrumbs | Supporting | Keep as-is | Good orientation, but not the main decision surface. |
| Intro copy | Supporting | Group | Useful when paired tightly with the tiles. |
| Other-workspaces buttons | Advanced | Demote | Helpful escape hatch but not core to health selection. |

## Health Overview (`/home/health/overview`)
- **Page name:** Overview
- **Primary role:** Build quality snapshot
- **Primary question:** What is the current build quality health for this Product Owner or selected product?
- **Secondary questions:** Which products are weakest? Is the current 30-day window healthy overall?
- **Primary signal:** Overall Build Quality summary card
- **Supporting signals:** Product build-quality cards; product filter chip; rolling-window chip

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Overall Build Quality summary | Core | Promote | It is the clearest single answer to the page question. |
| Product build-quality card grid | Supporting | Keep as-is | Necessary follow-up for where issues sit. |
| 30-day window chip | Supporting | Group | Important scope context, but secondary to the health result. |
| Product filter chip | Supporting | Keep as-is | Useful only when scoped. |
| Header action buttons | Advanced | Demote | Navigation should not compete with the health signal. |

## Validation Triage (`/home/validation-triage`)
- **Page name:** Validation Triage
- **Primary role:** Validation issue entry point
- **Primary question:** Which validation category needs attention first?
- **Secondary questions:** Is a product scope active? How many items sit in each category?
- **Primary signal:** Category card grid
- **Supporting signals:** Product scope chip; explanatory text; breadcrumb/header

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Four triage category cards | Core | Promote | They are the route’s direct action surface. |
| “Open queue” explanatory copy | Supporting | Keep as-is | It explains the next action without clutter. |
| Product scope chip | Supporting | Keep as-is | Important only when narrowing the triage scope. |
| Health/Home navigation buttons | Advanced | Demote | Useful but not part of category prioritization. |

## Validation Queue (`/home/validation-queue`)
- **Page name:** Validation Queue
- **Primary role:** Rule-level prioritization page
- **Primary question:** Which validation rule should be worked next within the chosen category?
- **Secondary questions:** How big is each rule group? Is the user on the correct category/product scope?
- **Primary signal:** Rule-group card grid sorted by impact
- **Supporting signals:** Category summary header; product chip; recovery panels

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Rule-group cards with Start fix session | Core | Promote | They are the route’s direct choice surface. |
| Category summary header | Supporting | Keep as-is | Gives high-value context before selection. |
| Product scope chip | Supporting | Keep as-is | Useful context but not the main decision. |
| Recovery / not-ready panels | Supporting | Keep as-is | Necessary state handling. |
| Navigation buttons | Advanced | Demote | Important but not primary. |

## Validation Fix Session (`/home/validation-fix`)
- **Page name:** Fix Session
- **Primary role:** Guided issue resolution workflow
- **Primary question:** What is the next work item to review and why is it failing validation?
- **Secondary questions:** How far through the session is the user? What item metadata helps resolve it?
- **Primary signal:** Current item card with violation message
- **Supporting signals:** Session progress banner; previous/next controls; metadata and description sections

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Current item title + violation alert | Core | Promote | This is the actionable heart of the page. |
| Session progress banner | Supporting | Keep as-is | Useful orientation but secondary to the current item. |
| Previous / Next navigation | Supporting | Keep as-is | Supports workflow traversal. |
| Metadata grid / description | Supporting | Keep as-is | Helps resolution after the violation is understood. |
| Completion state card | Core | Keep as-is | Correct dominant state when the workflow is finished. |

## Backlog Health (`/home/health/backlog-health`)
- **Page name:** Backlog Health
- **Primary role:** Refinement readiness inspection
- **Primary question:** Which epics are ready, and which need refinement next?
- **Secondary questions:** Which feature/PBI states are blocking readiness? Which product is in scope?
- **Primary signal:** Ready vs Needs Refinement split
- **Supporting signals:** Product selector; epic progress expansion panels; PBI readiness table

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Ready for Implementation section | Core | Keep as-is | Directly answers what can move forward. |
| Needs Refinement section | Core | Promote | This is usually the more actionable follow-up and should remain prominent. |
| Product selector / chip | Supporting | Keep as-is | Necessary scoping control. |
| Feature/PBI nested expansion panels | Supporting | Keep as-is | Needed for drill-down into blockers. |
| Header navigation buttons | Advanced | Demote | Secondary to the backlog assessment itself. |

## Delivery Workspace (`/home/delivery`)
- **Page name:** Delivery workspace
- **Primary role:** Delivery navigation hub
- **Primary question:** Which delivery lens should the user open?
- **Secondary questions:** Does the user need sprint delivery, sprint execution, or portfolio delivery? Is another workspace more appropriate?
- **Primary signal:** Delivery view tile group
- **Supporting signals:** Introductory copy; cross-workspace shortcuts

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Delivery view tiles | Core | Promote | They are the entire purpose of the page. |
| Header + breadcrumbs | Supporting | Keep as-is | Good orientation. |
| Intro copy | Supporting | Group | Useful only when paired with the tiles. |
| Other-workspaces buttons | Advanced | Demote | Helpful but secondary to delivery choice. |

## Sprint Delivery (`/home/delivery/sprint`)
- **Page name:** Sprint Delivery
- **Primary role:** Stakeholder sprint report
- **Primary question:** What was delivered in the selected sprint?
- **Secondary questions:** What was build quality during the sprint? How should the user drill from portfolio to product/epic/feature levels?
- **Primary signal:** Sprint navigation + delivery content for the current sprint
- **Supporting signals:** Build Quality section; drill-down breadcrumb strip; lower-level delivery tables/charts

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Current sprint name/date in Sprint Navigation | Core | Promote | It anchors the entire report. |
| Main delivery drill-down content | Core | Keep as-is | It answers what landed in the sprint. |
| Build Quality block | Supporting | Keep as-is | Important secondary health context. |
| Drill-up breadcrumb strip | Supporting | Keep as-is | Valuable once deeper drill-down happens. |
| Back/Home buttons | Advanced | Demote | Navigation should not outrank the delivery signal. |

## Sprint Execution (`/home/delivery/execution`)
- **Page name:** Sprint Execution
- **Primary role:** Sprint diagnostics page
- **Primary question:** What churn or starvation happened inside the sprint?
- **Secondary questions:** What was completed first? Which unfinished or added items explain the churn?
- **Primary signal:** Sprint Execution Summary metrics
- **Supporting signals:** Completion Order table; Potential Starvation Signals; Unfinished PBIs / added-removed lists

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Sprint Execution Summary card grid | Core | Promote | It is the clearest first answer to sprint-health questions. |
| Completion Order table | Supporting | Keep as-is | Strong secondary diagnostic. |
| Starvation signals table | Supporting | Keep as-is | Direct follow-up for churn analysis. |
| Empty / missing-team / missing-sprint alerts | Core | Keep as-is | They correctly block analysis when context is missing. |
| Header descriptive copy | Supporting | Keep as-is | Sets the diagnostic framing clearly. |

## Portfolio Delivery (`/home/delivery/portfolio`)
- **Page name:** Portfolio Delivery
- **Primary role:** Cross-product delivery composition view
- **Primary question:** How was delivery distributed across products/features over the selected sprint range?
- **Secondary questions:** Where is bug pressure concentrated? What does the portfolio summary look like overall?
- **Primary signal:** Portfolio Summary card grid
- **Supporting signals:** Product contribution chart; feature contribution chart; bug distribution table

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Portfolio Summary metrics | Core | Promote | Best first answer for the page’s scope. |
| Product Contribution panel | Supporting | Keep as-is | Explains product-level composition. |
| Feature Contribution panel | Supporting | Keep as-is | Explains feature-level composition. |
| Bug Distribution table | Supporting | Keep as-is | Important secondary risk lens. |
| Aggregated snapshot explainer | Supporting | Group | Necessary scope framing but not the main signal. |

## Trends Workspace (`/home/trends`)
- **Page name:** Trends (Past)
- **Primary role:** Trend navigation hub
- **Primary question:** Which past-behavior signal should the user inspect next?
- **Secondary questions:** Which trend tiles show concern? What does the bug trend look like right now?
- **Primary signal:** Trend Signals tile grid
- **Supporting signals:** Bug trend chart; info note; cross-workspace navigation

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Trend Signals grid | Core | Promote | It is the route’s main decision surface. |
| Bug Trend chart | Supporting | Keep as-is | Good embedded preview, but subordinate to tile choice. |
| Info note about horizon and interactions | Advanced | Demote | Helpful documentation, not primary analysis. |
| Other-workspaces buttons | Advanced | Demote | Escape hatch, not trend selection. |

## Delivery Trends (`/home/trends/delivery`)
- **Page name:** Delivery Trends
- **Primary role:** Multi-sprint delivery trend analysis
- **Primary question:** Is delivery throughput improving or degrading over time?
- **Secondary questions:** How are story points, completion %, and bug creation moving? What does per-sprint drill-down show?
- **Primary signal:** PBI Throughput Trend primary chart
- **Supporting signals:** Story Point Delivery Trend; Progress Trend; Bug Trend; drill-down table

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| PBI Throughput Trend chart | Core | Promote | It is explicitly the primary visualization and best headline answer. |
| Trend window controls | Supporting | Keep as-is | Scope definition is necessary but should remain secondary. |
| Secondary visualization grid | Supporting | Keep as-is | Strong supporting evidence for the main trend. |
| Per-sprint drill-down expansion | Advanced | Hide behind advanced mode | Valuable detail but not needed for default first view. |
| Header navigation buttons | Advanced | Demote | Navigation should not compete with the chart. |

## Portfolio Flow Trend (`/home/portfolio-progress`)
- **Page name:** Portfolio Flow Trend
- **Primary role:** Strategic portfolio trend page
- **Primary question:** Is the portfolio backlog expanding, contracting, or flowing sustainably?
- **Secondary questions:** How are stock, remaining ratio, and throughput moving? What does CDC read-only history add?
- **Primary signal:** Combined Flow chart
- **Supporting signals:** Portfolio Stock Trend; Remaining Scope Ratio; Throughput Trend

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Combined Flow chart | Core | Promote | It is the top strategic answer and already framed as net emphasis. |
| Three supporting charts | Supporting | Keep as-is | They answer the next-best secondary questions. |
| Net-only toggle | Advanced | Keep as-is | Useful analytic control, but secondary to default reading. |
| Portfolio CDC read-only panel | Advanced | Hide behind advanced mode | Valuable, but too heavy for default first view. |
| Missing-team/range alerts | Core | Keep as-is | Correctly block analysis when scope is absent. |

## Pull Request Insights (`/home/pull-requests`)
- **Page name:** Pull Request Insights
- **Primary role:** PR flow efficiency analysis
- **Primary question:** How healthy is PR flow, and where are the worst friction points?
- **Secondary questions:** Which repositories/authors drive the pattern? Which PRs deserve immediate attention?
- **Primary signal:** Summary metrics + PR scatter chart
- **Supporting signals:** Top 3 Friction PRs; repository breakdown; author breakdown; longest-open list

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Summary metrics grid | Core | Keep as-is | It establishes the health baseline quickly. |
| PR scatter chart | Core | Promote | Best explanatory visualization for distribution and outliers. |
| Top 3 Friction PRs cards | Supporting | Keep as-is | Actionable follow-up to the scatter. |
| Repository filter | Supporting | Keep as-is | Important scoping control. |
| Longest Open / Repository / Author breakdown tables | Advanced | Group | Valuable, but they compete if all are equally emphasized. |

## Pipeline Insights (`/home/pipeline-insights`)
- **Page name:** Pipeline Insights
- **Primary role:** Pipeline stability analysis
- **Primary question:** Which pipelines or products are creating the most delivery risk?
- **Secondary questions:** What is the global failure/warning picture? Which product sections deserve drill-in?
- **Primary signal:** Global Summary + Top Pipelines in Trouble
- **Supporting signals:** Per-product pipeline sections; display options; pipeline build quality warning

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Global Summary cards | Core | Keep as-is | Good first answer at aggregate level. |
| Top Pipelines in Trouble cards | Core | Promote | Most actionable first-view signal on the page. |
| Per-product pipeline sections | Supporting | Keep as-is | Necessary product-level drill-in. |
| Display options panel | Advanced | Demote | Useful tuning, but not the primary signal. |
| “Select a team and sprint” empty state | Core | Keep as-is | Correct blocker when no scope exists. |

## PR Delivery Insights (`/home/pr-delivery-insights`)
- **Page name:** PR Delivery Insights
- **Primary role:** PR classification and friction page
- **Primary question:** How much of PR activity maps to delivery vs bug/disturbance work?
- **Secondary questions:** Which epics show the most friction? What team/sprint context is active?
- **Primary signal:** PR Classification Summary
- **Supporting signals:** Epic Friction Overview; context summary chips; lower breakdown tables/charts

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| PR Classification Summary | Core | Promote | Strongest direct answer to the page question. |
| Epic Friction Overview table | Supporting | Keep as-is | Best secondary drill-down. |
| Collapsible context panel | Supporting | Keep as-is | Good because it preserves capability without taking over first view. |
| Context chips when collapsed | Supporting | Keep as-is | They preserve scope visibility above the fold. |
| Additional lower breakdown sections | Advanced | Group | Valuable but should not outrank the classification summary. |

## Bug Insights (`/home/bugs`)
- **Page name:** Bug Insights
- **Primary role:** Bug trend and severity snapshot
- **Primary question:** What is the current bug burden and severity mix?
- **Secondary questions:** Is bug resolution keeping pace? Should the user jump into triage?
- **Primary signal:** Bug metric card grid
- **Supporting signals:** Severity distribution; Ready to triage panel

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Bug metric cards | Core | Promote | They give the clearest first answer. |
| Severity distribution section | Supporting | Keep as-is | Good second question support. |
| “Ready to triage?” call-to-action panel | Supporting | Keep as-is | Strong next step after reading the metrics. |
| Header buttons | Advanced | Demote | Navigation should not compete with the insights. |

## Bugs Triage (`/bugs-triage`)
- **Page name:** Bugs Triage
- **Primary role:** Bug triage workbench
- **Primary question:** Which bugs are still untriaged and how should they be tagged/severity-scored?
- **Secondary questions:** Which filters best isolate the problem set? What details are on the selected bug?
- **Primary signal:** Split-pane bug tree + details panel
- **Supporting signals:** Tag filters; untriaged count in header; empty-selection state

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Split-pane tree + details workspace | Core | Promote | It is the actual triage workflow. |
| Header bug counts | Supporting | Keep as-is | Good orientation before drilling in. |
| Tag filters section | Supporting | Keep as-is | Core scoping aid for triage. |
| Empty-selection prompt | Supporting | Keep as-is | Helps first interaction. |
| Home button | Advanced | Demote | Necessary but not part of triage. |

## Planning Workspace (`/home/planning`)
- **Page name:** Planning workspace
- **Primary role:** Planning navigation hub
- **Primary question:** Which planning surface should the user open next?
- **Secondary questions:** Is project-scoped planning available? Does the need concern roadmap sequence or sprint placement?
- **Primary signal:** Planning view tile group
- **Supporting signals:** Intro copy; cross-workspace navigation

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Planning view tiles | Core | Promote | They are the reason the hub exists. |
| Conditional Project Overview tile | Supporting | Keep as-is | Important when project scope exists. |
| Intro copy | Supporting | Group | Explains choice, but should stay close to tiles. |
| Other-workspaces buttons | Advanced | Demote | Secondary to planning view selection. |

## Project Planning Overview (`/planning/{projectAlias}/overview`)
- **Page name:** Project Planning Overview
- **Primary role:** Project-scoped planning summary
- **Primary question:** What is the planning status and risk shape of this project across products?
- **Secondary questions:** Is the project imbalanced or overcommitted? How is effort distributed by product?
- **Primary signal:** Project context summary cards
- **Supporting signals:** Detected risks chip row; product distribution table

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Project context summary cards | Core | Promote | Best first answer to project planning state. |
| Detected risks chip row | Supporting | Keep as-is | Good secondary risk framing. |
| Product distribution table | Supporting | Keep as-is | Supports follow-up allocation questions. |
| Roadmaps / Plan Board actions | Advanced | Demote | Useful exits, but not core to understanding this page. |

## Product Roadmaps (`/planning/product-roadmaps`, `/planning/{projectAlias}/product-roadmaps`)
- **Page name:** Product Roadmaps
- **Primary role:** Read-only roadmap portfolio view
- **Primary question:** How is roadmap epic order distributed across products?
- **Secondary questions:** Is one product dominating the roadmap? What does the projected timeline look like per product?
- **Primary signal:** Product lanes with epic cards
- **Supporting signals:** Project roadmap summary; projected timeline snippets; reporting/snapshot menus

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Product lanes with epic cards | Core | Promote | This is the main visual answer. |
| Project roadmap summary panel | Supporting | Keep as-is | Valuable project-scoped framing. |
| Per-lane projected timeline | Supporting | Keep as-is | Important second-order signal. |
| Reporting menu | Advanced | Hide behind advanced mode | Powerful capability, but not part of default roadmap reading. |
| Snapshot menu | Advanced | Hide behind advanced mode | Same rationale. |
| Read-only chip | Supporting | Keep as-is | Helps set expectation immediately. |

## Product Roadmap Editor (`/planning/product-roadmaps/{productId}`)
- **Page name:** Product Roadmap Editor
- **Primary role:** Roadmap ordering workbench
- **Primary question:** What epics belong on this product roadmap and in what order?
- **Secondary questions:** What available epics can be added? Which roadmap item should be edited next?
- **Primary signal:** Roadmap Epics column
- **Supporting signals:** Available Epics column; save-status chip; search field

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Roadmap Epics column | Core | Promote | It represents the primary editable artifact. |
| Available Epics column | Supporting | Keep as-is | Critical supporting inventory for roadmap building. |
| Save-status chip | Supporting | Keep as-is | Important confidence feedback. |
| Search field | Supporting | Keep as-is | Helps use the available-epics list without overwhelming the view. |
| All Roadmaps / Home buttons | Advanced | Demote | Useful exits but secondary to editing. |
| Placeholder metadata area | Noise | Candidate for removal | It takes visible space without current user value. |

## Plan Board (`/planning/plan-board`, `/planning/{projectAlias}/plan-board`)
- **Page name:** Plan Board
- **Primary role:** Sprint allocation board
- **Primary question:** Where should unplanned work go across upcoming sprint columns?
- **Secondary questions:** Is the project overcommitted? Which sprint columns are at risk? What remains unplanned?
- **Primary signal:** Backlog tree + sprint columns board
- **Supporting signals:** Project planning summary; sprint capacity indicators; sprint resolution info alert

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Backlog tree and sprint columns | Core | Promote | This is the planning decision surface. |
| Sprint capacity indicators | Supporting | Keep as-is | Essential supporting signal for placement decisions. |
| Project planning summary | Supporting | Keep as-is | Valuable context, especially in project-scoped mode. |
| Refresh from TFS action | Advanced | Demote | Important capability but not part of default board reading. |
| Sprint resolution info alert | Supporting | Keep as-is | Helps interpret column behavior when sprint data is inferred. |

## Multi-Product Planning (`/planning/multi-product`)
- **Page name:** Multi-Product Planning
- **Primary role:** Cross-product timeline comparison
- **Primary question:** How do roadmap forecasts line up across products on a shared time axis?
- **Secondary questions:** Where are pressure zones or capacity collisions? Which products/epics lack forecasts?
- **Primary signal:** Global forecast axis + product lanes
- **Supporting signals:** Product multi-select/filter bar; pressure/collision overlays; delayed/missing-forecast chips

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Global axis + product lanes | Core | Promote | They directly answer the shared-axis planning question. |
| Product filter / cluster / collision controls | Supporting | Keep as-is | Important scoping aids for comparison. |
| Pressure and capacity collision overlays | Supporting | Keep as-is | Good supporting insight once the main timeline is read. |
| Lane warning chips | Supporting | Keep as-is | Helpful summary of issues per lane. |
| Planning button | Advanced | Demote | Exit navigation, not core analysis. |

## Settings (`/settings`, `/settings/{topic}`)
- **Page name:** Settings
- **Primary role:** Settings hub
- **Primary question:** Which configuration topic does the user need to manage?
- **Secondary questions:** Is there an active profile for cache sections? Which topic is active now?
- **Primary signal:** Topic navigation sidebar
- **Supporting signals:** Active topic content panel

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Topic sidebar nav | Core | Promote | It is the primary decision surface. |
| Topic content panel | Supporting | Keep as-is | Supports the chosen topic. |
| Profile-required info alerts | Supporting | Keep as-is | Important for cache topics. |

## Work Item State Classification (`/settings/workitem-states`)
- **Page name:** Work Item States
- **Primary role:** Canonical state mapping configuration
- **Primary question:** How should each TFS state map into canonical lifecycle states?
- **Secondary questions:** Which types still need selection? Can the configuration be saved safely?
- **Primary signal:** Per-type state classification cards
- **Supporting signals:** lifecycle explanation list; save/reset actions

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Per-type state classification cards | Core | Promote | This is the core task surface. |
| Lifecycle explanation bullets | Supporting | Keep as-is | Important interpretation aid. |
| Save / Reset actions | Supporting | Keep as-is | Essential workflow completion controls. |
| Default-configuration info alert | Supporting | Keep as-is | Good context for first-time users. |

## Manage Teams (`/settings/teams`)
- **Page name:** Manage Teams
- **Primary role:** Team administration page
- **Primary question:** Which teams exist and what team record needs creation/edit/archive action?
- **Secondary questions:** Should archived teams be visible? Is a new TFS-backed team being added?
- **Primary signal:** Team list
- **Supporting signals:** Show Archived switch; Add Team action; inline editor

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Team list | Core | Promote | It is the main administrative surface. |
| Show Archived switch | Supporting | Keep as-is | Useful secondary filter. |
| Add Team button | Supporting | Keep as-is | Key creation action. |
| Inline editor panel | Supporting | Keep as-is | Necessary once editing begins. |

## Manage Product Owner (`/settings/productowner/{id}`)
- **Page name:** Manage Product Owner
- **Primary role:** Product-owner asset administration
- **Primary question:** What products belong to this Product Owner and how should they be managed?
- **Secondary questions:** Are orphaned products available? Does profile metadata need editing?
- **Primary signal:** Product list
- **Supporting signals:** profile header; orphaned products section; add/edit/remove actions

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Product list | Core | Promote | Main purpose of the page. |
| Profile header | Supporting | Keep as-is | Useful context but secondary to product management. |
| Orphaned products section | Supporting | Keep as-is | Important secondary management task. |
| Edit Profile / Back buttons | Advanced | Demote | Helpful exits, not core management surface. |

## Edit Product Owner (`/settings/productowner/edit/{id?}`)
- **Page name:** Add/Edit Product Owner
- **Primary role:** Product-owner profile editor
- **Primary question:** What Product Owner record should be created or updated?
- **Secondary questions:** Which goals should be linked? Which avatar should represent the profile?
- **Primary signal:** Product Owner form
- **Supporting signals:** goals selector; picture picker; save/cancel/delete actions

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Product Owner form fields | Core | Promote | Directly supports create/update intent. |
| Goals multi-select | Supporting | Keep as-is | Useful but optional metadata. |
| Picture picker | Supporting | Keep as-is | Secondary personalization. |
| Save action | Core | Promote | Highest-value control in the flow. |
| Delete action | Advanced | Demote | Valuable, but should not compete with save during editing. |

## Work Item Explorer (`/workitems`)
- **Page name:** Work Item Explorer
- **Primary role:** Deep work-item inspection tool
- **Primary question:** What does the scoped work-item hierarchy contain, and what details are on the selected node?
- **Secondary questions:** Which validation filters narrow the set? What fixes or history are visible for the current selection?
- **Primary signal:** Split-pane tree grid + detail panel
- **Supporting signals:** toolbar; validation summary/history; validation filter row

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| Tree grid + detail panel | Core | Promote | This is the main exploratory workflow. |
| Toolbar | Supporting | Keep as-is | Important command surface for exploration. |
| Validation summary panel | Supporting | Keep as-is | Helpful high-level quality context. |
| Validation history panel | Advanced | Hide behind advanced mode | Valuable but not needed for default exploration. |
| Validation filters row | Supporting | Group | Useful, but visually noisy in raw checkbox form. |

## Not Found (`/not-found`)
- **Page name:** Not Found
- **Primary role:** Missing-route notice
- **Primary question:** Did the requested content exist?
- **Secondary questions:** None
- **Primary signal:** Not Found message
- **Supporting signals:** brief explanatory paragraph

| Element | Classification | Proposed action | Justification |
|---|---|---|---|
| “Not Found” heading | Core | Keep as-is | Directly answers the page question. |
| Explanatory paragraph | Supporting | Keep as-is | Adds enough context without clutter. |

## Cross-page inconsistencies

### Team meaning
- Team is **navigation hub context only** on workspace hubs, but **required analytical scope** on Sprint Delivery, Sprint Execution, PR Delivery Insights, Pipeline Insights, Portfolio Flow, and sometimes Bug Insights.
- Team is **not applied at all** on project-scoped planning pages, which is correct structurally but creates a visible discontinuity when users move from delivery/trends into planning.
- PR Insights shows team context in summary chips but leads with **repository filtering**, making team feel secondary there versus primary on other engineering-health pages.

### Sprint meaning
- Sprint means **single current sprint** on Sprint Delivery, Sprint Execution, Pipeline Insights, and PR Delivery Insights.
- Sprint means **range / trend horizon** on Delivery Trends and Portfolio Flow.
- Sprint is **optional / absent** on PR Insights and Bug Insights, even though those pages sit adjacent to sprint-scoped engineering pages.
- Plan Board uses sprint as a **future allocation column**, not historical analysis scope, so the same term shifts from retrospective window to planning destination.

### Product scope
- Product is a **global optional chip/filter** on many health and bug pages.
- Product is **route-owned** on roadmap editor and sometimes project-owned on planning routes, making it immutable there.
- Home uses product as **dashboard context selector**, while Planning hub and project-scoped planning pages often treat product as secondary to project or roadmap structure.
- Multi-Product Planning uses **multi-select product scope**, which differs from the mostly single-product or all-products semantics elsewhere.

## High-risk removal candidates (flag only, no action)
- Product Roadmap Editor **placeholder metadata area**: visible reserved space with no present user value; high risk because it suggests missing capability and consumes scarce card real estate.
- Work Item Explorer **raw validation checkbox row**: high-value capability but visually noisy and disconnected from the main explorer interaction; risky because naïve removal would hide critical filtering power.
- Trends workspace **large explanatory note**: useful governance context, but it competes with signal tiles and the embedded bug chart; risky because removal could also remove important interpretation guidance.
- Product Roadmaps **Reporting / Snapshot menus**: capability-rich but structurally advanced; risky because over-emphasis pulls focus from the roadmap lanes, while hiding too aggressively could bury real product value.
- Bug Insights **Ready to triage panel**: it duplicates navigation intent already present in the header; risky because the CTA is still valuable when the metrics show active bug pressure.
