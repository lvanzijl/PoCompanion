## What changed
- Added GET-length guard for reporting revisions requests.
- When URL exceeds threshold, client now uses POST to `/_apis/wit/reporting/workitemrevisions?api-version=...` with JSON body containing field list and paging parameter.
- Added test seam via constructor injection for field whitelist to synthesize long field lists in tests only.

## Why
- Large field lists can exceed practical URL limits; Azure DevOps/TFS supports POST variant for reporting revisions.
- Reference docs:
  - https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/reporting-work-item-revisions/read-reporting-revisions-post

## How tests validate correctness
- Test with oversized synthetic whitelist verifies POST method and expected JSON body keys.
- Control test verifies GET remains used for normal whitelist size.
