# Forecasting Domain Model

Status: Canonical definition for Forecasting before CDC extraction  
Purpose: Define the canonical future-looking forecasting semantics, inputs, outputs, and boundaries before implementation of a dedicated Forecasting slice.

This document is the authoritative domain definition for Forecasting.

If current implementation differs from this document, the implementation is a deviation until explicitly reviewed.

---

## Domain Purpose

The Forecasting slice answers one question:

**Given canonical historical delivery facts and current remaining scope, what future delivery outcome should PoTool project?**

It covers:

- completion-date forecasting
- delivery-rate calibration for future planning
- confidence and distribution signals for future delivery
- explicit forecasting policies that consume canonical historical delivery facts

It does **not** cover:

- reconstruction of past sprint facts
- ownership of sprint analytics calculations
- ownership of delivery-trend calculations
- UI presentation or risk-label wording

Forecasting is future-oriented.

It consumes canonical analytics from adjacent slices and converts them into forward-looking projections.

---

## Canonical Domain Concepts

### DeliveryForecast

DeliveryForecast is the aggregate future-delivery result for one forecast request.

It combines:

- remaining forecast scope
- expected delivery rate
- completion projection
- forecast confidence
- optional forecast distribution bands

DeliveryForecast is the top-level concept returned by the Forecasting slice.

### VelocityCalibration

VelocityCalibration is the canonical calibration view over historical delivery throughput.

It captures:

- recent delivered story-point samples
- planning bands such as low/median/high throughput
- predictability signals derived from completed versus committed delivery
- optional diagnostic ratios such as hours per story point

VelocityCalibration does **not** predict a completion date by itself.

Its purpose is to provide calibrated future-delivery assumptions for forecasting policies.

### CompletionProjection

CompletionProjection is the deterministic or policy-based projection of when remaining scope is expected to complete.

It contains:

- projected completion date
- projected sprint count remaining
- projected remaining scope burn-down against expected delivery rate

CompletionProjection answers the date-oriented planning question.

### ForecastDistribution

ForecastDistribution is the probability-oriented or band-oriented view of future outcomes.

It represents bounded or percentile-style forecast ranges such as:

- conservative delivery rate
- expected delivery rate
- optimistic delivery rate
- low/high completion bands when a forecasting policy exposes them

ForecastDistribution exists to prevent Forecasting from collapsing every future-looking signal into one single point estimate.

---

## Canonical Inputs

Forecasting consumes already-defined canonical inputs.

### DeliveryTrendSeries

DeliveryTrendSeries is the historical sequence of canonical sprint delivery facts used to derive future expectations.

Typical contents include:

- delivered story points per sprint
- committed scope per sprint
- spillover or churn context when a policy needs predictability inputs

DeliveryTrendSeries belongs to DeliveryTrends or SprintAnalytics producers, not to Forecasting.

### SprintDeliverySummary

SprintDeliverySummary is one canonical historical sprint summary used by forecasting policies and calibration.

Typical values include:

- sprint window identity
- delivered story points
- committed story points
- removed scope
- delivered effort when diagnostic calibration is needed

Forecasting may sample or aggregate SprintDeliverySummary records, but it does not define how they are reconstructed.

### Remaining backlog scope

Remaining backlog scope is the current unfinished forecast scope expressed in canonical planning units.

For delivery forecasting, the canonical unit is:

- remaining story points for active Epic, Feature, or comparable scoped backlog

Derived estimates may participate in remaining forecast scope when the estimation rules allow them for aggregation and forecasting.

Forecasting does **not** redefine estimation rules or rollup precedence.

---

## Canonical Outputs

### ProjectedCompletionDate

ProjectedCompletionDate is the forecasted calendar date when remaining scope is expected to finish under the selected forecasting policy.

It is a completion-projection output, not a historical fact.

### ExpectedDeliveryRate

ExpectedDeliveryRate is the forecast throughput assumption used for future projection.

The canonical throughput unit is:

- story points delivered per sprint

Effort-hours remain diagnostic calibration data and must not replace story-point delivery as the primary forecasting throughput unit.

### ForecastConfidence

ForecastConfidence is the trust signal attached to a forecast result.

It expresses how reliable the projected outcome is under the chosen policy and available historical evidence.

ForecastConfidence is an output of Forecasting, even when the underlying confidence model is supplied by calibration or distribution logic.

---

## Relationships

The canonical relationship chain is:

1. DeliveryTrendSeries provides historical sprint delivery facts.
2. SprintDeliverySummary records are sampled from that history.
3. VelocityCalibration derives future-delivery assumptions from those summaries.
4. Remaining backlog scope defines what still needs to be delivered.
5. CompletionProjection combines remaining scope with the expected delivery rate.
6. DeliveryForecast packages the projection, confidence, and optional distribution outputs.
7. ForecastDistribution refines DeliveryForecast when the policy exposes multiple future-outcome bands instead of a single point estimate.

Scope and unit rules:

- Forecasting uses story-point delivery as the primary throughput signal.
- Forecasting may use effort-based signals only as calibration or diagnostics.
- Forecasting must keep current-scope semantics and historical-delivery semantics aligned with the canonical domain rules.

---

## Domain Boundaries

### What belongs inside Forecasting

- future-oriented forecasting concepts and aggregates
- completion-projection policies
- delivery-rate calibration policies
- confidence and distribution semantics for future delivery
- orchestration that combines remaining scope with historical delivery inputs

### What Forecasting consumes but does not own

- DeliveryTrends historical reconstruction
- SprintAnalytics calculations and sprint-window semantics
- canonical hierarchy rollups and remaining-scope calculations
- shared statistics helpers

### What belongs outside Forecasting

- historical sprint projection services
- backlog loading and filtering workflows
- state-mapping configuration
- UI components, DTO presentation choices, and endpoint transport compatibility

Forecasting must consume canonical facts from adjacent slices instead of re-implementing those calculations.

---

## Dependencies

Forecasting depends on:

- DeliveryTrends for historical delivery facts such as delivered story points, spillover, and committed scope history
- SprintAnalytics for sprint-window semantics, first-Done delivery attribution, and sprint-level summaries
- Statistics helpers for percentile, variance, standard deviation, and similar reusable math

Forecasting does **not** own those dependencies.

They remain authoritative source slices or shared helpers whose outputs Forecasting consumes to produce future-looking results.

---

## Final Boundary Statement

The clean boundary is:

- **DeliveryTrends** reconstructs historical delivery facts
- **SprintAnalytics** defines sprint-level historical semantics
- **Statistics helpers** provide reusable math primitives
- **Forecasting** consumes those inputs to produce DeliveryForecast, VelocityCalibration, CompletionProjection, and ForecastDistribution outputs

Forecasting is therefore a future-prediction slice layered on top of canonical historical analytics, not a replacement for those source calculations.
