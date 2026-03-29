# Canonical Work Item Test and Fixture Fix Report

## Summary

This change fixes the remaining canonical work-item type drift in the targeted test and replay-fixture cluster without weakening the strict domain contract.

The root problem was consistent across the failing cluster:

- strict canonical-domain models such as `CanonicalWorkItem` were still being constructed with raw/application-facing work item types like `Product Backlog Item`
- replay fixtures reconstructed `CanonicalWorkItem` directly from persisted raw type values instead of canonicalizing first
- some tests still asserted raw `PBI`-family values after a mapper had already canonicalized them

The fix keeps the domain strictness intact and corrects only the tests/fixtures so they align with the existing intended architecture:

- raw types stay in raw/persistence/application-facing layers
- canonical types are used once values cross into the strict canonical domain

## Files Changed

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CdcReplayFixtureValidationTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`

## Fix Categories

### 1. Canonical constants used instead of raw values

Direct construction of strict canonical-domain models was updated to use canonical values where required.

Applied changes:

- replaced raw `WorkItemType.Pbi` usages with `CanonicalWorkItemTypes.Pbi` in tests that directly construct `CanonicalWorkItem`
- updated canonical-domain-adjacent `WorkItemSnapshot` test data to use canonical `PBI` where those snapshots are consumed by canonical sprint/history services
- updated state-classification lookup keys for PBI test cases to use canonical `PBI` so they match canonicalized snapshots/domain inputs

This was done in:

- `HierarchyRollupServiceTests`
- `SprintCommitmentCdcServicesTests`

### 2. Replay fixture canonicalization

The replay fixture path in `CdcReplayFixtureValidationTests` previously rebuilt `CanonicalWorkItem` directly from persisted raw `WorkItemEntity.Type` values.

That was corrected by reusing the existing adapter path:

- `workItem.ToCanonicalWorkItem()`

This keeps the fixture aligned with production canonicalization behavior and avoids duplicating raw-to-canonical mapping logic inside the test.

Related state lookup keys in the same replay fixture were also updated to use canonical `PBI` values.

### 3. Assertion updates

`HistoricalSprintInputMapperTests` had expectation drift.

`HistoricalSprintInputMapper.ToSnapshot(...)` already canonicalizes the work item type via `ToCanonicalWorkItemType()`, so the test expectation was updated from raw:

- `WorkItemType.Pbi`

to canonical:

- `CanonicalWorkItemTypes.Pbi`

No mapper behavior was changed.

## Validation

### Focused canonical work-item cluster

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~HierarchyRollupServiceTests|FullyQualifiedName~CdcReplayFixtureValidationTests|FullyQualifiedName~SprintCommitmentCdcServicesTests|FullyQualifiedName~HistoricalSprintInputMapperTests" -v minimal
```

Result:

- Passed: 22
- Failed: 0
- Skipped: 0
- Total: 22

### Supporting canonical-domain slice

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~CanonicalStoryPointResolutionServiceTests|FullyQualifiedName~DomainWorkItemFieldSemanticsTests|FullyQualifiedName~PortfolioFlowProjectionServiceTests|FullyQualifiedName~SprintTrendProjectionServiceSqliteTests" -v minimal
```

Result:

- Passed: 27
- Failed: 0
- Skipped: 0
- Total: 27

### Broader unit suite

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release -v minimal
```

Result:

- Passed: 1663
- Failed: 17
- Skipped: 0
- Total: 1680

Observed remaining failures were in unrelated `CdcGeneratedDomainMapDocumentTests` audit/documentation checks, not in the canonical work-item test/fixture cluster addressed here.

## Guardrails Preserved

The fix does **not** weaken domain strictness.

Explicitly preserved:

- `CanonicalWorkItem` constructor strictness
- `CanonicalWorkItemTypes.EnsureCanonical(...)`
- existing raw-to-canonical mapping behavior in adapters
- the architectural boundary between raw work item types and strict canonical domain inputs

No production domain guards were relaxed.
No canonical enforcement rules were removed.
No production runtime behavior was changed for this fix.

## Remaining Related Risks

The targeted drift cluster is resolved, but the same class of issue could still reappear if future tests:

- directly construct `CanonicalWorkItem` with raw `WorkItemType.Pbi` or `User Story`
- build canonical-domain state lookup dictionaries keyed by raw rather than canonical work item type values
- bypass existing adapter methods when replaying persisted/raw fixtures into canonical domain models

The strongest prevention remains:

- using `CanonicalWorkItemTypes.*` in strict-domain tests
- reusing `ToCanonicalWorkItem()` / `ToSnapshot()` adapter paths whenever fixtures cross into canonical-domain inputs

## Security Summary

- No security-sensitive production behavior was changed; the fix is limited to tests, replay fixtures, and documentation.
- Mandatory code review reported no issues.
- CodeQL returned no actionable alerts for the changed code, but the C# analysis also reported that scanning was skipped because the database size was too large, so there were no additional localized findings to address.

## Final Status

The canonical work-item failure cluster targeted by this prompt is **resolved**.

The failing tests and fixtures were corrected to honor the existing strict canonical-domain contract, and validation confirms the targeted suites now pass without weakening domain rules.
