# IActionResult Enforcement Gap

## Endpoints found

A controller scan of `PoTool.Api` found **11** HTTP endpoints returning `IActionResult` / `Task<IActionResult>`.

All 11 were classified as **LOW RISK** because they do not return shared DTO payloads internally. They return one of the following untyped outcomes instead:

- `NoContent()`
- bare `Ok()`
- `NotFound()` or `NotFound(string)`
- `BadRequest(string)`
- `StatusCode(500, string)`

Endpoints found:

1. `TeamsController.DeleteTeam` — obsolete delete endpoint, returns `NoContent` / `NotFound`
2. `ProductsController.DeleteProduct` — command endpoint, returns `NoContent` / `NotFound`
3. `ProductsController.LinkTeamToProduct` — link command, returns bare `Ok` / `NotFound`
4. `ProductsController.UnlinkTeamFromProduct` — unlink command, returns `NoContent` / `NotFound`
5. `ProductsController.DeleteRepository` — delete command, returns `NoContent`
6. `BugTriageController.RecordFirstSeen` — side-effect endpoint, returns bare `Ok`
7. `RoadmapSnapshotsController.DeleteSnapshot` — delete command, returns `NoContent` / `NotFound`
8. `ProfilesController.DeleteProfile` — delete command, returns `NoContent` / `NotFound`
9. `WorkItemsController.RefreshFromTfs` — refresh command, returns bare `Ok`, `NotFound(string)`, or `500`
10. `WorkItemsController.UpdateBacklogPriority` — mutation command, returns bare `Ok`, `BadRequest(string)`, or `500`
11. `WorkItemsController.UpdateIterationPath` — mutation command, returns bare `Ok`, `BadRequest(string)`, or `500`

No remaining untyped endpoint was found to return a `PoTool.Shared` DTO payload internally, so there were **no HIGH RISK endpoints** requiring conversion to `ActionResult<T>` in this pass.

## Converted endpoints

Converted endpoints: **0**

Reason:

- the remaining untyped endpoints are command/infrastructure style endpoints with no shared DTO success payload to protect
- converting them to `ActionResult<T>` would not improve shared-contract runtime enforcement because they do not expose typed shared response bodies

## Allowed exceptions

Added explicit allow-annotation support:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Filters/AllowUntypedResponseAttribute.cs`

Applied `[AllowUntypedResponse]` to all 11 remaining untyped endpoints listed above.

Added enforcement test:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/IActionResultUntypedResponseAuditTests.cs`

The test reflects over all API controllers and fails if any HTTP endpoint returns `IActionResult` / `Task<IActionResult>` without explicit `[AllowUntypedResponse]` approval.

## Final guarantee status

Final status after this change:

- untyped endpoints found: **11**
- explicitly allowed untyped endpoints: **11**
- unannotated untyped endpoints: **0**
- high-risk shared-payload untyped endpoints remaining: **0**

This closes the remaining `IActionResult` escape hatch by making every untyped endpoint an explicit, audited exception instead of an accidental bypass.

Validation completed:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~IActionResultUntypedResponseAuditTests|FullyQualifiedName~SharedDtoRuntimeContractEnforcementTests|FullyQualifiedName~HealthCalculationControllerTests|FullyQualifiedName~ServiceCollectionTests"`
