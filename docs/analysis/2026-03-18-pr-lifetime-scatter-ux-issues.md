# Bug Analysis Report

## Metadata
- Bug: PR Lifetime scatter has unclear colors and unusable legend
- Area: UI / PR Insights / Visualization
- Status: Analysis complete

## ROOT_CAUSE

- **Color issue — UI/chart configuration root cause.** Point colors are assigned only in `PoTool.Client/Components/Charts/PullRequestDeliveryScatterSvg.razor`.
- `GetStatusColor(pt.Status)` maps `"completed"` to `var(--mud-palette-success)`, `"abandoned"` to `var(--mud-palette-error)`, and every other status, including `"active"`, to `var(--mud-palette-default)`.
- Because the active state uses that dark/neutral MudBlazor token directly on a dark chart surface, the poor contrast comes from the client visualization rule, not from the API, DTO, or source data.
- **Legend clarity issue — UI/chart configuration root cause caused by contract drift.** The backend already resolves and returns `Status`, `EpicId`, and `EpicName` on each `PrDeliveryScatterPointDto`, and unit tests verify that resolved epic titles such as `"My Epic"` reach the DTO.
- The unclear state legend is introduced in `PoTool.Client/Components/Charts/PullRequestDeliveryScatterSvg.razor`, where entries are hardcoded instead of derived from the rendered point contract.
- That drift shows up in three ways: the legend advertises `Merged (rework)` even though this scatter DTO has no rework-specific color field; it uses legend CSS class names without a matching `PullRequestDeliveryScatterSvg.razor.css`; and its epic legend iterates `_epicShapeMap` keyed by `EpicId?.ToString() ?? "none"`, so labels become raw IDs / `none` instead of `EpicName`.
- **Legend layout issue — UI/chart configuration root cause.** Placement is determined in the component markup, not by the API or by a chart library default.
- The legend is rendered as a plain HTML block immediately after the `<svg>` (`<div class="pr-scatter-legend">...</div>`), so it naturally sits below the chart.
- Because that layout is a wrapping row with no density-aware grouping or right-side container, readability drops quickly once state items, overlay items, and multiple epic items are present.

## CURRENT_BEHAVIOR
- **Data → API → DTO → render path:** PR data is loaded from cached `PullRequests`, `PullRequestIterations`, `PullRequestFileChanges`, `PullRequestWorkItemLinks`, and `WorkItems` inside `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs`. The handler classifies each PR, resolves Epic/Feature ancestry through `WorkItemResolutionService.ResolveAncestry`, and projects `PrDeliveryScatterPointDto` objects with `Status`, `Category`, `EpicId`, `EpicName`, `FeatureId`, and `FeatureName`. `PoTool.Api/Controllers/PullRequestsController.cs` exposes that payload at `GET api/PullRequests/delivery-insights`, `PoTool.Client/ApiClient/ApiClient.PrDeliveryInsights.cs` fetches it through `IPullRequestsClient.GetDeliveryInsightsAsync`, `PoTool.Client/Pages/Home/PrDeliveryInsights.razor` stores it in `_data`, and the page passes `_data.ScatterPoints` into `PullRequestDeliveryScatterSvg` for final rendering.
- **How colors are assigned to PR states:** Color assignment happens only in the chart component, not in the handler or DTO. `PullRequestDeliveryScatterSvg.razor` derives point fill from `GetStatusColor(pt.Status)`. The DTO only carries raw `Status`; it does not carry a presentation color token or a richer color category.
- **How legend items are generated:** The legend is assembled manually in `PullRequestDeliveryScatterSvg.razor` from fixed `<span>` elements for `Merged`, `Merged (rework)`, `Abandoned`, optional `Active`, optional median/P90 overlays, and then a loop over `_epicShapeMap`. It does not reuse a shared legend model, and it does not derive state entries from the same data structure that drives point rendering.
- **How epic identifiers are mapped to shapes/labels:** On parameter set, the component builds `_epicShapeMap` by calling `GetEpicKey(pt.EpicId)`, where the key is `epicId?.ToString() ?? "none"`, and assigns shapes in first-seen order from `["circle", "square", "triangle"]`. Point rendering uses `GetEpicShape(pt.EpicId)`, so the shape itself is driven by epic ID. The legend then iterates that same dictionary and treats the dictionary key as the display label, which is why it prints raw IDs / `none` instead of the already-available `EpicName`.
- **How legend layout is determined:** Layout is determined locally by the chart component markup and CSS intent, not by backend data. The legend is rendered after the SVG, so it sits beneath the chart, and the only styling pattern present elsewhere in the repo for these class names is `PoTool.Client/Components/Charts/PullRequestScatterSvg.razor.css`, which belongs to a different component. `PullRequestDeliveryScatterSvg` has no same-name isolated CSS file, so even the intended legend coloring/layout rules are not attached to this component.
- **Where the defects originate:** I did not find a primary data-layer defect for the reported UX issues. The handler resolves epic titles and copies them into the scatter DTO, and tests in `PoTool.Tests.Unit/Handlers/GetPrDeliveryInsightsQueryHandlerTests.cs` confirm `EpicName` is populated for delivery-mapped PRs. The visible problems are introduced at the client visualization layer: unsuitable active color selection, a hardcoded legend that drifts from the delivery scatter DTO contract, ID-based epic legend labeling, missing component-local legend styling, and a below-chart legend layout that does not scale with item count.

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the full path and the evidence points strongly to the PR Lifetime scatter component itself rather than to missing source data.

The backend contract is already carrying the information the legend needs for understandable labels: `Status`, `EpicId`, and resolved `EpicName` are all present on `PrDeliveryScatterPointDto`, and the handler tests explicitly verify epic title resolution. That means the `93787` / `93786` style labels are not coming from missing epic names in the data layer; they appear because the client legend uses an internal epic-ID key as if it were a display string.

The legend problems also share a common pattern: this component looks like an adaptation of the other PR scatter chart, but the adaptation was incomplete. The other chart has a dedicated `ColorCategory` contract and an isolated CSS file. The delivery scatter does not. As a result, the delivery legend still shows workflow-friction concepts such as `Merged (rework)`, does not have component-local legend styling attached, and is not guaranteed to stay aligned with the actual point-rendering rules.

So the strict analysis contract is: the observed defects are predominantly UI/chart-configuration defects. The data/API/DTO path is supplying enough information for meaningful labels, but the visualization layer is not reusing that contract consistently for color semantics, legend labeling, or dense-layout behavior.
</comments>
