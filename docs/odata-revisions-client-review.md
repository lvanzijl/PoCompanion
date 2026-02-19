# OData Revisions Client Baseline Review

## Call paths into `RealODataRevisionTfsClient`

### `GetRevisionsAsync(...)`
- `PoTool.Api/Services/RevisionIngestionService.cs:1173`  
  Main ingestion loop calls `revisionSource.GetRevisionsAsync(window.StartUtc, continuationToken, allowedWorkItemIds, ...)`.

### `GetWorkItemRevisionsAsync(workItemId, ...)`
- `PoTool.Api/Services/RevisionIngestionService.cs:870`  
  Fallback ingestion path requests revisions per individual work item.
- `PoTool.Api/Services/RelationRevisionHydrator.cs:196`  
  Relation hydration requests full revision history for one work item.

## Current behavior (baseline)
- Query shape includes `$top`, `$select`, and `$orderby=ChangedDate asc,WorkItemId asc,Revision asc`.
- Optional filters include `ChangedDate ge ...` and server-side scope filter for `WorkItemId` range/id-list.
- Pagination primarily follows `@odata.nextLink`.
- If `@odata.nextLink` is missing on a full page, the client can synthesize seek-style continuation from the max tuple.

## Known flaws to address
- Filter literal formatting is not OData-safe: datetime values are currently emitted without single-quoted literal formatting.
- Paging is still brittle when tokens/pages stop advancing; no dedicated bounded "no-progress pages" control in OData options.
- `GetAllRevisionsForWorkItemAsync` still performs extra client-side filtering after requesting scoped pages.
- Scope strategy is implemented in URL filter construction but does not expose a dedicated scope-specific API entry point.

## Query samples captured from request logs/tests

Sample first-page query (decoded):

`https://analytics/WorkItemRevisions?$top=200&$filter=ChangedDate ge 2026-01-01T00:00:00.0000000+00:00 and WorkItemId ge 10 and WorkItemId le 11&$select=WorkItemId,Revision,ChangedDate,WorkItemType,Title,State,Reason,IterationPath,AreaPath,CreatedDate,ClosedDate,Effort,Tags,Severity,ChangedBy&$orderby=ChangedDate asc,WorkItemId asc,Revision asc`

Sample synthesized seek query (decoded):

`https://analytics/WorkItemRevisions?$top=2&$filter=ChangedDate ge 2026-01-01T00:00:00.0000000+00:00 and WorkItemId ge 42 and WorkItemId le 42 and (ChangedDate gt 2026-01-02T00:00:00.0000000+00:00 or (ChangedDate eq 2026-01-02T00:00:00.0000000+00:00 and WorkItemId gt 42) or (ChangedDate eq 2026-01-02T00:00:00.0000000+00:00 and WorkItemId eq 42 and Revision gt 2))&$select=WorkItemId,Revision,ChangedDate,WorkItemType,Title,State,Reason,IterationPath,AreaPath,CreatedDate,ClosedDate,Effort,Tags,Severity,ChangedBy&$orderby=ChangedDate asc,WorkItemId asc,Revision asc`
