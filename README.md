# PO Tool

A Product Owner companion tool for managing work items (Epics, Features, PBIs) from Azure DevOps/TFS.

## Architecture

This solution follows a three-layer architecture as defined in `docs/ARCHITECTURE_RULES.md`:

### Projects

- **PoTool.Core** - Domain models, DTOs, and interfaces (infrastructure-free)
- **PoTool.Api** - ASP.NET Core Web API with EF Core persistence and SignalR
- **PoTool.Client** - Blazor WebAssembly frontend
- **PoTool.App** - Shell application (console app placeholder, MAUI planned)
- **PoTool.Tests.Unit** - MSTest unit tests

### Key Components

#### Core Layer
- `WorkItemDto` - Immutable DTO for work items
- `ITfsClient` - Interface for TFS/Azure DevOps integration
- `IWorkItemRepository` - Repository interface for work item persistence

#### API Layer
- `WorkItemsController` - REST API endpoints for work items
- `WorkItemSyncService` - Background service for syncing work items from TFS
- `WorkItemHub` - SignalR hub for real-time updates
- `PoToolDbContext` - EF Core database context
- `WorkItemRepository` - Repository implementation with SQLite persistence

#### Client Layer
- `WorkItemExplorer` - Blazor component for viewing work items
- `WorkItemService` - Service for API communication

## Getting Started

### Prerequisites
- .NET 10 SDK

### Build
```bash
dotnet build
```

### Run API
```bash
cd PoTool.Api
dotnet run
```

The API will be available at `http://localhost:5000` with:
- OpenAPI documentation at `/openapi/v1.json`
- Health endpoint at `/health`
- SignalR hub at `/hubs/workitems`

### Run Client
```bash
cd PoTool.Client
dotnet run
```

The Blazor client will be available at `https://localhost:5001`.

## Database

The application uses SQLite for local caching:
- Database file: `potool.db` (created automatically)
- Migrations: Auto-created on startup (EnsureCreated)

## Work Item Tree Feature

The Work Item Tree feature (as described in `features/Simple_workitem_explorer.md`) provides:

1. **Hierarchical View** - Display Epics → Features → PBIs
2. **Local Caching** - SQLite-based caching for offline access
3. **Pull & Cache** - Manual sync button to retrieve work items from TFS
4. **Search & Filter** - Filter work items by title
5. **Real-time Updates** - SignalR notifications for sync status

### Current Implementation Status

This PR provides the **scaffolding baseline**:
- ✅ Core contracts and DTOs
- ✅ API controllers and endpoints
- ✅ Background service stub
- ✅ EF Core entities and DbContext
- ✅ Repository interface and implementation
- ✅ SignalR hub
- ✅ Blazor client components (basic)

### Future Enhancements
- Implement `ITfsClient` with Azure DevOps REST API
- Add hierarchical tree rendering (parent-child relationships)
- Implement text highlighting for search matches
- Add configuration dialog for Area Path and PAT
- Add PAT encryption and secure storage
- Implement proper error handling and loading states

## Architecture Compliance

This implementation follows:
- ✅ `docs/ARCHITECTURE_RULES.md` - Three-layer separation, DI, EF Core, SignalR
- ✅ `docs/ux-principles.md` - Clean UI, overview→detail pattern, minimal design

## License

TBD
