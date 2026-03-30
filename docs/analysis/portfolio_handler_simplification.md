# Portfolio Handler Simplification

_Generated: 2026-03-16_

## Removed Handler Calculations

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
  - removed local `ComputeSummary(...)`
  - removed handler-owned `CompletionPercent`
  - removed handler-owned `NetFlowStoryPoints`
  - removed handler-owned cumulative net flow, scope-delta, and trajectory formulas
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
  - removed handler-owned product delivery totals
  - removed handler-owned product contribution percentages
  - removed handler-owned feature contribution percentages

## New CDC Portfolio Results

- `PoTool.Core.Domain/Domain/Portfolio/PortfolioFlowSummaryService.cs`
  - `PortfolioFlowTrendRequest`
  - `PortfolioFlowProjectionInput`
  - `PortfolioFlowSummaryResult`
  - `PortfolioFlowTrendSummaryResult`
  - `PortfolioFlowTrendResult`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioDeliverySummaryResult.cs`
  - `PortfolioDeliverySummaryRequest`
  - `PortfolioDeliveryProductProjectionInput`
  - `PortfolioFeatureContributionInput`
  - `PortfolioDeliverySummaryResult`
  - `PortfolioProductDeliverySummaryResult`
  - `PortfolioFeatureContributionSummaryResult`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioDeliverySummaryService.cs`
  - `IPortfolioDeliverySummaryService`
  - `PortfolioDeliverySummaryService`

## Updated Handlers

- `GetPortfolioProgressTrendQueryHandler`
  - now loads products, sprints, and `PortfolioFlowProjectionEntity` rows
  - delegates portfolio rollups to `IPortfolioFlowSummaryService.BuildTrend(...)`
  - maps CDC-owned results to `PortfolioProgressTrendDto`
- `GetPortfolioDeliveryQueryHandler`
  - now loads products, sprints, `SprintMetricsProjectionEntity` rows, and feature progress
  - delegates portfolio delivery totals and shares to `IPortfolioDeliverySummaryService.BuildSummary(...)`
  - maps CDC-owned results to `PortfolioDeliveryDto`

## DTO Compatibility Decisions

- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
  - existing legacy aliases remain unchanged and continue to map to canonical story-point fields
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`
  - legacy `TotalCompletedEffort` and `CompletedEffort` names remain for compatibility
  - new canonical aliases (`TotalDeliveredStoryPoints`, `DeliveredStoryPoints`, `DeliveredSharePercent`) document the story-point meaning explicitly
  - `TotalCompletedBugs` is an alias over `TotalBugsClosed`

## Test Adjustments

- handler tests now verify CDC service invocation plus DTO mapping
- new CDC service tests verify:
  - completion percent
  - net flow
  - cumulative net flow
  - scope-change summary
  - trajectory
  - product delivery totals
  - product delivery shares
  - feature contribution shares
- existing DI and representative SQLite projection coverage were updated for the new service seams

## Lines of Code Removed

- removed approximately 100 lines of handler-owned rollup arithmetic across the two portfolio handlers
- the added code is concentrated in CDC-owned services, result records, and focused tests rather than duplicated in application handlers
