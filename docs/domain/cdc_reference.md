# Canonical Domain Core Reference

Status: Authoritative CDC consolidation  
Purpose: Describe the stable Canonical Domain Core (CDC) as the single human-readable reference for slice ownership, boundaries, and dependency direction.

The CDC is the canonical analytics interpretation for PoTool. It defines the domain meaning of story points, effort hours, commitment, spillover, delivery trend, stock, inflow, throughput, remaining scope, and forecast without coupling those concepts to handlers, DTO names, or persistence details.

This document consolidates what is already established in the slice domain models and audit reports. It does not redesign semantics. When a detailed rule or formula is needed, the linked slice references remain authoritative.

## Purpose of the CDC

The CDC exists to keep every delivery-facing analytic slice aligned to one domain vocabulary and one ownership model.

It provides:

- one canonical interpretation of work-item hierarchy, state, estimation, sprint timing, and source truth
- one stable inventory of completed slices and their boundaries
- one explanation of which domain outputs belong inside the CDC versus in application adapters
- one dependency map so future work does not reintroduce duplicated semantics

Authoritative foundations:

- `docs/domain/domain_model.md`
- `docs/domain/rules/hierarchy_rules.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/state_rules.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/propagation_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/source_rules.md`

## Slice Overview

### Core Concepts

Purpose:

- define the shared domain primitives used by every CDC slice

Canonical inputs:

- work-item hierarchy and product scope from `docs/domain/domain_model.md`
- canonical estimation semantics from `docs/domain/rules/estimation_rules.md`
- canonical state semantics from `docs/domain/rules/state_rules.md`
- sprint windows and commitment timing from `docs/domain/rules/sprint_rules.md`
- source-truth and propagation rules from `docs/domain/rules/source_rules.md` and `docs/domain/rules/propagation_rules.md`

Canonical outputs:

- shared meaning of product scope, delivery unit, story points, effort hours, sprint window, commitment timestamp, canonical Done, Removed, and first-Done attribution

Downstream consumers:

- BacklogQuality
- SprintCommitment
- DeliveryTrends
- Forecasting
- EffortDiagnostics
- PortfolioFlow
- Shared Statistics

What remains outside the slice:

- handler orchestration
- DTO shaping
- persistence entities
- UI labels and compatibility aliases

### BacklogQuality

Purpose:

- own current-state backlog validation, readiness scoring, and implementation readiness without historical sprint reconstruction

Canonical inputs:

- current work-item snapshots
- canonical hierarchy and product scope
- canonical estimation and state semantics
- backlog validation rules cataloged in `docs/audits/backlog_quality_cdc_summary.md`

Canonical outputs:

- canonical validation findings for `SI`, `RR`, and `RC` rule families
- backlog readiness scores
- implementation readiness scores

Downstream consumers:

- validation triage handlers
- validation queue handlers
- validation impact-analysis handlers

What remains outside the slice:

- queue composition and triage orchestration
- dashboard health heuristics
- compatibility aliases such as `RC-2` and `EFF`
- display metadata and rule-description presentation

Primary references:

- `docs/audits/backlog_quality_cdc_summary.md`

### SprintCommitment

Purpose:

- reconstruct historical sprint commitment, post-commitment scope movement, first-Done completion, and spillover

Canonical inputs:

- sprint metadata and sprint ordering
- `System.IterationPath` history
- `System.State` history
- canonical state mapping
- current snapshots used only as reconstruction anchors and current-state cache

Canonical outputs:

- `SprintCommitment`
- `SprintScopeAdded`
- `SprintScopeRemoved`
- `SprintCompletion`
- `SprintSpillover`
- `SprintFactResult` for commitment, added scope, removed scope, delivered scope, delivered-from-added scope, spillover, and remaining story points

Downstream consumers:

- DeliveryTrends
- PortfolioFlow throughput attribution
- sprint metrics and sprint execution handlers

What remains outside the slice:

- handler-specific presentation models
- sprint execution UI heuristics
- compatibility naming at transport boundaries

Primary references:

- `docs/domain/sprint_commitment_domain_model.md`
- `docs/domain/sprint_commitment_cdc_summary.md`
- `docs/audits/sprint_commitment_application_alignment.md`
- `docs/audits/cdc_coverage_audit.md`

### DeliveryTrends

Purpose:

- own historical delivery-trend projections, feature and epic progress rollups, progression deltas, and product-level delivery summaries built from sprint facts

Canonical inputs:

- SprintCommitment outputs
- canonical hierarchy resolution
- first-Done delivery attribution
- spillover results

Canonical outputs:

- sprint delivery projections
- sprint trend metrics
- feature progress
- epic progress
- progression deltas
- product delivery progress summaries
- portfolio delivery composition summaries, including product shares and feature contribution shares

Downstream consumers:

- trend-focused API handlers
- trend dashboards
- Forecasting

What remains outside the slice:

- backlog loading and filtering orchestration
- forecast confidence semantics
- portfolio stock and inflow semantics

Primary references:

- `docs/audits/delivery_trend_analytics_cdc_summary.md`

### Forecasting

Purpose:

- own future-looking forecast semantics derived from historical delivery facts

Canonical inputs:

- delivery-trend history and sprint delivery summaries
- remaining scope in story points
- sprint-window semantics
- shared statistical helpers used for distribution and percentile calculations

Canonical outputs:

- delivery forecast
- completion projection
- velocity calibration
- forecast distribution and planning bands

Downstream consumers:

- roadmap and forecast handlers
- forecast-oriented UI surfaces

What remains outside the slice:

- historical sprint reconstruction
- backlog retrieval and filtering
- UI risk labels and page composition
- transport-specific response shapes

Primary references:

- `docs/domain/forecasting_domain_model.md`
- `docs/audits/forecasting_cdc_summary.md`

### EffortDiagnostics

Purpose:

- own the stable effort-diagnostics formulas for effort imbalance and concentration risk

Canonical inputs:

- effort hours resolved from current work-item data
- shared pure-math statistics where the contract is repository-wide

Canonical outputs:

- imbalance metrics
- concentration-risk metrics
- reusable domain statistics for effort diagnostics

Downstream consumers:

- effort imbalance handlers
- effort concentration handlers

What remains outside the slice:

- estimation suggestions
- capacity planning
- capacity calibration
- handler-owned grouping, filtering, and recommendation text

Primary references:

- `docs/audits/effort_diagnostics_cdc_extraction_report.md`

### EffortPlanning

Purpose:

- own the stable effort-planning formulas for effort distribution, estimation quality, and effort suggestions

Canonical inputs:

- effort hours resolved from current work-item snapshots
- current work-item grouping context such as area path, iteration path, type, and retrieved timestamp
- shared pure-math statistics where the contract is repository-wide
- effort-estimation defaults supplied by application settings

Canonical outputs:

- area-path effort totals
- iteration effort totals
- heat-map cells and utilization percentages
- effort-quality rollups by type and over time
- effort suggestions with similarity ranking, median effort, confidence, and explanation factors

Downstream consumers:

- effort distribution handlers
- effort estimation quality handlers
- effort estimation suggestion handlers

What remains outside the slice:

- product-scoped loading
- completed-state filtering
- candidate-item filtering such as `OnlyInProgressItems`
- DTO shaping and analysis timestamps

Primary references:

- `docs/audits/effort_planning_cdc_extraction.md`

### PortfolioFlow

Purpose:

- own canonical portfolio stock, inflow, throughput, remaining scope, net flow, and completion semantics in story points

Canonical inputs:

- hierarchy and portfolio membership resolution
- canonical story-point resolution
- canonical state mapping
- sprint windows
- first-Done delivery attribution
- resolved portfolio-entry transitions

Canonical outputs:

- portfolio stock per sprint
- portfolio inflow per sprint
- portfolio throughput per sprint
- remaining scope per sprint
- completion percent and net flow per sprint
- multi-sprint portfolio trend summaries, including cumulative net flow, scope change, remaining-scope change, and trajectory

Downstream consumers:

- portfolio projection materialization
- portfolio trend handlers and adapters

What remains outside the slice:

- ranking and portfolio UI composition
- legacy effort-based transport contracts
- non-canonical compatibility summaries preserved for current clients

Primary references:

- `docs/domain/portfolio_flow_model.md`
- `docs/audits/portfolio_flow_projection.md`
- `docs/audits/portfolio_flow_projection_validation.md`

### Shared Statistics

Purpose:

- own repository-wide pure math with one agreed semantic contract and support CDC slices that rely on reusable statistical primitives

Canonical inputs:

- numeric samples already prepared by owning slices

Canonical outputs:

- shared mean, median, variance, standard deviation, and percentile semantics
- stable effort-diagnostics support primitives where their ownership is explicitly centralized

Downstream consumers:

- Forecasting
- EffortDiagnostics
- other slices only when the statistical contract is truly shared rather than slice-local

What remains outside the slice:

- slice-specific confidence heuristics
- slice-specific utilization bands
- local nullable or empty-sample contracts intentionally preserved for isolated consumers

Primary references:

- `docs/audits/statistical_core_cleanup_report.md`

## Cross-Slice Dependencies

Dependency direction is intentionally one-way:

1. Core Concepts feed every other slice.
2. BacklogQuality depends on Core Concepts only.
3. SprintCommitment depends on Core Concepts only and provides historical sprint facts.
4. DeliveryTrends depends on SprintCommitment plus Core Concepts.
5. Forecasting depends on DeliveryTrends, Core Concepts, and Shared Statistics.
6. EffortDiagnostics depends on Core Concepts and Shared Statistics.
7. PortfolioFlow depends on SprintCommitment outputs plus Core Concepts.

In shorthand:

- Core Concepts -> BacklogQuality
- Core Concepts -> SprintCommitment
- SprintCommitment -> DeliveryTrends
- SprintCommitment -> PortfolioFlow
- DeliveryTrends -> Forecasting
- Shared Statistics -> Forecasting
- Shared Statistics -> EffortDiagnostics

No CDC slice should depend on handlers, DTOs, UI pages, or persistence entities for its semantics.

For the redrawable map of nodes and edges, see:

- `docs/domain/cdc_domain_map.md`

## What Stays Outside the CDC

The CDC is not the owner of:

- API handler orchestration, request assembly, filtering, and DTO mapping
- UI page composition, chart layout, labels, and display metadata
- transport compatibility aliases preserved for current clients
- persistence entity design and read-model materialization mechanics
- dashboard-only heuristics that have not been canonically adopted
- ranking, queue composition, recommendation copy, and other workflow-specific presentation concerns

These concerns may consume CDC outputs, but they must not redefine CDC semantics.

## Application Boundary

The application boundary is the seam where handlers and services load data, call CDC slices, and shape results for current clients.

Inside the CDC:

- canonical records
- canonical formulas
- canonical state, estimation, sprint, source-truth, and propagation semantics

Outside the CDC but allowed to consume it:

- `PoTool.Api` handlers and services
- compatibility adapters that preserve older response shapes
- UI-facing view models and labels

Application alignment references:

- `docs/audits/sprint_commitment_application_alignment.md`
- `docs/audits/cdc_coverage_audit.md`

## Persistence Boundary

Persistence is downstream from CDC semantics, not the source of those semantics.

The CDC defines what must be calculated. Projection entities and tables define how selected outputs are materialized for application use.

Current persistence facts described by the CDC documentation:

- raw current-state work-item snapshots remain the base input for snapshot-driven slices
- raw work-item history remains the source of truth for historical reconstruction
- sprint metrics and portfolio flow projections materialize selected CDC outputs for downstream consumption

Persistence references:

- `docs/audits/portfolio_flow_projection.md`
- `docs/audits/portfolio_flow_projection_validation.md`

## Future Architecture Directions

The completed CDC is stable enough to support structural work that was intentionally deferred until semantic ownership settled.

Established future directions already implied by the existing documents:

- keep migrating application consumers from compatibility wrappers toward direct CDC-backed projections
- isolate or retire remaining snapshot-style wording where historical replay is now canonical
- finish adapter cleanup for backlog-quality aliases and category inference
- preserve Shared Statistics as the reusable pure-math core while leaving slice-specific confidence heuristics local
- use the stable CDC as the foundation for persistence abstraction, optional hexagonal architecture, and later API contract cleanup

Completion summary reference:

- `docs/audits/cdc_completion_summary.md`

## Compatibility Debt Still Present

The remaining debt is application and transport debt, not CDC semantic debt.

Documented examples:

- legacy `*Effort` DTO names still appear at transport boundaries even where canonical semantics are story points
- those effort-named fields are legacy transport aliases for story points when they surface CDC-derived delivery, forecasting, or progress values; the naming does not indicate a separate CDC concept
- compatibility aliases such as `RC-2` and `EFF` remain in backlog-quality adapters and UI metadata
- pages and handlers still preserve older response shapes for clients during migration
- portfolio consumers still carry legacy effort-oriented compatibility surfaces while canonical stock, inflow, throughput, and remaining scope semantics are story-point based

Compatibility references:

- `docs/audits/application_semantic_audit.md`
- `docs/audits/portfolio_flow_projection.md`
- `docs/audits/portfolio_flow_projection_validation.md`
- `docs/audits/portfolio_flow_consumers_audit.md`
- `docs/audits/backlog_quality_cdc_summary.md`
