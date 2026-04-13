# TFS Verification Sampling Fix

## Original Behavior
- Analytics field verification sampled a single work item payload from the WIQL result set.
- The payload check treated missing analytics fields on that one item as a hard verification failure.
- The sampling query ordered by work item ID, which could select an old item that never populated the analytics fields.

## Root Cause
- Verification conflated field metadata existence with field presence on one sampled payload.
- Older or non-applicable work items often omit analytics fields even when the fields exist in TFS metadata.
- That made the verification report produce false negatives for otherwise valid configurations.

## New Sampling Strategy
- Verification now builds a hardened WIQL query for recent work items using:
  - `SELECT [System.Id]`
  - `FROM WorkItems`
  - `WHERE [System.TeamProject] = <project>`
  - `AND [System.State] <> 'Removed'`
  - `AND [System.AreaPath] UNDER <configured area path>` when configured
  - `ORDER BY [System.ChangedDate] DESC`
- The verifier takes up to 5 recent IDs client-side.
- If the area-scoped recent sample is empty, verification retries once without the area-path filter and logs the fallback.
- Payload retrieval continues to use the existing `workitemsbatch` endpoint.

## Validation Semantics
- Metadata validation still uses the TFS fields endpoint and fails when required fields do not exist.
- Payload validation now classifies each analytics field across sampled items as:
  - present
  - absent
  - empty
- Payload absence or emptiness is reported diagnostically and no longer fails the verification when metadata exists.
- Verification fails only when required metadata is missing or the payload retrieval path cannot produce a usable sampled payload.

## Error Reporting Improvements
- The work-item-fields capability output now reports:
  - sampled work item IDs
  - sampled work item types when available
  - per-field metadata status
  - per-field payload counts for present, absent, and empty states
  - interpretation text that explains likely causes
- Fallback parsing for alternate `workitemsbatch` response shapes is logged explicitly and preserved in diagnostics.

## Edge Case Handling
- Empty recent-sample results no longer fail verification.
- Area-scoped empty samples retry once with a broader project-wide recent query.
- If no eligible work items are available after sampling, verification returns a success result with diagnostics instead of a hard failure.
- Partial payload responses are normalized so sampled IDs still produce stable diagnostics without crashes.

## Test Coverage
- Added coverage for:
  - multi-item recent sampling diagnostics
  - metadata-missing failure behavior
  - payload-missing diagnostic success behavior
  - empty-value payload diagnostics
  - no-work-item handling
  - area-scope fallback sampling
  - fallback payload-shape parsing
- Updated WIQL regression coverage to assert the new recent-sampling query shape while preserving hardened query construction.

## Result
- Analytics field verification no longer depends on a single legacy work item sample.
- Metadata existence is distinguished from sampled payload presence.
- Missing payload values on sampled items now surface as diagnostics instead of false verification failures.
