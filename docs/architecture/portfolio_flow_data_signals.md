# PortfolioFlow Data Signals

Status: Design only  
Purpose: Define the minimum ingestion and persistence changes required to make the canonical PortfolioFlow model computable from repository data.

Reference documents:

- `docs/domain/portfolio_flow_model.md`
- `docs/audits/portfolio_flow_feasibility.md`
- `docs/audits/portfolio_flow_semantic_audit.md`

This document is intentionally limited to **historical signals, ingestion, persistence, and projection boundaries**.  
It does not introduce UI changes, handler rewrites, or projection implementation details beyond what is necessary to make PortfolioFlow computable.

## Required Historical Signals

The feasibility audit identified three missing canonical signals:

1. historical `Microsoft.VSTS.Scheduling.StoryPoints`
2. historical portfolio membership
3. explicit `EnteredPortfolio(w)` detection

The minimum repository additions should therefore persist:

1. **Story point change history** for PBIs
2. **Resolved portfolio membership changes** for PBIs
3. **Canonical portfolio-entry events** derived from membership transitions

The design goal is to add the smallest set of durable facts needed so that these PortfolioFlow formulas become reliable for historical sprints:

- `Stock(s)`
- `RemainingScope(s)`
- `Inflow(s)`
- `Throughput(s)`

## StoryPoints History

### Required ingested field

The revision field that must be ingested is:

- `Microsoft.VSTS.Scheduling.StoryPoints`

This is the primary canonical field in the estimation rules and must be persisted historically before historical `ScopeSP(w, t)` can be reconstructed reliably.

### Ledger design

`ActivityEventLedgerEntryEntity` already supports generic field-change storage:

- `FieldRefName`
- `OldValue`
- `NewValue`
- `EventTimestamp`
- hierarchy context fields

That means the entity shape is already sufficient to store StoryPoints changes as normal ledger rows.  
No new table is required for this signal, and no schema extension is required if StoryPoints is stored as a standard field-change event.

The minimal change is:

1. add `Microsoft.VSTS.Scheduling.StoryPoints` to `RevisionFieldWhitelist`
2. let `ActivityEventIngestionService` persist StoryPoints deltas into `ActivityEventLedgerEntryEntity`
3. treat those rows as canonical estimate-history input for historical replay

### StoryPointChanged event structure

The canonical persisted event can be expressed as:

`StoryPointChanged(workItemId, timestamp, oldStoryPoints, newStoryPoints, updateId)`

Stored form:

- `WorkItemId` = target PBI
- `FieldRefName` = `Microsoft.VSTS.Scheduling.StoryPoints`
- `EventTimestamp` / `EventTimestampUtc` = revision timestamp
- `OldValue` = previous StoryPoints value as string or null
- `NewValue` = new StoryPoints value as string or null
- `UpdateId` = TFS update identifier for deduplication

Design notes:

- only PBI StoryPoints participate in canonical PortfolioFlow scope
- bug and task rows may still be ingested generically, but PortfolioFlow must ignore them
- no separate StoryPoints history table is needed unless later performance evidence justifies one

## Portfolio Membership Timeline

### Problem

`ResolvedWorkItemEntity` is a current resolution snapshot.  
It does not preserve historical `ResolvedProductId`, so the repository cannot answer whether a PBI belonged to the portfolio at an arbitrary historical timestamp.

### Options evaluated

#### Option A — store `ResolvedProductId` timeline in the ledger

Design:

- persist a ledger event whenever a PBI's resolved portfolio membership changes
- use the resolved value, not only the raw trigger field

Pros:

- directly answers the historical question PortfolioFlow needs: "was this PBI in the portfolio at time `t`?"
- resilient to different underlying causes: parent change, area change, backlog-root remapping, or product assignment change
- keeps replay simple because PortfolioFlow consumes one canonical membership signal
- additive to existing snapshot resolution

Cons:

- requires one additional canonical event type during resolution/ingestion

#### Option B — persist hierarchy change events and recompute membership on replay

Pros:

- reuses existing raw change signals such as `System.Parent` and `System.AreaPath`
- appears minimal at first glance

Cons:

- not sufficient by itself because membership depends on resolved hierarchy and product rules, not one raw field
- replay becomes fragile when ancestor moves or resolution rules evolve
- every historical query would have to recompute portfolio membership from raw graph changes

#### Option C — materialize portfolio membership snapshots per ingestion cycle

Pros:

- simple historical lookup for coarse-grained reporting

Cons:

- snapshots are heavier than necessary
- membership changes between cycles can be lost or blurred
- introduces a larger persistence surface than the canonical event actually required by PortfolioFlow

### Recommended design

Choose **Option A**.

The safest minimal design is to persist a **resolved membership timeline**, not only raw hierarchy changes and not coarse snapshots.

Canonical event:

`PortfolioMembershipChanged(workItemId, timestamp, oldResolvedProductId, newResolvedProductId, triggerFieldRefName)`

Minimal persistence approach:

- extend the activity ledger so it can store resolved membership transitions as first-class rows
- keep `ResolvedWorkItemEntity` as the current snapshot table
- add resolved membership rows only when the effective `ResolvedProductId` changes

Because `ActivityEventLedgerEntryEntity` currently stores only one generic `FieldRefName` plus string old/new values, it can support this event with a small extension in one of two acceptable forms:

1. **preferred:** reserve a synthetic field name such as `PoTool.ResolvedProductId`
2. **alternative:** add an explicit event-kind column if the ledger is already being revised for broader CDC work

For the minimal PortfolioFlow step, the preferred choice is the synthetic field approach because it keeps the existing generic ledger shape intact.

### Why resolved membership is the right historical fact

PortfolioFlow does not need to know every intermediate hierarchy mutation.  
It needs to know **when a PBI started or stopped belonging to the portfolio**.

Persisting `ResolvedProductId` transitions gives exactly that fact and isolates PortfolioFlow from future changes in hierarchy-replay rules.

## Portfolio Entry Event

### Canonical rule

`EnteredPortfolio(w)` is:

> the **first timestamp when PBI `w` transitions from not belonging to the portfolio to belonging to the portfolio, according to the resolved portfolio-membership timeline**

This is the single canonical rule.

It deliberately normalizes several possible triggers into one meaning:

- PBI created under portfolio root
- PBI parent changed into portfolio hierarchy
- `System.AreaPath` changed into portfolio scope
- product assignment or backlog-root resolution changed

These are implementation triggers only.  
They must not create different semantic entry-event definitions.

### Detection

Detection should happen from the resolved membership timeline:

1. ingest raw field changes that can affect resolution
2. recompute effective `ResolvedProductId` during resolution/incremental ingestion
3. when membership changes from `null` or non-portfolio to the target portfolio, emit one membership-change row
4. the first such transition for a PBI is `EnteredPortfolio(w)`

### Stored form

No separate `EnteredPortfolio` table is required.

The event is derived from the first ledger row matching:

- `FieldRefName = PoTool.ResolvedProductId`
- `OldValue` is null or not equal to the portfolio product id
- `NewValue` equals the portfolio product id

Canonical event structure:

`EnteredPortfolio(workItemId, timestamp, resolvedProductId, triggerFieldRefName, updateId)`

Storage recommendation:

- store the membership transition row in the ledger
- optionally materialize `FirstEnteredPortfolioAtUtc` later in a projection if query volume demands it

This keeps ingestion minimal while still making canonical inflow computable.

## Historical Scope Resolution

### Canonical rule

Historical `ScopeSP(w, t)` must use the story-point value that was effective at timestamp `t`.

Therefore choose:

- **Option A — use StoryPoints value at timestamp**

Reject:

- **Option B — fallback to BusinessValue when StoryPoints missing** as a replacement for historical StoryPoints history  
  BusinessValue remains a canonical fallback inside the estimation rules, but it is not a substitute for storing StoryPoints history.
- **Option C — derived sibling average** as a replacement for missing historical storage  
  Derived estimates remain an aggregation rule, not a persistence strategy.

### Resolution rule at time `t`

Historical resolution should remain:

1. historical `StoryPoints(t)`
2. historical `BusinessValue(t)` if StoryPoints is missing
3. derived sibling average using sibling values effective at `t`
4. missing estimate

### Can `CanonicalStoryPointResolutionService` be reused?

Yes, with one boundary condition.

`CanonicalStoryPointResolutionService` already implements the canonical precedence:

- StoryPoints
- BusinessValue
- derived sibling average
- missing

The service can operate on historical values **if the caller reconstructs a point-in-time work-item view** before invoking it.

That means the required new work is not a new estimation algorithm.  
The required work is:

- reconstruct `StoryPoints` at `t` from ledger history
- reconstruct `BusinessValue` at `t` from ledger history
- provide the relevant sibling set effective at `t`

So the design decision is:

- **reuse `CanonicalStoryPointResolutionService` unchanged**
- add historical input reconstruction around it

## Projection Strategy

### Principle

Persist **atomic historical facts** in the ledger.  
Project **expensive sprint-level aggregates** for repeated portfolio reporting.

### Recommended boundaries

| Signal | Recommended storage strategy | Reason |
| --- | --- | --- |
| `Throughput(s)` | **Projection** | already a sprint-level repeated metric; should remain materialized once historical StoryPoints at `FirstDone` are available |
| `Inflow(s)` | **Projection from persisted `EnteredPortfolio`/membership events** | canonical event exists, but portfolio pages need repeated sprint aggregation; projecting avoids repeated event scans |
| `Stock(s)` | **Projection** | requires replay of membership, state, and historical scope at sprint end; too expensive and fragile for repeated on-demand computation |
| `RemainingScope(s)` | **Projection** | same replay inputs as stock, so compute in the same projection pass |

### Minimal implementation implication

The minimum durable inputs are:

- `StoryPointChanged`
- `PortfolioMembershipChanged`
- existing state-change ledger rows
- existing work item snapshots

From those inputs, a dedicated PortfolioFlow projection can later materialize per-sprint values for:

- `Stock(s)`
- `RemainingScope(s)`
- `Inflow(s)`
- `Throughput(s)`

### Why not compute everything on demand?

On-demand replay remains acceptable for one-off diagnostics or backfill verification, but it should not be the steady-state source for portfolio pages because:

- stock and remaining scope share a replay-heavy sprint-end snapshot calculation
- inflow depends on historical entry classification
- repeated historical series queries would duplicate expensive replay logic

The correct boundary is therefore:

- **ledger = source of historical facts**
- **projection = source of portfolio-flow series**

## Migration Impact

### Backwards compatibility

Existing portfolio pages can continue operating on legacy metrics until a new PortfolioFlow projection exists.

This design is additive:

- current handlers and pages remain untouched
- current `ResolvedWorkItemEntity` snapshot remains valid for present-state features
- legacy effort-based portfolio metrics can continue to serve existing pages temporarily
- the new projection can be introduced beside the legacy one and switched over later

### Minimal repository changes implied by this design

1. add `Microsoft.VSTS.Scheduling.StoryPoints` to revision ingestion
2. persist resolved membership transitions using a canonical ledger field such as `PoTool.ResolvedProductId`
3. derive `EnteredPortfolio(w)` from the first membership transition into the portfolio
4. reuse `CanonicalStoryPointResolutionService` against reconstructed historical inputs
5. add a dedicated PortfolioFlow projection that materializes stock, remaining scope, inflow, and throughput

### What is intentionally not changed yet

This design does **not** require:

- replacing legacy portfolio pages immediately
- removing legacy effort-based metrics immediately
- introducing per-cycle membership snapshots
- introducing a separate StoryPoints history table
- changing the canonical domain formulas already defined in `docs/domain/portfolio_flow_model.md`

### Final design statement

The minimum safe design is:

- keep the existing generic activity ledger as the historical fact store
- add StoryPoints history to that ledger
- add resolved portfolio-membership transitions to that ledger
- define `EnteredPortfolio(w)` from the first resolved-membership transition into the portfolio
- project sprint-level PortfolioFlow metrics from those persisted facts

That is the smallest ingestion and persistence expansion that makes canonical PortfolioFlow computable without forcing PortfolioFlow to replay fragile raw hierarchy semantics on every query.
