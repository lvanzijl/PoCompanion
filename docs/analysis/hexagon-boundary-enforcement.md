# Hexagon Boundary Enforcement

_Generated: 2026-03-17_

Reference documents:

- `docs/architecture/cdc-reference.md`
- `docs/architecture/cdc-domain-map.md`
- `docs/analysis/application-handler-cleanup.md`
- `docs/analysis/application-simplification-audit.md`

## Architecture rules

- CDC/domain remains the single owner of analytics semantics.
- In this repository, the application-handler seam is the `PoTool.Api/Handlers/**` layer plus projection materialization services that adapt CDC outputs for persistence or transport.
- Allowed dependency direction is:

  Infrastructure (TFS adapters, DB projections)  
  ↓  
  Application handlers and adapters  
  ↓  
  CDC / Domain

- No reverse semantic flow is allowed from handlers, persistence, or UI back into `PoTool.Core.Domain`.

## Allowed dependencies

Handlers may load repositories, EF projections, and DTO inputs, but canonical semantics must be delegated through CDC-owned seams such as:

- `IBacklogQualityAnalysisService`
- `ISprintFactService`
- `IPortfolioFlowSummaryService`
- `IPortfolioDeliverySummaryService`

Current boundary-protected examples:

- `PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`

These handlers may orchestrate loading and DTO mapping, but domain outputs such as `SprintFactResult`, portfolio flow trend summaries, and backlog quality analysis stay CDC-owned.

## Forbidden dependencies

- `PoTool.Core.Domain` must not reference `PoTool.Api`, `PoTool.Client`, `PoTool.Infrastructure`, or adapter namespaces such as `PoTool.Integrations.*`.
- CDC/domain files must not import API persistence or client namespaces.
- Handlers must not reintroduce story-point rollup helpers such as `SumStoryPoints(...)`, `SumDeliveredStoryPoints(...)`, or `ResolveSprintStoryPoints(...)`.
- Handlers must not recompute `CompletionPercent` or `NetFlowStoryPoints` inline; those semantics belong to CDC outputs such as `IPortfolioFlowSummaryService`.
- Handlers must not re-derive delivery shares or sprint fact rates when the CDC already exposes those semantics through `ISprintFactService` or `IPortfolioDeliverySummaryService`.

Representative violations that should fail the boundary tests:

- `CompletionPercent = delivered / total`
- `NetFlowStoryPoints = inflow - throughput`
- `EffortShare = delivered / totalDelivered`

## Enforcement tests

The automated boundary guardrails live in:

- `PoTool.Tests.Unit/Architecture/HexagonBoundaryTests.cs`

Those tests enforce four checks:

1. `PoTool.Core.Domain` assembly references do not point outward to API, client, or infrastructure layers.
2. CDC/domain source files do not import forbidden namespaces.
3. CDC-backed handlers continue to reference canonical services like `IBacklogQualityAnalysisService`, `ISprintFactService`, and `IPortfolioFlowSummaryService`.
4. Handler files fail with CDC ownership guidance if they introduce story point rollups, completion percentage calculations, or flow calculations inline.

Failure guidance is intentional: move semantic math into CDC ownership instead of adding new handler-local helpers.
