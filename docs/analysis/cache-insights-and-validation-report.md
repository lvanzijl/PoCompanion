# Cache Insights & Validation — Design Report

## 1. Current Cache Items / Types

The application caches the following entity types per ProductOwner (profile):

| Entity Type | Table | Scoped To |
|---|---|---|
| Work Items | `WorkItems` | Global (all profiles) |
| Revision Headers | `RevisionHeaders` | Global |
| Revision Field Deltas | `RevisionFieldDeltas` | Per RevisionHeader (FK) |
| Revision Relation Deltas | `RevisionRelationDeltas` | Per RevisionHeader (FK) |
| Pull Requests | `PullRequests` | Per Product (→ ProductOwner) |
| PR Iterations | `PullRequestIterations` | Per PullRequest (FK) |
| PR Comments | `PullRequestComments` | Per PullRequest (FK) |
| PR File Changes | `PullRequestFileChanges` | Per PullRequest (FK) |
| Pipeline Runs | `CachedPipelineRuns` | Per ProductOwner |
| Cached Metrics | `CachedMetrics` | Per ProductOwner |
| Cached Validation Results | `CachedValidationResults` | Global |
| Sprint Metrics Projections | `SprintMetricsProjections` | Per Product |
| Resolved Work Items | `ResolvedWorkItems` | Per Product |
| Work Item Relationship Edges | `WorkItemRelationshipEdges` | Per ProductOwner |
| Sprints | `Sprints` | Global |
| Revision Ingestion Watermarks | `RevisionIngestionWatermarks` | Per ProductOwner |

All data is stored in SQLite (default) or SQL Server via EF Core (`PoToolDbContext`).

## 2. How Cache Reset Works Today

- **API Endpoint:** `DELETE api/CacheSync/{productOwnerId}` (`CacheSyncController.DeleteCache`)
- **Repository:** `CacheStateRepository.ResetCacheStateAsync(productOwnerId)`
- **Behavior:** Deletes ALL cached entities for the ProductOwner in a single operation. Resets `ProductOwnerCacheStateEntity` to `Idle` with null watermarks/timestamps.
- **Guard:** Cannot reset while sync is running (409 Conflict).
- **No granularity:** There is no per-type reset; it's all-or-nothing.

## 3. Revision Field Whitelist

**Location:** `PoTool.Integrations.Tfs/Clients/RealRevisionTfsClient.cs` (lines 56–72)

```csharp
private static readonly string[] FieldWhitelist = new[]
{
    "System.Id", "System.WorkItemType", "System.Title", "System.State",
    "System.Reason", "System.IterationPath", "System.AreaPath",
    "System.CreatedDate", "System.ChangedDate", "System.ChangedBy",
    "Microsoft.VSTS.Common.ClosedDate", "Microsoft.VSTS.Scheduling.Effort",
    "System.Tags", "Microsoft.VSTS.Common.Severity"
};
```

This whitelist is currently **private** inside the TFS client. It controls which fields are requested from the TFS reporting API and stored in revision headers/deltas.

**Refactoring:** The whitelist is extracted to `PoTool.Core/RevisionFieldWhitelist.cs` as a public static class, making it the single source of truth. Both ingestion and validation code reference it.

## 4. Design

### A. Cache Insights
- **Backend:** `GET api/CacheSync/{productOwnerId}/insights` returns `CacheInsightsDto` with counts per entity type, grouped by product where applicable, plus health metadata (last sync, status, error).
- **Implementation:** Server-side aggregation queries via `CacheStateRepository` extension methods.
- **UI:** A "Cache Insights" card on the Settings page showing counts in a table.

### B. Granular Cache Reset
- **Backend:** `POST api/CacheSync/{productOwnerId}/reset` with `CacheResetRequest` body specifying which entity types to clear.
- **Implementation:** Extended `CacheStateRepository` with per-type deletion methods.
- **UI:** Checkbox list of entity types + "Reset Selected" + "Reset All" buttons with confirmation dialog.

### C. Revision Cache Validation
- **Backend Service:** `RevisionCacheValidationService` with:
  - `ValidateWorkItemAsync(workItemId)` — replays revisions, fetches REST state, diffs
  - `ValidateSampleAsync(sampleSize, mode)` — validates random/recent items
- **Backend Endpoint:** `POST api/CacheSync/{productOwnerId}/validate`
- **Shared Whitelist:** Uses `RevisionFieldWhitelist` from `PoTool.Core`
- **Normalization:** Null/missing fields → canonical empty string for comparison
- **UI:** Input for work item ID, sample controls, results table with pass/fail and diff view, JSON export
