## What changed
- Reporting page parsing now reads paging/completion from payload first (`isLastBatch`, `nextLink`, payload `continuationToken`) and only falls back to `x-ms-continuationtoken` header.
- Added continuation-token source tracking (`None`, `PayloadNextLink`, `PayloadContinuationToken`, `Header`) in page payload metadata.
- Completion logic now treats `isLastBatch=true` as terminal even if tokens are present.

## Why
- Azure DevOps/TFS reporting endpoint documents payload-driven paging fields and completion flags.
- Relying on undocumented headers as primary source can cause incorrect continuation handling.
- Reference docs:
  - https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/reporting-work-item-revisions/read-reporting-revisions-get

## How tests validate correctness
- Recorded payload tests assert precedence: `nextLink` token > payload `continuationToken` > header fallback.
- `isLastBatch=true` fixture verifies continuation token is suppressed.
