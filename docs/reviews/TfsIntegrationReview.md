# TFS Integration Review — Fix-Ready, Risk-Ranked Findings

## 1. Executive Summary
- **TFS-001 (S1):** Transient HTTP failures and rate limits never trigger retries because `HandleHttpErrorsAsync` does not surface status codes or `TfsRateLimitException`, causing whole syncs to fail on a single 429/5xx response.
- **TFS-002 (S1):** List endpoints for projects, teams, repositories, pull requests, pipelines, iterations, and comments ignore continuation tokens, silently truncating data once the default page size is reached.
- **TFS-003 (S1):** Configured `TimeoutSeconds` is not enforced on read calls, so stalled TFS requests can block sync stages for unbounded durations relative to configuration.
- **TFS-004 (S1):** Work item sync watermarks use `RetrievedAt` instead of actual `System.ChangedDate`, allowing updates between query execution and retrieval timestamps to be skipped.
- **TFS-005 (S1):** Date filters for WIQL work item sync and pull request queries apply a `Z` suffix without converting to UTC, so local timestamps are interpreted as UTC and shift filter windows.

## 2. Scope Inventory

### TFS-facing integration layer
| File path | Class | Responsibility | Exact TFS API areas touched |
| --- | --- | --- | --- |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` | `RealTfsClient` | Core client setup, URL building | All TFS REST endpoints via `ProjectUrl`/`CollectionUrl` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Infrastructure.cs` | `RealTfsClient` | Repositories, retry/error handling | `/_apis/git/repositories`, HTTP error handling |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs` | `RealTfsClient` | Area paths, WIQL, work item batch retrieval | `/_apis/wit/classificationnodes/areas`, `/_apis/wit/wiql`, `/_apis/wit/workitemsbatch` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` | `RealTfsClient` | Recursive hierarchy discovery and ancestor completion | `/_apis/wit/wiql` (WorkItemLinks), `/_apis/wit/workitemsbatch` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemRevisions.cs` | `RealTfsClient` | Per-item revisions | `/_apis/wit/workitems/{id}/revisions` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs` | `RealTfsClient` | Work item PATCH/POST write operations | `/_apis/wit/workitems/{id}`, `/_apis/wit/workitems/$type` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.PullRequests.cs` | `RealTfsClient` | Git PRs, iterations, comments, file changes | `/_apis/git/repositories`, `/_apis/git/repositories/{repo}/pullrequests`, `threads`, `iterations`, `changes` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs` | `RealTfsClient` | Build/release definitions and runs | `/_apis/build/definitions`, `/_apis/build/builds`, `/_apis/release/definitions`, `/_apis/release/releases` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Teams.cs` | `RealTfsClient` | Projects, teams, iterations, work item types | `/_apis/projects`, `/_apis/projects/{project}/teams`, `/_apis/work/teamsettings/teamfieldvalues`, `/_apis/work/teamsettings/iterations`, `/_apis/wit/workitemtypes` |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | `RealTfsClient` | Verify TFS capabilities with real calls | Projects, WIQL, work items batch, repos/PRs, build/release endpoints |
| `PoTool.Integrations.Tfs/Clients/RealRevisionTfsClient.cs` | `RealRevisionTfsClient` | Reporting revisions + per-item revisions | `/_apis/wit/reporting/workitemrevisions`, `/_apis/wit/workitems/{id}/revisions` |
| `PoTool.Integrations.Tfs/Clients/TfsRequestSender.cs` | `TfsRequestSender` | Per-request timeout and header handling | Shared request wrapper for all TFS REST calls |
| `PoTool.Integrations.Tfs/Clients/TfsRequestThrottler.cs` | `TfsRequestThrottler` | Concurrency throttling | Shared throttling for read/write TFS calls |

### Immediate callers (one hop)
| File path | Class | Responsibility | TFS API areas touched via client |
| --- | --- | --- | --- |
| `PoTool.Api/Services/Sync/WorkItemSyncStage.cs` | `WorkItemSyncStage` | Sync work items from TFS | WIQL + work items batch |
| `PoTool.Api/Services/Sync/TeamSprintSyncStage.cs` | `TeamSprintSyncStage` | Sync team iterations | Team settings iterations |
| `PoTool.Api/Services/Sync/PullRequestSyncStage.cs` | `PullRequestSyncStage` | Sync pull requests | Git repositories + pull requests |
| `PoTool.Api/Services/Sync/PipelineSyncStage.cs` | `PipelineSyncStage` | Sync pipeline runs | Build/release definitions + runs |
| `PoTool.Api/Services/RevisionIngestionService.cs` | `RevisionIngestionService` | Reporting revisions ingestion | Reporting work item revisions |
| `PoTool.Api/Services/RelationRevisionHydrator.cs` | `RelationRevisionHydrator` | Hydrate relation deltas | Per-item work item revisions |
| `PoTool.Api/Services/LiveWorkItemReadProvider.cs` | `LiveWorkItemReadProvider` | Live work item reads | WIQL + work items batch |
| `PoTool.Api/Services/LivePullRequestReadProvider.cs` | `LivePullRequestReadProvider` | Live PR reads | Git PRs, threads, iterations, changes |
| `PoTool.Api/Services/LivePipelineReadProvider.cs` | `LivePipelineReadProvider` | Live pipeline reads | Build/release definitions + runs |
| `PoTool.Api/Handlers/WorkItems/GetAreaPathsFromTfsQueryHandler.cs` | `GetAreaPathsFromTfsQueryHandler` | Settings-only area path discovery | Classification nodes API |
| `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs` | `GetGoalsFromTfsQueryHandler` | Settings-only goals selector | WIQL + work items batch |
| `PoTool.Api/Handlers/Settings/GetWorkItemTypeDefinitionsQueryHandler.cs` | `GetWorkItemTypeDefinitionsQueryHandler` | Settings-only type/state discovery | Work item type definitions |
| `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs` | `ValidateWorkItemQueryHandler` | Validate work item ID | Work items batch |
| `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs` | `GetWorkItemRevisionsQueryHandler` | Retrieve revision history | Work item revisions |
| `PoTool.Api/Services/BugTriageStateService.cs` | `BugTriageStateService` | Write-back severity/tags | Work item PATCH updates |
| `PoTool.Api/Handlers/WorkItems/BulkAssignEffortCommandHandler.cs` | `BulkAssignEffortCommandHandler` | Bulk effort updates | Work item PATCH updates |
| `PoTool.Api/Handlers/WorkItems/FixValidationViolationBatchCommandHandler.cs` | `FixValidationViolationBatchCommandHandler` | Bulk state updates | Work item PATCH updates |
| `PoTool.Api/Handlers/ReleasePlanning/SplitEpicCommandHandler.cs` | `SplitEpicCommandHandler` | Work item creation and relinking | Work item creation + parent link update |
| `PoTool.Api/Controllers/StartupController.cs` | `StartupController` | Startup team lookup | Teams + team settings |
| `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs` | `ApiApplicationBuilderExtensions` | TFS validation/verification endpoints | Projects + verification steps |

## 3. Findings

TFS-001 — Transient and rate-limit failures never trigger retries

Severity: S1  
Fix Complexity: S  
Blast Radius: High  

Affected code:
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Infrastructure.cs`
  - `HandleHttpErrorsAsync`
  - `ExecuteWithRetryAsync`
  - `IsTransient`
- `PoTool.Shared/Exceptions/TfsRateLimitException.cs` (unused in RealTfsClient)

What is wrong:
- `HandleHttpErrorsAsync` always throws `new TfsException(...)` without status code or a 429-specific exception.
- `ExecuteWithRetryAsync` only retries `TfsRateLimitException` or `TfsException` with `StatusCode >= 500`; since `StatusCode` is never set, retries never happen.

Failure mode:
- A single 429 or 5xx response immediately aborts sync stages, verification, or live reads with no retry/backoff.

Why this matters:
- Large syncs are highly likely to hit transient 5xx/429 responses; without retries, data ingestion and metrics fail repeatedly.

Root cause:
- `HandleHttpErrorsAsync` uses `new TfsException(message)` instead of `new TfsException(message, statusCode, body)` and never creates `TfsRateLimitException` for 429.

Fix (Copilot-actionable):
- In `RealTfsClient.Infrastructure.HandleHttpErrorsAsync`, capture `(int)response.StatusCode`, body, and the `Retry-After` header (e.g., `var retryAfter = response.Headers.RetryAfter?.Delta;` and pass it into `TfsRateLimitException`).
- If status is 429, throw `new TfsRateLimitException(message, body, retryAfter)`.
- Otherwise throw `new TfsException(message, statusCode, body)` so `StatusCode` is populated.
- Update `IsTransient` to treat `StatusCode >= 500` and `408` as transient; leave auth/403 as non-transient.
- Do **not** change method signatures or external behavior beyond retry classification.

Offline proof / test:
- Unit test: given a 429 response with `Retry-After`, assert `TfsRateLimitException` with `RetryAfter` is thrown.
- Unit test: given a 500 response, assert `TfsException.StatusCode == 500` and `ExecuteWithRetryAsync` retries at least once.

Risk if left unfixed:
- Sync, verification, and live reads will continue to fail on transient outages, causing repeated operational incidents.

---

TFS-002 — Continuation tokens are ignored for all list endpoints

Severity: S1  
Fix Complexity: M  
Blast Radius: High  

Affected code:
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Infrastructure.cs`
  - `GetRepositoriesInternalAsync` (`/_apis/git/repositories`)
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.PullRequests.cs`
  - `GetPullRequestsAsync` (`/_apis/git/repositories/{repo}/pullrequests`)
  - `GetPullRequestIterationsAsync` (`/iterations`)
  - `GetPullRequestCommentsAsync` (`/threads`)
  - `GetPullRequestFileChangesAsync` (`/changes`)
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Pipelines.cs`
  - `GetPipelinesAsync` (`/_apis/build/definitions`, `/_apis/release/definitions`)
  - `GetPipelineRunsAsync` (`/_apis/build/builds`, `/_apis/release/releases`)
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Teams.cs`
  - `GetTfsProjectsAsync` (`/_apis/projects`)
  - `GetTfsTeamsAsync` (`/_apis/projects/{project}/teams`)
  - `GetTeamIterationsAsync` (`/_apis/work/teamsettings/iterations`)
  - `GetWorkItemTypeDefinitionsAsync` (`/_apis/wit/workitemtypes`)

What is wrong:
- None of the list endpoints inspect `x-ms-continuationtoken` or `continuationToken` and therefore only return the first page.

Failure mode:
- Repositories, PRs, pipeline definitions, runs, and team metadata are silently truncated once the default page size is exceeded (typically 100).

Why this matters:
- Sync stages (PRs, pipelines, sprints) and live reads will operate on partial datasets, producing incorrect metrics and dashboards without any error.

Root cause:
- List endpoints are implemented as single-request reads without pagination loops.

Fix (Copilot-actionable):
- Add a shared paging helper in `RealTfsClient.Infrastructure` (e.g., `FetchPagedAsync`) that:
  - Executes the initial request.
  - Reads `x-ms-continuationtoken` (header) first; if absent, fall back to `continuationToken` in the body.
  - Repeats until token is empty, appending `continuationToken` to the query string.
- Update each affected method to use the helper and aggregate results across pages.
- Preserve existing DTO mapping; do **not** change returned shapes or order unless required for paging.

Offline proof / test:
- Unit test: simulate two pages with `x-ms-continuationtoken` and verify aggregated count.
- Unit test: verify that the helper stops when the token is empty and does not duplicate items.

Risk if left unfixed:
- Production datasets with >100 entities will always be partially ingested, leading to persistent under-reporting.

---

TFS-003 — Configured timeouts are ignored on read calls

Severity: S1  
Fix Complexity: S  
Blast Radius: High  

Affected code:
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.*` (all GET/POST read calls)
- `PoTool.Integrations.Tfs/Clients/RealRevisionTfsClient.cs` (all read calls)
- `PoTool.Integrations.Tfs/Clients/TfsRequestSender.cs` (timeout wrapper unused)

What is wrong:
- `TfsConfigEntity.TimeoutSeconds` is only applied to write PATCH operations; read calls use `HttpClient.GetAsync/PostAsync` directly.
- The dedicated `TfsRequestSender` timeout wrapper is injected but never used.

Failure mode:
- Stalled TFS requests can block sync stages far longer than configured, preventing progress callbacks and increasing lock contention.

Why this matters:
- Sync stages are long-running and gated by external TFS calls; unbounded waits result in job timeouts and stalled ingestion.

Root cause:
- Read calls bypass `TfsRequestSender` and do not create per-request timeout tokens using `TimeoutSeconds`.

Fix (Copilot-actionable):
- Replace direct `httpClient.GetAsync/PostAsync` calls with `_requestSender.SendAsync` and pass `config.TimeoutSeconds`.
- For `RealRevisionTfsClient`, add a small wrapper that mirrors the same timeout behavior (or reuse `TfsRequestSender`).
- Do **not** change existing request headers or URLs.

Offline proof / test:
- Unit test: use a fake handler that delays beyond `TimeoutSeconds` and assert `TimeoutException` is thrown for a read call.

Risk if left unfixed:
- Sync pipelines can hang or exceed job limits during TFS slowdowns, causing repeated failures and incomplete cache refresh.

---

TFS-004 — Work item watermarks use retrieval time instead of ChangedDate

Severity: S1  
Fix Complexity: M  
Blast Radius: Medium  

Affected code:
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`
  - `RequiredWorkItemFields` (missing `System.ChangedDate`)
  - Work item parsing (no `ChangedDate` extraction)
- `PoTool.Shared/WorkItems/WorkItemDto.cs` (no `ChangedDate` field)
- `PoTool.Api/Services/Sync/WorkItemSyncStage.cs`
  - `UpsertWorkItemsAsync` uses `RetrievedAt` as watermark
- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`
  - `TfsChangedDate` is never populated

What is wrong:
- Incremental sync uses `RetrievedAt` as a watermark, but TFS filtering is based on `System.ChangedDate`.
- The client does not fetch or propagate `System.ChangedDate`, so the watermark can be later than the actual change timestamps.

Failure mode:
- Updates that occur between WIQL execution and `RetrievedAt` are skipped on the next incremental run, causing silent data loss.

Why this matters:
- Work item changes drive all downstream metrics; missing revisions corrupt sprint trends and validation results.

Root cause:
- `System.ChangedDate` is not requested or stored, so the only available timestamp is the local retrieval time.

Fix (Copilot-actionable):
- Add `System.ChangedDate` to `RequiredWorkItemFields`.
- Extend `WorkItemDto` with `ChangedDate` and populate it in both batch phases.
- Map `ChangedDate` into `WorkItemEntity.TfsChangedDate` during sync.
- Use the max `ChangedDate` (not `RetrievedAt`) as the work item watermark.
- Do **not** alter unrelated DTO fields or cache schema beyond populating the existing `TfsChangedDate` column.

Offline proof / test:
- Unit test: given two work items with `ChangedDate` values, ensure the watermark equals the max changed date.
- Unit test: verify `TfsChangedDate` is persisted when mapping a work item DTO.

Risk if left unfixed:
- Incremental sync will permanently miss updates, leading to incorrect product health metrics and stale work item state.

---

TFS-005 — Incremental time filters encode non-UTC timestamps as UTC

Severity: S1  
Fix Complexity: XS  
Blast Radius: Medium  

Affected code:
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`
  - `GetWorkItemsAsync` (`System.ChangedDate` filter)
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.PullRequests.cs`
  - `GetPullRequestsAsync` (`searchCriteria.minTime`, `searchCriteria.maxTime`)

What is wrong:
- Date filters are formatted as `yyyy-MM-ddTHH:mm:ssZ` directly from `DateTimeOffset`, adding a `Z` suffix without converting to UTC. This causes local timestamps to be interpreted as UTC by TFS.

Failure mode:
- Incremental sync windows shift by the local UTC offset, causing missing or duplicated work items and PRs.

Why this matters:
- Incremental sync correctness depends on exact time windows; incorrect offsets lead to silent data loss.

Root cause:
- The code formats `DateTimeOffset` without converting to UTC or preserving the offset.

Fix (Copilot-actionable):
- Convert to UTC and format using round-trip ISO 8601 (`var utc = since.Value.ToUniversalTime(); var formatted = utc.ToString("O");`). This yields a `Z` suffix and matches the format already used in pipeline filters.
- Apply the same helper for `minTime`/`maxTime` in pull requests.
- URL-encode the formatted value.

Offline proof / test:
- Unit test: given `2025-01-01T10:00:00+02:00`, assert query string includes `2025-01-01T08:00:00.0000000Z`.

Risk if left unfixed:
- Incremental sync will systematically drift and skip changes in non-UTC environments.

## 4. Cross-cutting systemic risks
- **Resilience gaps across clients** (TFS-001, TFS-003): retry logic and timeout enforcement are inconsistent, so transient outages either fail immediately or stall for long periods.
- **Paging/continuation inconsistencies** (TFS-002): list endpoints across Git, build, and team APIs share the same truncation risk, meaning data completeness cannot be trusted.
- **Time-based sync drift** (TFS-004, TFS-005): incremental filters and watermarks are not aligned to authoritative TFS timestamps, leading to gradual data loss.

## 5. Stop-the-line invariants
1. **All list endpoints must exhaust continuation tokens** (`RealTfsClient.Infrastructure.FetchPagedAsync`): verify by unit test that a two-page response is fully aggregated; log page count.
2. **429 responses must throw `TfsRateLimitException` with `Retry-After`** (`RealTfsClient.Infrastructure.HandleHttpErrorsAsync`): unit test with a fake 429 response.
3. **All TFS read calls must enforce `TimeoutSeconds`** (`TfsRequestSender.SendAsync` or equivalent wrapper): test with a delayed handler and assert timeout.
4. **Incremental filters must use UTC-normalized timestamps** (`GetWorkItemsAsync`, `GetPullRequestsAsync`): unit test with non-UTC `DateTimeOffset`.
5. **Work item watermark must equal max `System.ChangedDate`** (`WorkItemSyncStage.UpsertWorkItemsAsync`): unit test using two items with different changed dates.
6. **Retry classification must see status codes for all failures** (`HandleHttpErrorsAsync`): unit test that 500 populates `TfsException.StatusCode` and triggers retry.

## 6. Fix roadmap (prioritized)

| Order | Finding ID | Severity | Blast Radius | Fix Complexity | Rationale |
| --- | --- | --- | --- | --- | --- |
| 1 | TFS-001 | S1 | High | S | Restores basic resilience; without it, any transient failure breaks sync. |
| 2 | TFS-002 | S1 | High | M | Prevents systemic data truncation across multiple sync stages. |
| 3 | TFS-003 | S1 | High | S | Ensures sync stages respect configured timeouts and avoid hangs. |
| 4 | TFS-004 | S1 | Medium | M | Fixes incremental work item accuracy and restores changed-date correctness. |
| 5 | TFS-005 | S1 | Medium | XS | Eliminates UTC offset drift in incremental filters with a small change. |
