# PO Companion

A Product Owner companion tool for managing Azure DevOps work items with local caching and hierarchical visualization.

## Architecture

This solution follows a 4-layer architecture as defined in [docs/ARCHITECTURE_RULES.md](docs/ARCHITECTURE_RULES.md):

### Core
Domain models, DTOs, and contracts. Infrastructure-free and fully testable.
- **WorkItems**: DTOs for work item data
- **Contracts**: Interfaces for TFS client and repositories

### Api
ASP.NET Core Web API with SignalR, EF Core, and background services.
- **Controllers**: REST API endpoints
- **Data**: EF Core entities, DbContext, repositories, and migrations
- **Services**: Background services for TFS synchronization
- **Hubs**: SignalR hubs for real-time updates

### Client
Blazor WebAssembly frontend.
- **Pages**: Main views (WorkItemExplorer, etc.)
- **Components**: Reusable UI components (WorkItemTree, WorkItemDetail)
- **Services**: HTTP client services for API communication

### Tests.Unit
MSTest unit tests for Core and Api layers.

## Key Features (In Progress)

### Work Item Tree (Epics → Features → PBIs)
- Hierarchical visualization of work items
- Local SQLite caching for offline access
- Inline search with text highlighting
- Real-time updates via SignalR
- Pull & cache command for TFS synchronization

See [features/Simple_workitem_explorer.md](features/Simple_workitem_explorer.md) for detailed specifications.

## Technology Stack

- **.NET 9.0**: Latest .NET framework
- **ASP.NET Core**: Web API with minimal APIs
- **Blazor WebAssembly**: SPA frontend
- **EF Core**: ORM with SQLite
- **SignalR**: Real-time communication
- **MSTest**: Unit testing framework
- **Scalar**: API documentation

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Visual Studio 2022 or VS Code

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Run Api
```bash
cd Api
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5170
- HTTPS: https://localhost:7001
- Scalar API Docs: http://localhost:5170/scalar/v1 (Development only)
- Health Check: http://localhost:5170/health

### Run Client
```bash
cd Client
dotnet run
```

The client will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

## Database

The application uses SQLite for local caching of TFS work items.

### Migrations
EF Core migrations are mandatory. To create a new migration:
```bash
cd Api
dotnet ef migrations add <MigrationName> --output-dir Data/Migrations
```

To apply migrations:
```bash
cd Api
dotnet ef database update
```

## Architecture Rules

All code must comply with [docs/ARCHITECTURE_RULES.md](docs/ARCHITECTURE_RULES.md). Key invariants:

1. Frontend never calls TFS directly
2. Backend is strictly separated from frontend and shell
3. Core contains all logic and remains infrastructure-free
4. Every TFS mutation is explicit, traceable, and without side effects
5. Views determine navigation; features determine content only
6. Backend must work both in-process and out-of-process
7. Unit tests never use real TFS connections
8. Only approved external packages may be added
9. Dependency Injection is always Microsoft DI
10. Use only the source-generated "Mediator" library

## UX Principles

All UI must follow [docs/ux-principles.md](docs/ux-principles.md):
- Fixed left-side navigation
- Overview → Detail pattern
- Minimal, clean, functional design
- Consistent interaction patterns
- No full-page transitions

## Current Status

**This is initial scaffolding.** The following are implemented:
- ✅ Solution structure with all projects
- ✅ Core DTOs and contracts
- ✅ Api controllers, repositories, and database
- ✅ Client components (scaffolds)
- ✅ EF Core migrations
- ✅ SignalR hub (stub)
- ✅ Background service (stub)
- ✅ Unit tests

**Not yet implemented:**
- ❌ TFS client implementation
- ❌ Actual work item synchronization
- ❌ Parent-child relationships
- ❌ Configuration dialog
- ❌ PAT storage and encryption
- ❌ Component styling
- ❌ Top-level menu bar

## Contributing

See [docs/ARCHITECTURE_RULES.md](docs/ARCHITECTURE_RULES.md) for architectural guidelines that must be followed.

## License

This project is proprietary.
