# Cache-backed generated client migration

## 1. Scope confirmation

Migrated services:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BuildQualityService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/MetricsStateService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/PipelineStateService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/PullRequestStateService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkItemService.cs` (cache-backed read paths only)
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ReleasePlanningService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProjectService.cs` (`GetPlanningSummaryAsync`)

Confirmation:

- This slice stayed in the client service layer only.
- No pages or components were refactored.
- UI-facing service signatures were preserved.

## 2. Migration summary

Before:

- direct `HttpClient`
- hard-coded `"/api/..."` routes
- `GetFromJsonAsync` / `PostAsJsonAsync` / `PutAsJsonAsync`
- per-service manual envelope reads or implicit unwrapping

After:

- NSwag-generated clients only for migrated cache-backed paths
- centralized generated-envelope conversion
- cache-backed read paths now convert generated `DataStateResponseDtoOf...` envelopes into shared response/result models

Generated clients used:

- `IBuildQualityClient`
- `IMetricsClient`
- `IPipelinesClient`
- `IPullRequestsClient`
- `IWorkItemsClient`
- `IReleasePlanningClient`
- `IProjectsClient`

## 3. Envelope handling approach

Approach used: shared helper.

Files:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GeneratedCacheEnvelopeHelper.cs`

What changed:

- added shared conversion from generated cache-backed envelopes to:
  - `CacheBackedClientResult<T>`
  - shared `DataStateResponseDto<T>`
- added JSON-based value conversion so generated NSwag wrapper/query DTOs can be projected back into shared DTO contracts without duplicating per-service mapping logic

Rationale:

- one place handles state, reason, retry metadata, and generated-to-shared DTO conversion
- avoids per-service envelope parsing
- keeps page-facing service contracts stable while removing raw `HttpClient`

## 4. State validation

Validated behaviors:

- `READY`
  - generated envelopes deserialize into shared payloads correctly
  - build quality and project planning summary continue to surface cache-backed success through `CacheBackedClientResult<T>`
- `NOT_READY`
  - metrics, pipeline, pull request, and work item state services preserve `NotReady` without crashing
- `FAILED`
  - failure state is preserved through the shared envelope conversion path
- `EMPTY`
  - empty state is preserved explicitly and not defaulted into success

Validation coverage:

- helper-level state conversion tests
- representative service tests for metrics, pipeline, pull requests, work items, release planning, build quality, and project planning summary
- governance audit for generated-client-only usage in migrated services

## 5. ProjectService completion

Confirmed:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProjectService.cs`
  - `GetPlanningSummaryAsync` now uses `IProjectsClient.GetPlanningSummaryAsync(...)`
  - raw `HttpClient` has been removed from `ProjectService`

`ProjectService` is now fully generated-client based.

## 6. Remaining violations

- None in the migrated cache-backed service-layer scope.
- `WorkItemService` intentionally retains direct `HttpClient` only for unchanged live/direct-TFS and mutation flows outside this slice.

## 7. Issues encountered

- generated NSwag cache envelopes use generated wrapper/query DTO types instead of the shared generic response types expected by existing pages
- release planning generated command/request types are class-based and required object-initializer construction
- portfolio read methods had existing handwritten client extensions that unwrap envelopes directly, so the migration had to call the generated nullable-enum overloads explicitly for state-preserving paths

No missing endpoints or contract mismatches blocked the migration.

## 8. Readiness

Stable after migration:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release` ✅
- targeted migration validation suite passed ✅
- generated-client governance and cache-backed contract audits passed ✅

System status:

- cache-backed services in scope now use generated clients
- shared envelope handling is centralized
- loading/error/empty/not-ready states are preserved instead of being silently flattened
