# PortfolioFlow Canonical Model

Status: Canonical definition for PortfolioFlow before CDC extraction  
Purpose: Define one stable portfolio stock/flow interpretation before implementation changes.

This document is the authoritative semantic model for PortfolioFlow.

If current implementation differs from this document, the implementation is a deviation until explicitly reviewed.

---

## Canonical Unit

PortfolioFlow uses **story-point scope** as its canonical unit.

Decision comparison:

| Criterion | Effort-hours | Story-point scope |
| --- | --- | --- |
| Alignment with DeliveryTrends | Low — DeliveryTrends treats delivered scope as story points | High — directly matches canonical delivery and velocity semantics |
| Alignment with Forecasting | Low — effort is diagnostic in Forecasting | High — Forecasting already uses remaining and delivered story points as its primary unit |
| Interpretability in UI | Medium — users often read it as "points" anyway, which causes confusion | High — "pts" already matches how trend and forecast screens are interpreted |
| Data availability | Medium — effort exists on several levels but is not the canonical delivery unit | High — authoritative story points exist on PBIs, with defined fallback and derived-estimate rules for aggregation |
| Risk of estimation drift | High — effort is frequently re-estimated and mixed with implementation-hours interpretation | Lower — story points are the canonical planning and delivery scope unit already used for velocity |

Canonical decision:

- **PortfolioFlow uses story-point scope**
- effort-hours remain diagnostic and calibration data outside the canonical PortfolioFlow unit
- bugs remain visible for workload and churn analysis, but they do **not** contribute to PortfolioFlow stock, inflow, outflow, or remaining scope because canonical story points on bugs are ignored by rule

## Stock Definition

Portfolio **stock** means:

> **the total story-point scope of resolved portfolio PBIs at sprint end, excluding Removed items**

This corresponds to candidate **B**.

Why this is the correct stock:

- stock must use the same unit as DeliveryTrends and Forecasting
- stock must represent the full scoped portfolio envelope at sprint end, not only the unfinished portion
- open backlog scope is a different concept and is defined below as **Remaining scope**
- the current effort-based reconstruction can later be replaced by story-point reconstruction without changing the semantic shape of the model

Implications:

- Done PBIs remain part of stock because they are part of total portfolio scope already resolved for the portfolio
- Removed items are excluded from stock
- Bugs contribute zero canonical stock because they are not valid story-point origin items

## Inflow Definition

Portfolio **inflow** means:

> **the story-point scope of PBIs that newly enter the portfolio backlog during the sprint**

This corresponds to candidate **A**.

Canonical inflow rules:

- include **new PBIs** that first become part of the portfolio backlog during the sprint window
- exclude **sprint commitment volume**; commitment is a SprintAnalytics planning snapshot, not portfolio backlog inflow
- exclude **estimation changes**; re-estimation is scope adjustment, not backlog entry
- exclude **reopened items**; reopen is rework against already-known scope, not new scope entering the portfolio

Why estimation changes are excluded:

- mixing backlog entry with re-estimation would make inflow ambiguous
- the semantic audit already showed that commitment- or estimate-based proxies blur the meaning of added scope
- estimation drift should remain a separate scope-adjustment concern if later needed, not part of canonical inflow

Why reopened items are excluded:

- the state rules treat reopen as rework (`Done → InProgress`), not as a new delivery or a new backlog item
- reopened scope affects the current open backlog state, but it does not represent scope newly entering the portfolio

## Outflow Definition

Portfolio **throughput/outflow** means:

> **the story points delivered in the sprint**

This corresponds to candidate **B**.

Canonical outflow rules:

- count only PBIs whose **first canonical Done transition** occurs inside the sprint window
- use canonical story-point scope as defined by the estimation rules
- exclude Bugs and Tasks
- exclude repeated Done transitions after reopen; only the first Done event counts

This keeps PortfolioFlow aligned with canonical velocity and DeliveryTrends throughput semantics.

## Remaining Scope Definition

**Remaining scope** means:

> **the current open backlog scope at sprint end**

This corresponds to candidate **A**.

Canonical remaining-scope rules:

- measure unfinished story-point scope at sprint end
- include PBIs whose canonical state at sprint end is `New` or `InProgress`
- exclude `Done` and `Removed`
- do **not** derive remaining scope as a residual from reconstructed stock minus cumulative throughput

Why the residual definition is rejected:

- it is range-relative and depends on the selected reporting window
- it breaks when reopen or estimate-change behavior differs from the selected range assumptions
- it turns a current-state question into a derived arithmetic artifact

Remaining scope is a sprint-end snapshot, not a residual reconstruction.

## Flow Equations

All PortfolioFlow formulas use **story-point scope**.

Notation:

- `ScopeSP(w, t)` = canonical story-point scope of PBI `w` at time `t`, using the canonical estimation rules
- `PortfolioPBIs(t)` = PBIs belonging to the portfolio at sprint end `t`
- `FirstDone(w)` = timestamp of the first canonical `Done` transition of `w`
- `EnteredPortfolio(w)` = timestamp when `w` first enters the portfolio backlog
- `SprintWindow(s)` = `[SprintStart, SprintEnd]` for sprint `s`

Formulas for sprint `s` with end timestamp `t_end`:

**Stock(s)**  
`Stock(s) = Σ ScopeSP(w, t_end)` for all `w ∈ PortfolioPBIs(t_end)` where `State(w, t_end) ≠ Removed`

**Throughput(s)**  
`Throughput(s) = Σ ScopeSP(w, FirstDone(w))` for all PBIs `w` where `FirstDone(w) ∈ SprintWindow(s)`

**Inflow(s)**  
`Inflow(s) = Σ ScopeSP(w, EnteredPortfolio(w))` for all PBIs `w` where `EnteredPortfolio(w) ∈ SprintWindow(s)`

**RemainingScope(s)**  
`RemainingScope(s) = Σ ScopeSP(w, t_end)` for all `w ∈ PortfolioPBIs(t_end)` where `State(w, t_end) ∈ { New, InProgress }`

**NetFlow(s)**  
`NetFlow(s) = Throughput(s) − Inflow(s)`

Interpretation:

- positive NetFlow = the portfolio digested more scope than it accepted during the sprint
- negative NetFlow = the portfolio accepted more new scope than it delivered during the sprint

**CompletionPercent(s)**  
`CompletionPercent(s) = ((Stock(s) − RemainingScope(s)) / Stock(s)) × 100`

When `Stock(s) = 0`, `CompletionPercent(s)` is undefined and should be emitted as null rather than forced to zero.

## Mapping From Current Metrics

| Current metric | Canonical concept | Required change |
| --- | --- | --- |
| `TotalScopeEffort` | `StockStoryPoints` | Replace effort-hour reconstruction with sprint-end story-point stock reconstruction for resolved PBIs; exclude Removed items and treat bugs as zero scope |
| `ThroughputEffort` | `ThroughputStoryPoints` | Replace effort-hours delivered in sprint with canonical first-Done story points |
| `CompletedPbiEffort` | `DeliveredStoryPoints` input to throughput | Stop using effort-hours as the portfolio outflow source; use delivered story points instead |
| `AddedEffort` | `InflowStoryPoints` | Replace the `PlannedEffort` commitment proxy with actual backlog-entry scope for PBIs entering the portfolio during the sprint |
| `PlannedEffort` | Outside PortfolioFlow inflow | Keep as SprintAnalytics commitment volume; do not reuse it as portfolio inflow |
| `RemainingEffort` | `RemainingScopeStoryPoints` | Replace the residual formula with a sprint-end open-backlog scope snapshot |
| `PercentDone` | `CompletionPercent` | Rebase from cumulative effort digestion to story-point completion derived from `Stock` and `RemainingScope` |
| `TotalCompletedEffort` / `ProductDeliveryDto.CompletedEffort` | `DeliveredStoryPoints` snapshot | Rename or remap legacy effort-named transport values so portfolio delivery summaries use explicit story-point semantics |
| `FeatureDeliveryDto.SprintCompletedEffort` | `FeatureDeliveredStoryPoints` | Keep the underlying story-point meaning but remove the legacy effort naming when the contract is revised |
| `AverageProgressPercent` / `ProgressionDelta` | Outside PortfolioFlow core | Keep as delivery/progression presentation metrics; do not mix them into the canonical PortfolioFlow stock/flow model |

## CDC Boundary

### Responsibilities inside PortfolioFlow CDC

PortfolioFlow CDC should own:

- **stock reconstruction** in canonical story-point scope at sprint end
- **flow computation** for inflow, throughput, remaining scope, net flow, and completion percent
- **portfolio trend summary** derived from the canonical portfolio flow series

### Responsibilities PortfolioFlow consumes but does not own

PortfolioFlow should consume:

- canonical hierarchy resolution and product-to-portfolio membership
- canonical state mapping
- canonical story-point resolution and derived-estimate rules
- sprint-window semantics and first-Done delivery attribution

### Responsibilities outside PortfolioFlow CDC

The following remain outside PortfolioFlow:

- **portfolio ranking** such as top products and top feature contribution shares
- **portfolio UI composition** such as cards, charts, labels, and transport-compatibility naming
- **product summaries** and delivery-composition rollups already owned by DeliveryTrends or other portfolio application handlers

## Final Boundary Statement

PortfolioFlow is the canonical **portfolio stock/flow** slice:

- it measures stock, inflow, outflow, remaining scope, and completion using one unit: **story-point scope**
- it does **not** own portfolio ranking or presentation aggregation
- it does **not** redefine SprintAnalytics, DeliveryTrends, or Forecasting; it consumes their canonical rules and inputs

The stable extraction target is therefore:

- **PortfolioFlow CDC** for canonical stock/flow semantics
- **application aggregation** for portfolio ranking and UI composition
