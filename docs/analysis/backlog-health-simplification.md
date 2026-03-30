> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Backlog Health Simplification

_Generated: 2026-03-16_

## Removed Compatibility Wrapper

- `GetBacklogHealthQueryHandler` no longer depends on `IHierarchicalWorkItemValidator`.
- `GetMultiIterationBacklogHealthQueryHandler` no longer depends on `IHierarchicalWorkItemValidator`.
- Both handlers now consume `IBacklogQualityAnalysisService`, which evaluates the canonical `BacklogQualityAnalyzer` over the current iteration snapshot set.

## CDC BacklogQuality Usage

- The new seam is `IBacklogQualityAnalysisService.AnalyzeAsync(...)`.
- The service resolves configured canonical state classifications, builds a `BacklogGraph`, and returns the canonical `BacklogQualityAnalysisResult`.
- Handler mapping now reads:
  - `IntegrityFindings` for parent-progress issue counts
  - `Findings` grouped by canonical rule family for refinement summaries
  - direct `BacklogQualityAnalysisResult` output instead of legacy tree-shaped validation wrapper results

## Handler Simplifications

- `GetBacklogHealthQueryHandler`
  - removed the compatibility-wrapper call and local interpretation of hierarchical validation result buckets
  - now loads work items, calls `IBacklogQualityAnalysisService`, and maps the result through `BacklogHealthDtoFactory`
- `GetMultiIterationBacklogHealthQueryHandler`
  - removed the compatibility-wrapper call from `CalculateIterationHealth(...)`
  - now delegates real sprint slots to `IBacklogQualityAnalysisService` and reuses `BacklogHealthDtoFactory`
- `BacklogHealthDtoFactory` now centralizes the shared DTO mapping so both handlers project the same canonical backlog-quality findings
- Placeholder sprint slots, blocked-state counts, in-progress-at-end counts, and trend narration remain handler-side because they are dashboard shaping rather than canonical backlog-quality rule ownership

## Test Updates

- Updated handler tests now mock `IBacklogQualityAnalysisService` instead of `IHierarchicalWorkItemValidator`
- Added `BacklogQualityAnalysisServiceTests` to verify configured state classifications flow into canonical backlog-quality findings
- Updated DI coverage to assert `IBacklogQualityAnalysisService` registration
- Added `BacklogHealthSimplificationDocumentTests` to guard this report

## Lines of Code Removed

- Compatibility-wrapper-specific handler code removed from existing tracked files: **446 lines**
- New shared seams and documentation added across service, mapper, tests, and audits: **298 lines**
- Net effect: the wrapper-specific handler branches were replaced with one reusable `IBacklogQualityAnalysisService` seam plus one shared `BacklogHealthDtoFactory`
