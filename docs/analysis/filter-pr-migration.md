# PR Slice Canonical Filter Migration

## Summary

The Pull Request slice now resolves filter input once at the API boundary and passes only an effective canonical filter downstream.

Implemented changes:

- added `PullRequestFilterResolutionService` to normalize requested PR scope into a deterministic `PullRequestEffectiveFilter`
- updated PR controllers to resolve effective filter metadata before calling Mediator
- removed handler-local team/product/repository/time interpretation in PR query handlers
- standardized PR repository scoping through effective repository scope plus effective product scope
- removed backend implicit "last 6 months" defaults from PR metrics, insights, and delivery insights
- wrapped PR endpoint responses in filter metadata envelopes containing:
  - `RequestedFilter`
  - `EffectiveFilter`
  - `InvalidFields`
  - `ValidationMessages`
- updated client-side PR calls to use handcrafted envelope-aware API methods instead of the old list/object deserializers

Why:

- PR filtering semantics were previously split across controller defaults and handler-local resolution
- different PR endpoints could interpret team/product/repository/time differently
- the PR slice is the first non-portfolio slice migrated to the canonical model, so it now establishes the cross-slice implementation pattern

## Affected Files

### Controllers

- `PoTool.Api/Controllers/PullRequestsController.cs`

### Filter resolution / shared PR filtering

- `PoTool.Api/Services/PullRequestFilterResolutionService.cs`
- `PoTool.Api/Services/PullRequestFiltering.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

### PR handlers

- `PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs`
- `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs`
- `PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs`
- `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs`

### PR query contracts / filter models

- `PoTool.Core/PullRequests/Filters/PullRequestFilterModels.cs`
- `PoTool.Core/PullRequests/Queries/GetPullRequestMetricsQuery.cs`
- `PoTool.Core/PullRequests/Queries/GetFilteredPullRequestsQuery.cs`
- `PoTool.Core/PullRequests/Queries/GetPrSprintTrendsQuery.cs`
- `PoTool.Core/PullRequests/Queries/GetPullRequestInsightsQuery.cs`
- `PoTool.Core/PullRequests/Queries/GetPrDeliveryInsightsQuery.cs`
- `PoTool.Core/Contracts/IPullRequestReadProvider.cs`

### PR read providers

- `PoTool.Api/Services/CachedPullRequestReadProvider.cs`
- `PoTool.Api/Services/LivePullRequestReadProvider.cs`
- `PoTool.Api/Services/LazyPullRequestReadProvider.cs`

### Shared/client DTO and API client updates

- `PoTool.Shared/PullRequests/PullRequestFilterDtos.cs`
- `PoTool.Client/ApiClient/ApiClient.PullRequestFilters.cs`
- `PoTool.Client/Services/PullRequestService.cs`
- `PoTool.Client/Services/WorkspaceSignalService.cs`
- `PoTool.Client/Pages/Home/PrOverview.razor`
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `PoTool.Client/Components/Flow/FlowPanel.razor`

### Tests

- `PoTool.Tests.Unit/Services/PullRequestFilterResolutionServiceTests.cs`
- `PoTool.Tests.Unit/Controllers/PullRequestsControllerCanonicalFilterTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPullRequestMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPullRequestInsightsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPrDeliveryInsightsQueryHandlerTests.cs`

## Before vs After

| Concern | Before | After |
| --- | --- | --- |
| Product scope | Parsed in controller, interpreted again in handlers/providers. | Resolved once into effective product scope at the API boundary. |
| Team scope | Handlers derived team → products locally, differently per endpoint. | Controller resolves team selection once into effective product/team scope. |
| Repository scope | Some handlers derived repositories locally, others ignored repository scope until later filtering. | Effective filter contains repository scope and downstream code consumes only that scope. |
| Time scope | Metrics/insights/delivery endpoints applied local defaults such as last 6 months. | No implicit backend date defaults; handlers use only the resolved effective time range. |
| Response metadata | PR responses did not expose canonical filter metadata. | All migrated PR endpoints now return canonical filter metadata envelopes. |
| List endpoints | Metrics/filter returned bare lists. | Metrics/filter now return envelope responses with list data plus canonical filter metadata. |

## Validation

Correctness was ensured by:

- compiling the full solution in Release mode
- running focused PR-slice tests covering:
  - PR metrics handler behavior
  - PR insights handler behavior
  - PR delivery insights handler behavior
  - new pull-request filter resolution service behavior
  - new controller envelope behavior

Validation command:

```bash
dotnet build PoTool.sln --configuration Release --no-restore
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~GetPullRequestMetricsQueryHandlerTests|FullyQualifiedName~GetPullRequestInsightsQueryHandlerTests|FullyQualifiedName~GetPrDeliveryInsightsQueryHandlerTests|FullyQualifiedName~PullRequestFilterResolutionServiceTests|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests" -v minimal
```

## Known Limitations

- The checked-in generated `ApiClient.g.cs` was intentionally not regenerated; the PR slice uses new handcrafted envelope-aware API client methods instead.
- PR overview pages still choose explicit date values in the UI when users leave the controls untouched; the migration removed backend implicit defaults, not those page-level UX defaults.
- The review-bottleneck endpoint was out of scope and remains unchanged because it is not part of the canonical PR query family targeted by this migration.
