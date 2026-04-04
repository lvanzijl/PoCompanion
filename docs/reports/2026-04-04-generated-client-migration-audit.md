# Generated Client Migration Audit

## 1. Executive summary

- **Is the OpenAPI/NSwag layer currently trustworthy enough?** **No**
- **Main blockers to a generated-clients-only policy**
  - Cache-backed routes are rewritten at runtime to `DataStateResponseDto<T>`, but the OpenAPI snapshot still declares the inner DTO for those routes.
  - The rewrite is cross-cutting and applies by route classification, so the mismatch affects entire endpoint families, not isolated examples.
  - The checked-in NSwag configuration excludes `DataStateDto` and `DataStateResponseDto`, so even a corrected OpenAPI document would not currently generate usable cache-aware signatures.
  - Existing handwritten partial client wrappers in `PoTool.Client/ApiClient/*.cs` also deserialize the inner query/response DTOs directly and are therefore unsafe on cache-backed routes.
  - Several status-sensitive flows still rely on raw `HttpClient` because generated clients do not surface 204/202/409/streaming behavior cleanly.
  - Several untyped `IActionResult` work-item endpoints are emitted as `FileResponse`/`application/octet-stream` in NSwag even though runtime behavior is not a file download.
- **Recommended next step**
  - Fix the cache-backed contract end-to-end first: make OpenAPI emit `DataStateResponseDto<T>` for cache-backed routes, update NSwag governance/config so those wrapper types are consumable, regenerate the snapshot/client, then add enforcement and migrate callers.

### Generated-client coverage snapshot

| Current frontend consumer | Generated client usage | Current status |
|---|---|---|
| `ProfileService`, `SettingsService`, `StateClassificationService`, `BacklogHealthCalculationService`, `SprintService`, `ProductService` | Live-allowed endpoints through generated clients | Mostly safe |
| `PipelineService`, `PullRequestService`, `WorkItemFilteringService`, parts of `WorkItemService` | Generated clients on cache-backed routes | Unsafe today because runtime returns `DataStateResponseDto<T>` |
| `HomeProductBarMetricsService`, `SprintDeliveryMetricsService`, `WorkspaceSignalService`, `RoadmapAnalyticsService` | Handwritten partial `IMetricsClient`/`IPullRequestsClient`/`IPipelinesClient` wrappers | Unsafe today for the same reason |
| `BacklogOverviewPage`, `TimelinePanel`, `ValidationHistoryPanel`, `ValidationSummaryPanel`, `DependenciesPanel`, `PortfolioProgressPage` | Direct page/component injection of generated clients | Mixed; several calls target cache-backed routes and are unsafe |
| Generated clients present but not wired in DI | `IBuildQualityClient`, `ICacheSyncClient`, `IProjectsClient`, `IReleasePlanningClient`, `IStartupClient`, `ITriageTagsClient` | Available but not consistently consumable |

## 2. Raw HttpClient inventory

| Service / location | Purpose | Current raw endpoints | Why generated client is not used today | Bucket |
|---|---|---|---|---|
| `PoTool.Client/Services/BuildQualityService.cs` | Build Quality cache-backed reads | `GET /api/buildquality/rolling`, `/sprint`, `/pipeline` | Generated `IBuildQualityClient` still exposes inner DTOs; runtime returns `DataStateResponseDto<T>` | **B** |
| `PoTool.Client/Services/MetricsStateService.cs` | Explicit cache-state reads for metrics and portfolio flow | `GET /api/metrics/*`, `/api/portfolio/*` state endpoints | Generated `IMetricsClient` and partial wrappers are not `DataStateResponseDto<T>`-aware | **B** |
| `PoTool.Client/Services/PipelineStateService.cs` | Explicit cache-state reads for pipeline insights | `GET /api/pipelines/insights` | Generated `IPipelinesClient` expects `PipelineQueryResponseDto<T>`, not `DataStateResponseDto<...>` | **B** |
| `PoTool.Client/Services/PullRequestStateService.cs` | Explicit cache-state reads for PR insights | `GET /api/pullrequests/insights`, `/delivery-insights`, `/sprint-trends` | Generated `IPullRequestsClient` expects inner envelopes only | **B** |
| `PoTool.Client/Services/ProjectService.cs` | Project discovery | `GET /api/projects`, `GET /api/projects/{alias}`, `GET /api/projects/{alias}/products` | `IProjectsClient` exists and matches these live-allowed routes; current usage is a convenience shortcut | **A** |
| `PoTool.Client/Services/ProjectService.cs` | Project planning summary | `GET /api/projects/{alias}/planning-summary` | Runtime wraps cache-backed response in `DataStateResponseDto<ProjectPlanningSummaryDto>`; generated `IProjectsClient.GetPlanningSummaryAsync()` does not | **B** |
| `PoTool.Client/Services/ReleaseNotesService.cs` | Load release notes | `GET /api/settings/release-notes` | `ISettingsClient.GetReleaseNotesAsync()` already exists | **A** |
| `PoTool.Client/Services/ConfigurationTransferService.cs` | Settings export/import | `GET /api/settings/configuration-export`, `POST /api/settings/configuration-import` | `ISettingsClient.ExportConfigurationAsync()` and `ImportConfigurationAsync()` already exist | **A** |
| `PoTool.Client/Services/TriageTagService.cs` | Triage tag CRUD | `GET/POST/PUT/DELETE /api/TriageTags*` | `ITriageTagsClient` already exists in `ApiClient.g.cs`, but is not registered or consumed | **A** |
| `PoTool.Client/Services/TeamService.cs` | Team creation | `POST /api/teams` | `ITeamsClient.CreateTeamAsync()` already exists; current raw call is legacy convenience | **A** |
| `PoTool.Client/Services/TfsConfigService.cs` | Read saved TFS config | `GET /api/tfsconfig` | Generated `IClient.GetTfsConfigAsync()` throws on 204 instead of surfacing nullable/empty semantics | **D** |
| `PoTool.Client/Services/TfsConfigService.cs` | Save config / validate / verify | `POST /api/tfsconfig`, `GET /api/tfsvalidate`, `POST /api/tfsverify` | Generated `IClient` exists, but current service relies on direct response/status/body inspection; best near-term shape is a thin wrapper over generated calls | **D** |
| `PoTool.Client/Services/CacheSyncService.cs` | Cache-sync status reads | `GET /api/CacheSync/{productOwnerId}`, `/status`, `/insights`, `/activity-ledger-validation`, `/changes-since-sync` | `ICacheSyncClient` exists and matches these routes; not wired in DI today | **A** |
| `PoTool.Client/Services/CacheSyncService.cs` | Cache-sync command flows | `POST /api/CacheSync/{productOwnerId}/sync`, `/cancel`, `DELETE /api/CacheSync/{productOwnerId}`, `POST /reset` | Generated client exists, but 202/409/404 workflow semantics still need a thin wrapper | **D** |
| `PoTool.Client/Services/StartupOrchestratorService.cs` | Startup readiness classification | `GET /api/startup/readiness` | `IStartupClient.GetStartupReadinessAsync()` exists; service can wrap generated exceptions into its own readiness model | **A** |
| `PoTool.Client/Services/ReleasePlanningService.cs` | Release-planning board reads and mutations | `GET/POST/PUT/DELETE /api/releaseplanning/*` | `IReleasePlanningClient` exists, but all `/api/releaseplanning/*` routes are cache-classified and runtime-wrapped; current contract is not trustworthy | **B** |
| `PoTool.Client/Services/WorkItemService.cs` | Cache-backed work-item state reads | `GET /api/workitems`, `/validated`, `/validation-triage`, `/validation-queue`, `/validation-fix`, `/by-root-ids` state calls | Generated `IWorkItemsClient` exposes inner DTOs only; runtime returns `DataStateResponseDto<T>` | **B** |
| `PoTool.Client/Services/WorkItemService.cs` | Cache-backed work-item reads that still deserialize inner DTOs | `GET /api/workitems/validated/{tfsId}`, `/validation-triage`, `/validation-queue`, `/validation-fix`, `/by-root-ids`, `/health-summary/{productId}` | Current raw calls are themselves contract-broken because those routes are cache-backed and wrapper-hidden in OpenAPI | **B** |
| `PoTool.Client/Services/WorkItemService.cs` | Live work-item helper routes | `GET /api/workitems/area-paths/from-tfs`, `/goals/from-tfs`, `/bug-severity-options`; `POST /api/workitems/by-root-ids/refresh-from-tfs`; `POST /api/workitems/{tfsId}/tags`; `POST /api/workitems/{tfsId}/title-description` | Generated client methods already exist for these live-allowed routes | **A** |
| `PoTool.Client/Services/WorkItemService.cs` | Refresh single work item from TFS | `POST /api/workitems/{tfsId}/refresh-from-tfs` | NSwag currently mis-generates this untyped endpoint as `Task<FileResponse>` | **C** |
| `PoTool.Client/Components/Onboarding/OnboardingWizard.razor` | TFS discovery during onboarding | `GET /api/startup/tfs-projects`, `/tfs-teams`, `/git-repositories` | `IStartupClient` exists but is not registered/used | **A** |
| `PoTool.Client/Components/Onboarding/OnboardingWizard.razor` | Save-and-verify onboarding flow | `POST /api/tfsconfig/save-and-verify` | Current flow consumes streamed progress lines plus SignalR; keep as a thin approved wrapper for now | **D** |
| `PoTool.Client/Components/Onboarding/OnboardingWizard.razor` | Product repository creation | `POST /api/products/{id}/repositories` | `ProductService.CreateRepositoryAsync()` already wraps the generated client | **A** |
| `PoTool.Client/Components/Settings/TfsTeamPickerDialog.razor` | Team picker discovery | `GET /api/startup/tfs-teams` | `IStartupClient.GetTfsTeamsAsync()` already exists | **A** |
| `PoTool.Client/Pages/TfsConfig.razor` | Legacy/unused page project lookup | `GET /api/startup/tfs-projects` | Route is disabled and page is marked obsolete; not worth migrating before deletion | **E** |

## 3. OpenAPI truthfulness gaps

| Endpoint / scope | Declared contract | Runtime behavior | Exact mismatch | Severity |
|---|---|---|---|---|
| All cache-backed routes resolved by `DataSourceModeConfiguration.RequiresCache(...)` in `PoTool.Api/Configuration/DataSourceModeConfiguration.cs` | Inner `ActionResult<T>` / `ActionResult<QueryResponseDto<T>>` contract from controller signature | `CacheBackedDataStateContractFilter` wraps runtime payloads into `DataStateResponseDto<T>` and short-circuits `NotReady` / `Failed` before the action executes | OpenAPI is generated from controller signatures and never reflects the runtime wrapper | **Critical** |
| `GET /api/buildquality/rolling`, `/api/buildquality/sprint`, `/api/buildquality/pipeline` | `DeliveryQueryResponseDto<BuildQualityPageDto>`, `DeliveryQueryResponseDto<DeliveryBuildQualityDto>`, `PipelineBuildQualityDto` | `DataStateResponseDto<...>` | Wrapper hidden; `Data` is nullable at runtime, generated signature is non-null inner DTO | **Critical** |
| `GET /api/projects/{alias}/planning-summary` | `ProjectPlanningSummaryDto`, 404 on missing summary | 200 with `DataStateResponseDto<ProjectPlanningSummaryDto>`; `NotFound()` becomes `DataStateDto.Empty` | Response code contract and payload shape both change | **Critical** |
| `/api/metrics/*` cache-backed routes including `/effort-distribution`, `/epic-forecast/{epicId}`, `/sprint-trend`, `/portfolio-progress-trend`, `/capacity-calibration`, `/portfolio-delivery`, `/home-product-bar`, `/work-item-activity/{workItemId}`, plus exact `/api/portfolio/*` routes | Inner metric DTO or query/response DTO | 200 `DataStateResponseDto<T>` | OpenAPI still declares the inner type; 404/204/5xx are normalized into 200 envelopes by the filter | **Critical** |
| `/api/pipelines/*` except `/api/pipelines/definitions` | `IEnumerable<PipelineDto>` or `PipelineQueryResponseDto<T>` | 200 `DataStateResponseDto<T>` | Entire controller family is cache-backed except the explicit definitions route | **Critical** |
| `/api/pullrequests/*` | `PullRequestDto`, `IEnumerable<PullRequestDto>`, `PullRequestQueryResponseDto<T>`, comments, iterations, file changes | 200 `DataStateResponseDto<T>` | Even detail/read endpoints such as `/api/pullrequests/{id}` and `/comments` are wrapper-hidden | **Critical** |
| `/api/releaseplanning/*` | Raw board/result DTOs with declared 400/404 semantics | 200 `DataStateResponseDto<T>` on success and pre-action `NotReady`/`Failed` envelopes on cache failure | Route classification applies to all verbs, so writes are also wrapper-hidden | **Critical** |
| `/api/filtering/*` | Raw filter response DTOs | 200 `DataStateResponseDto<T>` | Generated `IFilteringClient` is built from a contract that is false at runtime | **Critical** |
| Cache-backed work-item read routes such as `GET /api/workitems`, `/validated`, `/validated/{tfsId}`, `/validation-triage`, `/validation-queue`, `/validation-fix`, `/filter/{filter}`, `/goals/all`, `/dependency-graph`, `/validation-history`, `/validation-impact-analysis`, `/by-root-ids`, `/backlog-state/{productId}`, `/health-summary/{productId}` | Raw DTOs / collections | 200 `DataStateResponseDto<T>` | Large work-item read surface still advertises inner DTOs only | **Critical** |
| `GET /api/tfsconfig` | 200 `TfsConfig`, 204 no content | Runtime matches 200/204, but generated clients surface only non-null `TfsConfig` | Nullability truth is lost on the client side even though OpenAPI declares 204 | **High** |
| `POST /api/workitems/{tfsId}/refresh-from-tfs`, `POST /api/workitems/{tfsId}/backlog-priority`, `POST /api/workitems/{tfsId}/iteration-path` | Swagger emits `application/octet-stream` / file response because the actions are untyped `IActionResult` | Runtime returns empty 200s or standard error responses, not file downloads | OpenAPI content type and generated client shape are wrong | **High** |

### Nullable / error-contract specifics

- `PoTool.Shared/DataState/DataStateResponseDto.cs` makes `Data` nullable by design, but current cache-backed generated signatures expose non-null inner DTOs.
- `CacheBackedDataStateContractFilter` converts `NotFoundResult`, `NotFoundObjectResult`, `NoContentResult`, and 5xx `ObjectResult`/`StatusCodeResult` cases into 200 envelopes, so declared response codes for cache-backed routes are not truthful.
- No OpenAPI processor currently compensates for the filter behavior; `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` only registers a plain `AddOpenApiDocument(...)` block.

## 4. NSwag correctness gaps

| Endpoint / client method | Expected signature | Actual generated signature | Impact |
|---|---|---|---|
| `IMetricsClient.GetEffortDistributionAsync()` | `Task<DataStateResponseDto<EffortDistributionDto>>` or approved cache wrapper | `Task<EffortDistributionDto>` | Direct generated call will deserialize the wrong JSON shape |
| `IMetricsClient.GetPortfolioProgressAsync()` in `ApiClient.PortfolioConsumption.cs` | Cache-aware wrapper over `DataStateResponseDto<PortfolioProgressDto>` | `Task<PortfolioProgressDto>` | Existing handwritten partial wrapper bakes in the wrong contract |
| `IMetricsClient.GetPortfolioDeliveryEnvelopeAsync()` / `GetCapacityCalibrationEnvelopeAsync()` / `GetSprintTrendMetricsEnvelopeAsync()` | `DataStateResponseDto<DeliveryQueryResponseDto<T>>` or `DataStateResponseDto<SprintQueryResponseDto<T>>` | `DeliveryQueryResponseDto<T>` / `SprintQueryResponseDto<T>` | Handwritten “envelope” helpers are only query-response aware, not cache-state aware |
| `IBuildQualityClient.GetRollingAsync()` / `GetSprintAsync()` / `GetPipelineAsync()` | `DataStateResponseDto<...>` | Inner DTO only | Explains why `BuildQualityService` had to bypass generated clients |
| `IPipelinesClient.GetInsightsAsync()` and `GetInsightsEnvelopeAsync()` | `DataStateResponseDto<PipelineQueryResponseDto<PipelineInsightsDto>>` | `PipelineQueryResponseDto<PipelineInsightsDto>` | Both generated and handwritten wrapper variants are unsafe |
| `IPullRequestsClient.GetInsightsAsync()` / `GetDeliveryInsightsAsync()` / `GetSprintTrendsAsync()` and matching `*EnvelopeAsync()` partials | `DataStateResponseDto<PullRequestQueryResponseDto<T>>` | `PullRequestQueryResponseDto<T>` | Same bug class as pipeline/metrics helpers |
| `IProjectsClient.GetPlanningSummaryAsync()` | `Task<DataStateResponseDto<ProjectPlanningSummaryDto>>` | `Task<ProjectPlanningSummaryDto>` | Project planning summary cannot safely move to generated clients yet |
| `IWorkItemsClient.GetAllAsync()`, `GetAllWithValidationAsync()`, `GetByIdWithValidationAsync()`, `GetValidationTriageAsync()`, `GetValidationQueueAsync()`, `GetValidationFixSessionAsync()`, `GetByRootIdsAsync()`, `GetBacklogStateAsync()`, `GetHealthSummaryAsync()` | Cache-aware `DataStateResponseDto<T>` signatures | Raw DTO / collection signatures | Large work-item read surface is unsafe today |
| `IFilteringClient.*` | Cache-aware `DataStateResponseDto<T>` signatures | Raw DTO signatures | Current generated filtering client is not trustworthy on runtime cache-backed routes |
| `IReleasePlanningClient.*` | Cache-aware signatures for every route under `/api/releaseplanning/*` | Raw DTO signatures | All read/write release-planning methods are contract-wrong today |
| `IClient.GetTfsConfigAsync()` | Nullable/204-aware result or wrapped response | `Task<TfsConfig>` and throws on 204 | Prevents generated-only use for TFS config reads |
| `IWorkItemsClient.RefreshFromTfsAsync()`, `UpdateBacklogPriorityAsync()`, `UpdateIterationPathAsync()` | `Task`, `Task<bool>`, or typed result | `Task<FileResponse>` | OpenAPI/NSwag interpret untyped `IActionResult` as file downloads |
| `PoTool.Client/nswag.json` | Must be able to consume cache wrapper types | `excludedTypeNames` includes `DataStateDto` and `DataStateResponseDto`; `additionalNamespaceUsages` omits `PoTool.Shared.DataState`; `wrapResponses` is `false` | Even with a corrected document, current config will not produce usable cache-aware clients |

## 5. Cache-backed endpoint audit

| Runtime-wrapped endpoints | Wrapper visible in OpenAPI? | Generated clients safe? |
|---|---|---|
| `GET /api/buildquality/rolling`, `/sprint`, `/pipeline` | No | No |
| `GET /api/projects/{alias}/planning-summary` | No | No |
| `GET /api/metrics/sprint`, `/backlog-health`, `/multi-iteration-health`, `/effort-distribution`, `/capacity-plan`, `/epic-forecast/{epicId}`, `/effort-imbalance`, `/effort-distribution-trend`, `/effort-concentration-risk`, `/effort-estimation-suggestions`, `/effort-estimation-quality`, `/sprint-trend`, `/portfolio-progress-trend`, `/capacity-calibration`, `/portfolio-delivery`, `/home-product-bar`, `/sprint-execution`, `/work-item-activity/{workItemId}` | No | No |
| `GET /api/portfolio/progress`, `/snapshots`, `/comparison`, `/trends`, `/signals` | No | No |
| `GET /api/pipelines`, `/{id}/runs`, `/metrics`, `/runs`, `/insights` | No for all cache-classified routes; `/definitions` is the live exception | No on cache-classified routes |
| `GET /api/pullrequests`, `/{id}`, `/by-workitem/{workItemId}`, `/metrics`, `/filter`, `/{id}/iterations`, `/{id}/comments`, `/{id}/filechanges`, `/sprint-trends`, `/review-bottleneck`, `/insights`, `/delivery-insights` | No | No |
| `GET /api/workitems`, `/area-paths`, `/validated`, `/validated/{tfsId}`, `/validation-triage`, `/validation-queue`, `/validation-fix`, `/filter/{filter}`, `/goals/all`, `/advanced-filter`, `/dependency-graph`, `/validation-history`, `/validation-impact-analysis`, `/by-root-ids`, `/backlog-state/{productId}`, `/health-summary/{productId}` | No | No |
| All `/api/releaseplanning/*` routes | No | No |
| All `/api/filtering/*` routes | No | No |

### Notes

- The wrapper is applied by `CacheBackedDataStateContractFilter` before/after action execution, not by controller return types.
- `EnforceSharedDtoActionResultContractFilter` already knows the runtime contract should be `DataStateResponseDto<T>` on cache-backed paths, which confirms the runtime/OpenAPI split is real rather than incidental.
- Current client-side cache handling is fragmented: some pages use raw `HttpClient` state services, some use generated partial wrappers, and both strategies are operating against incomplete contracts.

## 6. Proposed repository rule

### Exact policy text

1. **All standard frontend ↔ backend HTTP calls must go through NSwag-generated clients or an approved thin wrapper located under `PoTool.Client/ApiClient/`.**
2. **Raw `HttpClient` is forbidden in feature services, pages, and components.**
3. **Approved thin wrappers may only do one of the following:**
   - adapt generated-client exception/status semantics into a richer UI-facing result;
   - handle generated-client gaps for streaming/progress or other explicitly approved protocol quirks;
   - normalize cache-backed `DataStateResponseDto<T>` consumption when the generated client surface cannot express it directly.
4. **Thin wrappers must not redefine endpoint paths, DTO contracts, or ad hoc JSON models that duplicate the generated client surface.**

### Allowed exceptions

- Streaming/progress flows such as `/api/tfsconfig/save-and-verify`.
- Short-term status-sensitive wrappers for endpoints like `GET /api/tfsconfig` and cache-sync trigger/delete/reset flows.
- Handwritten partial client extensions under `PoTool.Client/ApiClient/` while the generated cache-backed contract is being repaired.

### Forbidden patterns

- Injecting `HttpClient` into `PoTool.Client/Services/**`, pages, or components for normal feature calls.
- Calling `_httpClient.GetFromJsonAsync<T>()` / `PostAsJsonAsync(...)` directly from feature services when a generated client exists.
- Handwritten page/component calls to `/api/*` URLs.
- Client wrappers that deserialize inner DTOs for cache-backed routes without first handling `DataStateResponseDto<T>`.

## 7. Enforcement plan

### Architecture tests

- Add a governance test that scans `PoTool.Client/Services`, `PoTool.Client/Pages`, and `PoTool.Client/Components` and fails on:
  - `@inject HttpClient`
  - constructor injection of `HttpClient`
  - direct `/api/` string literals outside `PoTool.Client/ApiClient/**`
- Add a client-contract governance test that enumerates cache-backed routes from `DataSourceModeConfiguration` and verifies the checked-in OpenAPI snapshot exposes `DataStateResponseDto<T>` for each shared-contract route.
- Extend NSwag governance tests so `DataStateDto` and `DataStateResponseDto` are no longer excluded once the contract fix lands.
- Add a generated-client fitness test that fails if cache-backed generated methods return inner DTOs instead of `DataStateResponseDto<T>` or an approved wrapper type.

### CI checks

- Keep the existing governance test category as the main PR gate for this rule.
- Add a snapshot diff check: when API contract code changes touch cache-backed controllers, filters, or route classification, the governed `PoTool.Client/ApiClient/OpenApi/swagger.json` must be regenerated in the same change.
- Add a client regen check: when the snapshot changes, `PoTool.Client/ApiClient/Generated/ApiClient.g.cs` must change in the same PR.

### Regeneration policy

- Treat `PoTool.Client/ApiClient/OpenApi/swagger.json` as the governed snapshot.
- Treat `PoTool.Client/nswag.json` as the only NSwag config.
- Regenerate clients only from the governed snapshot after API contract tests pass.
- Require one owner pair for contract updates: API owner updates the OpenAPI truth, client owner regenerates and validates the generated surface.

### Ownership

- **API owner:** cache-backed contract truth (`DataSourceModeConfiguration`, filters, controller/OpenAPI metadata).
- **Client owner:** `nswag.json`, `ApiClient/Generated/ApiClient.g.cs`, and approved wrappers in `PoTool.Client/ApiClient/`.
- **Governance owner:** MSTest audits that enforce the rule in CI.

## 8. Recommended fix order

### First

**Make cache-backed OpenAPI truthful.**

Justification:
- This is the root defect class.
- Until the document shows `DataStateResponseDto<T>` for cache-backed routes, regeneration keeps reintroducing wrong signatures.
- This work must include the response-code story: cache-backed 404/204/5xx behavior is normalized into 200 envelopes today and the document must reflect that choice explicitly.

### Second

**Update NSwag/governance so generated output can represent the truthful contract.**

Justification:
- The current config still excludes `DataStateDto` and `DataStateResponseDto`, and current partial wrappers are wrapper-blind.
- Fixing OpenAPI without fixing generation still leaves generated clients unusable.
- This is also the moment to correct the `IActionResult`/`FileResponse` bug class for work-item mutation endpoints.

### Third

**Add enforcement, then migrate breadth-first from easiest live routes to cache-backed features.**

Suggested migration order:
1. Immediate-A items: `TriageTagService`, `ReleaseNotesService`, `ConfigurationTransferService`, `TeamService.CreateTeamAsync`, `ProjectService` live routes, startup discovery calls, cache-sync read calls, repository creation in onboarding.
2. Thin-wrapper D items: `TfsConfigService`, cache-sync command flows, onboarding save-and-verify.
3. Cache-backed B items after contract repair: `BuildQualityService`, `MetricsStateService`, `PipelineStateService`, `PullRequestStateService`, `ReleasePlanningService`, cache-backed `WorkItemService` reads, `ProjectService.GetPlanningSummaryAsync`, generated cache-backed client consumers.

Justification:
- Correctness must come before enforcement.
- Live-allowed routes give quick wins and shrink raw `HttpClient` usage without depending on the cache contract repair.
- Cache-backed migration should wait until the generated surface is genuinely trustworthy.
