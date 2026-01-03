## Architecture Summary (Read First)

PO Companion is a strictly layered web application.

- **Core** contains all business logic and is infrastructure-agnostic.
- **Backend (ASP.NET Core)** exposes all functionality via Web API and SignalR.
- **Frontend (Blazor WebAssembly)** consumes backend APIs only.
- **Hosting** - ASP.NET Core hosts both backend and frontend as a single executable.

Hard rules:
- Frontend never talks to TFS.
- Core never depends on infrastructure, UI, HTTP, or EF Core.
- Backend is the only layer allowed to integrate with TFS.
- All mutations are explicit, logged, and traceable.
- UI navigation is view-driven; features never add navigation.
- The backend must run both in-process and standalone without code changes.
- Warnings are not allowed - all projects MUST treat warnings as errors.

Violating these rules is a design error, not an implementation shortcut.

---

## Documentation Index

### Core Architecture Documents (Mandatory)
1. **[COPILOT_ARCHITECTURE_CONTRACT.md](COPILOT_ARCHITECTURE_CONTRACT.md)** - Rules for AI-assisted development
2. **[ARCHITECTURE_RULES.md](ARCHITECTURE_RULES.md)** - Complete architecture principles and layer boundaries
3. **[UI_RULES.md](UI_RULES.md)** - Frontend and component rules
4. **[PROCESS_RULES.md](PROCESS_RULES.md)** - Development workflow and review standards
5. **[TFS_INTEGRATION_RULES.md](TFS_INTEGRATION_RULES.md)** - TFS integration rules
6. **[Fluent_UI_compat_rules.md](Fluent_UI_compat_rules.md)** - Fluent UI Compact rules
7. **[mock-data-rules.md](mock-data-rules.md)** - Mock data generation rules (MANDATORY for testing and development)

### Additional Documentation
8. **[PAT_STORAGE_BEST_PRACTICES.md](PAT_STORAGE_BEST_PRACTICES.md)** - Security best practices for credential storage



---

## Quick Links

- **Architecture**: [Architecture Rules](ARCHITECTURE_RULES.md)
- **AI Guidelines**: [Copilot Contract](COPILOT_ARCHITECTURE_CONTRACT.md)
