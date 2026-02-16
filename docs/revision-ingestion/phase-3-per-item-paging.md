## What changed
- Added `$top`/`$skip` support to per-work-item revisions URL builder.
- Implemented paged loop for per-item revisions retrieval with cumulative append and skip advancement.
- Added loop-safety guard for non-advancing successful empty pages.
- Preserved cross-page delta continuity by carrying previous field snapshot across page boundaries.

## Why
- Per-item revisions endpoint is paged and cannot be assumed to return all revisions in one response.
- Reference docs:
  - https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/revisions/list

## How tests validate correctness
- Recorded page1/page2 fixtures validate total count and revision ordering across pages.
- Boundary delta test validates revision N to N+1 delta continuity across pages.
- Empty non-advancing page test validates loop termination protection.
