# Failure Classification Normalization — Build Quality vs Pipeline Insights

## Summary

Failure classification is now normalized across Build Quality and Pipeline Insights through one shared canonical analytical outcome classifier.

The normalization rule is:

- both slices first normalize raw build/run result strings into canonical analytical outcomes
- slice-specific metrics are applied only after that normalization step

The canonical analytical categories are:

- `Succeeded`
- `Failed`
- `Warning`
- `Canceled`
- `Unknown`
- `Ignored`

This resolves the next semantic blocker identified in cross-slice validation:

- Build Quality previously interpreted raw result strings directly with fixed rules
- Pipeline Insights previously interpreted raw result strings directly with toggle-driven rules

After this change:

- both slices normalize `PartiallySucceeded` to `Warning`
- both slices normalize `Canceled` to `Canceled`
- both slices normalize missing, `Unknown`, `None`, and unrecognized states to `Unknown`
- excluded states are represented consciously as `Ignored` only after canonical normalization

This preserves intentional metric differences while removing silent disagreement about what raw outcomes mean.

## Current Classification Inventory

### Before normalization

| Location | Raw states handled | Mapping before | Fixed or toggle-driven | Denominator decision |
| --- | --- | --- | --- | --- |
| `PoTool.Api/Services/BuildQuality/BuildQualityProvider.cs` | `Succeeded`, `Failed`, `PartiallySucceeded`, `Canceled`; everything else fell through implicitly | `Succeeded`→success, `Failed`→failure, `PartiallySucceeded`→eligible partial-success bucket, `Canceled`→tracked but excluded, unknown/missing not counted | Fixed | Decided here: eligible denominator = `Succeeded + Failed + PartiallySucceeded` |
| `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | `Succeeded`, `Failed`, `PartiallySucceeded`, `Canceled`, `Unknown`, `None`, null/empty | `Succeeded`→completed success, `Failed`→completed failure, `PartiallySucceeded`→warning only when toggle enabled, `Canceled`→completed only when toggle enabled, `Unknown`/`None`/missing skipped | Toggle-driven | Decided here: completed denominator changed with `IncludePartiallySucceeded` and `IncludeCanceled` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs` | raw Azure DevOps build/release states | Parsed to `PipelineRunResult` transport enum | Fixed parsing, not analytical | Not decided here |
| `PoTool.Api/Services/CachedPipelineReadProvider.cs` | cached string states | Parsed to `PipelineRunResult` transport enum | Fixed parsing, not analytical | Not decided here |

### Observed mismatch before normalization

1. **`PartiallySucceeded`**
   - Build Quality treated it as always eligible
   - Pipeline Insights treated it as eligible only when the toggle was enabled
   - Neither slice shared a canonical intermediate meaning

2. **`Canceled`**
   - Build Quality always tracked it separately and always excluded it from the eligible denominator
   - Pipeline Insights optionally included it in the completed denominator
   - Again, no shared canonical intermediate meaning existed

3. **Unknown or missing states**
   - Build Quality mostly collapsed them implicitly by string mismatch
   - Pipeline Insights skipped them explicitly for some known tokens
   - Unrecognized states were not normalized through one shared rule

### After normalization

| Location | Raw states handled | Mapping after | Fixed or toggle-driven | Denominator decision |
| --- | --- | --- | --- | --- |
| `PoTool.Core/Pipelines/Analytics/PipelineAnalyticalOutcomeClassifier.cs` | null, empty, `Unknown`, `None`, unrecognized values, plus known result strings | raw result → canonical analytical outcome | Fixed canonical normalization | Not decided here |
| `PoTool.Api/Services/BuildQuality/BuildQualityProvider.cs` | canonical outcomes from shared classifier | `Succeeded`, `Failed`, `Warning`, `Canceled`, `Unknown` | Fixed | Decided after normalization: eligible denominator = `Succeeded + Failed + Warning` |
| `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | canonical outcomes from shared classifier, then inclusion rules | `Warning` or `Canceled` may become `Ignored` based on toggles | Toggle-driven after normalization | Decided after normalization: completed denominator uses post-normalization included outcomes |

## Canonical Classification Matrix

### Final normalized categories

| Raw result | Canonical analytical outcome |
| --- | --- |
| `Succeeded` | `Succeeded` |
| `Failed` | `Failed` |
| `PartiallySucceeded` | `Warning` |
| `Canceled` | `Canceled` |
| `Unknown` | `Unknown` |
| `None` | `Unknown` |
| missing / null / empty | `Unknown` |
| any unrecognized value | `Unknown` |

### Post-normalization inclusion rules

`Ignored` is not a raw-result category. It is a post-normalization analytical state used when a slice chooses to exclude an already normalized canonical outcome from a specific metric.

#### Build Quality

- `Succeeded` → included
- `Failed` → included
- `Warning` → included
- `Canceled` → excluded from eligible denominator
- `Unknown` → excluded from eligible denominator

#### Pipeline Insights

- `Succeeded` → included
- `Failed` → included
- `Warning` → included when `IncludePartiallySucceeded=true`, otherwise `Ignored`
- `Canceled` → included when `IncludeCanceled=true`, otherwise `Ignored`
- `Unknown` → excluded from completed metrics

This means both slices now start from the same interpretation of raw data even when their formulas still differ.

## Code Changes

### 1. Added shared canonical classifier

Added:

- `PoTool.Core/Pipelines/Analytics/PipelineAnalyticalOutcomeClassifier.cs`

This file introduces:

- `PipelineAnalyticalOutcome`
- `PipelineAnalyticalOutcomeClassifier.Normalize(string? rawResult)`
- `PipelineAnalyticalOutcomeClassifier.ApplyMetricInclusion(...)`

This is the single canonical normalization layer shared by both slices.

### 2. Updated Build Quality to use canonical normalization first

Updated:

- `PoTool.Api/Services/BuildQuality/BuildQualityProvider.cs`

Build Quality now:

1. normalizes each raw build result via the shared classifier
2. counts canonical outcomes
3. applies its existing fixed formula

Intentional preserved behavior:

- `Warning` contributes to eligible builds
- `Canceled` remains tracked but excluded from the eligible denominator
- unknown and missing states remain non-eligible

### 3. Updated Pipeline Insights to use canonical normalization first

Updated:

- `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`

Pipeline Insights now:

1. normalizes each raw run result via the shared classifier
2. applies toggle-driven inclusion rules after normalization
3. derives completed/failed/warning/succeeded counts from canonical outcomes

Intentional preserved behavior:

- `IncludePartiallySucceeded` still controls whether canonical `Warning` contributes to metrics
- `IncludeCanceled` still controls whether canonical `Canceled` contributes to the completed denominator
- unknown and missing states no longer disappear by ad hoc string checks; they normalize to `Unknown` first

### 4. Updated tests

Added or updated:

- `PoTool.Tests.Unit/Pipelines/PipelineAnalyticalOutcomeClassifierTests.cs`
- `PoTool.Tests.Unit/Services/BuildQualityProviderTests.cs`
- `PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs`

The tests now verify:

- `PartiallySucceeded` normalizes to `Warning`
- `Canceled` normalizes to `Canceled`
- null, `Unknown`, `None`, and unrecognized states normalize predictably
- Build Quality excludes unknown/missing states from eligible-build metrics
- Pipeline Insights ignores unknown/missing states only after canonical normalization

## Slice-Specific Behavior After Normalization

Failure classification is now normalized, but metric formulas still differ intentionally.

### Build Quality

Still intentionally:

- fixed semantics
- no UI toggles
- eligible denominator = `Succeeded + Failed + Warning`
- `Canceled` excluded from denominator
- unknown/missing states excluded from denominator

### Pipeline Insights

Still intentionally:

- toggle-driven presentation and denominator behavior
- `Warning` may be included or ignored based on `IncludePartiallySucceeded`
- `Canceled` may be included or ignored based on `IncludeCanceled`
- unknown/missing states are not shown as separate product metrics today

### What no longer differs

What raw states *mean* is now shared:

- `PartiallySucceeded` always means canonical `Warning`
- `Canceled` always means canonical `Canceled`
- missing or unrecognized states always mean canonical `Unknown`

## Validation

### Workflow/build status

Checked recent GitHub Actions runs for branch `copilot/introduce-persistence-abstraction`.

Observed status at validation time:

- latest run in progress
- recent completed runs successful
- no recent failed branch run required failure-log inspection

### Commands run

Baseline:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Focused classification tests:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PipelineAnalyticalOutcomeClassifierTests|FullyQualifiedName~BuildQualityProviderTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests" -v minimal`

Full validation:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

### Result

The focused classification tests passed after the shared normalizer was introduced and the slice-specific tests were updated.

Full validated test projects also passed after the change.

## Security Summary

- No new dependencies were added.
- No secrets or credentials were introduced.
- The change is limited to analytical classification, tests, and documentation.
- Persistence schema, sync logic, repository identity, and time-window semantics were not changed.

## Remaining Semantic Gaps

This prompt resolves only the cross-slice failure-classification blocker.

Remaining larger alignment gaps still include:

- repository identity semantics
- product scoping differences
- time-window semantics and inclusion boundaries
- any future need to expose canonical unknown/ignored counts in public DTOs

Those remain intentionally out of scope for this change.

## Final Status

**Failure classification is now normalized across Build Quality and Pipeline Insights: yes**

Both slices now use one shared canonical normalization layer before applying slice-specific analytical formulas.
