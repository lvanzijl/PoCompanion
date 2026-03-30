# Pipeline Slice Canonical Filter Migration

## Summary

The Pipeline slice now resolves shared filter scope once at the API boundary and passes only a deterministic effective filter downstream.

Implemented changes:

- added `PipelineFilterResolutionService` to normalize requested pipeline scope into a canonical `PipelineEffectiveFilter`
- updated migrated pipeline endpoints to resolve filter metadata before dispatching queries
- wrapped migrated responses in envelopes containing:
  - `RequestedFilter`
  - `EffectiveFilter`
  - `InvalidFields`
  - `ValidationMessages`
- removed handler-local product, repository, branch, and implicit time fallback logic from pipeline metrics and runs queries
- moved sprint-to-window derivation for pipeline insights to the controller boundary resolver
- updated the pipeline client/service path to read envelope responses without changing UI behavior

Why:

- pipeline filtering semantics were previously split across controller parsing and handler-local assumptions
- metrics and runs hardcoded `refs/heads/main` and a local last-6-month window
- insights derived product scope locally from `productOwnerId` instead of consuming canonical scope
- different pipeline endpoints could not reliably explain requested versus effective scope

## Affected Files

### Controllers

- `PoTool.Api/Controllers/PipelinesController.cs`

### Filter resolution / shared pipeline filtering

- `PoTool.Api/Services/PipelineFilterResolutionService.cs`
- `PoTool.Api/Services/PipelineFiltering.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

### Pipeline handlers

- `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs`
- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

### Pipeline query contracts / filter models

- `PoTool.Core/Pipelines/Filters/PipelineFilterModels.cs`
- `PoTool.Core/Pipelines/Queries/GetPipelineMetricsQuery.cs`
- `PoTool.Core/Pipelines/Queries/GetPipelineRunsForProductsQuery.cs`
- `PoTool.Core/Pipelines/Queries/GetPipelineInsightsQuery.cs`

### Shared/client DTO and API client updates

- `PoTool.Shared/Pipelines/PipelineFilterDtos.cs`
- `PoTool.Client/ApiClient/ApiClient.PipelineFilters.cs`
- `PoTool.Client/Services/PipelineService.cs`
- `PoTool.Client/Pages/Home/PipelineInsights.razor`

### Tests

- `PoTool.Tests.Unit/Services/PipelineFilterResolutionServiceTests.cs`
- `PoTool.Tests.Unit/Controllers/PipelinesControllerCanonicalFilterTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Services/PipelineServiceTests.cs`

## Before vs After

### Before

- metrics and runs parsed raw `productIds` in the controller and interpreted shared scope again inside handlers
- metrics and runs hardcoded `refs/heads/main`
- metrics and runs defaulted to a hidden last-6-month time window
- insights used raw `productOwnerId` and `sprintId` inside the handler to derive scope
- migrated endpoints returned raw payloads with no filter metadata

### After

- controllers resolve requested pipeline scope once and pass `PipelineEffectiveFilter` to handlers
- handlers consume only effective product, repository, and time scope for shared filtering semantics
- metrics and runs use resolved pipeline scope plus per-pipeline default-branch filtering instead of a hardcoded main branch
- insights consume effective product scope and resolved sprint window from the boundary resolver
- migrated endpoints consistently return requested/effective filter metadata alongside payload data

## Validation

Correctness was ensured by:

- compiling the full solution in Release mode
- running focused Pipeline tests covering:
  - pipeline metrics handler behavior
  - pipeline insights handler behavior
  - pipeline breakdown/scatter behavior
  - new pipeline filter resolution service behavior
  - new controller envelope behavior
  - pipeline client service envelope behavior

Validation command:

```bash
dotnet build PoTool.sln --configuration Release
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~GetPipelineMetricsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsBreakdownTests|FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~BuildQualityQueryHandlerTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal
```

An unrelated pre-existing build-quality audit/document test was already failing before this migration and was excluded from the focused validation set.

## Known Limitations

- branch/default-branch handling remains slice-local metadata, not a shared canonical filter input
- when a pipeline definition has no stored default branch, pipeline metrics and runs keep the existing fallback of including that pipeline's runs
- the single-pipeline `GET /api/Pipelines/{id}/runs` and definitions endpoint were left unchanged because they are not part of the migrated shared-scope path
