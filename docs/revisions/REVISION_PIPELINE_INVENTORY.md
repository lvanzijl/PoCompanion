# Revision Pipeline Inventory (Phase 0)

## Old approach components (non-OData)

- `PoTool.Core/Contracts/IRevisionTfsClient.cs` (legacy REST reporting revision contract)
- `PoTool.Integrations.Tfs/Clients/RealRevisionTfsClient.cs` (calls `/_apis/wit/reporting/workitemrevisions` and per-item revisions)
- `PoTool.Integrations.Tfs/Clients/RestReportingRevisionSource.cs` (adapts legacy REST client to `IWorkItemRevisionSource`)
- `PoTool.Integrations.Tfs/Clients/WorkItemRevisionSourceSelector.cs` (runtime strategy selection between REST and OData)
- `PoTool.Shared/Settings/RevisionSource.cs` + `TfsConfigEntity.RevisionSource` + `ProfileEntity.RevisionSourceOverride` (strategy configuration and override)
- DI registrations in `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` for:
  - `IRevisionTfsClient -> RealRevisionTfsClient`
  - `IWorkItemRevisionSource -> RestReportingRevisionSource`
  - `IWorkItemRevisionSourceSelector -> WorkItemRevisionSourceSelector`
- UI/config strategy selector in `PoTool.Client/Components/Onboarding/OnboardingWizard.razor`

## OData approach components

- `PoTool.Core/Contracts/IWorkItemRevisionSource.cs` (revision source abstraction)
- `PoTool.Integrations.Tfs/Clients/RealODataRevisionTfsClient.cs` (Analytics OData `WorkItemRevisions` retrieval)
- `PoTool.Integrations.Tfs/Clients/ODataRevisionQueryBuilder.cs` (OData query construction)
- `PoTool.Core/RevisionFieldWhitelist.cs` (field mapping for OData selection/parsing)
- `PoTool.Api/Services/RevisionIngestionService.cs` (ingests revisions through `IWorkItemRevisionSource`)
- `PoTool.Api/Services/Sync/RevisionSyncStage.cs` (sync pipeline stage invoking ingestion)

## Consumers (feature/service -> revision usage)

### Runtime consumers of stored revisions

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
  - Reads ingested revision tables (`RevisionHeaders`, `RevisionFieldDeltas`) for sprint trend projections.
- `PoTool.Api/Services/WorkItemResolutionService.cs`
  - Reads latest revision snapshots for resolved/hierarchical work item state.
- `PoTool.Api/Services/CacheManagementService.cs`
  - Reads revision headers/deltas to build revision timelines and replayed state.
- `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs`
  - Serves revision history queries from persisted revision entities.
- `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs`
  - Uses persisted revision data for state timeline responses.
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
  - Uses projected metrics derived from revision-ingested data.

### Runtime entry points that fetch revisions from upstream

- `PoTool.Api/Services/RevisionIngestionService.cs`
  - Primary upstream retrieval flow.
  - Current implementation resolves source via `IWorkItemRevisionSourceSelector` and then pages revisions.
- `PoTool.Api/Services/RelationRevisionHydrator.cs`
  - Resolves source via `IWorkItemRevisionSourceSelector` and calls per-work-item revision retrieval.
- `PoTool.Tools.TfsRetrievalValidator/Program.cs`
  - Resolves `IWorkItemRevisionSource` directly and validates ingestion retrieval behavior.
