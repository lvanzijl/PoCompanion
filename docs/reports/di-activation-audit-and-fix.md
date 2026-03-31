# DI Activation Audit and Fix

## Summary

Audited DI-activated API handlers and supporting services for constructor ambiguity, optional-parameter activation risk, and registration mismatches. The reported startup failure was caused by multiple DI-activated metrics handlers exposing two public constructors that were both viable activation candidates. I fixed that pattern by collapsing the affected handlers and the same-pattern provider/factory classes to a single public constructor each, then updated focused unit tests to exercise the intended activation path.

## Root cause of the reported boot failure

The failing metrics handlers each exposed two public constructors:

- one constructor using the current `SprintScopedWorkItemLoader`-based dependency style
- one legacy constructor building that loader from repository/provider dependencies

That left multiple public activation paths on the same DI-activated class. During startup validation / runtime activation, DI could not safely choose between the available constructors for the same handler type, producing the reported `System.AggregateException`.

## Files changed

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LivePipelineReadProvider.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LivePullRequestReadProvider.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LiveWorkItemReadProvider.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetBacklogHealthQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetMultiIterationBacklogHealthQueryHandlerMultiProductTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/LivePullRequestReadProviderTests.cs`

## Changed classes

### `GetBacklogHealthQueryHandler`
- **Original ambiguity/risk:** Two public constructors: current loader-based path plus legacy provider/repository/mediator path.
- **Chosen fix:** Removed the legacy public constructor; kept the loader-based constructor only.
- **Why this fix was selected:** `SprintScopedWorkItemLoader` is already registered and is the current dependency style used across the feature. Keeping only that path removes ambiguity without changing handler behavior.

### `GetMultiIterationBacklogHealthQueryHandler`
- **Original ambiguity/risk:** Two public constructors with the same loader-vs-legacy split.
- **Chosen fix:** Removed the legacy public constructor; kept the loader-based constructor only.
- **Why this fix was selected:** This aligns the handler with the same scoped loading abstraction used by the rest of the sprint/backlog metrics area.

### `GetSprintCapacityPlanQueryHandler`
- **Original ambiguity/risk:** Two public constructors: direct loader injection and legacy provider/repository/mediator composition.
- **Chosen fix:** Removed the legacy public constructor.
- **Why this fix was selected:** The handler only needs the scoped loader and logger at runtime. Removing the transitional constructor is the smallest maintainable fix.

### `GetSprintMetricsQueryHandler`
- **Original ambiguity/risk:** Two public constructors mixed an older repository-backed adapter path with the current loader-based path.
- **Chosen fix:** Removed the legacy public constructor and kept the constructor that accepts `SprintScopedWorkItemLoader`.
- **Why this fix was selected:** This handler was the highest-risk example because the old overload mixed repository-era composition with the new scoped loader model. Preserving only the current constructor removes ambiguity and standardizes the feature on one dependency style.

### `LiveWorkItemReadProvider`
- **Original ambiguity/risk:** Two public constructors, one delegating to the other with optional request-context dependencies.
- **Chosen fix:** Removed the shorter public overload and kept the full constructor.
- **Why this fix was selected:** This is the same DI ambiguity pattern in another DI-activated class. The full constructor is already satisfied by existing registrations, so collapsing to one public constructor removes future activation risk with no behavior change.

### `LivePullRequestReadProvider`
- **Original ambiguity/risk:** Same dual-public-constructor pattern as `LiveWorkItemReadProvider`.
- **Chosen fix:** Removed the shorter public overload and kept the full constructor.
- **Why this fix was selected:** Prevents the same activation failure class from moving to pull-request flows later.

### `LivePipelineReadProvider`
- **Original ambiguity/risk:** Same dual-public-constructor pattern as the other live providers.
- **Chosen fix:** Removed the shorter public overload and kept the full constructor.
- **Why this fix was selected:** Keeps the provider on one explicit activation path and avoids another latent DI activation failure.

### `DataSourceAwareReadProviderFactory`
- **Original ambiguity/risk:** Two public constructors with one transitional overload omitting HTTP context access.
- **Chosen fix:** Removed the shorter public overload and kept the full constructor.
- **Why this fix was selected:** This class is created by DI and participates in the same provider graph. Leaving both constructors public would preserve the same predictive ambiguity pattern.

## Additional predicted DI risks found

- **Fixed now**
  - Dual public constructors on the four reported metrics handlers.
  - Dual public constructors on live read providers and `DataSourceAwareReadProviderFactory`, which were likely to fail later for the same reason.

- **Deferred / not changed**
  - `ProductOwnerNotFoundException` still has multiple public constructors, but it is an exception type instantiated explicitly by application code, not a DI-activated service or handler.
  - Optional constructor parameters remain on some single-constructor classes (for example mock-mode handlers and `SprintTrendProjectionService`), but those classes do not expose multiple public constructor choices and did not show missing-registration failures in this audit.

## Validation performed

- Reviewed the composition root in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`.
- Scanned the repository for classes with multiple public constructors to identify DI-activated risks beyond the originally reported handlers.
- Added / updated focused unit coverage:
  - constructor-count regression test for the audited DI targets
  - DI resolution test for the audited handlers/providers/factory
  - handler/provider unit tests updated to instantiate the surviving constructor path
- Ran:
  - `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~GetBacklogHealthQueryHandlerTests|FullyQualifiedName~GetMultiIterationBacklogHealthQueryHandlerMultiProductTests|FullyQualifiedName~GetSprintMetricsQueryHandlerTests|FullyQualifiedName~LivePullRequestReadProviderTests|FullyQualifiedName~LivePipelineReadProviderDataSourceEnforcementTests"`

## Remaining risks / follow-up

- Unit-test DI validation was kept focused on the audited services instead of broad ASP.NET host-wide `ValidateOnBuild`, because the current unit-test container setup does not fully emulate all MVC hosting infrastructure and produced unrelated framework activation failures.
- If broader startup validation is desired later, add an application-host integration test that boots the real API host rather than expanding the unit-test service collection in isolation.

## Final outcome

The reported metrics handlers now each have one unambiguous public constructor, the same predictive pattern was removed from related DI-activated provider/factory classes, focused tests pass, and the full solution builds successfully. No additional DI-activated multi-public-constructor risks remain in `PoTool.Api` from this audit.
