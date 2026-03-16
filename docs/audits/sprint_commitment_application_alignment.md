# Sprint Commitment Application Alignment

## Legacy application-layer usages located

The application layer previously referenced legacy sprint helpers directly in these files:

| File | Type | Legacy helper usage found |
| --- | --- | --- |
| `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` | Handler | `SprintCommitmentLookup`, `FirstDoneDeliveryLookup` |
| `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | Handler | `SprintCommitmentLookup`, `SprintSpilloverLookup`, `FirstDoneDeliveryLookup` |
| `PoTool.Api/Services/PortfolioFlowProjectionService.cs` | Service | `FirstDoneDeliveryLookup` |

`PoTool.Api/Services/SprintTrendProjectionService.cs` was already aligned to the CDC interfaces and did not require helper-removal changes.

## Handlers migrated

The following application-layer components now depend on CDC sprint services instead of the legacy helpers:

- `GetSprintMetricsQueryHandler`
  - `ISprintCommitmentService`
  - `ISprintScopeChangeService`
  - `ISprintCompletionService`
- `GetSprintExecutionQueryHandler`
  - `ISprintCommitmentService`
  - `ISprintScopeChangeService`
  - `ISprintCompletionService`
  - `ISprintSpilloverService`
- `PortfolioFlowProjectionService`
  - `ISprintCompletionService`

`SprintTrendProjectionService` already consumed `ISprintCommitmentService`, `ISprintCompletionService`, and `ISprintSpilloverService`, so no migration logic change was needed there.

## Legacy helper references removed

Current status for `PoTool.Api`:

- no handler references `SprintCommitmentLookup`
- no handler references `SprintSpilloverLookup`
- no handler references `FirstDoneDeliveryLookup`
- no service references those helper classes directly
- legacy helpers remain behind `PoTool.Core.Domain.Cdc.Sprints`

## Tests updated

Focused test updates cover:

- `GetSprintMetricsQueryHandlerTests`
  - semantic regression coverage remains in place
  - added verification that the handler uses CDC sprint services for commitment/scope/completion reconstruction
- `GetSprintExecutionQueryHandlerTests`
  - semantic regression coverage remains in place
  - added verification that the handler uses CDC sprint services for commitment/scope/completion/spillover reconstruction
- `PortfolioFlowProjectionServiceTests`
  - constructor setup updated to supply `ISprintCompletionService`
- `SprintTrendProjectionServiceTests`
  - service registration updated so the aligned constructor graph includes `ISprintScopeChangeService`
- `SprintTrendProjectionServiceSqliteTests`
  - portfolio-flow projection setup updated to supply `ISprintCompletionService`

## Remaining migration risks

- `FirstDoneDeliveryLookup.GetEventTimestamp` call sites outside the application layer still exist inside the domain helper and CDC implementations by design; that is the intended containment boundary.
- Additional future sprint analytics added under `PoTool.Api` should inject CDC interfaces immediately rather than reintroducing direct helper usage.
- The audit should be revisited if new sprint analytics handlers or services are introduced outside the current metrics/projection paths.
