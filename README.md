# PO Tool

A Product Owner companion tool for managing work items (Goal → Objective → Epic → Feature → PBI → Task) from Azure DevOps/TFS.

## Architecture

This solution follows a three-layer architecture as defined in `docs/ARCHITECTURE_RULES.md`:

### Projects

- **PoTool.Core** - Domain models, DTOs, and interfaces (infrastructure-free)
- **PoTool.Api** - ASP.NET Core Web API with EF Core persistence and SignalR
- **PoTool.Client** - Blazor Hybrid frontend (Razor class library)
- **PoTool.Tests.Unit** - MSTest unit tests
- **PoTool.Tests.Integration** - Reqnroll integration tests
- **PoTool.Tests.Blazor** - bUnit Blazor component tests

### Key Components

#### Core Layer
- `WorkItemDto` - Immutable DTO for work items
- `ITfsClient` - Interface for TFS/Azure DevOps integration
- `IWorkItemRepository` - Repository interface for work item persistence

#### API Layer
- `WorkItemsController` - REST API endpoints for work items
- `WorkItemSyncService` - Background service for syncing work items from TFS
- `CacheSyncHub` - SignalR hub for cache sync progress updates
- `PoToolDbContext` - EF Core database context
- `WorkItemRepository` - Repository implementation with SQLite persistence

#### Client Layer
- `WorkItemExplorer` - Blazor component for viewing work items
- `WorkItemService` - Service for API communication

## Getting Started

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022 (version 17.8 or later) or VS Code
- ASP.NET and web development workload (for Visual Studio)

Quick start:
1. Open `PoTool.sln` in Visual Studio 2022
2. Set **PoTool.Api** as the startup project
3. Press **F5** to run
4. Open browser to `http://localhost:5291`

### Running from Command Line

#### Build
```bash
dotnet build
```

#### Run Application
```bash
cd PoTool.Api
dotnet run
```

The API will be available at `http://localhost:5291` with:
- **Main Application**: `http://localhost:5291`
- **OpenAPI documentation**: `http://localhost:5291/swagger` (development only)
- **Health endpoint**: `http://localhost:5291/health`
- **SignalR hub (cache sync)**: `http://localhost:5291/hubs/cachesync`

## Database

The application uses SQLite for local caching:
- Database file: `potool.db` (created automatically)
- Migrations: Auto-created on startup (EnsureCreated)

## Work Item Tree Feature

The Work Item Tree feature (as described in `features/Simple_workitem_explorer.md`) provides:

1. **Hierarchical View** - Display Goal → Objective → Epic → Feature → PBI → Task
2. **Local Caching** - SQLite-based caching for offline access
3. **Pull & Cache** - Manual sync button to retrieve work items from TFS
4. **Search & Filter** - Filter work items by title
5. **Cache Sync Progress** - SignalR notifications for cache synchronization status

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
- Add configuration dialog for Area Path
- Implement proper error handling and loading states

## Documentation

- **[User Manual](docs/USER_MANUAL.md)** - Complete guide for Product Owners with use cases and scenarios
- **[Dutch Manual](docs/GEBRUIKERSHANDLEIDING.md)** - Nederlandse gebruikershandleiding

## Architecture Compliance

This implementation follows:
- ✅ `docs/ARCHITECTURE_RULES.md` - Three-layer separation, DI, EF Core, SignalR
- ✅ `docs/ux-principles.md` - Clean UI, overview→detail pattern, minimal design

## License

TBD
