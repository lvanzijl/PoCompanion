# UI Semantic Correction Audit

## Labels corrected

- `PoTool.Client/Components/Forecast/ForecastPanel.razor` now labels legacy forecast scope values as `Total Story Points`, `Delivered Story Points`, and `Remaining Story Points`.
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor` now labels roadmap epic progress with explicit story-point wording.
- `PoTool.Client/Pages/Home/DeliveryTrends.razor` now labels effort-hour drill-down columns as hours instead of points.
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor` now labels portfolio effort-hour summary and product contribution surfaces as hours.
- `PoTool.Client/Pages/Home/SprintTrend.razor` now labels effort deltas as hours and story-point delivery surfaces as story points.

## SP surfaces migrated

- `PoTool.Client/Pages/Home/SprintExecution.razor` summary cards now use `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, and `SpilloverSP`.
- `PoTool.Client/Pages/Home/DeliveryTrends.razor` story-point delivery chart now reads `TotalCompletedPbiStoryPoints` / `CompletedPbiStoryPoints`.
- `PoTool.Client/Pages/Home/SprintTrend.razor` product delivery summary and epic/feature delivery progress now display story-point delivery values.
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor` feature contribution remains on the canonical story-point surface behind legacy property names.

## Effort-hour surfaces clarified

- `PoTool.Client/Pages/Home/DeliveryTrends.razor` drill-down table keeps effort-hour fields explicit as hours.
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor` portfolio summary and product contribution remain effort-hour based and no longer display `pts`.
- `PoTool.Client/Pages/Home/SprintTrend.razor` sprint scope-change deltas remain effort-hour based and now display hours explicitly.

## Remaining semantic debt

- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` still represents portfolio flow with effort-based proxy metrics and should stay effort-based until the model changes.
- Legacy DTO and generated client property names such as `TotalEffort`, `CompletedEffort`, and `SprintCompletedEffort` remain in place for backward compatibility.
- Portfolio flow remains effort-based until its model changes.
