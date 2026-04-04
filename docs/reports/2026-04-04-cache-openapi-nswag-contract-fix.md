# Cache OpenAPI / NSwag Contract Fix

## 1. Executive summary

- **What was wrong**
  - Cache-backed routes were still documented as returning inner DTOs even though `CacheBackedDataStateContractFilter` wraps them at runtime as `DataStateResponseDto<T>`.
  - The governed OpenAPI snapshot and NSwag-generated client therefore exposed unsafe raw signatures for cache-backed families.
  - Three untyped work-item mutation endpoints were also emitted as `FileResponse`/`application/octet-stream` even though runtime behavior was plain 200/400/404/500 HTTP responses.
- **Was runtime behavior preserved?** Yes.
  - The existing filter-based cache-backed wrapping remains the runtime mechanism.
  - Controllers were not rewritten to manually wrap cache-backed success responses.
- **Is OpenAPI now truthful?** Yes for the cache-backed bug class addressed here.
  - Cache-backed routes now document `DataStateResponseDtoOf...` schemas and no longer advertise normalized 204/404/5xx responses.
- **Are generated clients now safe for cache-backed endpoints?** Yes.
  - Cache-backed generated methods now return wrapper-aware generated types instead of raw inner DTOs.
  - The previously incorrect `FileResponse` work-item mutation methods now generate plain `Task` methods.

## 2. OpenAPI changes

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/CacheBackedDataStateOpenApiOperationProcessor.cs`.
- Registered it in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`.
- The processor:
  - reuses `DataSourceModeConfiguration.RequiresCache(...)` to identify cache-backed routes;
  - reuses `SharedDtoActionResultContractResolver` to discover the declared inner payload type;
  - replaces 200-response schemas with generated `DataStateResponseDtoOf...` component references;
  - removes normalized 204/404/5xx responses for cache-backed routes.

### How cache-backed routes are represented now

- `GET /api/buildquality/rolling`
  - before: `DeliveryQueryResponseDto<BuildQualityPageDto>`
  - now: `#/components/schemas/DataStateResponseDtoOfDeliveryQueryResponseDtoOfBuildQualityPageDto`
- `GET /api/portfolio/progress`
  - before: `PortfolioProgressDto`
  - now: `#/components/schemas/DataStateResponseDtoOfPortfolioProgressDto`
- `GET /api/projects/{alias}/planning-summary`
  - before: `ProjectPlanningSummaryDto`
  - now: `#/components/schemas/DataStateResponseDtoOfProjectPlanningSummaryDto`
- `GET /api/workitems/backlog-state/{productId}`
  - before: `ProductBacklogStateDto`
  - now: `#/components/schemas/DataStateResponseDtoOfProductBacklogStateDto`
- `POST /api/filtering/by-validation-with-ancestors`
  - before: `FilterByValidationResponse`
  - now: `#/components/schemas/DataStateResponseDtoOfFilterByValidationResponse`

### Response-code clarifications

- Cache-backed routes now document the real 200-envelope behavior for:
  - `NotReady`
  - `Failed`
  - `Empty`
  - normalized `NotFound` / `NoContent` / 5xx cases
- Non-cache-backed work-item mutation endpoints were explicitly annotated so OpenAPI now reflects:
  - `RefreshFromTfs`: 200 / 404 / 500
  - `UpdateBacklogPriority`: 200 / 400 / 500
  - `UpdateIterationPath`: 200 / 400 / 500

## 3. NSwag/config changes

- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`
  - added `PoTool.Shared.DataState` to `additionalNamespaceUsages`
- Kept the governed snapshot/client flow intact:
  - snapshot: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
  - generated client: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- Extended `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs`
  - now also asserts the DataState namespace is available to generated output

### Wrapper-type handling changes

- NSwag now generates wrapper-aware cache-backed response types such as:
  - `DataStateResponseDtoOfDeliveryQueryResponseDtoOfBuildQualityPageDto`
  - `DataStateResponseDtoOfSprintQueryResponseDtoOfSprintMetricsDto`
  - `DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto`
  - `DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestMetricsDto`
  - `DataStateResponseDtoOfProjectPlanningSummaryDto`
  - `DataStateResponseDtoOfIEnumerableOfWorkItemWithValidationDto`

### Generation limitations that remain

- Closed generic query/filter envelope types are still generated as concrete NSwag classes rather than being mapped back to shared generic C# types.
- To keep callers safe, client-side code now consumes those generated envelopes through reflection-based helpers instead of assuming shared generic return types.

## 4. Generated client validation

### Endpoint families verified

- Build quality
- Metrics
- Pipelines
- Pull requests
- Projects planning summary
- Work items cache-backed reads
- Release planning
- Filtering

### Representative before vs after examples

- `IBuildQualityClient.GetRollingAsync`
  - before: `Task<DeliveryQueryResponseDto<BuildQualityPageDto>>`
  - after: `Task<DataStateResponseDtoOfDeliveryQueryResponseDtoOfBuildQualityPageDto>`
- `IMetricsClient.GetSprintMetricsAsync`
  - before: `Task<SprintQueryResponseDto<SprintMetricsDto>>`
  - after: `Task<DataStateResponseDtoOfSprintQueryResponseDtoOfSprintMetricsDto>`
- `IPipelinesClient.GetMetricsAsync`
  - before: `Task<PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>>`
  - after: `Task<DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto>`
- `IPullRequestsClient.GetMetricsAsync`
  - before: `Task<PullRequestQueryResponseDto<IReadOnlyList<PullRequestMetricsDto>>>`
  - after: `Task<DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestMetricsDto>`
- `IProjectsClient.GetPlanningSummaryAsync`
  - before: `Task<ProjectPlanningSummaryDto>`
  - after: `Task<DataStateResponseDtoOfProjectPlanningSummaryDto>`
- `IWorkItemsClient.GetAllWithValidationAsync`
  - before: `Task<ICollection<WorkItemWithValidationDto>>`
  - after: `Task<DataStateResponseDtoOfIEnumerableOfWorkItemWithValidationDto>`
- `IReleasePlanningClient.GetBoardAsync`
  - before: `Task<ReleasePlanningBoardDto>`
  - after: `Task<DataStateResponseDtoOfReleasePlanningBoardDto>`
- `IFilteringClient.FilterByValidationWithAncestorsAsync`
  - before: `Task<FilterByValidationResponse>`
  - after: `Task<DataStateResponseDtoOfFilterByValidationResponse>`

### Related client-side safety work

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GeneratedCacheEnvelopeHelper.cs`
- Updated direct cache-backed consumers and affected client helper files so they now unwrap generated cache envelopes safely instead of treating responses as raw DTOs.

### Remaining unsafe generated methods

- None found in the cache-backed families listed above.
- The previously unsafe work-item mutation methods were also corrected from `Task<FileResponse>` to `Task`.

## 5. Runtime preservation check

- `CacheBackedDataStateContractFilter` remains the runtime response shaper.
- `DataSourceModeConfiguration` remains the source of truth for cache-backed route classification.
- No second runtime response-wrapping mechanism was introduced.
- The OpenAPI fix is document-generation-only for cache-backed routes.
- The only runtime-adjacent controller adjustments were explicit response annotations on three non-cache-backed work-item mutation endpoints so their existing behavior is documented correctly.

## 6. Guardrails added

- Extended `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CacheBackedDataStateContractAuditTests.cs`
  - verifies cache-backed controller contracts still resolve to `DataStateResponseDto<>`
  - verifies the governed OpenAPI snapshot uses `DataStateResponseDto...` component refs
  - verifies normalized 204/404/5xx statuses do not reappear for cache-backed routes
  - verifies representative generated client signatures stay wrapper-aware
- Extended `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs`
  - verifies `PoTool.Shared.DataState` remains part of the governed NSwag config

### Future drift now prevented

- Cache-backed routes silently regressing to raw inner OpenAPI contracts
- Generated clients silently regressing to raw inner return types for key cache-backed families
- Work-item mutation routes silently regressing back to `FileResponse` generation

## 7. Remaining follow-ups

- No contract-layer follow-up is required for the cache-backed bug class fixed here.
- There is an unrelated pre-existing documentation-anchor governance failure outside this change set (`DocumentationVerification_AllMarkdownLinksAndAnchorsResolve`).
