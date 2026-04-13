# Filter state initialization fix

## Root causes

- First-load pages could observe the route before shared defaults finished resolving, so page data gates saw an unresolved team/sprint state and stopped loading.
- Shared default resolution only updated navigation state indirectly, which left some first renders blocked until a manual reset or follow-up navigation occurred.
- Range defaults collapsed to a single sprint when a current sprint existed because the shared resolver did not also load the full sprint list for range pages.
- Shared filter summaries and canonical filter notices rendered raw team and sprint identifiers because readable labels were not carried through the shared mapping path.

## Changes made

### Client shared filter resolution

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterAutoResolveService.cs` to normalize unresolved shared filter state into a usable default before pages query data.
- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterStore.cs` to auto-resolve missing team/sprint state during shared route tracking and shared state updates, then warm shared labels before observers render.
- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterDefaultsService.cs` to reuse the same shared auto-resolution path and still push the resolved state back into the URL.
- Registered the new shared services in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Program.cs`.

### Shared display mapping

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterLabelService.cs` for shared team/sprint label caching used by shared filter summaries.
- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/FilterSummaryBar.razor` to render readable team and sprint labels from the shared label service.
- Extended shared canonical filter metadata in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Metrics/SprintFilterDtos.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Metrics/DeliveryFilterDtos.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Pipelines/PipelineFilterDtos.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/PullRequests/PullRequestFilterDtos.cs`
- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/CanonicalClientResponse.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/CanonicalClientResponseFactory.cs` so shared canonical notices render readable team and sprint names instead of raw IDs.
- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/CanonicalFilterLabelExtensions.cs` and updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GeneratedClientDtoMappings.cs` so generated client envelopes retain the new shared label metadata.

### Server shared filter metadata

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CanonicalFilterDisplayLabelLoader.cs`.
- Updated:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintFilterResolutionService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DeliveryFilterResolutionService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PipelineFilterResolutionService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PullRequestFilterResolutionService.cs`
- These shared resolution services now return canonical team/sprint label maps alongside requested/effective filters, invalid fields, and validation messages.

## Before vs after

### Before

- Sprint Trends and Sprint Delivery could start with an unresolved team/sprint state on first load.
- Page query gates could block the first load until the user manually reset time or retried.
- Range pages could default to an incomplete or narrow sprint selection.
- Shared filter summary chips and canonical filter notices showed raw IDs such as team IDs or `Sprint #701`.

### After

- Shared filter state is auto-resolved before first data queries run, so first load can proceed with a valid team/sprint default.
- Sprint-based pages receive deterministic defaults from the shared filter layer, including a multi-sprint window for range pages when sprint history is available.
- Invalid rolling selections are normalized through the same shared path without leaving the page in a silent invalid state.
- Shared filter summaries and canonical notices render readable team and sprint labels.

## Architectural implications

- The fix keeps filter correction and initialization in shared filter/service infrastructure rather than adding per-page exceptions.
- API contracts were extended in a backward-compatible way by adding label metadata to canonical filter envelopes; existing requested/effective filter payload shapes remain intact.
- The generated client layer now explicitly mirrors the additional canonical label metadata so shared UI formatting stays deterministic at render time.

## Validation

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~GlobalFilterDefaultsServiceTests|FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~CanonicalClientResponseFactoryTests|FullyQualifiedName~GeneratedCacheEnvelopeHelperTests|FullyQualifiedName~SprintFilterResolutionServiceTests|FullyQualifiedName~DeliveryFilterResolutionServiceTests|FullyQualifiedName~PipelineServiceTests" --logger "console;verbosity=minimal"`
