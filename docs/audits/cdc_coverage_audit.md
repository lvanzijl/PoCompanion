# CDC Coverage Audit

_Generated: 2026-03-16_

Reference documents:

- `docs/domain/domain_model.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/source_rules.md`
- `docs/audits/sprint_commitment_application_alignment.md`

Files analyzed:

- `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`
- `PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/FirstDoneDeliveryLookup.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

## Phase 1 — Direct helper usage scan

Direct production usages of the legacy sprint helpers were scanned for:

- `SprintCommitmentLookup`
- `SprintSpilloverLookup`
- `FirstDoneDeliveryLookup`

Findings:

| Helper | Allowed production consumer(s) | Result |
| --- | --- | --- |
| `SprintCommitmentLookup` | `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs` | No `PoTool.Api` consumer references remain. |
| `SprintSpilloverLookup` | `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs` | No `PoTool.Api` consumer references remain. |
| `FirstDoneDeliveryLookup` | `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs` | No `PoTool.Api` consumer references remain. |

Containment result:

- Delivery analytics consumers in `PoTool.Api` no longer call `SprintCommitmentLookup`, `SprintSpilloverLookup`, or `FirstDoneDeliveryLookup` directly.
- The only production consumer of those helpers is the CDC adapter implementation in `SprintCdcServices.cs`.
- The helper definition files remain in `PoTool.Core.Domain/Domain/Sprints`, but application flow reaches them exclusively through the CDC services.

## Phase 2 — API layer verification

Handler dependency verification:

- `GetSprintMetricsQueryHandler`
  - `ISprintCommitmentService`
  - `ISprintScopeChangeService`
  - `ISprintCompletionService`
- `GetSprintExecutionQueryHandler`
  - `ISprintCommitmentService`
  - `ISprintScopeChangeService`
  - `ISprintCompletionService`
  - `ISprintSpilloverService`

API-layer result:

- The handler layer collectively references all required CDC interfaces:
  - `ISprintCommitmentService`
  - `ISprintScopeChangeService`
  - `ISprintCompletionService`
  - `ISprintSpilloverService`
- `GetSprintExecutionQueryHandler` is the handler that consumes the spillover-specific CDC path.
- `ApiServiceCollectionExtensions.cs` registers the CDC sprint services as the injected implementations for the API layer.

## Phase 3 — Projection verification

Projection service verification:

- `SprintTrendProjectionService`
  - builds `firstDoneByWorkItem` through `ISprintCompletionService`
  - resolves `nextSprintPath` through `ISprintSpilloverService`
  - builds committed IDs and commitment timestamps through `ISprintCommitmentService`
  - passes those CDC outputs into `SprintDeliveryProjectionRequest`
- `PortfolioFlowProjectionService`
  - builds `firstDoneByWorkItem` through `ISprintCompletionService`
  - uses the CDC completion output as the throughput attribution source for portfolio flow
  - does not reconstruct first-Done delivery via `FirstDoneDeliveryLookup`

Projection result:

- `SprintTrendProjectionService` consumes CDC outputs rather than reconstructing sprint commitment, completion, or spillover semantics inline.
- `PortfolioFlowProjectionService` consumes CDC completion output instead of replaying first-Done delivery semantics directly.
- No projection service under `PoTool.Api/Services` references `SprintCommitmentLookup`, `SprintSpilloverLookup`, or `FirstDoneDeliveryLookup` directly.

## Phase 4 — Coverage conclusion

Coverage conclusion:

- CDC sprint services are the exclusive application-facing gateway for sprint commitment, scope change, completion, and spillover reconstruction.
- Delivery analytics handlers and projection services in `PoTool.Api` now flow through CDC services instead of direct helper calls.
- Remaining helper references are intentionally contained behind `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`.
