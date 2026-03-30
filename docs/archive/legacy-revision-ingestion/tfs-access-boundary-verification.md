# TFS Access Boundary Verification

## Summary
Current status: **Partially implemented**.

A TFS access gateway now exists in the main API application, and normal `ITfsClient` resolution in `PoTool.Api` is routed through that gateway (`PoTool.Api/Services/TfsAccessGateway.cs:22-223`, `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:375-386`).

That means most API consumers that still inject `ITfsClient` are **not** direct bypasses anymore, because DI resolves `ITfsClient` to `ITfsAccessGateway` / `TfsAccessGateway`.

However, the implementation is not complete across the whole repository:
- a separate tool still registers `ITfsClient` directly to `RealTfsClient` with no gateway (`PoTool.Tools.TfsRetrievalValidator/Program.cs:71-92`)
- raw `RealTfsClient` and `MockTfsClient` remain directly registered/constructible, so future bypass is still easy to introduce (`PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:349-372`)
- there is no repo-wide architectural test that proves all production TFS access must flow through the gateway

---

## Gateway Detection
### Exists or not
**Exists.**

### Name and location
- Interface: `ITfsAccessGateway` — `PoTool.Api/Services/TfsAccessGateway.cs:22-24`
- Implementation: `TfsAccessGateway` — `PoTool.Api/Services/TfsAccessGateway.cs:33-223`

### What it does
`TfsAccessGateway` wraps an inner `ITfsClient` (`PoTool.Api/Services/TfsAccessGateway.cs:35-59`) and validates access before delegating every method call (`PoTool.Api/Services/TfsAccessGateway.cs:66-69`).

It enforces three access purposes:
- `Read`
- `Mutation`
- `Verification`

Defined at `PoTool.Api/Services/TfsAccessGateway.cs:26-31`.

### Enforcement performed by the gateway
For request-scoped calls:
- if `HttpContext` exists
- and the gateway method is classified as `Read`
- and `IDataSourceModeProvider.Mode == Cache`

then it blocks the call and throws `InvalidDataSourceUsageException` (`PoTool.Api/Services/TfsAccessGateway.cs:72-84`).

The gateway also logs each access attempt (`PoTool.Api/Services/TfsAccessGateway.cs:86-92`).

### Important limitation
The gateway does **not** independently resolve route intent. It relies on:
- middleware to classify routes and set mode (`PoTool.Api/Middleware/DataSourceModeMiddleware.cs:28-110`)
- the presence of `HttpContext` to distinguish request-time reads from background/sync usage (`PoTool.Api/Services/TfsAccessGateway.cs:74-84`)

So the gateway is a real boundary, but not a full standalone policy engine.

---

## TFS Access Inventory

### Notes for interpretation
- In `PoTool.Api`, `ITfsClient` resolves through the gateway because DI maps:
  - `ITfsAccessGateway` -> `TfsAccessGateway`
  - `ITfsClient` -> `ITfsAccessGateway`
  (`PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:375-386`)
- Therefore, API call sites below that inject `ITfsClient` **do go through the gateway at runtime**.
- Direct REST calls to TFS still exist in the transport implementation (`RealTfsClient`), which is expected below the boundary.

### Sync / ingestion
All of the following use `ITfsClient` in `PoTool.Api` and therefore go **through the gateway via API DI**:

| File | Class | Pattern | Through gateway? |
|---|---|---|---|
| `PoTool.Api/Services/Sync/WorkItemSyncStage.cs:16,25` | `WorkItemSyncStage` | constructor injection of `ITfsClient` | Yes |
| `PoTool.Api/Services/Sync/TeamSprintSyncStage.cs:14,23` | `TeamSprintSyncStage` | constructor injection of `ITfsClient` | Yes |
| `PoTool.Api/Services/Sync/PullRequestSyncStage.cs:24,32` | `PullRequestSyncStage` | constructor injection of `ITfsClient` | Yes |
| `PoTool.Api/Services/Sync/PipelineSyncStage.cs:17,25` | `PipelineSyncStage` | constructor injection of `ITfsClient` | Yes |
| `PoTool.Api/Services/Sync/SyncPipelineRunner.cs:87,483,542` | `SyncPipelineRunner` | `GetRequiredService<ITfsClient>()` plus helper parameters | Yes |
| `PoTool.Api/Services/ActivityEventIngestionService.cs:18,24` | `ActivityEventIngestionService` | constructor injection of `ITfsClient` | Yes |
| `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs:36` | `WorkItemRelationshipSnapshotService` | `GetRequiredService<ITfsClient>()` | Yes |

### Live providers
All of the following use `ITfsClient` in `PoTool.Api` and therefore go **through the gateway via API DI**. They also retain their own provider-level cache-mode guard:

| File | Class | Pattern | Through gateway? |
|---|---|---|---|
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs:17,24,32` | `LiveWorkItemReadProvider` | constructor injection of `ITfsClient` | Yes |
| `PoTool.Api/Services/LivePipelineReadProvider.cs:20,28,37` | `LivePipelineReadProvider` | constructor injection of `ITfsClient` | Yes |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs:17,24,32` | `LivePullRequestReadProvider` | constructor injection of `ITfsClient` | Yes |

### Other services / handlers
All of the following are API-layer `ITfsClient` consumers and therefore go **through the gateway via API DI**:

#### Services / controllers / endpoints
| File | Class / endpoint | Pattern | Through gateway? |
|---|---|---|---|
| `PoTool.Api/Services/Configuration/ImportConfigurationService.cs:25,30` | `ImportConfigurationService` | constructor injection | Yes |
| `PoTool.Api/Services/Configuration/ExportConfigurationService.cs:19,28` | `ExportConfigurationService` | constructor injection | Yes |
| `PoTool.Api/Services/BugTriageStateService.cs:19,24` | `BugTriageStateService` | constructor injection | Yes |
| `PoTool.Api/Controllers/StartupController.cs:17,19` | `StartupController` | constructor injection | Yes |
| `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:237-315` | `/api/tfsvalidate`, `/api/tfsverify`, `/api/tfsconfig/save-and-verify` | minimal API parameter injection | Yes |
| `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:462-468` | startup validation | `GetRequiredService<ITfsClient>()` | Yes |

#### Handlers
| File | Class | Pattern | Through gateway? |
|---|---|---|---|
| `PoTool.Api/Handlers/Settings/GetWorkItemTypeDefinitionsQueryHandler.cs:20,24` | `GetWorkItemTypeDefinitionsQueryHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/ReleasePlanning/SplitEpicCommandHandler.cs:21,29` | `SplitEpicCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/UpdateWorkItemIterationPathCommandHandler.cs:15,20` | `UpdateWorkItemIterationPathCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/BulkAssignEffortCommandHandler.cs:17,22` | `BulkAssignEffortCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs:14,18` | `GetWorkItemRevisionsQueryHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/UpdateWorkItemBacklogPriorityCommandHandler.cs:15,20` | `UpdateWorkItemBacklogPriorityCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/GetAreaPathsFromTfsQueryHandler.cs:14,18` | `GetAreaPathsFromTfsQueryHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/RefreshWorkItemFromTfsCommandHandler.cs:15,20` | `RefreshWorkItemFromTfsCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/RefreshWorkItemsByRootIdsFromTfsCommandHandler.cs:15,20` | `RefreshWorkItemsByRootIdsFromTfsCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/FixValidationViolationBatchCommandHandler.cs:17,22` | `FixValidationViolationBatchCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/UpdateWorkItemTitleDescriptionCommandHandler.cs:16,21` | `UpdateWorkItemTitleDescriptionCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs:15,19` | `ValidateWorkItemQueryHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/UpdateWorkItemTagsCommandHandler.cs:16,21` | `UpdateWorkItemTagsCommandHandler` | constructor injection | Yes |
| `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs:19,25` | `GetGoalsFromTfsQueryHandler` | constructor injection | Yes |

### Unexpected direct usages
These are current places where the intended boundary is **not** universally enforced.

#### Direct tool-side bypass
| File | Class | Pattern | Through gateway? |
|---|---|---|---|
| `PoTool.Tools.TfsRetrievalValidator/Program.cs:71-92` | validator tool startup | registers `ITfsClient` directly as `RealTfsClient` | **No** |

This is the clearest current repository-level bypass of the boundary requirement.

#### Raw transport implementation below the boundary
These direct REST call sites exist in `RealTfsClient` and are expected as the actual TFS transport layer, not as API-layer bypasses:

| File | Endpoint family | Notes |
|---|---|---|
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs:142,233,417,471,554,623,695` | `_apis/wit/...` | work item reads / WIQL / batch |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemRevisions.cs:25` | `_apis/wit/workitems/{id}/revisions` | revisions |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemUpdates.cs:20` | `_apis/wit/workItems/{id}/updates` | update history |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs:46,114,188,283,512,587,640,769,832,901` | `_apis/wit/workitems/...` | write/update/create |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs:106,261,596` | `_apis/wit/...` | hierarchy queries |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Teams.cs:443` | `_apis/wit/workitemtypes` | work item type definitions |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs:27,190,277,353,739,740` | `_apis/build/...` | pipeline definitions and runs |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.PullRequests.cs:73,329,401,495,569` | `_apis/git/...` | PRs, iterations, threads, changes, work items |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Infrastructure.cs:25,47` | `_apis/git/repositories...` | repository discovery |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs:193,245,291,389,463,506,658,709,756,816,843,937,968` | `_apis/wit/...`, `_apis/git/...`, `_apis/build/...` | verification checks |

#### Named HttpClient usage
| File | Pattern | Through gateway? |
|---|---|---|
| `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:360-367` | registers `TfsClient.NTLM` | N/A registration |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:100` | `CreateClient("TfsClient.NTLM")` | Under transport layer |
| `PoTool.Tools.TfsRetrievalValidator/Program.cs:71-79` | registers `TfsClient.NTLM` in tool | Tool-local bypass path |

---

## Enforcement Status
### What is actually enforced today

#### 1. Unknown and ambiguous HTTP routes are blocked in middleware
`DataSourceModeMiddleware` resolves route intent and throws on unknown routes (`PoTool.Api/Middleware/DataSourceModeMiddleware.cs:33-46`).

It also blocks intentionally ambiguous routes such as `/state-timeline` (`PoTool.Api/Middleware/DataSourceModeMiddleware.cs:50-59`, `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:167-184`).

#### 2. Cache-only HTTP routes are blocked unless cache is ready
For cache-only analytical routes, middleware sets cache mode and blocks the request when cache is not ready (`PoTool.Api/Middleware/DataSourceModeMiddleware.cs:61-103`).

#### 3. Live-allowed routes are explicitly classified
Route intent configuration distinguishes:
- `LiveAllowed`
- `CacheOnlyAnalyticalRead`
- `BlockedAmbiguous`
- `Unknown`

(`PoTool.Api/Configuration/DataSourceModeConfiguration.cs:14-20`, `96-138`)

Examples of explicit live-allowed discovery/config routes:
- `/api/pipelines/definitions`
- `/api/workitems/area-paths/from-tfs`
- `/api/workitems/goals/from-tfs`
- `/api/workitems/{id}/revisions`

(`PoTool.Api/Configuration/DataSourceModeConfiguration.cs:56-77`, `187-209`)

#### 4. Gateway blocks request-time TFS reads when mode is Cache
If a request reaches a consumer with `Mode == Cache`, the gateway blocks any `Read` access (`PoTool.Api/Services/TfsAccessGateway.cs:72-84`).

#### 5. Live providers still have their own secondary guard
The live providers continue to throw on cache-only usage independently of the gateway (`PoTool.Api/Services/LiveWorkItemReadProvider.cs:147-164`, `PoTool.Api/Services/LivePipelineReadProvider.cs:231-248`, `PoTool.Api/Services/LivePullRequestReadProvider.cs:463-471`).

### Where enforcement does **not** fully exist
- The gateway does not classify routes itself; it trusts middleware and current `modeProvider.Mode` (`PoTool.Api/Services/TfsAccessGateway.cs:72-84`).
- Background/no-`HttpContext` calls are allowed by the gateway by design (`PoTool.Api/Services/TfsAccessGateway.cs:74-84`). That is correct for sync/ingestion, but it is not a universal policy barrier for every possible non-request caller.
- The validator tool bypasses the gateway entirely (`PoTool.Tools.TfsRetrievalValidator/Program.cs:81-92`).

### Does current code guarantee the stated behavior?
- **CacheOnly routes cannot access TFS:** **Yes, in the main API request path**, through middleware + gateway + provider guard.
- **LiveAllowed routes can access TFS intentionally:** **Yes**, via explicit route intent and gateway-allowed reads.
- **Unknown/ambiguous routes cannot bypass enforcement:** **Yes, in the main API request path**, because middleware blocks them before execution.
- **No code anywhere can call TFS outside the boundary:** **No**, because the validator tool still registers direct `RealTfsClient` access without the gateway.

---

## DI / Bypass Risk
### Is `ITfsClient` still injected directly in normal consumers?
**Yes.** Many services, handlers, sync stages, controllers, and endpoints still inject `ITfsClient` directly.

### Is that still a bypass?
**In `PoTool.Api`, no.** Because `ITfsClient` is now mapped to `ITfsAccessGateway` / `TfsAccessGateway` (`PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:375-386`).

### Is direct usage still easy to add?
**Yes.**

Reasons:
1. `RealTfsClient` and `MockTfsClient` are still directly registered in DI (`PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:349-372`).
2. A new consumer could inject `RealTfsClient` instead of `ITfsClient` and bypass the boundary.
3. A separate executable already does exactly this with `ITfsClient -> RealTfsClient` (`PoTool.Tools.TfsRetrievalValidator/Program.cs:81-92`).
4. There is no architectural test scanning production code for forbidden direct `RealTfsClient`/`MockTfsClient` usage.

### Future-bypass risk assessment
**Moderate.**

The main API runtime path is protected, but the codebase does not yet make bypass impossible.

---

## Test Coverage
### Tests that exist
#### Gateway-specific
- `PoTool.Tests.Unit/Services/TfsAccessGatewayTests.cs:16-39`
  - proves request-time cache-mode read access is blocked
- `PoTool.Tests.Unit/Services/TfsAccessGatewayTests.cs:41-80`
  - proves background/no-`HttpContext` read access is allowed

#### DI wiring
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs:218-257`
  - proves `ITfsClient` resolves through `ITfsAccessGateway` / `TfsAccessGateway`
  - proves gateway wraps mock and real clients correctly

#### Middleware / route intent
- `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs:10-120`
  - proves route classification for cache-only, live-allowed, blocked-ambiguous, and unknown routes
- `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs:45-215`
  - proves cache-only blocking, live route allowance, and blocked-ambiguous behavior
- `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs:311-325`
  - proves unknown routes throw `RouteNotClassifiedException`

#### Live provider secondary guard
- `PoTool.Tests.Unit/Services/LivePipelineReadProviderDataSourceEnforcementTests.cs:16-85`
  - proves live pipeline provider still blocks cache-mode usage and allows live-mode usage

### What is missing
- No test proves the **entire production codebase** cannot bypass the gateway.
- No test fails if a new API consumer injects `RealTfsClient` directly.
- No test covers the **tool project** bypass path.
- No architectural audit test scans for forbidden direct raw-client registration/injection.

---

## Conclusion
**Partially implemented**

Why:
1. A real gateway abstraction now exists and is wired into the main API DI path.
2. Normal `ITfsClient` usage in `PoTool.Api` goes through the gateway.
3. Middleware + route intent + gateway now provide real request-path enforcement.
4. But the implementation is not repository-wide complete because:
   - `PoTool.Tools.TfsRetrievalValidator` bypasses the gateway entirely
   - raw clients remain directly registerable/injectable
   - there is no architectural test preventing future bypass

---

## Recommended Next Step
Update `PoTool.Tools.TfsRetrievalValidator` so its `ITfsClient` resolution also goes through `TfsAccessGateway`, removing the current direct `RealTfsClient` registration bypass (`PoTool.Tools.TfsRetrievalValidator/Program.cs:81-92`).
