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
3. **[UX_PRINCIPLES.md](UX_PRINCIPLES.md)** - User experience guidelines
4. **[UI_RULES.md](UI_RULES.md)** - Frontend and component rules
5. **[PROCESS_RULES.md](PROCESS_RULES.md)** - Development workflow and review standards

### TFS Integration Documentation (New)
6. **[TFS_ONPREM_INTEGRATION_PLAN.md](TFS_ONPREM_INTEGRATION_PLAN.md)** - Complete technical plan for on-premises TFS integration
   - Executive summary and current state analysis
   - REST API vs SDK approach decision
   - Authentication strategy (PAT + NTLM)
   - Work Items enhancement plan
   - Pull Requests implementation guide
   - Error handling and resilience patterns
   - 5-phase implementation plan (50-62 hours)
   - Success metrics and risk mitigation

7. **[TFS_INTEGRATION_EXECUTIVE_SUMMARY.md](TFS_INTEGRATION_EXECUTIVE_SUMMARY.md)** - Stakeholder-friendly summary
   - Overview and current state
   - Proposed solution
   - Implementation phases with effort
   - Success criteria
   - Risk assessment
   - Next steps

8. **[TFS_INTEGRATION_QUICK_REFERENCE.md](TFS_INTEGRATION_QUICK_REFERENCE.md)** - Developer quick reference
   - API endpoints reference
   - Authentication examples
   - Testing patterns
   - Common tasks and code snippets
   - Debugging tips
   - Common issues and solutions

### Additional Documentation
9. **[SINGLE_EXECUTABLE_ARCHITECTURE.md](SINGLE_EXECUTABLE_ARCHITECTURE.md)** - ASP.NET Core single executable hosting model
10. **[PAT_STORAGE_BEST_PRACTICES.md](PAT_STORAGE_BEST_PRACTICES.md)** - Security best practices for credential storage

---

## How to Use This Documentation

### For New Developers
1. Read **Architecture Summary** (above)
2. Read **ARCHITECTURE_RULES.md** - Understand layer boundaries
3. Read **COPILOT_ARCHITECTURE_CONTRACT.md** - AI assistance rules
4. Read **TFS_INTEGRATION_QUICK_REFERENCE.md** - For TFS-related work

### For Stakeholders/Product Owners
1. Read **TFS_INTEGRATION_EXECUTIVE_SUMMARY.md** - Implementation plan overview
2. Review implementation phases and timelines
3. Assess risks and success metrics

### For Implementation Teams
1. Read **TFS_ONPREM_INTEGRATION_PLAN.md** - Complete technical details
2. Follow phase-by-phase implementation guide
3. Refer to **TFS_INTEGRATION_QUICK_REFERENCE.md** during development
4. Follow **PROCESS_RULES.md** for reviews and merges

### For AI Agents
1. **ALWAYS** read and follow **COPILOT_ARCHITECTURE_CONTRACT.md**
2. Reference relevant rule documents before generating code
3. Use **TFS_INTEGRATION_QUICK_REFERENCE.md** for TFS integration tasks

---

## Quick Links

- **Planning**: [TFS Integration Executive Summary](TFS_INTEGRATION_EXECUTIVE_SUMMARY.md)
- **Implementation**: [TFS Integration Technical Plan](TFS_ONPREM_INTEGRATION_PLAN.md)
- **Development**: [TFS Integration Quick Reference](TFS_INTEGRATION_QUICK_REFERENCE.md)
- **Architecture**: [Architecture Rules](ARCHITECTURE_RULES.md)
- **AI Guidelines**: [Copilot Contract](COPILOT_ARCHITECTURE_CONTRACT.md)
