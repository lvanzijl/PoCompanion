# CDC Domain Map Audit Fix Report

## Summary

The failing CDC domain map audit was caused by drift between the generated snapshot document and the current public service interfaces present in the source tree.

The audit reflection logic detected more public interfaces than were documented in `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/cdc-domain-map-generated.md`, with the missing interfaces concentrated in the `DeliveryTrends` slice.

Specifically:

- the generated document declared **17** public service interfaces
- the current source scan detected **27** public service interfaces
- the generated document was missing **10** currently public interfaces

Missing interfaces from the generated document:

- `IEpicAggregationService`
- `IEpicProgressService`
- `IFeatureForecastService`
- `IInsightService`
- `IPlanningQualityService`
- `IPortfolioSnapshotComparisonService`
- `IPortfolioSnapshotFactory`
- `IPortfolioSnapshotValidationService`
- `IProductAggregationService`
- `ISnapshotComparisonService`

## Root Cause

The audit test source of truth is the reflection-style scan implemented in `CdcGeneratedDomainMapDocumentTests.GetPublicInterfaces(...)`, which enumerates `public interface` declarations from the configured slice directories.

That logic is correct for the current test contract. The failure happened because the snapshot document `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/cdc-domain-map-generated.md` was not updated after the `DeliveryTrends` implementation evolved and added additional public interfaces.

In short:

- implementation evolved
- audit remained strict
- generated snapshot/document did not keep up

## Fix Applied

**Option A was applied:** the expected snapshot/document was updated to match the current implementation.

No production behavior, domain logic, or generator logic was changed.

Updated file:

- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/cdc-domain-map-generated.md`

Changes made:

- updated the declared service count from `17` to `27`
- added the missing `DeliveryTrends` public interfaces to the `Service interfaces` section
- preserved the existing audit strictness and existing test logic

## Before vs After

| Metric | Before | After |
|---|---:|---:|
| Declared service count in generated document | 17 | 27 |
| Actual detected public interfaces | 27 | 27 |
| Missing interfaces in generated document | 10 | 0 |
| Generator/reflection logic changed | No | No |
| Snapshot/document aligned with current source | No | Yes |

## Validation

### Source of truth used

Audit logic:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CdcGeneratedDomainMapDocumentTests.cs`

Validated document:

- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/cdc-domain-map-generated.md`

Current runtime-discovered interface total:

- `27`

### Targeted CDC audit tests

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~CdcGeneratedDomainMapDocumentTests" -v minimal
```

Result:

- Passed: 2
- Failed: 0
- Skipped: 0
- Total: 2

### Full unit suite

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release -v minimal
```

Result:

- Passed: 1674
- Failed: 6
- Skipped: 0
- Total: 1680

Remaining failures are outside the scope of this CDC domain map audit fix. The full-suite output still includes these unrelated failures:

- `GetWorkItemsByRootIdsAsync_HandlesItemsWithMissingRelations`
- `GetWorkItemsByRootIdsAsync_CompletesAncestors_WhenRootHasParents`
- `SanitizeFilter_RemovesSQLInjectionAttempts`
- `Handle_CallsTfsClientWithNullDepth`
- `BuildQualityMissingIngestionBuild168570CodeAnalysisReport_ReportExistsWithRequiredSectionsAndDiagnosis`
- `CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors`

## Security Summary

- No production behavior or runtime logic was changed; the fix is limited to an audit snapshot document and this report.
- Automated code review found no issues requiring changes.
- CodeQL returned no actionable alerts for this change, although the C# analysis also reported that scanning was skipped because the database size was too large.

## Final Status

All tests passing: **no**

The `CdcGeneratedDomainMapDocumentTests` audit failure is resolved, and the generated CDC domain map snapshot now matches the current source-reported public service interface inventory.
