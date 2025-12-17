## Architecture Summary (Read First)

PO Companion is a strictly layered desktop application.

- **Core** contains all business logic and is infrastructure-agnostic.
- **Backend (ASP.NET Core)** exposes all functionality via Web API and SignalR.
- **Frontend (Blazor WASM)** consumes backend APIs only.
- **Shell (MAUI)** hosts the frontend and manages backend lifecycle, nothing more.

Hard rules:
- Frontend never talks to TFS.
- Core never depends on infrastructure, UI, HTTP, or EF Core.
- Backend is the only layer allowed to integrate with TFS.
- All mutations are explicit, logged, and traceable.
- UI navigation is view-driven; features never add navigation.
- The backend must run both in-process and out-of-process without code changes.

Violating these rules is a design error, not an implementation shortcut.
