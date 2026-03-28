# Pipeline Run Truncation Corrective Fix

## Summary

This fix corrects the Pipeline slice data-integrity bug where cached runs were previously limited with a single global top-N before final effective scope filtering.

What changed:

- `CachedPipelineReadProvider.GetRunsForPipelinesAsync(...)` now selects cached runs **per pipeline definition** instead of taking one global top-N across all requested pipelines
- the method still applies its acquisition-boundary filters first (`branchName`, `minStartTime`)
- combined results are ordered after per-pipeline selection, preserving deterministic downstream behavior
- focused regression tests were added for:
  - uneven activity across two pipelines
  - branch-compatible acquisition behavior
  - runs-handler completeness
  - metrics-handler completeness

Why:

- a very active pipeline could previously fill the global top-N window
- quieter pipelines with valid in-scope runs could therefore disappear from metrics/runs results
- this caused undercounting and incomplete endpoint payloads even though the effective filter itself was correct

---

## Root Cause

The previous cached provider implementation queried all requested pipeline runs together and then applied:

```csharp
query
    .OrderByDescending(r => r.CreatedDateUtc)
    .Take(top)
```

That meant:

1. all requested pipelines competed for the same top-N window
2. later effective scope filtering (`PipelineFiltering.ApplyRunScope(...)`) happened only **after** that truncation
3. if one pipeline had many recent runs, older but still in-scope runs from another pipeline were never returned to the handler

This was a correctness bug because the returned data depended on global cross-pipeline ordering rather than final effective scope completeness.

---

## Affected Files

### Production code

- `PoTool.Api/Services/CachedPipelineReadProvider.cs`

### Tests

- `PoTool.Tests.Unit/Services/CachedPipelineReadProviderSqliteTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineRunsForProductsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineMetricsQueryHandlerTests.cs`

### Documentation

- `docs/filters/filter-pipeline-truncation-fix.md`
- `docs/release-notes.json`

---

## Before vs After

### Before

- cached provider loaded runs for all requested pipelines together
- the provider applied one global `OrderByDescending(...).Take(top)`
- handlers then applied final branch/default-branch/pipeline scope via `PipelineFiltering.ApplyRunScope(...)`
- quieter pipelines could be truncated out before final scope filtering

### After

- cached provider resolves requested pipeline definitions first
- for each in-scope pipeline definition, it applies the acquisition-boundary filters and selects up to `top` cached runs **for that pipeline**
- all selected per-pipeline results are combined and ordered deterministically
- handlers still consume the same `EffectiveFilter`
- final canonical filtering remains unchanged and deterministic

In short:

- **before:** global top-N across all pipelines
- **after:** top-N per pipeline, then combine

---

## Validation

Correctness was validated with:

- solution build in Release mode
- focused pipeline regression suite including the new truncation tests

Validation commands:

```bash
dotnet build PoTool.sln --configuration Release --no-restore
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~CachedPipelineReadProviderSqliteTests|FullyQualifiedName~GetPipelineRunsForProductsQueryHandlerTests|FullyQualifiedName~GetPipelineMetricsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsBreakdownTests|FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~BuildQualityQueryHandlerTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal
```

Observed result:

- build succeeded
- **52 / 52** focused tests passed

New regression coverage proves:

- a busy pipeline no longer hides a quiet pipeline’s valid runs
- branch-filtered acquisition still behaves correctly with per-pipeline limiting
- the runs handler returns all final in-scope runs
- the metrics handler still produces entries for quieter pipelines when valid runs exist

---

## Known Limitations

- the fix is intentionally limited to the cached multi-pipeline acquisition path
- live-provider behavior and canonical filter contracts were left unchanged
- final branch/default-branch filtering still happens where it already lived; this fix only removes the premature global truncation bug
- the approach uses sequential per-pipeline queries for correctness and minimal scope; it does not attempt a larger query-shape optimization
