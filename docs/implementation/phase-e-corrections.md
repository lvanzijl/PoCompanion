# Phase E Corrections

## Summary

Applied the requested Phase E correction pass to harden the CDC portfolio snapshot contract without adding new functionality, dual-scale compatibility, or alternate timestamp ownership.

Snapshot progress now follows the accepted CDC calculated-progress scale of `0..1`, while `TimeCriticality` remains a separate `0..100` override concept outside this snapshot model.

## Corrections applied

### 1. Snapshot progress scale

Applied:

- Changed snapshot progress validation to reject values outside `0..1`.
- Updated `PortfolioSnapshotItem.Progress` test inputs and comparison test inputs/outputs to use fractional progress values such as `0.4`, `0.5`, and `0.65`.
- Kept the change strict: no conversion layer, no silent coercion, and no support for `0..100`-style snapshot progress.

Verification:

- Invalid snapshot progress like `50` is rejected.
- Valid snapshot progress like `0.5` is accepted.
- Comparison deltas now operate on the same `0..1` scale, so matching-row delta assertions use values such as `0.25`.

### 2. Timestamp ownership

Applied:

- Removed item-level timestamp ownership from `PortfolioSnapshotItem`.
- Timestamp is owned by `PortfolioSnapshot` only.
- Updated historical validation to derive the latest work-package breakdown from the ordered parent snapshots instead of per-item timestamps.

Verification:

- The snapshot model has one authoritative timestamp source.
- Snapshot items no longer validate or expose an independent timestamp.

### 3. Comparison semantics

Applied:

- Kept the exact business key unchanged: `ProductId + ProjectNumber + WorkPackage`.
- Preserved current comparison null semantics for new and removed rows.
- Kept deterministic ordering and direct delta calculation for rows that exist in both snapshots.

Verification:

- New row semantics remain: previous values = `null`, delta = `null` (`delta = null`).
- Removed row semantics remain: current values = `null`, delta = `null` (`delta = null`).
- No null-to-zero coercion or synthetic baseline behavior was introduced.

### 4. Strict validation

Applied:

- Preserved the existing block on mixed project-level and work-package rows within the same project snapshot.
- Preserved the existing block on incomplete work-package breakdowns relative to the latest historical breakdown.
- Preserved the existing block on falling back from work-package history to project-level rows.

Verification:

- These rules continue to throw and remain enforced as errors, not warnings.

### 5. Optional structural cleanup

Decision:

- Left `PortfolioSnapshotValidationService` and `PortfolioSnapshotComparisonService` in the existing service file to avoid unnecessary churn during this correction-only pass.

## Files changed

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/DeliveryTrendModelValidation.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs`
- `PoTool.Tests.Unit/Audits/PhaseECorrectionsDocumentTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotComparisonServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotValidationServiceTests.cs`
- `docs/implementation/phase-e-corrections.md`

## Test updates

Updated focused coverage for:

- valid `0..1` snapshot progress acceptance
- rejection of invalid `0..100`-style snapshot progress
- single-source timestamp ownership on `PortfolioSnapshot`
- unchanged null semantics for new and removed comparison rows
- unchanged strict validation for mixed rows, incomplete breakdowns, and fallback to project-level rows

## Build/test results

Required checks for this correction pass:

- `dotnet build PoTool.sln --configuration Release`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~PortfolioSnapshot|FullyQualifiedName~PhaseECorrectionsDocumentTests|FullyQualifiedName~PhaseESnapshotsDocumentTests|FullyQualifiedName~ServiceCollectionTests" -v minimal`

Status after applying the corrections:

- `dotnet build PoTool.sln --configuration Release` — passed
- relevant snapshot/document/service-registration unit tests — passed

## Remaining risks

- The pre-existing Phase E implementation report remains a historical implementation artifact; this correction report is the authoritative validation record for the contract fixes in this issue.
- Snapshot progress is now strict `0..1`; any future callers that still supply `0..100` values will fail fast and must be corrected at the caller rather than normalized here.
