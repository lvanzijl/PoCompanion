# Final Test Fixes Report

## Summary

The last six failing tests were resolved with minimal, targeted fixes split across three categories:

1. **Hierarchy retrieval behavior / fixture alignment**
   - `RealTfsClient` ancestor completion needed to inspect relation-expansion payloads for already discovered items so it could discover missing parents of roots.
   - Two hierarchy tests also needed their mocked HTTP response sequences aligned to the current single-recursive-WIQL plus relation-inspection flow.

2. **Expectation drift**
   - The SQL-sanitization test used the wrong assertion shape for the current sanitizer output.
   - Two audit/documentation tests had stale expected text/anchors compared with current source and generated documents.

3. **One real handler contract mismatch**
   - `GetAreaPathsFromTfsQueryHandler` was still hardcoding `depth: 5` instead of propagating `null` depth to request all area-path levels.

CI investigation note:

- GitHub Actions workflow history was checked first.
- No recent failed workflow runs were available for this branch.
- The latest completed workflow run inspected (`23717438745`) reported **no failed jobs**, so local test reproduction was used as the source of truth for these remaining failures.

## Per-Test Fix

| Test | Root Cause | Fix Type (test/code) |
|---|---|---|
| `GetWorkItemsByRootIdsAsync_CompletesAncestors_WhenRootHasParents` | Test fixture still modeled the old multi-query descendant traversal, while the client now uses one recursive WIQL query and then relation inspection for ancestor discovery. | Both: code + test |
| `GetWorkItemsByRootIdsAsync_HandlesItemsWithMissingRelations` | Ancestor completion needed to tolerate relation-expansion payloads that omit `relations` and log the diagnostic instead of failing the hierarchy pass. | Code |
| `SanitizeFilter_RemovesSQLInjectionAttempts` | Test assertion expected containment using reversed MSTest argument order instead of matching the actual sanitized string returned by `InputValidator.SanitizeFilter`. | Test |
| `Handle_CallsTfsClientWithNullDepth` | Handler passed hardcoded `depth: 5` even though the contract/test expects `null` depth to fetch all levels. | Code |
| `BuildQualityMissingIngestionBuild168570CodeAnalysisReport_ReportExistsWithRequiredSectionsAndDiagnosis` | Audit document wording drifted from the strict test expectation (`"Line" / "Lines"` vs `"Line" or "Lines"`). | Documentation |
| `CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors` | Audit anchor expected an older epic-forecast handler call shape (`new GetSprintMetricsQuery(path)`) while the current handler now builds `GetSprintMetricsQuery` with `SprintEffectiveFilter`. | Test |
| `GetWorkItemsByRootIdsAsync_MapsBacklogPriorityFromHierarchyFields` | After the ancestor-completion hardening, this hierarchy fixture needed one additional mocked relation-expansion response before the fields batch. | Test |

## Validation

### Targeted tests

Command used:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~GetWorkItemsByRootIdsAsync_CompletesAncestors_WhenRootHasParents|FullyQualifiedName~GetWorkItemsByRootIdsAsync_HandlesItemsWithMissingRelations|FullyQualifiedName~SanitizeFilter_RemovesSQLInjectionAttempts|FullyQualifiedName~Handle_CallsTfsClientWithNullDepth|FullyQualifiedName~BuildQualityMissingIngestionBuild168570CodeAnalysisReport_ReportExistsWithRequiredSectionsAndDiagnosis|FullyQualifiedName~CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors" -v minimal
```

Result:

- Passed: 6
- Failed: 0
- Skipped: 0
- Total: 6

Follow-on hierarchy regression check:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~GetWorkItemsByRootIdsAsync_MapsBacklogPriorityFromHierarchyFields" -v minimal
```

Result:

- Passed: 1
- Failed: 0
- Skipped: 0
- Total: 1

### Full suite

Command used:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release -v minimal
```

Result:

- Passed: 1680
- Failed: 0
- Skipped: 0
- Total: 1680

## Security Summary

- No new dependencies were added.
- Changes were limited to one defensive hierarchy fix, one handler parameter correction, strict test updates, and audit/document alignment.
- No security issues were intentionally relaxed; the sanitization test was aligned to the actual sanitizer output rather than weakening the sanitization logic.

## Final Status

All tests passing: **yes**
