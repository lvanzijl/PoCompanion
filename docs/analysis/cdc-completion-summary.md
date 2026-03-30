# CDC Completion Summary

_Generated: 2026-03-16_

## Completed Slices

The completed Canonical Domain Core now consists of these stable slices:

- Core Concepts
  - shared hierarchy, estimation, state, sprint, propagation, and source-truth semantics from `docs/architecture/domain-model.md` and `docs/domain/rules/*.md`
- BacklogQuality
  - current-state backlog validation and readiness semantics documented in `docs/analysis/backlog-quality-cdc-summary.md`
- SprintCommitment
  - commitment, scope change, completion, and spillover semantics documented in `docs/architecture/sprint-commitment-domain-model.md`, `docs/architecture/sprint-commitment-cdc-summary.md`, and `docs/analysis/sprint-commitment-application-alignment.md`
- DeliveryTrends
  - historical delivery-trend semantics documented in `docs/analysis/delivery-trend-analytics-cdc-summary.md`
- Forecasting
  - forecast, calibration, and completion projection semantics documented in `docs/architecture/forecasting-domain-model.md` and `docs/analysis/forecasting-cdc-summary.md`
- EffortDiagnostics
  - stable effort-diagnostics formulas documented in `docs/analysis/effort-diagnostics-cdc-extraction-report.md`
- PortfolioFlow
  - story-point stock, inflow, throughput, and remaining scope semantics documented in `docs/architecture/portfolio-flow-model.md`, `docs/analysis/portfolio-flow-projection.md`, and `docs/analysis/portfolio-flow-projection-validation.md`
- Shared Statistics
  - reusable pure-math contracts documented in `docs/analysis/statistical-core-cleanup-report.md`

Overall CDC status:

- the semantic slices are established
- cross-slice dependency direction is established
- the CDC can now be referenced as one stable semantic core rather than as isolated extraction reports

## Application Alignment Status

Application alignment is substantially complete for the finished CDC slices.

Confirmed alignment points:

- SprintCommitment is the application-facing gateway for commitment, completion, and spillover reconstruction according to `docs/analysis/sprint-commitment-application-alignment.md` and `docs/analysis/cdc-coverage-audit.md`
- DeliveryTrends consumes SprintCommitment outputs rather than re-creating commitment semantics
- Forecasting consumes historical delivery outputs rather than owning sprint-history replay
- BacklogQuality and EffortDiagnostics keep handlers focused on loading, orchestration, filtering, and DTO mapping
- PortfolioFlow has canonical projection materialization in place even where some consumers still preserve compatibility-oriented transport shapes

Remaining application-side work is structural rather than semantic:

- continue migrating compatibility surfaces toward direct CDC-backed contracts
- keep newer handlers from reintroducing slice-local semantic reimplementation

## UI Semantic Alignment Status

UI semantic alignment is good enough for CDC stability, but compatibility naming remains visible at transport and presentation seams.

Current status:

- the CDC vocabulary now consistently uses story points, effort hours, stock, inflow, throughput, remaining scope, commitment, spillover, delivery trend, and forecast
- UI and transport compatibility layers still preserve older naming where current clients expect it
- `docs/analysis/application-semantic-audit.md` and `docs/analysis/portfolio-flow-consumers-audit.md` document the remaining semantic mismatch at the application and presentation boundaries

Examples of remaining UI or transport compatibility debt:

- legacy `*Effort` DTO names for story-point semantics
- backlog-quality `RC-2` and `EFF` aliasing
- portfolio pages and handlers that still preserve older response shapes while canonical stock and throughput semantics are already story-point based

## Projection Status

Projection work is far enough along to support the stable CDC.

Current projection status:

- sprint metrics projections are aligned behind the CDC sprint services
- PortfolioFlow has canonical story-point projection materialization and projection validation documented in `docs/analysis/portfolio-flow-projection.md` and `docs/analysis/portfolio-flow-projection-validation.md`
- projection documents now act as persistence and consumer references, not as replacements for semantic ownership

What still remains:

- some application consumers still read compatibility-oriented projection surfaces or preserve legacy field shapes
- later migration can simplify those consumers once CDC-backed contracts are adopted end to end

## Remaining Structural Work

The main remaining work is no longer semantic extraction. It is structural cleanup around the stabilized CDC.

Remaining structural work:

- reduce compatibility-only transport aliases where clients can adopt canonical naming
- continue application consumer migration to CDC-backed projection entities
- finish minor adapter cleanup for BacklogQuality alias and category inference seams
- optionally tighten documentation and tests around remaining compatibility layers as those migrations complete

No additional slice redesign is required to call the CDC complete at the semantic level.

## Recommended Next Phase

Recommended next phase:

- treat the CDC as the stable semantic core
- use it to drive persistence abstraction decisions
- use it to support optional hexagonal architecture if that direction is chosen
- use it to plan later API contract cleanup without re-opening slice semantics

The CDC is now stable enough to support:

- persistence abstraction
- optional hexagonal architecture
- later API contract cleanup
