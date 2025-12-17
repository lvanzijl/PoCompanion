# Copilot Architecture Contract — PO Companion

You MUST follow these rules when generating or modifying code.

## Layering
- Core MUST NOT reference:
  - ASP.NET Core
  - EF Core
  - SignalR
  - HTTP
  - TFS APIs
  - UI frameworks
- Api MAY reference Core.
- Frontend MAY reference generated API clients only.
- Shell MUST NOT contain business logic.

## Communication
- Frontend MUST communicate with backend ONLY via:
  - HTTP Web API
  - SignalR
- Direct method calls across layers are FORBIDDEN.
- SignalR MUST NOT be used for business state synchronization.

## TFS
- Only the backend may access TFS.
- All TFS access MUST go through an interface (e.g. ITfsClient).
- All TFS mutations MUST:
  - be explicit backend actions
  - be logged
  - have no hidden side effects

## Persistence
- Local database is non-canonical.
- Cached data MUST be disposable.
- Loss of cache MUST NOT break functionality.

## Mediator
- MediatR is FORBIDDEN.
- Only the source-generated Mediator library is allowed.
- Commands and Queries MUST be defined in Core.
- Handlers MUST live in Api.
- UI MUST NOT use mediator directly.

## Frontend
- UI components MUST be open-source Blazor components.
- No JavaScript or TypeScript UI widgets.
- No ad-hoc UI components if a library equivalent exists.
- No navigation changes by features.

## Testing
- Unit tests MUST use MSTest.
- Unit tests MUST NOT call real TFS.
- All business logic MUST be unit-testable without infrastructure.

## Dependencies
- Adding a dependency requires explicit approval.
- Prefer fewer dependencies over convenience.
- Microsoft DI is mandatory.

