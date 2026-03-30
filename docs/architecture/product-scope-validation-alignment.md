# Product Scope Validation Alignment — Build Quality vs Pipeline Insights

## Summary

Product scope validation is now aligned across Build Quality and Pipeline Insights.

The aligned rule is:

- explicit product IDs must be validated against Product Owner ownership when a Product Owner scope is present
- out-of-scope or invalid explicit selections must not silently pass through
- fallback behavior and validation reporting must match Build Quality semantics

Before this change:

- Build Quality validated explicit product IDs against PO ownership
- Pipeline Insights only normalized explicit product IDs and did not enforce the same owner-scope fallback

That created inconsistent behavior and a potential cross-product scope leak.

After this change:

- Build Quality and Pipeline Insights both resolve `All` to the full owned product set
- explicit out-of-scope or mixed product selections fall back to the full owned product set
- invalid explicit input records validation issues instead of being silently accepted
- pipeline validation reporting continues to flow through the existing canonical filter response metadata

## Current Behavior Comparison

### Build Quality

Reference implementation:

- `PoTool.Api/Services/DeliveryFilterResolutionService.cs`

Observed behavior:

- loads the owned product set for the requested Product Owner
- resolves `All` to all owned products
- normalizes explicit IDs
- when any explicit ID is outside owner scope, replaces the entire product selection with the full owned set
- records a validation issue on `ProductIds`
- when explicit input is invalid/empty after normalization, falls back and records a validation issue

Validation reporting:

- exposed through `DeliveryFilterResolution.Validation`
- surfaced to API consumers via `DeliveryQueryResponseDto.InvalidFields` and `ValidationMessages`

### Pipeline Insights before alignment

Implementation before this change:

- `PoTool.Api/Services/PipelineFilterResolutionService.cs`

Observed behavior before:

- `All` resolved to all owned products when a Product Owner scope was present
- explicit product IDs were only normalized to positive distinct IDs
- no validation against PO ownership was performed for explicit IDs
- out-of-scope explicit product IDs could pass through into effective pipeline scope
- no equivalent validation issue was recorded for those out-of-scope explicit product selections

Validation reporting before:

- pipeline responses already had canonical reporting fields
  - `PipelineQueryResponseDto.InvalidFields`
  - `PipelineQueryResponseDto.ValidationMessages`
- but product scope mismatches were not being emitted consistently because explicit product ownership validation was missing

### Key difference

The inconsistency was not in reporting infrastructure. It was in resolution policy:

- Build Quality enforced owner scope on explicit product IDs
- Pipeline Insights did not

## Canonical Validation Rules

The unified behavior is now:

### 1. Requested product scope = `All`

- if a Product Owner scope exists: resolve to all owned product IDs
- otherwise: remain `All`

### 2. Requested product scope = explicit IDs

- normalize to positive distinct IDs
- if no Product Owner scope exists: keep the normalized explicit IDs
- if a Product Owner scope exists:
  - validate every explicit ID against the owned product set
  - if all are valid: keep them unchanged
  - if any are out-of-scope: replace the entire effective selection with the full owned product set
  - record a validation issue on `ProductIds`

### 3. Empty or invalid explicit input

- treat the request as invalid explicit selection, not as `All`
- resolve to the full owned product set when a Product Owner scope exists
- otherwise fall back to `All`
- record a validation issue on `ProductIds`

This is now the effective contract for the pipeline slice and matches Build Quality semantics for product ownership enforcement.

## Code Changes

### 1. Pipeline filter resolution now loads owner products first

Updated:

- `PoTool.Api/Services/PipelineFilterResolutionService.cs`

Changes:

- added `LoadOwnerProductIdsAsync(...)`
- replaced the old `ResolveProductIdsAsync(...)` normalization-only flow with a Build Quality-style `ResolveProductIds(...)`
- reused the same semantic branches as `DeliveryFilterResolutionService`

Behavior now matches Build Quality for:

- `All`
- explicit valid selections
- explicit invalid selections
- explicit mixed valid/out-of-scope selections

### 2. Explicit empty product selections are no longer silently converted to `All`

Updated:

- `PoTool.Api/Services/PipelineFilterResolutionService.cs`

Changes:

- `ToIntSelection(...)` now preserves an explicitly supplied empty product list as an explicit selection instead of converting it to `All`

Why this matters:

- without this change, `ProductIds: []` was indistinguishable from no product filter
- that made it impossible to apply the canonical “empty explicit input triggers fallback + validation” behavior

### 3. No new reporting mechanism was introduced

Unchanged design:

- `PipelineFilterResolution.Validation`
- `PipelineFilterResolutionService.ToResponse(...)`
- `PipelineQueryResponseDto.InvalidFields`
- `PipelineQueryResponseDto.ValidationMessages`

Pipeline Insights now uses the existing reporting path, but product-scope validation now feeds it correctly.

### 4. Controllers and handlers

No controller or handler redesign was required.

Reason:

- the inconsistency lived in the pipeline filter resolution policy
- existing controllers and handlers already consume `PipelineFilterResolutionService`
- once filter resolution is corrected, the pipeline slice inherits the aligned behavior without new public APIs or new abstractions

### 5. Tests updated

Updated:

- `PoTool.Tests.Unit/Services/PipelineFilterResolutionServiceTests.cs`

Added coverage for:

- explicit valid product IDs remain unchanged
- explicit out-of-scope product IDs fall back to all owner products
- mixed valid/out-of-scope selections fall back to all owner products
- explicit empty product selection falls back and records validation
- existing `All` behavior remains intact

Reference behavior retained and revalidated via:

- `PoTool.Tests.Unit/Services/DeliveryFilterResolutionServiceTests.cs`

## Validation Reporting

Pipeline Insights now surfaces product-scope validation the same way it already surfaces other canonical filter corrections:

- `InvalidFields` includes `ProductIds`
- `ValidationMessages` contains the fallback explanation

Examples of emitted pipeline validation messages now include:

- product selection cannot be empty when not using `ALL`
- one or more selected products are outside the Product Owner's scope and were replaced with all owner products

No new DTOs or reporting abstractions were introduced.

## Validation

### Workflow/build status

Checked recent GitHub Actions runs for branch `copilot/introduce-persistence-abstraction`.

Observed status at validation time:

- latest Copilot run in progress
- recent completed branch runs successful
- no recent failed branch run required failure-log investigation

### Commands run

Baseline:

- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Focused validation:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~DeliveryFilterResolutionServiceTests|FullyQualifiedName~BuildQualityQueryHandlerTests|FullyQualifiedName~BuildQualityProviderTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests" -v minimal`

Full validation:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

### Results

- baseline build succeeded
- baseline validated test projects succeeded
- focused product-scope alignment tests passed
- full validated test projects passed after the change

## Remaining Semantic Gaps

This change resolves the remaining Build Quality vs Pipeline Insights inconsistency for product scope validation.

Still outside this prompt:

- deeper data integrity concerns beyond filter resolution
- non-pipeline slices that may have independent scope semantics
- sync-time correctness concerns
- any future policy for partially missing repository or pipeline scope inputs beyond the existing validation model

## Final Status

**Product scope validation is now aligned across Build Quality and Pipeline Insights: yes**

Pipeline Insights now enforces Product Owner ownership on explicit product selections with the same fallback and validation-reporting behavior used by Build Quality.
