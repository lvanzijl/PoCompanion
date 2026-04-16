# Trend filter mapping fix

## Root cause analysis

- The shared global filter store correctly preserved `Time.Mode = Range` with `StartSprintId` and `EndSprintId`.
- Trend request construction did not have a shared translation layer for that range state.
- Delivery Trends, Portfolio Delivery, Portfolio Flow Trend, and the Trends workspace PR trend signal each translated the range independently, while shared trend signal loading still ignored the requested range and preferred recent sprints.
- Because the range-to-request mapping was duplicated and incomplete, trend calls could lose the selected sprint window before reaching backend trend endpoints.
- Backend filter resolution also lacked an explicit guard for malformed multi-sprint requests where an empty `SprintIds` list was passed without any range boundaries.

## Affected endpoints and pages

### Pages

- `/home/trends`
- `/home/trends/delivery`
- `/home/delivery/portfolio`
- `/home/portfolio-progress`

### Endpoints

- `/api/Metrics/sprint-trend`
- `/api/Metrics/portfolio-delivery`
- `/api/Metrics/portfolio-progress-trend`
- `/api/PullRequests/sprint-trends`

## Before vs after request examples

### Before

- Global filter state:
  - `TeamId = 10`
  - `Time.Mode = Range`
  - `StartSprintId = 45`
  - `EndSprintId = 49`
- Request builders depended on page-local translation or recent-sprint fallback.
- Shared trend reads could drift away from the selected range or fail when the local translation path did not supply a valid sprint list.

### After

- Global filter state:
  - `TeamId = 10`
  - `Time.Mode = Range`
  - `StartSprintId = 45`
  - `EndSprintId = 49`
- Shared mapper output:
  - `SprintIds = [45,46,47,48,49]`
  - `RangeStartUtc = first sprint start`
  - `RangeEndUtc = last sprint end`
- Trend requests now use the shared resolved sprint scope consistently before calling the backend.

## Implemented fix

- Added a shared client-side mapper: `PoTool.Client/Helpers/GlobalFilterTrendRequestMapper.cs`
- Added a shared request model: `PoTool.Client/Models/TrendSprintRangeRequest.cs`
- Updated shared client state services to accept the shared range request model instead of raw page-built sprint lists for:
  - sprint trend metrics
  - portfolio delivery
  - portfolio progress trend
  - PR sprint trends
- Updated range-based trend pages and the Trends workspace PR trend signal to use the shared mapper.
- Updated `WorkspaceSignalService` trend loading to honor the requested global sprint range instead of silently preferring recent sprints when the active filter is range-based.
- Hardened backend sprint, delivery, and pull-request filter resolution services so explicitly empty multi-sprint input without range boundaries is marked invalid and logged with requested/effective time diagnostics.

## Validation results per page

### Automated coverage

- `GlobalFilterTrendRequestMapperTests`
  - verifies inclusive sprint-id resolution and boundary timestamps
  - verifies unresolved output when the selected range cannot be mapped
- `TrendFilterRangeMappingTests`
  - verifies shared trend signal loading uses the requested range sprint ids
  - verifies backend sprint, delivery, and PR filter resolution rejects empty explicit multi-sprint input
- Existing canonical filter and global filter tests still pass

### Commands run

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~GlobalFilterTrendRequestMapperTests|FullyQualifiedName~TrendFilterRangeMappingTests|FullyQualifiedName~MetricsControllerSprintCanonicalFilterTests|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~GlobalFilterContextResolverTests|FullyQualifiedName~GlobalFilterRouteServiceTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~SprintFilterResolutionServiceTests|FullyQualifiedName~DeliveryFilterResolutionServiceTests|FullyQualifiedName~PullRequestFilterResolutionServiceTests" --logger "console;verbosity=minimal"`

### Manual browser validation

- Not executed in this sandbox session.
- Remaining manual check: open the affected pages with a known multi-sprint selection and confirm backend requests carry the expected sprint ids.

## Contract changes and assumptions

- No public API contract changes were introduced.
- Trend endpoints still receive sprint-based scope.
- The shared client mapper assumes the selected team sprint list is available and contains the requested boundary sprints for range-mode trend pages.

## Remaining risks and follow-up improvements

- Range-aware mapping is now shared for the affected client trend paths, but additional range-capable analytical flows should adopt the same mapper if they start consuming `StartSprintId`/`EndSprintId`.
- Manual browser verification is still recommended against representative seeded data to confirm each page renders populated data instead of the previous unavailable state.
