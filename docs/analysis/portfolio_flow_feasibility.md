# PortfolioFlow Feasibility Audit

_Generated: 2026-03-16_

Reference documents:

- `docs/architecture/portfolio-flow-model.md`
- `docs/analysis/portfolio_flow_semantic_audit.md`
- `docs/analysis/portfolio_flow_domain_exploration.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/state-rules.md`
- `docs/rules/source-rules.md`

Files analyzed:

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Services/ActivityEventIngestionService.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/WorkItemResolutionService.cs`
- `PoTool.Api/Persistence/Entities/ActivityEventLedgerEntryEntity.cs`
- `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs`
- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`
- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`
- `PoTool.Core/RevisionFieldWhitelist.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- `PoTool.Core.Domain/Domain/Estimation/CanonicalStoryPointResolutionService.cs`
- `PoTool.Core.Domain/Domain/Sprints/FirstDoneDeliveryLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/StateClassificationLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/StateReconstructionLookup.cs`

Overall conclusion:

- the repository can already reconstruct **state-based historical events**
- the repository can already detect **first-Done throughput**
- the repository does **not** yet store enough history to compute the canonical PortfolioFlow model **reliably** in story-point scope for historical sprints

The main blockers are:

1. missing historical `Microsoft.VSTS.Scheduling.StoryPoints` changes
2. missing historical `ResolvedProductId` / portfolio-membership resolution
3. no persisted `EnteredPortfolio` timestamp or equivalent materialized event

## Stock Reconstruction Feasibility

Canonical requirement:

> `Stock(s) = Σ ScopeSP(w, t_end)` for all `w ∈ PortfolioPBIs(t_end)` where `State(w, t_end) ≠ Removed`

### What is available now

**PBI state at sprint end:** feasible

- `GetPortfolioProgressTrendQueryHandler.ComputeHistoricalScopeEffort` already replays `System.State` changes from `ActivityEventLedgerEntryEntity` to decide whether an item was `Removed` at sprint end.
- `StateReconstructionLookup.GetStateAtTimestamp` provides a reusable point-in-time state reconstruction helper based on current snapshot state plus later `System.State` events.
- `ActivityEventIngestionService` persists `System.State` updates into the activity ledger, so canonical state-at-time reconstruction is supported by repository data.

**Story-point resolution rule:** feasible for the current snapshot only

- `CanonicalStoryPointResolutionService` already resolves canonical story points from `StoryPoints -> BusinessValue -> derived sibling average -> missing`.
- `WorkItemEntity` stores current `StoryPoints` and `BusinessValue`.

### What is missing

**Story-point value at sprint end:** not reliable

- `RevisionFieldWhitelist` includes `Microsoft.VSTS.Common.BusinessValue`, but it does **not** include `Microsoft.VSTS.Scheduling.StoryPoints`.
- `ActivityEventIngestionService` only persists whitelisted field changes, so the ledger does not contain a historical StoryPoints timeline.
- Because the primary canonical field is missing from history, `ScopeSP(w, t_end)` cannot be replayed exactly when a PBI was re-estimated after sprint end.

**PortfolioPBIs(t_end):** not reliable

- `ResolvedWorkItemEntity` is a current resolution snapshot, not a historical timeline.
- `WorkItemResolutionService` deletes and rebuilds resolved rows on each resolution run, so historical `ResolvedProductId` membership is not preserved.
- This means current repository data can tell which PBIs belong to the portfolio **now**, but not which PBIs belonged to the portfolio at an arbitrary historical sprint end.

### Feasibility conclusion

Stock reconstruction is **partially feasible**:

- **state at sprint end** can be reconstructed
- **exact story-point stock at sprint end** cannot be reconstructed reliably with current persisted history
- **historical portfolio membership at sprint end** cannot be reconstructed reliably from `ResolvedWorkItemEntity` alone

The repository therefore supports an **approximation pattern** for stock reconstruction, but not the exact canonical `Stock(s)` definition yet.

## Inflow Detection Feasibility

Canonical requirement:

> `Inflow(s) = Σ ScopeSP(w, EnteredPortfolio(w))` for all PBIs where `EnteredPortfolio(w) ∈ SprintWindow(s)`

### What signal should define portfolio entry

The canonical entry event should be:

> the **first timestamp when a PBI becomes part of the portfolio backlog through hierarchy/product resolution**

This is a better definition than:

- creation date alone
- sprint commitment
- re-estimation
- reopen transitions

Creation date is only correct when an item is created directly inside the portfolio backlog. It is not a safe general definition for `EnteredPortfolio(w)`.

### What is available now

**Creation date:** available

- `WorkItemEntity.CreatedDate` stores the current snapshot creation timestamp.
- `GetPortfolioProgressTrendQueryHandler` already uses `CreatedDate` to exclude items that did not yet exist at a sprint end.

**Hierarchy-related history:** partially available

- `ActivityEventIngestionService` persists `System.Parent` changes.
- `ActivityEventIngestionService` persists `System.IterationPath` changes.
- `RevisionFieldWhitelist` includes `System.AreaPath`, so area-path changes are also present in the ledger as generic field-change events.
- `ActivityEventLedgerEntryEntity` stores `ParentId`, `FeatureId`, and `EpicId` context per ingested event.

### What is missing

**EnteredPortfolio(w):** not explicitly stored

- there is no dedicated portfolio-entry event
- there is no historical `ResolvedProductId`
- there is no historical resolved hierarchy snapshot for every timestamp

**Historical product resolution:** not reliable

- `WorkItemResolutionService` resolves membership from the current work-item graph and configured backlog roots, then overwrites `ResolvedWorkItemEntity`.
- If a PBI entered a portfolio because an ancestor moved, or because the hierarchy changed over time, current repository data does not materialize that membership transition as a stable historical signal.

### Feasibility conclusion

Inflow detection is **not reliably feasible** with current repository data.

Available signals can support approximations:

- `CreatedDate`
- first `System.Parent` change
- first `System.AreaPath` change
- first `System.IterationPath` change

But none of these is a reliable canonical `EnteredPortfolio(w)` signal without historical portfolio-membership resolution. This is the highest-risk gap in the current repository.

## Throughput Detection Feasibility

Canonical requirement:

> `Throughput(s) = Σ ScopeSP(w, FirstDone(w))` for all PBIs where `FirstDone(w) ∈ SprintWindow(s)`

### What is available now

**First Done transition detection:** feasible now

- `FirstDoneDeliveryLookup.Build` scans state-change history and returns the first transition into canonical `Done`.
- `StateClassificationLookup` maps raw states to canonical lifecycle states.

**Reopen handling:** feasible now

- `FirstDoneDeliveryLookup.GetFirstDoneTransitionTimestamp` ignores transitions where both the old and new states are canonically Done.
- Because only the first non-Done → Done transition is returned, reopen cycles do not create duplicate delivery events.

**Story points are resolved at Done:** implemented now

- `SprintDeliveryProjectionService.Compute` checks whether `effectiveFirstDoneByWorkItem` falls inside the sprint window.
- When a PBI is delivered in the sprint, it resolves story points through `ResolvePbiStoryPointEstimate`.
- `SprintMetricsProjectionEntity.CompletedPbiStoryPoints` persists the delivered story-point total.

### What is missing

**Story-point value at `FirstDone(w)`:** not exact historically

- throughput event detection is reliable
- the quantity uses the current snapshot estimate resolved during projection
- if story points change after Done, the repository cannot replay the exact historical `ScopeSP(w, FirstDone(w))` because StoryPoints history is not stored

### Feasibility conclusion

Throughput detection is the strongest part of the model:

- **first Done transition detection exists**
- **reopen handling exists**
- **story points are resolved when delivery is counted**

The remaining limitation is historical estimate drift after Done. So throughput is **operationally feasible**, but exact canonical story-point-at-event reconstruction is still conditional on missing StoryPoints history.

## Remaining Scope Feasibility

Canonical requirement:

> `RemainingScope(s) = Σ ScopeSP(w, t_end)` for all `w ∈ PortfolioPBIs(t_end)` where `State(w, t_end) ∈ { New, InProgress }`

### What is available now

**State at sprint end:** feasible

- `StateReconstructionLookup.GetStateAtTimestamp` can reconstruct raw state at `t_end`.
- `StateClassificationLookup` can map the reconstructed raw state into canonical `New`, `InProgress`, `Done`, or `Removed`.
- `SprintSpilloverLookup` already uses this state-at-sprint-end reconstruction pattern in production logic.

**Story-point resolution:** feasible for current snapshots

- `CanonicalStoryPointResolutionService` already provides the canonical story-point resolution logic needed once the target item set is known.

### What is missing

Remaining scope depends on the same two historical signals as stock:

1. historical `PortfolioPBIs(t_end)`
2. historical `ScopeSP(w, t_end)`

Current repository data can reconstruct the **open/closed state filter** at sprint end, but it cannot reconstruct the exact historical story-point values or exact historical portfolio membership reliably.

### Feasibility conclusion

Remaining scope is **partially feasible**:

- the sprint-end open-state snapshot can be reconstructed
- the exact story-point quantity is not reliable for historical sprints with estimate changes or portfolio-membership changes

## Missing Signals

The following canonical signals cannot currently be computed reliably:

1. **Historical StoryPoints timeline**  
   `Microsoft.VSTS.Scheduling.StoryPoints` changes are not ingested into `ActivityEventLedgerEntryEntity`.

2. **Historical portfolio membership timeline**  
   `ResolvedWorkItemEntity` is a current snapshot and is rebuilt by `WorkItemResolutionService`; it is not a time-series table.

3. **Explicit `EnteredPortfolio(w)` timestamp**  
   No persisted event marks the first time a PBI became part of the portfolio backlog.

4. **Historical resolved product assignment**  
   The repository does not store `ResolvedProductId` at each event timestamp, so product entry/exit cannot be replayed reliably.

5. **Exact story-point-at-Done reconstruction**  
   Throughput quantity is vulnerable to post-delivery estimate drift because StoryPoints history is absent.

6. **Exact sprint-end story-point stock snapshot**  
   The replay pattern exists, but the required canonical story-point history does not.

## Computational Cost

| Computation | Current data shape | On-demand cost | Projection suitability |
| --- | --- | --- | --- |
| Stock reconstruction | Current snapshots + event replay | **Moderate to high** — replay per sprint across all candidate PBIs, similar to `ComputeHistoricalScopeEffort` | Better as a projection or materialized snapshot once historical story-point and membership signals exist |
| Inflow detection | Creation date + hierarchy/area/iteration events | **High** — requires scanning events and replaying portfolio membership rules across hierarchy changes | Should be precomputed once `EnteredPortfolio` semantics and signals are added |
| Throughput detection | State events + current snapshot estimates | **Low to moderate** — `FirstDoneDeliveryLookup` is linear in state events and is already reused by projections | Already a good fit for `SprintMetricsProjectionEntity` |
| Remaining scope snapshot | Same inputs as stock + state filter | **Moderate to high** — same replay cost as stock plus open-state classification | Best computed together with stock in the same projection |

### Recommendation

- **Throughput** should remain precomputed in projections; the repository already does this via `SprintTrendProjectionService` and `SprintMetricsProjectionEntity`.
- **Stock** and **RemainingScope** can be computed on demand only as approximations with current signals; exact canonical versions should be projected after the missing historical signals are added.
- **Inflow** should not be computed on demand from current data for canonical reporting; it needs a dedicated entry signal or historical membership materialization first.

## Implementation Risk

Overall implementation risk for the canonical PortfolioFlow model is **medium-high**.

### Low-risk areas

- canonical state mapping
- first-Done detection
- reopen handling
- projection storage for throughput totals

### Medium-risk areas

- reusing the current historical replay pattern for story-point stock once the right signals exist
- combining state-at-time reconstruction with canonical open-scope filtering

### High-risk areas

- defining and reconstructing `EnteredPortfolio(w)` without historical `ResolvedProductId`
- reconstructing `PortfolioPBIs(t_end)` for historical sprints
- reconstructing `ScopeSP(w, t_end)` and `ScopeSP(w, FirstDone(w))` without StoryPoints history

### Final feasibility assessment

The canonical PortfolioFlow model is **not fully implementable with current repository data as-is**.

What is already feasible:

- canonical state-at-time reconstruction
- canonical first-Done throughput event detection
- projection-based storage of delivered story points

What is not yet reliable enough for canonical CDC extraction:

- exact stock reconstruction in story-point scope
- exact remaining scope reconstruction in story-point scope
- exact inflow detection via `EnteredPortfolio(w)`

CDC extraction should therefore wait until the repository captures:

1. historical StoryPoints changes
2. historical portfolio-membership resolution or an explicit portfolio-entry event
3. a stable rule for `EnteredPortfolio(w)` materialization
