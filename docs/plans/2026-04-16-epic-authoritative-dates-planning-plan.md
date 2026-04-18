# Epic Authoritative Dates Planning Plan

## 1. Decision Summary

- **Recommended design choice:** Treat Epic planning start/end dates as PoTool-owned planning data, not as forecast data. Multi-Product Planning becomes the primary edit surface, while Product Roadmaps and related planning views become read surfaces for those authoritative dates plus forecast support signals.
- **Verified in repository:** Current Planning workspace timing is forecast-led, not authoritative-date-led. Product Roadmaps and Multi-Product Planning both render forecast completion from `PlanningEpicProjectionDto.EstimatedCompletionDate`, while start timing is derived in `RoadmapTimelineLayout` from `SprintsRemaining` and sprint cadence. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs:8-16`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs:78-145`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:33-35`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:300-319`
- **Verified in repository:** Multi-Product Planning already exists as a separate planning route and already loads both per-product planning projections and per-team sprint metadata, making it the closest current surface to the requested workflow. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanningWorkspace.razor:74-80`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:1-10`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:409-456`

## 2. Verified Current-State Constraints

- **Verified in repository:** No explicit Epic planning start/end field is currently requested from TFS during work-item reads. The required field list contains `System.IterationPath`, `System.CreatedDate`, `System.ChangedDate`, `Microsoft.VSTS.Common.ClosedDate`, `Microsoft.VSTS.Common.BacklogPriority`, and repository-specific analytics fields, but no Epic planning start/end field. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:37-59`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs:18-23`
- **Verified in repository:** No explicit Epic planning start/end field is currently mapped into `WorkItemDto`, `WorkItemEntity`, or `PlanningEpicProjectionDto`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs:7-31`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/WorkItemEntity.cs:48-184`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs:8-16`
- **Verified in repository:** The current product planning projection query only joins roadmap Epics with `ForecastProjectionEntity` and maps `SprintsRemaining`, `EstimatedCompletionDate`, `Confidence`, and `LastUpdated`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs:37-70`
- **Verified in repository:** Current write flows follow a stable pattern: mutate TFS through `ITfsClient`, then refresh the cache, and if the immediate TFS reread is stale, force the just-written value into the cache anyway. This already exists for `BacklogPriority` and `IterationPath`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/UpdateWorkItemBacklogPriorityCommandHandler.cs:31-68`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/UpdateWorkItemIterationPathCommandHandler.cs:31-68`
- **Verified in repository:** Current synchronization and repository upsert behavior mirror TFS-backed work-item data directly into `WorkItemEntity`. Any new TFS-mirrored fields added to `WorkItemDto`/`WorkItemEntity` would be overwritten on normal cache refresh unless explicitly protected. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs:119-223`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs:233-305`
- **Verified in repository:** Multi-Product Planning currently has no mutation path. It shows forecast-derived bars, product filters, cluster overlays, and collision hints only. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:45-149`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:151-255`
- **Verified in repository:** Product Roadmaps is read-only for planning visualization and continues to load raw roadmap Epics from cached work items plus forecast support data from the product planning projections endpoint. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:857-950`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:1493-1528`
- **Verified in repository:** `RoadmapTimelineLayout` assumes forecast-only input and derives `StartDate` from `EstimatedCompletionDate` and `SprintsRemaining`, with a fallback duration when `SprintsRemaining` is absent. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs:78-145`
- **Verified in repository:** The repository’s SQLite rule requires queryable timestamps to have UTC `DateTime` companion columns and forbids relying on `DateTimeOffset` for server-side predicates/sorts. Existing work-item persistence already follows that pattern for `CreatedDateUtc` and `TfsChangedDateUtc`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs:331-342`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs:149-159`, `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/sqlite-timestamp-fix-audit.md:27-28`

## 3. TFS Field Strategy

- **Verified in repository:** No existing Epic start/end field reference name is used anywhere in the current runtime field list, revision whitelist, or planning DTOs. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:37-59`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/RevisionFieldWhitelist.cs:14-35`
- **Verified in repository:** No known custom date field under the currently used custom namespaces (`Rhodium.*`) is referenced for work-item reads or writes. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:31-32`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs:331-357`
- **Verified in repository:** The TFS verification path already queries `_apis/wit/fields` and can tell whether a candidate field exists in the live collection metadata. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs:386-420`
- **Verified in repository:** The production TFS client already performs direct JSON Patch updates against `/fields/<referenceName>` and supports Azure DevOps Server 2022.2 / TFS 2019+ style work-item PATCH flows. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.cs:7-11`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs:759-777`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs:822-840`
- **Recommended design choice:** Introduce configurable field reference names in `TfsConfigEntity` for authoritative Epic planning dates, rather than hard-coding one live-instance-specific field pair. This keeps the feature usable whether the instance exposes standard fields or requires custom fields. Relevant current configuration types are `TfsConfigEntity`, `ITfsConfigurationService`, `TfsConfigurationService`, `TfsConfigService`, and the TFS settings UI. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TfsConfigEntity.cs:5-64`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsConfigurationService.cs:9-24`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/TfsConfigurationService.cs:27-145`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TfsConfigService.cs:29-77`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/TfsConfigSection.razor:21-167`
- **Recommended design choice:** Use the standard Azure DevOps/TFS scheduling field candidates `Microsoft.VSTS.Scheduling.StartDate` and `Microsoft.VSTS.Scheduling.TargetDate` as the first preferred defaults, but only after runtime metadata verification confirms they exist and are writable for Epic on the live process template.
- **Unknown / requires confirmation outside repository:** Whether the live Azure DevOps/TFS process template exposes `Microsoft.VSTS.Scheduling.StartDate` and `Microsoft.VSTS.Scheduling.TargetDate` on Epic specifically.
- **Unknown / requires confirmation outside repository:** If the standard fields are absent or unsupported for Epic, the instance will need new custom fields. No existing custom-field reference names for this purpose are present in the repository today.

## 4. Ownership and Precedence Model

- **Recommended design choice:** PoTool must own authoritative Epic planning dates in local persistence as first-class planning data and must also mirror them to mapped TFS fields on save. The local PoTool values are the planning source of truth; the mapped TFS fields are an external projection of that truth.
- **Verified in repository:** Relying only on TFS-mirrored work-item fields would not satisfy the requested ownership rule, because ordinary work-item refresh/upsert paths currently overwrite cached work-item state directly from TFS data. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs:119-223`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs:275-305`
- **Recommended design choice:** Read-path precedence should be:
  1. PoTool authoritative Epic planning dates (local-owned store)
  2. no authoritative date values
  3. forecast/projection data as secondary visual support only
- **Recommended design choice:** Write-path behavior should be:
  1. user selects start sprint + sprint count in PoTool
  2. PoTool resolves the concrete sprint range
  3. PoTool computes authoritative start/end dates
  4. PoTool persists those authoritative values locally
  5. PoTool writes the same values into the configured TFS fields
  6. PoTool refreshes the cache and forces the just-written values into the cache-facing read model when an immediate TFS reread is stale, matching the existing `BacklogPriority` / `IterationPath` mutation pattern
- **Recommended design choice:** Forecast/projection values (`SprintsRemaining`, `EstimatedCompletionDate`, `Confidence`, `LastUpdated`) remain read-only support signals and never overwrite authoritative planning dates.
- **Recommended design choice:** External edits to the mapped TFS fields must not become planning truth. The safest realistic enforcement in the current architecture is:
  - PoTool never promotes mapped TFS fields above the local authoritative store
  - any explicit save through the planning feature reasserts the locally calculated values back into TFS
  - optional divergence indication is driven by comparing the local authoritative store with the latest mapped TFS/cache values
- **Unknown / requires confirmation outside repository:** Whether the product wants silent background reassertion outside explicit planning saves. The repository currently has no background mutation loop for planning pages; all existing work-item mutations are user-triggered through explicit API calls. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs:287-383`

## 5. Affected Surfaces

| Surface | Current behavior | Required change | Change type |
|---|---|---|---|
| `PoTool.Client/Pages/Home/MultiProductPlanning.razor` | Loads products, projections, and sprint histories; renders forecast-derived shared-axis lanes only. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:409-456`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:472-524` | Add Epic selection/edit flow, preview of authoritative start/end dates, save action, and rendering precedence for authoritative dates over forecast bars. | Read + write |
| `PoTool.Client/Pages/Home/ProductRoadmaps.razor` | Loads roadmap Epics from cached work items and overlays forecast projection signals. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:857-950` | Render authoritative dates first, keep forecast completion as secondary signal, and show divergence if authoritative dates and forecast differ. | Read |
| `PoTool.Client/Services/RoadmapTimelineLayout.cs` | Builds bars from forecast completion + derived duration only. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs:78-145` | Accept authoritative start/end dates and source precedence, with forecast fallback only when authoritative dates are absent. | Read |
| `PoTool.Shared/Planning/PlanningProjectionDtos.cs` + `PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs` | Product planning DTO contains only forecast support fields. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs:8-16`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs:60-107` | Extend or replace the planning read contract so roadmap pages receive authoritative start/end dates and any supporting authored-sprint metadata together with forecast signals. | Read |
| `PoTool.Shared/WorkItems/WorkItemDto.cs`, `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`, `PoTool.Api/Repositories/WorkItemRepository.cs` | TFS work-item mirror has no planning date fields and sync/upsert mirrors TFS values directly. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs:7-31`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs:119-305` | Add local authoritative planning persistence that survives sync and is queryable by planning read handlers. | Read + write |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`, `...WorkItems.cs`, `...WorkItemsUpdate.cs`, `...Verification.cs` | Current TFS read/write paths do not request or patch authoritative planning date fields. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:25-59`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs:741-920`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs:386-420` | Add configurable field references, read mapping of mapped TFS date fields, targeted write method for authoritative date patching, and verification coverage for field metadata / writeability. | Read + write |
| `PoTool.Api/Controllers/WorkItemsController.cs`, `PoTool.Core/WorkItems/Commands/*`, `PoTool.Api/Handlers/WorkItems/*`, `PoTool.Client/Services/WorkItemService.cs` | Existing mutation endpoints exist for tags, title/description, backlog priority, and iteration path. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs:287-383`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkItemService.cs:318-381` | Add a new work-item planning-date mutation endpoint/command/service method for authoritative Epic planning dates, with the same refresh-and-override cache pattern. | Write |
| `PoTool.Shared/Settings/TfsConfigEntity.cs`, `PoTool.Api/Services/TfsConfigurationService.cs`, `PoTool.Client/Services/TfsConfigService.cs`, `PoTool.Client/Components/Settings/TfsConfigSection.razor` | TFS config currently stores URL/project/default area path/auth settings only. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TfsConfigEntity.cs:5-64`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/TfsConfigSection.razor:21-167` | If field refs are configurable, extend config persistence/API/UI to store the start/end field reference names. | Read + write |
| `PoTool.Api/Services/MockTfsClient.cs`, `PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs` | Mock write paths currently exist for tags, backlog priority, iteration path, title/description only. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockTfsClient.cs:475-630`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs:865-949` | Add mock support for authoritative Epic date mutation so planning UI and handler tests can run without live TFS. | Write |
| Unit tests (`GetProductPlanningProjectionsQueryHandlerTests`, `RoadmapTimelineLayoutTests`, work-item update handler tests) | Current tests cover forecast projection reading, forecast-only timeline layout, and mutation cache override for backlog priority/iteration path. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetProductPlanningProjectionsQueryHandlerTests.cs:25-75`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/RoadmapTimelineLayoutTests.cs:11-60`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/UpdateWorkItemIterationPathCommandHandlerTests.cs:49-155` | Add coverage for authoritative date read precedence, date calculation, mutation refresh behavior, config field strategy, and UI timeline precedence. | Read + write |

## 6. Date Calculation Rules

- **Verified in repository:** Sprint metadata already arrives as `SprintDto` with `StartUtc` and `EndUtc`, and current planning pages already depend on those fields for concrete sprint windows. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/SprintDto.cs:7-17`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor:745-753`
- **Recommended design choice:** Build a canonical product sprint sequence from all distinct team sprints for the product, using concrete sprint windows rather than cadence averages. The closest existing repository pattern is Plan Board’s de-duplication by normalized iteration path plus ordering by `StartUtc`/`Id`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor:706-753`
- **Recommended design choice:** Calculation rule:
  - selected start sprint = one concrete entry in the canonical product sprint sequence
  - `N` sprints = inclusive range starting at the selected sprint
  - authoritative start date = selected sprint’s `StartUtc`
  - final sprint = selected start sprint + `(N - 1)` by ordered sequence position
  - authoritative end date = final sprint’s `EndUtc`
- **Recommended design choice:** If sprint metadata is missing (`StartUtc` or `EndUtc` absent), duplicated in a way that cannot be de-duplicated to one canonical sequence entry, unresolved for the product, or non-contiguous such that `N` sprints cannot be resolved safely, the save must be blocked and no authoritative dates should be written.
- **Recommended design choice:** Persist both authoritative timestamps as `DateTimeOffset?` for external/TFS fidelity and add UTC `DateTime?` companion columns (`...Utc`) for SQLite-safe predicates and ordering, following existing repository persistence rules.
- **Recommended design choice:** Normalize the persisted planning dates to their UTC calendar date boundaries for timeline rendering consistency. Current timeline rendering already normalizes forecast dates to UTC midnight before positioning. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs:97-145`
- **Unknown / requires confirmation outside repository:** Whether the mapped TFS fields are truly date-only fields or datetime fields on the live process template. The repository does not currently expose that metadata as part of planning reads.

## 7. Backend Change Plan

- **Recommended design choice:** Add a local authoritative planning persistence model for Epic dates, separate from forecast projection storage and separate from TFS-mirrored forecast semantics.
  - **Option A — separate entity/table (recommended):** keep PoTool-owned planning authority isolated from the TFS mirror and from forecast projections.
  - **Option B — local-only columns on `WorkItemEntity`:** viable, but easier to couple accidentally to TFS mirror upsert logic.
  - **Trade-off:** Option A is cleaner for ownership and divergence tracking; Option B touches fewer joins but increases sync-collision risk.
- **Recommended design choice:** Extend the planning read handler (`GetProductPlanningProjectionsQueryHandler`) or add a successor planning read handler that joins:
  - roadmap Epic membership/order from current cached work items + resolved work items
  - local authoritative planning date store
  - forecast support signals from `ForecastProjectionEntity`
- **Recommended design choice:** Add a new work-item mutation endpoint and command for authoritative planning dates, aligned with existing work-item mutation patterns:
  - controller endpoint in `WorkItemsController`
  - command in `PoTool.Core/WorkItems/Commands`
  - handler in `PoTool.Api/Handlers/WorkItems`
  - client wrapper in `WorkItemService`
  - generated API contract refresh afterward
- **Recommended design choice:** The handler should:
  1. validate the target work item is an Epic and that the sprint range resolves
  2. persist local authoritative planning values
  3. write mapped TFS fields through `ITfsClient`
  4. refresh work-item cache from TFS
  5. if the TFS reread is stale, override the cache-facing mapped date fields with the just-written values, following the existing backlog-priority / iteration-path pattern
- **Recommended design choice:** Extend `ITfsClient`, `TfsAccessGateway`, `RealTfsClient`, `MockTfsClient`, and `BattleshipMockDataFacade` with an Epic-date update method that patches both mapped TFS fields in a single PATCH request.
- **Recommended design choice:** Extend TFS configuration storage/API/UI with two field reference names if configurability is chosen: one for start date and one for end date.
- **Recommended design choice:** Extend TFS verification to include:
  - metadata presence for the configured date fields through `_apis/wit/fields`
  - optional write-check guidance for those fields on a user-selected work item, similar in spirit to the current tag-based write verification
- **Recommended design choice:** Update migrations from a clean build only, and include both generated migration files per repository rules.

## 8. UI Change Plan

- **Verified in repository:** Multi-Product Planning already shows one row per Epic on a shared axis and is therefore the correct primary edit surface for minimal Epic date editing without redesigning the entire workspace. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:151-255`
- **Recommended design choice:** Minimal Multi-Product Planning UX should add:
  - an Epic-level edit affordance on each row
  - an editor panel for the selected Epic
  - a start sprint picker populated from the canonical product sprint sequence
  - a sprint-count input
  - a preview block showing computed authoritative start date and end date before save
  - a save action that writes authoritative dates
  - continued display of forecast completion, confidence, and missing-forecast states as secondary signals
- **Recommended design choice:** Visual precedence on Multi-Product Planning:
  - authoritative start/end dates drive the bar and the row-end label when present
  - forecast completion remains visible as a supporting chip/tooltip line
  - if both exist and differ, the UI shows the forecast as a separate support signal instead of reusing the authoritative label text
- **Recommended design choice:** Visual precedence on Product Roadmaps:
  - lane timeline uses authoritative dates when present
  - forecast completion date, `~N sprints`, confidence, and last-updated remain on the card as secondary signals
  - divergence between authoritative dates and forecast completion is explicit rather than implicit
- **Recommended design choice:** `RoadmapTimelineLayout` should accept enough input to render:
  - authoritative start/end window
  - forecast completion support signal
  - source metadata indicating whether the visible bar came from authoritative or fallback forecast data
- **Unknown / requires confirmation outside repository:** Whether the team wants inline editing, a modal, or a side panel for the Multi-Product editor chrome. The repository already uses both page-local panels and dialogs elsewhere, but current code does not establish one mandatory planning editor shell for this page.

## 9. Rollout / Compatibility Notes

- **Recommended design choice:** Null authoritative dates are allowed initially. Existing Epics without authoritative dates should keep using the current forecast-only rendering path so rollout does not break existing planning views.
- **Verified in repository:** Current planning pages already tolerate missing forecast data and unresolved cadence by showing “No forecast”, “Missing forecast”, or “Sprint cadence unavailable” states. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:167-177`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:220-265`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:250-267`
- **Recommended design choice:** During rollout, timeline rendering precedence should be:
  - authoritative dates if both authoritative start and end exist
  - forecast-only rendering otherwise
- **Recommended design choice:** Do not remove or reinterpret the existing forecast projection pipeline during the first rollout slice. `ForecastProjectionEntity` and `PlanningEpicProjectionDto` remain necessary for secondary signals and backwards-compatible rendering.
- **Recommended design choice:** Divergence between authoritative dates and forecast signals should not block the page; it should only be indicated.
- **Unknown / requires confirmation outside repository:** Whether backfilling initial authoritative dates from current forecast values is allowed. The task direction says forecast data must not become authoritative automatically, so the safer assumption is no automatic backfill.

## 10. Implementation Slices

### Slice 1 — Field strategy + local authoritative persistence

- Extend TFS config storage to hold configurable start/end field reference names if the configurable strategy is accepted.
- Add local authoritative Epic planning persistence (preferred: separate entity/table) with UTC companion columns.
- Add migration and persistence wiring.

### Slice 2 — Read model and planning contract

- Extend the planning read contract returned for roadmap Epics so authoritative dates are available to planning surfaces.
- Update `GetProductPlanningProjectionsQueryHandler` (or replacement read handler) to join authoritative dates with existing forecast support data.
- Add unit tests for read precedence and null-authoritative fallback behavior.

### Slice 3 — TFS write endpoint + cache refresh

- Add new `ITfsClient` / `TfsAccessGateway` / `RealTfsClient` Epic-date mutation support.
- Add new work-item controller endpoint, request contract, command, and handler.
- Reuse the existing “write → reread → force written value into cache if stale” pattern from backlog-priority / iteration-path handlers.
- Add unit tests for successful write, stale reread override, and null reread fallback.

### Slice 4 — Multi-Product Planning editor

- Add Epic selection/edit affordance on Multi-Product Planning.
- Add start sprint selection, sprint count input, date preview, and save.
- Load concrete product sprint sequence for editing, not just cadence.
- Add component/unit tests for editor state and disabled/error behavior.

### Slice 5 — Timeline precedence + support signals

- Update `RoadmapTimelineLayout` to render authoritative dates first.
- Update Multi-Product Planning and Product Roadmaps to show authoritative dates plus forecast support signals.
- Add divergence indication where both date sources exist and differ.
- Update existing timeline tests and add precedence coverage.

### Slice 6 — Verification, mocks, and settings completeness

- Extend TFS verification and mock TFS paths for the new field pair and mutation.
- Extend TFS settings UI/API if field refs are configurable.
- Add tests for config persistence and mock mutation behavior.

## 11. Risks and Repository-Grounded Unknowns

- **Verified in repository:** Current cache synchronization mirrors TFS-backed work-item fields directly; without a separate PoTool-owned authoritative store, external edits in TFS would win on the next sync. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs:119-223`
- **Verified in repository:** Multi-Product Planning currently resolves only sprint cadence, not a durable editable product sprint sequence. Editing requires concrete sprint-range resolution logic beyond the current average-duration model. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:676-690`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/SprintCadenceResolver.cs:12-35`
- **Unknown / requires confirmation outside repository:** The exact live TFS field reference names and Epic field availability on the target process template.
- **Unknown / requires confirmation outside repository:** Whether a product with multiple teams should use one shared canonical sprint calendar or require the user to choose a team-specific sprint stream when calendars diverge materially.
- **Unknown / requires confirmation outside repository:** Whether the product wants divergence warnings only, or a stronger reassertion workflow beyond explicit user saves.
- **Recommended design choice:** Do not implement any automatic conversion of forecast dates into authoritative planning dates during rollout.
