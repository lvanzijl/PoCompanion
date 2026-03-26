# Phase F Snapshot Lifecycle & Capture Strategy

## Summary

Implemented Phase F for CDC portfolio snapshots by adding explicit work-package lifecycle state, active-only completeness validation, deterministic snapshot capture, and lifecycle-aware comparison output.

Phase F now provides:

- `WorkPackageLifecycleState` with `Active` and `Retired`
- `PortfolioSnapshotItem.LifecycleState` with default `Active`
- validation based on the latest active lifecycle state per work package
- `PortfolioSnapshotFactory` for deterministic snapshot capture from current epic state
- lifecycle-aware comparison output without changing progress/weight delta semantics

No UI, forecasting, heuristic inference, or historical mutation was introduced.

## Lifecycle model details

`PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs` now models lifecycle explicitly.

- `WorkPackageLifecycleState.Active`
  - contributes to aggregation
  - must appear in the next snapshot, either still `Active` or explicitly transitioned to `Retired`
- `WorkPackageLifecycleState.Retired`
  - does not contribute to aggregation
  - is not required in later snapshots
  - remains valid in the historical snapshot where the transition was recorded

`PortfolioSnapshotItem` retains the existing exact business key:

- `ProductId`
- `ProjectNumber`
- `WorkPackage`

and now also carries `LifecycleState`, defaulting to `Active`.

## Validation changes

`PortfolioSnapshotValidationService` now enforces lifecycle-aware completeness instead of assuming work packages are static.

Updated rules:

1. Historical work-package usage still blocks fallback to project-level rows.
2. Validation evaluates lifecycle by business key across prior snapshots ordered by `Timestamp`.
3. Only the latest `Active` work packages are required in the candidate snapshot.
4. A required active work package may transition by being present as `Retired` in the next snapshot.
5. Once a work package is historically `Retired`, it cannot be reactivated as `Active`.
6. A retired work package is not required in later snapshots and may drop out entirely.

This replaces the Phase E rule of “latest breakdown must be complete” with “latest active breakdown must be complete.”

## Capture strategy

Added `IPortfolioSnapshotFactory` and `PortfolioSnapshotFactory`.

Factory responsibilities:

- use a single request timestamp for the whole snapshot
- map current epic inputs into `Active` snapshot rows
- compare against the immediately previous snapshot only
- mark prior active work-package rows as `Retired` when they are missing from current input
- never mutate the previous snapshot
- never reintroduce previously retired work packages
- sort output by exact business key for deterministic ordering

Determinism guarantee:

- same request timestamp + same epic input + same previous snapshot = same output
- input row order does not affect output row order
- no per-item timestamps are generated
- no randomness or heuristic lifecycle inference is used

## Comparison updates

`PortfolioSnapshotComparisonItem` now includes:

- `PreviousLifecycleState`
- `CurrentLifecycleState`

Comparison semantics:

- `Active → Active` keeps normal progress/weight comparison
- `Active → Retired` is allowed and reported as a lifecycle transition
- retired rows are treated as non-contributing for progress/weight comparison, so removal-style null semantics stay unchanged
- `Retired → Active` is blocked during validation/factory capture and therefore should not reach comparison

Lifecycle is an additional signal. It does not replace the existing `ProgressDelta` or `WeightDelta` contract.

## Test coverage

Added or updated focused tests in:

- `PoTool.Tests.Unit/Services/PortfolioSnapshotValidationServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotComparisonServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotFactoryTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `PoTool.Tests.Unit/Audits/PhaseFLifecycleDocumentTests.cs`

Covered scenarios:

- first snapshot rows default to `Active`
- `Active → Active` capture remains active
- `Active → Retired` capture is created when a prior work package disappears
- retired work packages do not reappear in later captures
- retired items are not required in later validation
- active items must still be present in the next snapshot
- invalid reactivation is blocked
- comparison reports lifecycle transitions
- progress/weight delta semantics remain unchanged for retired rows
- capture output is deterministic regardless of input ordering

## Files changed

- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotFactory.cs`
- `PoTool.Tests.Unit/Audits/PhaseFLifecycleDocumentTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotComparisonServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotFactoryTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotValidationServiceTests.cs`
- `docs/implementation/phase-f-lifecycle.md`

## Build/test results

Required checks for this phase:

- `dotnet build PoTool.sln --configuration Release`
- relevant `dotnet test` coverage for portfolio snapshot lifecycle, comparison, DI, and report audits

Status at implementation time:

- baseline focused snapshot tests before code changes — passed
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~PortfolioSnapshot|FullyQualifiedName~PhaseFLifecycleDocumentTests|FullyQualifiedName~ServiceCollectionTests" -v minimal` — passed
- `dotnet build PoTool.sln --configuration Release` — passed

## Remaining risks

- Phase F defines lifecycle and capture semantics in the domain layer; downstream persistence or API consumers still need to respect `LifecycleState` when aggregating snapshot rows.
- The factory intentionally depends on the immediately previous snapshot. If future work requires longer historical reconstruction, that should remain a separate phase rather than expanding this capture contract implicitly.
