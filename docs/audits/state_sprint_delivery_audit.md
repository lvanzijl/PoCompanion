# PoTool State + Sprint + Delivery Audit

## Summary

### Files analyzed

- `docs/domain/domain_model.md`
- `docs/domain/rules/state_rules.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/propagation_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/source_rules.md`
- `PoTool.Api/Services/WorkItemStateClassificationService.cs`
- `PoTool.Api/Services/ActivityEventIngestionService.cs`
- `PoTool.Api/Services/WorkItemResolutionService.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs`
- `PoTool.Api/Persistence/Entities/ActivityEventLedgerEntryEntity.cs`
- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
- `PoTool.Tests.Unit/Services/WorkItemResolutionServiceTests.cs`
- `docs/domain/REPOSITORY_DOMAIN_DISCOVERY.md`
- `docs/Reports/SprintAttributionAnalysis.md`
- `docs/bug_trend_followups.md`

### State logic findings

- The repository has a canonical state mapping service in `PoTool.Api/Services/WorkItemStateClassificationService.cs`, which matches the domain rule that TFS states should resolve to `New`, `InProgress`, `Done`, or `Removed`.
- Multiple analytics paths bypass that mapping and compare raw TFS state strings directly:
  - `PoTool.Api/Services/SprintTrendProjectionService.cs` (lines 232-234, 284-287, 383-395, 629-636)
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` (lines 25-26, 184-190)
- Those direct comparisons break the canonical rule for projects whose done semantics are not exactly `"Done"` or `"Closed"`.

### Delivery logic findings

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` (lines 73-99) calculates sprint delivery from current `IterationPath` membership plus current done state. That is snapshot logic, not `first transition to canonical Done within SprintWindow`.
- `PoTool.Api/Services/SprintTrendProjectionService.cs` (lines 232-239) detects a delivery if any state event changed to raw `"Done"` during the sprint window. This is closer to event-based delivery, but it still bypasses canonical mapping and does not enforce the global first-done rule across reopen scenarios.
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` (lines 184-197) treats currently-done items as completed and orders them by `ClosedDate`, so reopened items and items completed outside the sprint window can be misattributed.

### Sprint logic findings

- `PoTool.Api/Services/ActivityEventIngestionService.cs` (lines 152-166) correctly persists timestamped field-change events, including `System.IterationPath` and `System.State`, giving the repository the raw data needed for canonical sprint analytics.
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` (lines 123-168) correctly uses iteration-path events within the sprint window to detect add/remove churn.
- Historical sprint commitment is not reconstructed using `CommitmentTimestamp = SprintStart + 1 day`. Instead:
  - `PoTool.Api/Services/WorkItemResolutionService.cs` (lines 109-124) resolves sprint membership from the current iteration path.
  - `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs` (lines 48-52) explicitly documents `ResolvedSprintId` as based on the current iteration path.
  - `PoTool.Api/Services/SprintTrendProjectionService.cs` (lines 313-319) uses that snapshot membership as planned scope.

## Critical Issues

### 1. Snapshot sprint metrics violate canonical delivery and sprint-window semantics

- **File:** `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- **Function:** `Handle`
- **Observed logic:** Filters work items by current `IterationPath` (`lines 73-75`) and counts completion from current canonical done state (`lines 85-91`).
- **Canonical rule:** Delivery is attributed to a sprint only when the work item's first-ever canonical `Done` transition falls within that `SprintWindow`; sprint analytics about what happened during a sprint must come from updates, not current snapshots (`docs/domain/domain_model.md:435-445`, `docs/domain/rules/source_rules.md:21-37`).
- **Explanation:** This handler answers “currently in this sprint path and currently done,” not “delivered in this sprint.” Items done during the sprint and later moved out disappear. Items done before the sprint but still sitting in the sprint path are counted as delivered. Reopened items are also misrepresented because no first-done event is consulted.

### 2. Planned scope is derived from current iteration membership, not commitment timestamp

- **File:** `PoTool.Api/Services/WorkItemResolutionService.cs`
- **Function:** `ResolveAllAsync`
- **Observed logic:** Resolves `ResolvedSprintId` directly from the current `wi.IterationPath` (`lines 109-124`).
- **File:** `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs`
- **Function:** `ResolvedSprintId` property documentation
- **Observed logic:** Explicitly says the stored sprint ID is “Based on the current iteration path of the work item” (`lines 48-52`).
- **File:** `PoTool.Api/Services/SprintTrendProjectionService.cs`
- **Function:** `ComputeProductSprintProjection`
- **Observed logic:** Planned PBIs and bugs come from `ResolvedSprintId == sprint.Id` (`lines 313-319`).
- **Canonical rule:** `Committed scope = items whose IterationPath equals the sprint at CommitmentTimestamp`, where `CommitmentTimestamp = SprintStart + 1 day` (`docs/domain/domain_model.md:427-431`, `docs/domain/rules/sprint_rules.md:27-38`).
- **Explanation:** Current sprint assignment is not the same as committed scope. Items added after commitment can appear planned, and committed items moved away later can disappear from planned scope. That breaks commitment, churn, and spillover semantics throughout projection-based delivery metrics.

### 3. Raw TFS state comparisons bypass canonical mapping in delivery analytics

- **File:** `PoTool.Api/Services/SprintTrendProjectionService.cs`
- **Function:** `ComputeProductSprintProjection`
- **Observed logic:** Directly compares state events to `"Done"` and `"Closed"` (`lines 232-234`, `lines 284-287`) instead of resolving canonical state through `IWorkItemStateClassificationService`.
- **Canonical rule:** “PoTool does not rely on raw TFS states” and “Analytics must always resolve the canonical state using this mapping” (`docs/domain/rules/state_rules.md:3-32`).
- **Explanation:** A project with done semantics like `Resolved`, `Completed`, or any custom mapped value will produce wrong delivery counts. The repository already has a configurable state classifier, so these hardcoded comparisons are a direct domain-model violation.

### 4. Sprint execution completion and initial-scope logic are not canonical commitment/delivery logic

- **File:** `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- **Function:** `Handle`
- **Observed logic:** Uses current done-state membership for completed items (`lines 184-190`), derives initial scope from current sprint items minus in-window adds plus removed items (`lines 193-197`), and derives “starved work” from that heuristic (`lines 199-204`).
- **Canonical rule:** Delivery must be first-done-within-window, committed scope must be reconstructed at `SprintStart + 1 day`, and spillover means committed + not done at sprint end + moved to next sprint (`docs/domain/domain_model.md:427-484`, `docs/domain/rules/sprint_rules.md:27-96`).
- **Explanation:** The handler has correct event-based add/remove detection, but its completion, initial scope, and starved/spillover-like logic still depend on current snapshots. That means it cannot reliably identify committed scope, true delivery timing, or canonical spillover.

## High Severity Issues

### 1. First-done rule can be violated after reopen

- **File:** `PoTool.Api/Services/SprintTrendProjectionService.cs`
- **Function:** `ComputeProductSprintProjection`
- **Observed logic:** Counts a PBI as completed if any in-window state event changes to raw `"Done"` (`lines 232-239`).
- **Canonical rule:** Only the first transition to canonical `Done` counts; `Done -> InProgress -> Done` must not create a new delivery (`docs/domain/rules/state_rules.md:34-50`, `docs/domain/rules/sprint_rules.md:45-54`).
- **Explanation:** If a work item first reached done in an earlier sprint, was reopened, and transitioned to done again in the current sprint, this service will count a second delivery because it only inspects in-window events and does not check for an earlier done transition.

### 2. Bug closure semantics are hardcoded and incomplete

- **File:** `PoTool.Api/Services/SprintTrendProjectionService.cs`
- **Function:** `ComputeProductSprintProjection`
- **Observed logic:** A bug is closed when an in-window state event changes to `"Done"` or `"Closed"` (`lines 284-292`).
- **Canonical rule:** Delivery and completion must resolve through canonical state mapping, not raw TFS names (`docs/domain/rules/state_rules.md:3-32`).
- **Explanation:** This misses valid done mappings such as `Resolved` and keeps bug analytics dependent on legacy process-template names.

### 3. Feature progress and delivery rollups depend on raw current state

- **File:** `PoTool.Api/Services/SprintTrendProjectionService.cs`
- **Function:** `ComputeProgressionDelta`
- **Observed logic:** Uses current `pbi.State == "Done"` to accumulate done effort (`lines 383-386`), then gates progression with an in-window raw `"Done"` event (`lines 391-395`).
- **File:** `PoTool.Api/Services/SprintTrendProjectionService.cs`
- **Function:** `ComputeFeatureProgress`
- **Observed logic:** Uses raw `"Done"` for child PBIs and feature state (`lines 629-638`).
- **Canonical rule:** Delivery-related signals should be state-mapped, and parent delivery should remain state-driven rather than inferred from raw child state strings (`docs/domain/rules/propagation_rules.md:46-62`, `docs/domain/rules/state_rules.md:3-32`).
- **Explanation:** These rollups can drift from the configured domain mapping and overstate or understate progress when the project uses non-`Done` raw states for canonical completion.

### 4. No production implementation uses `CommitmentTimestamp = SprintStart + 1 day`

- **File:** `PoTool.Api/Services/WorkItemResolutionService.cs`
- **Function:** `ResolveAllAsync`
- **Observed logic:** Uses current iteration path only (`lines 109-124`).
- **File:** `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- **Function:** `Handle`
- **Observed logic:** Initial scope is inferred from current membership plus in-window iteration changes (`lines 193-197`).
- **Canonical rule:** Committed scope must be reconstructed at `SprintStart + 1 day` (`docs/domain/rules/sprint_rules.md:27-38`).
- **Explanation:** The required commitment timestamp is not used anywhere in the audited production paths. That makes committed-scope-based metrics non-canonical by construction.

### 5. Spillover detection is absent; “starved work” is a different heuristic

- **File:** `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- **Function:** `Handle`
- **Observed logic:** Builds `starvedItems` when later-added work completed while initial-scope work remained unfinished (`lines 199-204`).
- **Canonical rule:** Spillover requires committed scope, not done at sprint end, and moved to next sprint (`docs/domain/domain_model.md:478-484`, `docs/domain/rules/sprint_rules.md:90-96`).
- **Explanation:** Starved work is not the same as spillover. The handler does not check commitment timestamp or movement to the next sprint, so the repository does not currently implement canonical spillover detection.

## Medium Issues

### 1. Sprint execution done-state fallback still hardcodes raw state names

- **File:** `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- **Function:** `Handle`
- **Observed logic:** Falls back to raw done names `Done` and `Closed` when state classifications are absent (`lines 25-26`, `lines 115-121`).
- **Canonical rule:** Analytics should resolve canonical state via mapping rather than hardcoded raw names (`docs/domain/rules/state_rules.md:26-32`).
- **Explanation:** This is less severe than the direct projection-service comparisons because it only applies when classification data is missing, but it is still inconsistent with the canonical rules.

### 2. Advanced filtering and backlog health contain direct raw state checks

- **File:** `PoTool.Api/Handlers/WorkItems/GetFilteredWorkItemsAdvancedQueryHandler.cs`
- **Function:** `Handle`
- **Observed logic:** Checks `wi.State == "In Progress"` as part of validation-issue filtering (`lines 111-118`).
- **File:** `PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`
- **Function:** `CountInProgressAtEnd`
- **Observed logic:** Counts in-progress items using raw `"In Progress"` and `"Active"` (`lines 151-163`).
- **Canonical rule:** State semantics should go through canonical mapping (`docs/domain/rules/state_rules.md:3-32`).
- **Explanation:** These are not the core sprint-delivery metrics, but they show inconsistent state semantics in analytics-adjacent code.

### 3. Existing unit tests reinforce snapshot and raw-state semantics

- **File:** `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- **Function:** `Setup`, `Handle_WithMatchingWorkItems_CalculatesMetricsCorrectly`, `Handle_WithVariousCompletedStates_RecognizesAll`
- **Observed logic:** Tests treat sprint metrics as current iteration membership and current completion state (`lines 38-45`, `lines 79-109`, `lines 133-156`).
- **Canonical rule:** Historical sprint delivery must be derived from event timestamps and first canonical done transition.
- **Explanation:** The tests document and protect the current snapshot behavior, which means the implementation gap is known and currently intentional rather than accidental.

## Low Issues

### 1. Historical-attribution limitations are already documented, but not yet aligned to the canonical domain model

- **File:** `docs/domain/REPOSITORY_DOMAIN_DISCOVERY.md`
- **Function:** Delivery model documentation
- **Observed logic:** Documents `GetSprintMetricsQueryHandler` as a snapshot model and `GetSprintExecutionQueryHandler` as a hybrid model using current done state and starved work (`lines 427-457`).
- **Canonical rule:** Sprint delivery, churn, and spillover should follow the domain model in `docs/domain/domain_model.md`.
- **Explanation:** The repository already records these implementation choices in documentation, which reduces ambiguity but also confirms non-canonical behavior remains present.

### 2. Existing attribution analysis already calls out the mixed strategy

- **File:** `docs/Reports/SprintAttributionAnalysis.md`
- **Function:** Repository-wide attribution analysis
- **Observed logic:** Documents hybrid and snapshot strategies for `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler`, and `SprintTrendProjectionService` (`lines 61-79`, `lines 186-199`).
- **Canonical rule:** Supporting documents for this audit override legacy assumptions and require the canonical domain model.
- **Explanation:** This is useful context, but it also shows the codebase still mixes commitment, snapshot, and event semantics across delivery features.

## Correct Implementations

- **Canonical state mapping service exists**
  - `PoTool.Api/Services/WorkItemStateClassificationService.cs` (lines 158-197)
  - Correctly resolves raw TFS state per work-item type into canonical state classifications.

- **Update history is ingested as timestamped field events**
  - `PoTool.Api/Services/ActivityEventIngestionService.cs` (lines 125-166)
  - Correctly persists `System.IterationPath`, `System.State`, `OldValue`, `NewValue`, and `EventTimestampUtc`, which is the required source for historical sprint analytics.

- **Sprint-window add/remove churn detection uses iteration-path events**
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` (lines 123-168)
  - Correctly detects entering and leaving the sprint during the sprint window from ingested update events.

- **State classification is configurable rather than globally hardcoded**
  - `PoTool.Api/Persistence/Entities/WorkItemStateClassificationEntity.cs` (via service usage) and `PoTool.Api/Services/WorkItemStateClassificationService.cs`
  - This matches the domain rule that mapping is stored in settings and can vary by work-item type.

- **Existing tests already validate event-based PBI completion in the projection service**
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` (lines 27-66)
  - Confirms the service at least tries to detect completion from activity events rather than only current snapshot state.

## Overall Assessment

**Non compliant**

The repository contains the building blocks required by the canonical domain model: configurable state mapping and timestamped update-event ingestion. However, the audited sprint and delivery implementation is not compliant overall because core production handlers still mix current iteration membership, current state snapshots, `ClosedDate`, and raw TFS state names in places where the domain model requires canonical state resolution plus first-done event semantics within the sprint window. Commitment timestamp reconstruction, canonical spillover detection, and globally correct first-done handling are not implemented in the audited code paths.

## Fix Progress — Canonical State Usage

- **Files changed**
  - `PoTool.Api/Services/StateClassificationLookup.cs`
  - `PoTool.Api/Services/WorkItemStateClassificationService.cs`
  - `PoTool.Api/Services/SprintTrendProjectionService.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`

- **Raw state comparisons removed**
  - Replaced sprint projection PBI and bug delivery checks with canonical done classification lookups.
  - Replaced sprint progression, feature progress, and epic progress raw done checks with canonical state resolution.
  - Replaced sprint execution completion classification and raw done fallback with `IWorkItemStateClassificationService` lookups.
  - Replaced sprint-scoped PBI completion event filtering from raw `"Done"` to the configured canonical done states for PBIs.

- **Tests added or updated**
  - Updated `SprintTrendProjectionServiceTests` to verify canonical mappings such as `Resolved -> Done` for PBI, bug, feature, and epic analytics.
  - Added `GetSprintExecutionQueryHandlerTests` to verify canonical done mapping is honored and raw done fallbacks are no longer used.

## Fix Progress — First Done Delivery Rule

- **Services updated**
  - `PoTool.Api/Services/FirstDoneDeliveryLookup.cs`
  - `PoTool.Api/Services/SprintTrendProjectionService.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`

- **Logic replaced**
  - Sprint trend projection delivery now counts PBIs and bugs from the first canonical `Done` transition timestamp found in update history, not from any in-window re-done event.
  - Sprint execution completion ordering now uses the first canonical `Done` transition within the sprint window instead of current snapshot state or `ClosedDate`.
  - Sprint execution delivery no longer depends on the work item still being in the sprint iteration path after the sprint window.

- **Test cases added**
  - `Done -> Reopened -> Done` counts only one delivery in `SprintTrendProjectionServiceTests`.
  - `Done before sprint -> reopened during sprint -> done again` does not create a second delivery in both projection and sprint execution tests.
  - `First done inside sprint window` counts delivery from update history, including when the item is no longer in the sprint iteration path.

## Fix Progress — Commitment Reconstruction

- **Services modified**
  - `PoTool.Api/Services/SprintCommitmentLookup.cs`
  - `PoTool.Api/Services/SprintTrendProjectionService.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`

- **New reconstruction logic**
  - Added `SprintCommitmentLookup` to reconstruct a work item's `System.IterationPath` at `CommitmentTimestamp = SprintStart + 1 day` by walking iteration-path updates backward from the current snapshot.
  - `SprintTrendProjectionService` now computes planned PBI and bug scope from reconstructed commitment membership instead of current `ResolvedSprintId` / snapshot iteration membership.
  - `GetSprintExecutionQueryHandler` now derives initial scope from reconstructed commitment membership and classifies added / removed churn only from iteration changes after the commitment timestamp.

- **Tests added**
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
    - committed item moved away later still counts as planned scope
    - item added after commitment is excluded from planned scope
  - `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
    - item added after commitment counts as added scope
    - committed item moved away later remains in initial scope and counts as removed scope

## Fix Progress — Spillover Detection

- **Logic implemented**
  - Added `StateReconstructionLookup` to replay post-sprint `System.State` changes backward from the current snapshot so spillover checks use the canonical state at `SprintEnd`.
  - Added `SprintSpilloverLookup` to identify only committed PBIs whose first post-sprint iteration move goes directly from the current sprint path to the next sprint path.
  - Kept the existing starved-work heuristic separate from spillover; starved work still reports unfinished initial-scope items when later-added work completed, while spillover now follows the canonical committed + not-done-at-end + direct-next-sprint rule.

- **Services modified**
  - `PoTool.Api/Services/StateReconstructionLookup.cs`
  - `PoTool.Api/Services/SprintSpilloverLookup.cs`
  - `PoTool.Api/Services/SprintTrendProjectionService.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
  - `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`
  - `PoTool.Api/Migrations/20260314054759_AddSprintProjectionSpilloverMetrics.cs`

- **Test coverage added**
  - `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
    - committed Sprint N → Sprint N+1 move counts as spillover
    - Sprint N → backlog → Sprint N+1 does not count as spillover
    - unfinished item still on the sprint path does not count as spillover
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
    - projection spillover metrics match the same three canonical scenarios
  - `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs`
    - trend aggregation includes spillover counts and effort from the projection rows

## Fix Progress — Snapshot Sprint Metrics Endpoint

- **Decision taken**
  - Converted `GetSprintMetricsQueryHandler` from a current snapshot endpoint into a historical sprint metrics endpoint.
  - Kept the existing route and DTO shape so downstream consumers continue to call the same API, but the handler now follows canonical sprint delivery semantics instead of current iteration membership.
  - This resolves the ambiguity called out earlier in this audit: the endpoint no longer represents "what is in the sprint today."

- **Changes implemented**
  - `GetSprintMetricsQueryHandler` now requires dated sprint metadata, reconstructs committed scope at `CommitmentTimestamp = SprintStart + 1 day`, includes scope added after commitment from iteration-history events, and counts completion from the first canonical `Done` transition inside the sprint window.
  - Handler, query, DTO, controller, and forecast comments were updated to document the historical semantics and point future readers back to the canonical sprint/domain rules.
  - `GetSprintMetricsQueryHandlerTests` were replaced with history-based tests that verify commitment reconstruction, added-scope handling, first-done-only delivery, and the absence of raw done-state fallbacks.

- **Endpoints affected**
  - `GET /api/Metrics/sprint`
  - Indirect consumer: `GetEpicCompletionForecastQueryHandler` velocity lookup via `GetSprintMetricsQuery`

## Re-Audit Results — Post Fix Validation

### Scope re-audited

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Services/WorkItemStateClassificationService.cs`
- `PoTool.Api/Services/FirstDoneDeliveryLookup.cs`
- `PoTool.Api/Services/SprintCommitmentLookup.cs`
- `PoTool.Api/Services/SprintSpilloverLookup.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/WorkItemStateClassificationServiceTests.cs`

### Reconstructed domain expectations

- **Canonical State Usage**
  - Analytics must resolve work item states through canonical mappings (`New`, `InProgress`, `Done`, `Removed`) rather than raw TFS state string checks.
  - Delivery-related logic must use canonical `Done` classification and ignore non-meaningful transitions.

- **Delivery Semantics**
  - Delivery is the **first** transition to canonical `Done` inside `SprintWindow`.
  - Reopen flows (`Done -> InProgress -> Done`) must not create a second delivery.
  - Delivery attribution must come from update history, not current snapshot state or current sprint membership.

- **Commitment Semantics**
  - Committed scope is the set of items whose `IterationPath` equals the sprint path at `CommitmentTimestamp = SprintStart + 1 day`.
  - Committed scope must be reconstructed from iteration history and must not depend on current snapshot membership.

- **Spillover Semantics**
  - Spillover requires all three conditions:
    - item was committed
    - item was not `Done` at `SprintEnd`
    - item moved to the next sprint
  - Spillover must stay distinct from heuristics such as starved work.

- **Snapshot Endpoint Clarification**
  - Historical sprint analytics endpoints must not be interpreted as current-board snapshot views.
  - Historical delivery and scope metrics must remain event-based even if the API route shape stays unchanged.

### Implementation re-scan

| Rule category | Status | Evidence |
| --- | --- | --- |
| Canonical state usage | **Compliant** | `PoTool.Api/Services/WorkItemStateClassificationService.cs` (`GetClassificationsAsync`, `GetClassificationAsync`) supplies configurable canonical mappings; `PoTool.Api/Services/FirstDoneDeliveryLookup.cs` (`Build`, `GetFirstDoneTransitionTimestamp`) and `PoTool.Api/Services/StateClassificationLookup.cs` resolve done-state checks canonically; no raw `"Done"` / `"Closed"` comparisons remain in `SprintTrendProjectionService`, `GetSprintExecutionQueryHandler`, or `GetSprintMetricsQueryHandler`. |
| Delivery = first canonical done in sprint | **Compliant** | `PoTool.Api/Services/FirstDoneDeliveryLookup.cs` (`GetFirstDoneTransitionTimestamp`) counts only the first canonical done transition; `PoTool.Api/Services/SprintTrendProjectionService.cs` (`ComputeProductSprintProjection`) and `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` (`Handle`) both consume `firstDoneByWorkItem` rather than current state or `ClosedDate`; `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` (`Handle`) uses the same first-done lookup for completed scope. |
| Commitment reconstruction at SprintStart + 1 day | **Compliant** | `PoTool.Api/Services/SprintCommitmentLookup.cs` (`GetCommitmentTimestamp`, `BuildCommittedWorkItemIds`, `GetIterationPathAtTimestamp`) reconstructs membership at `SprintStart + 1 day`; `SprintTrendProjectionService.ComputeProjectionsAsync`, `GetSprintExecutionQueryHandler.Handle`, and `GetSprintMetricsQueryHandler.Handle` all use that helper to derive committed scope. |
| Canonical spillover detection | **Compliant** | `PoTool.Api/Services/SprintSpilloverLookup.cs` (`BuildSpilloverWorkItemIds`) checks committed membership, canonical state at sprint end via `StateReconstructionLookup.GetStateAtTimestamp`, and a direct move to the next sprint path; `SprintTrendProjectionService.ComputeProductSprintProjection` and `GetSprintExecutionQueryHandler.Handle` delegate spillover classification to this helper; starved work remains separate in `GetSprintExecutionQueryHandler.Handle`. |
| Snapshot/historical separation | **Compliant** | `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` (`Handle`) now explicitly reconstructs historical committed scope and historical first-done delivery from activity history. The handler no longer implements “current board state in this sprint path” semantics, which resolves the earlier ambiguity documented in this audit. |

### Component-by-component status

- **`PoTool.Api/Services/WorkItemStateClassificationService.cs`**
  - **Class:** `WorkItemStateClassificationService`
  - **Methods:** `GetClassificationsAsync`, `GetClassificationAsync`, `IsDoneStateAsync`
  - **Status:** **Compliant**
  - **Audit note:** State interpretation is centralized in canonical mappings loaded from configuration, matching the domain rules.

- **`PoTool.Api/Services/SprintTrendProjectionService.cs`**
  - **Class:** `SprintTrendProjectionService`
  - **Methods:** `ComputeProjectionsAsync`, `ComputeProductSprintProjection`, `ComputeProgressionDelta`, `ComputeFeatureProgress`
  - **Status:** **Compliant**
  - **Audit note:** Projection delivery uses `FirstDoneDeliveryLookup`; planned scope is injected from `SprintCommitmentLookup`; spillover delegates to `SprintSpilloverLookup`; feature/epic progress uses canonical done classification rather than raw state strings.

- **`PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`**
  - **Class:** `GetSprintExecutionQueryHandler`
  - **Method:** `Handle`
  - **Status:** **Compliant**
  - **Audit note:** Completion ordering, initial scope, added scope, removed scope, and spillover now align with the canonical sprint rules. The remaining starved-work heuristic is explicitly separate from spillover.

- **`PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`**
  - **Class:** `GetSprintMetricsQueryHandler`
  - **Method:** `Handle`
  - **Status:** **Compliant**
  - **Audit note:** This handler now behaves as a historical sprint metrics query: it reconstructs commitment at `SprintStart + 1 day`, includes added scope from iteration history, and counts completed scope from first canonical done transitions within the sprint window.

### Remaining violations

- **None identified in the re-audited rule categories.**

The prior critical findings for raw state checks, re-delivery after reopen, snapshot-based commitment, and missing spillover logic are no longer present in the re-audited production paths.

### Test coverage validation

The requested unit-test coverage now exists.

- **First Done delivery semantics**
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
    - `ComputeProductSprintProjection_FirstDoneInsideSprint_CountsCanonicalDelivery`
  - `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
    - `Handle_UsesHistoricalCommitmentAndFirstDoneSemantics`
  - `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
    - `Handle_CountsFirstDoneWithinSprint_WhenItemIsNoLongerInSprintIteration`

- **Reopen handling**
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
    - `ComputeProductSprintProjection_DoneReopenedDone_CountsOnlyFirstDoneDelivery`
    - `ComputeProductSprintProjection_DoneBeforeSprint_ReopenedDuringSprint_DoesNotCountDelivery`
  - `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
    - `Handle_DoesNotCountSecondDoneTransition_WhenFirstDoneWasBeforeSprint`
  - `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
    - `Handle_DoesNotCountSecondDone_WhenFirstDoneWasBeforeSprint`

- **Commitment timestamp reconstruction**
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
    - `ComputeProductSprintProjection_UsesCommittedScope_WhenItemMovedAwayAfterCommitment`
    - `ComputeProductSprintProjection_ExcludesItemsAddedAfterCommitment_FromPlannedScope`
  - `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
    - `Handle_TreatsItemAddedAfterCommitment_AsAddedScope`
    - `Handle_KeepsCommittedItemInInitialScope_WhenMovedAwayAfterCommitment`
  - `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
    - `Handle_UsesHistoricalCommitmentAndFirstDoneSemantics`

- **Spillover detection**
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
    - `ComputeProductSprintProjection_CountsCommittedPbiMovedDirectlyToNextSprint_AsSpillover`
    - `ComputeProductSprintProjection_DoesNotCountBacklogRoundTripToNextSprint_AsSpillover`
    - `ComputeProductSprintProjection_DoesNotCountUnfinishedCommittedPbiStillOnSprintPath_AsSpillover`
  - `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
    - `Handle_CountsCommittedPbiMovedDirectlyToNextSprint_AsSpillover`
    - `Handle_DoesNotCountBacklogRoundTripToNextSprint_AsSpillover`
    - `Handle_DoesNotCountUnfinishedItemStillOnSprintPath_AsSpillover`

- **Coverage verdict**
  - No coverage gap remains for the four requested validation categories.
  - Focused validation run: `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~GetSprintMetricsQueryHandlerTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~WorkItemStateClassificationServiceTests" -v minimal` → **86/86 passed**.

### Summary of improvements

- Canonical state mapping is now consistently used across the re-audited sprint analytics code paths.
- Delivery detection now follows the first canonical done transition rule and is resilient to reopen scenarios.
- Commitment reconstruction now uses the canonical `SprintStart + 1 day` timestamp.
- Spillover is now implemented canonically and no longer conflated with starved work.
- The sprint metrics endpoint has been clarified by implementation: it now behaves as a historical analytics endpoint, not a current-board snapshot.

### Risk assessment

- **Current compliance risk:** **Low**
- **Operational caveat:** canonical analytics still depend on correct state-mapping configuration. If a team leaves a real done-state unmapped, the audited services will undercount done transitions rather than infer raw TFS semantics. That is safer than the previous hardcoded behavior, but it remains a configuration-quality dependency rather than a code-path violation.

### Recommended next fixes if any remain

- No blocking follow-up is required for the re-audited rule set.
- Optional follow-up only:
  - add an operational/configuration audit to detect unmapped live TFS states so canonical analytics cannot silently degrade due to incomplete settings.

### Final audit verdict

**Domain Compliance Level: Fully compliant**

The sprint analytics pipeline covered by this audit now aligns with the domain model for canonical state usage, first-done delivery attribution, commitment reconstruction at `SprintStart + 1 day`, canonical spillover detection, and the historical interpretation of sprint metrics.
