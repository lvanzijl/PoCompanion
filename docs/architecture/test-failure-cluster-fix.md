# Remaining Test Failure Cluster Fix Report

## Summary

This change fixes the remaining unit-test failure cluster caused by outdated test setup and one incorrect ordering assertion.

The failures were all test-side issues, not production bugs:

- dependency graph tests were still seeding legacy JSON strings while the handler now reads typed `WorkItemDto.Relations`
- selection service tests were creating `TreeNode` instances without `JsonPayload`, even though selection/deserialization now depends on that field
- the scatter ordering test used `Assert.IsLessThanOrEqualTo(...)` with reversed arguments, so it asserted the opposite of the intended ascending order

The fix keeps production behavior unchanged and updates only the affected tests to match the current contracts.

## Files Changed

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemSelectionServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs`

## Fix Details

### Dependency graph

`GetDependencyGraphQueryHandler` now uses the typed `Relations` property on `WorkItemDto` instead of parsing legacy JSON payloads.

To keep the existing dependency-graph tests minimal and realistic, the shared test helper was updated to:

- keep accepting the existing JSON fixture strings already used by the tests
- parse those JSON relation arrays in the helper
- populate `WorkItemDto.Relations` with typed `WorkItemRelation` entries, including extracted target work item IDs from the relation URLs
- continue returning no relations for invalid JSON or payloads without a `relations` array

This preserves the current test bodies while aligning the setup with the actual production input shape.

### Selection service

`WorkItemSelectionService` intentionally returns no-op selection behavior when `TreeNode.JsonPayload` is missing, because it deserializes the selected work item from that field.

The selection tests were updated so `CreateTestNode(...)` now sets:

- `JsonPayload = JsonSerializer.Serialize(workItem)`

This matches the real persistence/transport format expected by the service and restores valid selection and keyboard-navigation behavior in the tests.

### Ordering test

The scatter ordering production logic was already correct:

- `GetPipelineInsightsQueryHandler.BuildScatterPoints(...)` orders runs by `CreatedDateOffset` ascending before building scatter points

The failing test assertion was incorrect because it passed the arguments to `Assert.IsLessThanOrEqualTo(...)` in reverse order.

The test now asserts ascending order correctly by swapping the arguments so the comparison matches the intended semantics.

## Validation

### CI / workflow investigation

GitHub Actions workflow runs were inspected first.

- Recent workflow pages on the current branch showed no completed failed runs in the most recent page.
- An older failed run was located (`23695490187`), but failed-job log download returned HTTP 404 via the available logs endpoint.
- Because the actionable failure cluster was reproducible locally, local targeted validation was used for the fix.

### Targeted cluster repro before fix

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~GetDependencyGraphQueryHandlerTests|FullyQualifiedName~WorkItemSelectionServiceTests|FullyQualifiedName~Scatter" -v minimal
```

Observed failures before fix:

- `GetDependencyGraphQueryHandlerTests`
- `WorkItemSelectionServiceTests`
- `GetPipelineInsightsScatterPointTests.Handle_ScatterPoints_OrderedByStartTimeAscending`

### Targeted cluster after fix

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~GetDependencyGraphQueryHandlerTests|FullyQualifiedName~WorkItemSelectionServiceTests|FullyQualifiedName~Handle_ScatterPoints_OrderedByStartTimeAscending" -v minimal
```

Result:

- Passed: 22
- Failed: 0
- Skipped: 0
- Total: 22

### Full unit suite after fix

Command:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release -v minimal
```

Result:

- Passed: 1673
- Failed: 7
- Skipped: 0
- Total: 1680

## Remaining Failures

The remaining failures are outside this dependency/selection/ordering cluster.

Observed remaining failures are in unrelated audit/documentation tests, including:

- `PoTool.Tests.Unit.Audits.CdcGeneratedDomainMapDocumentTests.GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource`

No dependency graph, selection service, or scatter ordering failures remained after the targeted fix.

## Security Summary

- No production security behavior was changed; the fix is limited to test setup, test helper parsing, one test assertion, and documentation.
- Automated code review found only non-blocking suggestions and no correctness or security issues requiring changes for this scope.
- CodeQL returned no actionable alerts for the changed code, but the C# analysis also reported that scanning was skipped because the database size was too large.

## Final Status

- cluster resolved: **yes**

The dependency graph, selection service, and scatter ordering failure cluster is resolved with minimal test-only changes and no production logic drift.
