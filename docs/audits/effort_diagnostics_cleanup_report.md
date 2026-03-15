# Effort Diagnostics Cleanup Report

## Scope
This cleanup covered only the stable EffortDiagnostics subset:
- EffortImbalance
- EffortConcentration
- shared statistical helpers used directly by those two handlers

This cleanup intentionally excluded:
- EstimationQuality
- EstimationSuggestions
- SprintCapacityPlanning
- CapacityCalibration

## Semantic Fixes Applied
- Clarified `GetEffortImbalanceQuery` and related controller/XML comments so the public contract now states that imbalance uses deviation from mean effort with threshold-relative risk bands.
- Clarified that `DefaultCapacityPerIteration` adds sprint utilization context only and does not affect imbalance classification.
- Clarified `GetEffortConcentrationRiskQuery` and related controller/XML comments so the stable subset is described as fixed-band concentration analysis.
- Retained `ConcentrationThreshold` only as a backward-compatible compatibility parameter and documented that the stable subset ignores it.
- Normalized stable-subset wording from points/story points to effort hours or effort amount in handler descriptions, recommendation text, DTO summaries, and feature documentation.
- Clarified that the concentration index is derived from the full distribution while the returned risk lists remain focused on visible low-or-higher concentration buckets.

## Shared Helper Extraction
- Centralized stable-subset statistical calculations in `PoTool.Core/Metrics/EffortDiagnosticsStatistics.cs`.
- Shared helpers now cover:
  - deviation-from-mean calculation
  - share-of-total calculation
  - weighted imbalance score calculation
  - normalized HHI calculation

## Compatibility Notes
- `ConcentrationThreshold` remains in the public query/controller shape to avoid unnecessary contract breakage for existing callers.
- The stable subset no longer treats that parameter as active configuration; fixed 25/40/60/80 concentration bands are now the documented canonical behavior.
- Visible concentration risk rows still exclude `None` buckets, but the overall concentration index now uses the full area and iteration distributions for semantic honesty.

## Tests Updated
- Updated focused handler tests for stable assertion ordering and current semantics.
- Added a focused test confirming `DefaultCapacityPerIteration` changes sprint description context without changing imbalance classification.
- Added a focused test confirming `ConcentrationThreshold` does not change stable concentration classification.
- Tightened concentration-index coverage so well-distributed portfolios still produce a non-zero index derived from the full distribution.

## Remaining Deferred Areas
- EstimationQuality
- EstimationSuggestions
- SprintCapacityPlanning
- CapacityCalibration

## Final Assessment
The stable EffortDiagnostics subset is now semantically cleaner and internally consistent enough to support domain-model definition and later CDC extraction work, without pulling unstable effort families into scope.
