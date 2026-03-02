# Sprint Scoping — Limitations and Current Implementation

## Current Implementation: Time-Window Scoping

Sprint scoping for **Pull Request Insights** and **Pipeline Insights** uses
**time-window overlap** only:

| Dataset | Inclusion rule |
|---|---|
| Pull Requests | PR `createdDate` or `completedDate` falls within the sprint start/end window |
| Pipeline Runs | Run `startedDate` or `finishedDate` falls within the sprint start/end window |

This is deterministic, requires no external lookups, and works entirely from
the cached data.

### How it works

- The `GetFilteredPullRequestsQuery` handler accepts `FromDate` / `ToDate`
  parameters. Setting these to the sprint start and end dates applies
  time-window scoping.
- The `GetPipelineMetricsQueryHandler` applies a 6-month rolling window by
  default. To scope by sprint, callers can set start-time filters.

## Linkage-Based Scoping — Not Currently Available

A deeper scoping approach would link artifacts to sprints through work-item
relationships:

- **PR → Work Item**: include PRs linked to work items assigned to the sprint.
- **Pipeline Run → Commit → Work Item**: include runs whose commits resolve
  work items in the sprint.

### Why this is not implemented

1. The repository does **not** currently store PR ↔ work-item link data in the
   local cache.
2. Pipeline run ↔ commit ↔ work-item resolution chains are not persisted.
3. Fetching these links at query time would require live TFS access, which
   defeats the purpose of cached/offline operation.

### Prerequisites for future implementation

- Ingest and persist PR ↔ work-item links during the `PullRequestSyncStage`.
- Ingest and persist commit ↔ work-item links during the `PipelineSyncStage`
  or a dedicated stage.
- Add query-time joins so that the handlers can filter by sprint iteration
  path through the link chain.

Until these data links are available, **time-window scoping is the only
reliable approach** for sprint-based filtering of PR and Pipeline Insights.
