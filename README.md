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
- `WorkItemHub` - SignalR hub for real-time updates
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

### Running the Application

The API and Client now run as separate processes for better separation of concerns.

#### Option 1: Two Terminal Development (Recommended)

**Terminal 1 - Start the API:**
```bash
cd PoTool.Api
dotnet run
```

**Terminal 2 - Start the Client:**
```bash
cd PoTool.Client
dotnet run
```

Then open your browser to `http://localhost:5292` (Client) which communicates with the API at `http://localhost:5291`.

#### Option 2: Visual Studio Multi-Project Startup

1. Open `PoTool.sln` in Visual Studio 2022
2. Right-click the Solution → **Configure Startup Projects**
3. Select **Multiple startup projects**
4. Set both **PoTool.Api** and **PoTool.Client** to "Start"
5. Press **F5** to run

### API Endpoints

The API is available at `http://localhost:5291` with:
- **OpenAPI documentation**: `http://localhost:5291/swagger` (development only)
- **Health endpoint**: `http://localhost:5291/health`
- **SignalR hub**: `http://localhost:5291/hubs/workitems`

### Building from Command Line

```bash
dotnet build
```

## Database

The application uses SQLite for local caching:
- Database file: `potool.db` (created automatically)
- Migrations: Auto-created on startup (EnsureCreated)

## API Client Generation

The Blazor client uses a strongly-typed C# client generated from the API's OpenAPI specification.

### Regenerating the API Client

When you make changes to API controllers or DTOs, regenerate the client:

```powershell
# 1. Generate fresh OpenAPI specification
.\tools\generate-openapi.ps1

# 2. Regenerate C# client
cd PoTool.Client
dotnet nswag run nswag.json
cd ..

# 3. Build and test
dotnet build
dotnet test
```

For detailed documentation, see:
- [`docs/dev/OPENAPI.md`](docs/dev/OPENAPI.md) - OpenAPI generation
- [`docs/dev/NSWAG.md`](docs/dev/NSWAG.md) - NSwag client generation

## Work Item Tree Feature

The Work Item Tree feature (as described in `features/Simple_workitem_explorer.md`) provides:

1. **Hierarchical View** - Display Goal → Objective → Epic → Feature → PBI → Task
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
- Add configuration dialog for Area Path
- Implement proper error handling and loading states

## Architecture Compliance

This implementation follows:
- ✅ `docs/ARCHITECTURE_RULES.md` - Three-layer separation, DI, EF Core, SignalR
- ✅ `docs/ux-principles.md` - Clean UI, overview→detail pattern, minimal design

## License

TBD
