# EffortDiagnostics Domain Model

Status: Canonical definition for the stable EffortDiagnostics subset  
Purpose: Define the canonical domain semantics for effort imbalance, effort concentration, and shared statistics before CDC extraction.

This document is the authoritative domain definition for the stable EffortDiagnostics subset.

If current implementation differs from this document, the implementation is a deviation until explicitly reviewed.

---

## Domain Purpose

The EffortDiagnostics stable subset answers one question:

**How is observed effort distributed across area-path and iteration-path buckets, and what risk follows from that distribution?**

Stable scope only:

- EffortImbalance
- EffortConcentration
- EffortDiagnosticsStatistics

Out of scope for this subset:

- EstimationQuality
- EstimationSuggestions
- SprintCapacityPlanning
- CapacityCalibration
- recommendation wording and API display strings

Canonical inputs:

- already-loaded work items
- observed effort values greater than zero
- area-path bucket selection chosen by the application layer
- iteration-path bucket selection chosen by the application layer

The subset is distribution-oriented.

It does **not** define:

- product loading
- filtering workflows
- work-item recommendation text
- DTO shape or controller contracts

---

## EffortImbalance

### Definition

EffortImbalance is the **distribution imbalance of observed effort across buckets**.

Canonical bucket families:

- area paths
- iteration paths

The domain measures imbalance as:

`DeviationFromMean = abs(BucketEffort - MeanEffort) / MeanEffort`

Where:

- `BucketEffort` is the observed effort in one bucket
- `MeanEffort` is the arithmetic mean across peer buckets of the same analysis

### Concepts

- **EffortImbalanceAnalysis**  
  The aggregate result for one stable-slice analysis. It contains:
  - area-path buckets
  - iteration-path buckets
  - overall imbalance risk level
  - weighted imbalance score

- **EffortImbalanceBucket**  
  One canonical bucket with:
  - bucket key
  - observed effort
  - mean effort
  - deviation from mean
  - imbalance risk level

- **MeanEffort**  
  Arithmetic mean across peer buckets.

- **DeviationFromMean**  
  Absolute relative deviation from that mean.

- **ImbalanceScore**  
  Weighted portfolio score:

  `ImbalanceScore = (MaxDeviation * 0.6 + AverageDeviation * 0.4) * 100`

  This score is diagnostic only.

### Risk classification rules

Bucket risk uses the caller-supplied imbalance threshold as the canonical base band.

If `Threshold = 0.3`, the default stable bands are:

- `Low` when deviation `< 0.3`
- `Medium` when deviation `>= 0.3` and `< 0.45`
- `High` when deviation `>= 0.45` and `< 0.75`
- `Critical` when deviation `>= 0.75`

General threshold-relative rule:

- `Low` when deviation `< threshold`
- `Medium` when deviation `< threshold * 1.5`
- `High` when deviation `< threshold * 2.5`
- `Critical` otherwise

Overall imbalance risk uses the maximum observed deviation with the stable overall bands:

- `Low` `< 30%`
- `Medium` `30% - <50%`
- `High` `50% - <80%`
- `Critical` `>= 80%`

Capacity values may be attached later as explanatory context, but capacity does **not** participate in the imbalance formula or risk classification.

---

## EffortConcentration

### Definition

EffortConcentration is the **share-of-total effort concentration across buckets**.

Canonical bucket families:

- area paths
- iteration paths

The domain measures concentration as:

`EffortShare = BucketEffort / TotalEffort`

### Concepts

- **EffortConcentrationAnalysis**  
  The aggregate result for one stable-slice analysis. It contains:
  - area-path buckets
  - iteration-path buckets
  - overall concentration risk level
  - normalized concentration index

- **EffortConcentrationBucket**  
  One canonical bucket with:
  - bucket key
  - effort amount
  - effort share
  - concentration risk level

- **EffortShare**  
  The share of total observed effort carried by one bucket.

- **ConcentrationIndex**  
  Normalized HHI:

  `HHI = Σ(share²)`

  `ConcentrationIndex = min(100, HHI * 100)`

  The concentration index is calculated from the **full bucket distribution**, even when a consumer chooses to display only visible low-or-higher risk buckets.

### Risk classification rules

Bucket and overall concentration risk use fixed stable bands:

- `None` `< 25%`
- `Low` `25% - <40%`
- `Medium` `40% - <60%`
- `High` `60% - <80%`
- `Critical` `>= 80%`

The backward-compatible `ConcentrationThreshold` query parameter is outside this domain model.

It is an application/API compatibility concern and does not alter the canonical stable-slice concentration semantics.

---

## Shared Statistical Model

The stable subset uses shared mathematical primitives only.

### Mean

Arithmetic average across supplied values.

### Deviation

Absolute relative deviation from the mean:

`abs(value - mean) / mean`

### Share-of-total

Relative contribution of one value to a total:

`value / total`

### Median

Middle ordered value, or the average of the two middle values when the sample count is even.

### Variance

Population variance over the supplied values.

### CoefficientOfVariation

Relative spread:

`sqrt(variance) / mean`

### HHI

Herfindahl-Hirschman Index over normalized shares:

`Σ(share²)`

The domain statistics contract exposes mathematical operations only.

It must not depend on:

- repositories
- work item entities
- DTOs
- filters
- orchestration services

---

## Domain Boundaries

### What belongs inside EffortDiagnostics

- effort bucket concepts
- mean, median, variance, coefficient of variation, share-of-total, and HHI
- deviation-from-mean calculation
- threshold-relative imbalance classification
- fixed-band concentration classification
- weighted imbalance score computation
- normalized concentration-index computation
- canonical analysis aggregates for area-path and iteration-path distributions

### What belongs outside (application layer)

- loading work items
- resolving products and backlog roots
- filtering by area path
- selecting recent iterations
- deciding which buckets are visible in API responses
- DTO shaping
- recommendation titles, descriptions, and UI wording
- compatibility query parameters that do not affect canonical stable-slice semantics

The application layer may prepare inputs and shape outputs.

The domain layer owns the mathematical meaning of the stable subset.

---

## Extraction Readiness

The stable EffortDiagnostics subset is **ready for CDC extraction**.

Reason:

- the canonical concepts are explicit
- statistical primitives are isolated from orchestration
- risk classification rules are defined independently from handlers and DTOs
- unstable effort families remain outside the slice

Remaining non-domain work for future extraction is operational only:

- moving handlers to consume the domain contracts
- extracting application-side bucket selection and DTO mapping
- separating recommendation decision rules from recommendation phrasing where needed
