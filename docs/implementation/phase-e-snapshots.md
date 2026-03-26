# Phase E Snapshot Model & Comparison Engine

## Summary

Implemented a dedicated CDC snapshot model for portfolio/project/work-package progress comparison without reusing roadmap snapshot semantics.

Phase E now provides:

- `PortfolioSnapshot` as the canonical delivery snapshot header
- `PortfolioSnapshotItem` as the canonical snapshot row
- strict snapshot-set validation for project/work-package consistency
- `PortfolioSnapshotComparisonService` for deterministic exact-key comparison
- deterministic result ordering that depends only on timestamps and business keys

No UI, forecasting, roadmap persistence reuse, or heuristic fallback logic was introduced.

## Snapshot model details

### Dedicated CDC types

Added `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs` with:

- `PortfolioSnapshot`
- `PortfolioSnapshotItem`
- `PortfolioSnapshotComparisonRequest`
- `PortfolioSnapshotComparisonItem`
- `PortfolioSnapshotComparisonResult`

`PortfolioSnapshotItem` carries the required Phase E fields:

- `Timestamp`
- `ProductId`
- `ProjectNumber`
- `WorkPackage`
- `Progress`
- `TotalWeight`

Validation built into the model enforces:

- `ProjectNumber` is required
- `WorkPackage` is optional globally but cannot be blank when supplied
- `ProductId` must be positive
- `Progress` must remain within `0..100`
- `TotalWeight` must be finite and non-negative
- every item in a snapshot must share the snapshot timestamp
- duplicate exact business keys are rejected

### Dedicated semantics only

The new snapshot model lives only under `PoTool.Core.Domain/Domain/DeliveryTrends`.

It does **not** inherit from or reuse:

- `RoadmapSnapshotEntity`
- roadmap snapshot service naming
- roadmap-specific compare semantics

Roadmap snapshot code was treated only as a technical pattern reference, not as a semantic base.

## Validation rules enforced

Added `IPortfolioSnapshotValidationService` and `PortfolioSnapshotValidationService` under `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs`.

Rules enforced for snapshot creation:

1. Within one snapshot, a project may contain:
   - exactly one project-level row (`WorkPackage = null`), or
   - only work-package rows (`WorkPackage != null`)
2. Mixed project-level and work-package rows for the same project are blocked immediately.
3. Once a project has historical work-package breakdown rows, later snapshots for that project may not fall back to project-level rows.
4. Historical work-package snapshots establish the minimum required work-package set for later snapshot creation.
   - If the latest known breakdown contains `WP-1` and `WP-2`, a new snapshot containing only `WP-1` is rejected as incomplete.
   - Additional work packages are allowed, but omission of known work packages is blocked.
5. Validation orders historical snapshots by `Timestamp` ascending before evaluating the candidate snapshot, so creation never depends on insertion order.

Invalid snapshot creation throws and does not downgrade to warnings or store partial data.

## Comparison contract

Added `IPortfolioSnapshotComparisonService` and `PortfolioSnapshotComparisonService`.

### Exact business key

Comparison uses the exact business key:

`ProductId + ProjectNumber + WorkPackage`

No fallback matching, fuzzy grouping, or row-order assumptions are used.

### Delta semantics

For each comparable key, the comparison output includes:

- `ProductId`
- `ProjectNumber`
- `WorkPackage`
- `PreviousProgress`
- `CurrentProgress`
- `ProgressDelta`
- `PreviousWeight`
- `CurrentWeight`
- `WeightDelta`

Chosen contract for missing rows:

- missing row in previous snapshot → `PreviousProgress = null`, `PreviousWeight = null`, `delta = null`
- missing row in current snapshot → `CurrentProgress = null`, `CurrentWeight = null`, `delta = null`

This keeps the meaning explicit:

- the row exists only on one side
- the service does not invent a synthetic baseline
- consumers can distinguish new/removed rows from real numeric deltas

### Determinism

`PortfolioSnapshotComparisonService` guarantees deterministic output by:

- rejecting `previous.Timestamp > current.Timestamp`
- ignoring source row order entirely
- sorting the merged key set by:
  - `ProductId`
  - `ProjectNumber` (ordinal)
  - project-level rows before work-package rows
  - `WorkPackage` (ordinal)

Same input gives the same output, and shuffled input rows produce the same ordered result.

## Test coverage

Added focused unit tests in:

- `PoTool.Tests.Unit/Services/PortfolioSnapshotValidationServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotComparisonServiceTests.cs`
- `PoTool.Tests.Unit/Audits/PhaseESnapshotsDocumentTests.cs`

Covered scenarios:

- valid project-level only snapshot
- valid full work-package breakdown snapshot
- invalid mixed project-level + work-package rows
- invalid incomplete historical work-package breakdown
- invalid fallback from work-package history to project-level rows
- validation ordering independence across shuffled historical snapshots
- identical keys with changed progress
- identical keys with changed weight
- new row in current snapshot
- removed row from current snapshot
- same input → same comparison output
- shuffled rows → same ordered comparison output
- invalid reverse timestamp comparison request

## Files changed

- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs`
- `PoTool.Tests.Unit/Audits/PhaseESnapshotsDocumentTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotComparisonServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotValidationServiceTests.cs`
- `docs/implementation/phase-e-snapshots.md`

## Build/test results

Required checks for this phase:

- `dotnet build PoTool.sln --configuration Release`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~PortfolioSnapshot|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~PhaseESnapshotsDocumentTests" -v minimal`

Status at implementation time:

- baseline `dotnet build PoTool.sln --configuration Release` — passed before changes
- targeted Phase E unit/doc tests — passed after implementation
- final `dotnet build PoTool.sln --configuration Release` — passed

## Remaining risks for next phase

- Phase E defines the canonical domain contract only; persistence capture plumbing is still a later concern.
- Historical work-package completeness is enforced against the latest known breakdown set; if a future phase needs explicit retirement semantics for work packages, that lifecycle rule will need to be modeled directly.
- Consumers will need to decide how to present new/removed rows in downstream reporting without converting `delta = null` into misleading numeric changes.
