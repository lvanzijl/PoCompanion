# CDC Reference

Status: Authoritative cross-slice CDC reference  
Purpose: Provide one canonical navigation and boundary document for the delivery-domain CDC slices without restating each slice's full semantics.

This document is the single authoritative CDC reference for cross-slice ownership, dependencies, and navigation.

Detailed formulas, rule thresholds, and slice-specific semantics remain in the linked domain model and audit documents. To avoid duplicated semantic descriptions, this reference summarizes ownership and signal boundaries only and links to the canonical source material for the full definitions.

## CDC Overview

The canonical delivery-domain CDC is organized around eight slices:

- estimation semantics
- backlog quality
- effort diagnostics
- statistical helpers
- delivery trends
- forecasting
- portfolio flow
- sprint commitment

These slices share one domain baseline:

- canonical work-item hierarchy and product scope live in [`docs/domain/domain_model.md`](./domain_model.md)
- primitive hierarchy rules live in [`docs/domain/rules/hierarchy_rules.md`](./rules/hierarchy_rules.md)
- primitive state rules live in [`docs/domain/rules/state_rules.md`](./rules/state_rules.md)
- primitive estimation rules live in [`docs/domain/rules/estimation_rules.md`](./rules/estimation_rules.md)
- primitive sprint rules live in [`docs/domain/rules/sprint_rules.md`](./rules/sprint_rules.md)
- primitive propagation rules live in [`docs/domain/rules/propagation_rules.md`](./rules/propagation_rules.md)
- primitive metrics rules live in [`docs/domain/rules/metrics_rules.md`](./rules/metrics_rules.md)
- primitive source-truth rules live in [`docs/domain/rules/source_rules.md`](./rules/source_rules.md)

If a slice summary, audit, projection report, or application adapter disagrees with those primitives, the primitive domain documents remain authoritative.

## Core primitives

| Primitive | Canonical role | Authoritative references |
| --- | --- | --- |
| Hierarchy and product scope | Defines the product boundary, operational hierarchy, and PBI as the delivery unit. | [`docs/domain/domain_model.md`](./domain_model.md), [`docs/domain/rules/hierarchy_rules.md`](./rules/hierarchy_rules.md) |
| Estimation semantics | Defines story-point origin, effort rollup, derived-estimate behavior, and forecasting-safe aggregation rules. | [`docs/domain/rules/estimation_rules.md`](./rules/estimation_rules.md), [`docs/audits/estimation_audit.md`](../audits/estimation_audit.md) |
| State semantics | Defines canonical `New`, `InProgress`, `Done`, and `Removed` mapping plus first-Done delivery rules. | [`docs/domain/rules/state_rules.md`](./rules/state_rules.md) |
| Sprint and metric semantics | Defines `SprintWindow`, `CommitmentTimestamp`, velocity, churn, spillover, and remaining-scope formulas. | [`docs/domain/rules/sprint_rules.md`](./rules/sprint_rules.md), [`docs/domain/rules/metrics_rules.md`](./rules/metrics_rules.md) |
| Source-truth semantics | Defines when snapshots answer current-state questions and when update history answers historical questions. | [`docs/domain/rules/source_rules.md`](./rules/source_rules.md) |
| Propagation semantics | Defines which signals propagate upward and which remain state-driven at their own level. | [`docs/domain/rules/propagation_rules.md`](./rules/propagation_rules.md) |
| Shared statistics primitives | Defines the repository-wide reusable math boundary and the distinction between shared math and slice-local heuristics. | [`docs/audits/statistical_helper_audit.md`](../audits/statistical_helper_audit.md) |

These primitives must be referenced, not redefined, by the slices below.

## Event signals

The delivery CDC slices depend on a small set of recurring signal families:

| Signal family | What it provides | Primary consumers | References |
| --- | --- | --- | --- |
| Current snapshots | Current hierarchy, current state, current estimate values, and current product membership. | backlog quality, forecasting remaining scope, portfolio flow context | [`docs/domain/rules/source_rules.md`](./rules/source_rules.md), [`docs/domain/backlog_quality_domain_model.md`](./backlog_quality_domain_model.md) |
| `System.IterationPath` history | Historical sprint membership, post-commitment adds/removes, and next-sprint moves. | sprint commitment, delivery trends | [`docs/domain/sprint_commitment_domain_model.md`](./sprint_commitment_domain_model.md), [`docs/exploration/sprint_commitment_domain_exploration.md`](../exploration/sprint_commitment_domain_exploration.md) |
| `System.State` history plus canonical mapping | First-Done delivery attribution, reopen handling, historical end-state reconstruction. | sprint commitment, delivery trends, portfolio flow | [`docs/domain/rules/state_rules.md`](./rules/state_rules.md), [`docs/domain/sprint_commitment_domain_model.md`](./sprint_commitment_domain_model.md), [`docs/domain/portfolio_flow_model.md`](./portfolio_flow_model.md) |
| Story-point and estimate history | Point-in-time story-point scope used for throughput, stock, inflow, and forecast inputs. | estimation semantics, forecasting, portfolio flow, sprint metrics | [`docs/domain/rules/estimation_rules.md`](./rules/estimation_rules.md), [`docs/audits/portfolio_flow_projection.md`](../audits/portfolio_flow_projection.md) |
| Resolved portfolio membership transitions | Historical portfolio-entry facts distinct from sprint commitment proxies. | portfolio flow | [`docs/audits/portfolio_flow_projection.md`](../audits/portfolio_flow_projection.md), [`docs/audits/portfolio_flow_projection_validation.md`](../audits/portfolio_flow_projection_validation.md) |
| Sprint metadata and ordering | Sprint window boundaries, commitment boundary, and next-sprint resolution. | sprint commitment, delivery trends, forecasting | [`docs/domain/rules/sprint_rules.md`](./rules/sprint_rules.md), [`docs/audits/delivery_trend_analytics_cdc_summary.md`](../audits/delivery_trend_analytics_cdc_summary.md), [`docs/domain/forecasting_domain_model.md`](./forecasting_domain_model.md) |

## Domain slices

### Estimation semantics

Estimation semantics own the canonical meaning of story points, effort, rollups, and derived estimates used by downstream delivery slices.

Authoritative references:

- [`docs/domain/rules/estimation_rules.md`](./rules/estimation_rules.md)
- [`docs/domain/domain_model.md`](./domain_model.md)
- [`docs/audits/estimation_audit.md`](../audits/estimation_audit.md)

### Backlog quality

Backlog quality owns current-state backlog validation, readiness scoring, structural integrity, refinement readiness, and implementation readiness. It is a snapshot-driven slice and does not own historical delivery reconstruction.

Authoritative references:

- [`docs/domain/backlog_quality_domain_model.md`](./backlog_quality_domain_model.md)
- [`docs/audits/backlog_quality_domain_exploration.md`](../audits/backlog_quality_domain_exploration.md)
- [`docs/audits/backlog_quality_cdc_summary.md`](../audits/backlog_quality_cdc_summary.md)

### Effort diagnostics

Effort diagnostics own the stable effort-imbalance and effort-concentration subset plus the EffortDiagnostics-owned statistics surface. Estimation suggestions, capacity planning, and other heuristic families remain outside this stable CDC subset unless separately audited.

Authoritative references:

- [`docs/domain/effort_diagnostics_domain_model.md`](./effort_diagnostics_domain_model.md)
- [`docs/audits/effort_diagnostics_domain_exploration.md`](../audits/effort_diagnostics_domain_exploration.md)
- [`docs/audits/effort_diagnostics_semantic_audit.md`](../audits/effort_diagnostics_semantic_audit.md)
- [`docs/audits/effort_diagnostics_cdc_extraction_report.md`](../audits/effort_diagnostics_cdc_extraction_report.md)

### Statistical helpers

Statistical helpers own only repository-wide reusable pure math that has one agreed semantic contract. Slice-specific confidence, volatility, utilization, and similarly named heuristics stay local to their owning slice until a separate semantic decision standardizes them.

Authoritative references:

- [`docs/audits/statistical_helper_audit.md`](../audits/statistical_helper_audit.md)
- [`docs/domain/forecasting_domain_model.md`](./forecasting_domain_model.md)
- [`docs/audits/trend_delivery_analytics_exploration.md`](../audits/trend_delivery_analytics_exploration.md)

### Delivery trends

Delivery trends own historical sprint-delivery projections, feature and epic progress rollups, progression deltas, and the canonical delivery-trend models built from sprint facts. Delivery trends consume sprint commitment outputs and do not redefine commitment semantics.

Authoritative references:

- [`docs/audits/trend_delivery_analytics_exploration.md`](../audits/trend_delivery_analytics_exploration.md)
- [`docs/audits/delivery_trend_analytics_cdc_summary.md`](../audits/delivery_trend_analytics_cdc_summary.md)
- [`docs/domain/sprint_commitment_domain_model.md`](./sprint_commitment_domain_model.md)

### Forecasting

Forecasting owns future-looking delivery forecasts, completion projections, calibration bands, and forecast confidence/distribution outputs. It consumes historical delivery facts and shared math rather than reconstructing sprint history itself.

Authoritative references:

- [`docs/domain/forecasting_domain_model.md`](./forecasting_domain_model.md)
- [`docs/audits/forecasting_domain_exploration.md`](../audits/forecasting_domain_exploration.md)
- [`docs/audits/forecasting_semantic_audit.md`](../audits/forecasting_semantic_audit.md)
- [`docs/audits/forecasting_cdc_summary.md`](../audits/forecasting_cdc_summary.md)

### Portfolio flow

Portfolio flow owns canonical portfolio stock, inflow, throughput, remaining scope, net flow, and completion semantics in story-point scope. Projection materialization and application migration are documented separately so the semantic model stays distinct from the persistence and consumer path.

Authoritative references:

- [`docs/domain/portfolio_flow_model.md`](./portfolio_flow_model.md)
- [`docs/audits/portfolio_flow_domain_exploration.md`](../audits/portfolio_flow_domain_exploration.md)
- [`docs/audits/portfolio_flow_semantic_audit.md`](../audits/portfolio_flow_semantic_audit.md)
- [`docs/audits/portfolio_flow_projection.md`](../audits/portfolio_flow_projection.md)
- [`docs/audits/portfolio_flow_projection_validation.md`](../audits/portfolio_flow_projection_validation.md)
- [`docs/audits/portfolio_flow_consumers_audit.md`](../audits/portfolio_flow_consumers_audit.md)
- [`docs/audits/portfolio_flow_application_migration.md`](../audits/portfolio_flow_application_migration.md)

### Sprint commitment

Sprint commitment owns commitment reconstruction, post-commitment scope changes, first-Done completion facts, spillover, and the execution metrics derived from those signals. Delivery trends and forecasting are downstream consumers of these outputs.

Authoritative references:

- [`docs/domain/sprint_commitment_domain_model.md`](./sprint_commitment_domain_model.md)
- [`docs/domain/sprint_commitment_cdc_summary.md`](./sprint_commitment_cdc_summary.md)
- [`docs/exploration/sprint_commitment_domain_exploration.md`](../exploration/sprint_commitment_domain_exploration.md)
- [`docs/audits/sprint_commitment_cdc_extraction.md`](../audits/sprint_commitment_cdc_extraction.md)
- [`docs/audits/sprint_commitment_application_alignment.md`](../audits/sprint_commitment_application_alignment.md)

## Projection consumers

The projection and consumer documents below are the canonical cross-links for read-model and downstream-consumer behavior:

- Sprint commitment -> delivery trends consumer chain:
  - [`docs/domain/sprint_commitment_domain_model.md`](./sprint_commitment_domain_model.md)
  - [`docs/audits/delivery_trend_analytics_cdc_summary.md`](../audits/delivery_trend_analytics_cdc_summary.md)
- Delivery trends -> forecasting consumer chain:
  - [`docs/domain/forecasting_domain_model.md`](./forecasting_domain_model.md)
  - [`docs/audits/forecasting_cdc_summary.md`](../audits/forecasting_cdc_summary.md)
- Portfolio flow projection summaries and validation:
  - [`docs/audits/portfolio_flow_projection.md`](../audits/portfolio_flow_projection.md)
  - [`docs/audits/portfolio_flow_projection_validation.md`](../audits/portfolio_flow_projection_validation.md)
  - [`docs/audits/portfolio_flow_consumers_audit.md`](../audits/portfolio_flow_consumers_audit.md)
  - [`docs/audits/portfolio_flow_application_migration.md`](../audits/portfolio_flow_application_migration.md)

Projection reports document materialization, validation, and consumer migration. They must not replace the owning slice's semantic definition.

## Application boundaries

The application boundary for CDC documentation is:

- domain model and rule documents define canonical meaning
- slice audits document extraction status, ownership verification, and migration sequencing
- projection summaries document persisted read models and downstream consumer paths
- API handlers and services own data loading, orchestration, persistence, and DTO mapping
- client/UI surfaces may keep compatibility naming, but canonical semantics remain anchored in the CDC references above

Application-facing semantic cleanup and compatibility debt are tracked in:

- [`docs/audits/application_semantic_audit.md`](../audits/application_semantic_audit.md)
- [`docs/audits/portfolio_flow_consumers_audit.md`](../audits/portfolio_flow_consumers_audit.md)
- [`docs/audits/portfolio_flow_application_migration.md`](../audits/portfolio_flow_application_migration.md)

When a new CDC slice document is added, extend this reference with links and ownership notes instead of duplicating full semantic descriptions here.
