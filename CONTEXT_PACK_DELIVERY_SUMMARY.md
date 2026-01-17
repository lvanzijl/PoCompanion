# Context Pack Generation - Delivery Summary

## What Was Created

A comprehensive **CONTEXT PACK** document has been generated and saved to:
```
docs/CONTEXT_PACK.md
```

This document provides a complete, authoritative context of the PO Companion repository for use by ChatGPT when generating Copilot prompts.

## Document Structure (672 lines, 34 KB)

The CONTEXT PACK follows the exact format requested and includes:

### 0) Repo Snapshot
- Repository purpose and domain model
- Complete tech stack (C# 10, .NET 10, Blazor WASM, ASP.NET Core, EF Core, SQLite)
- Build/test/run commands

### 1) Repository Map
- All 7 projects with paths, responsibilities, entry points, and dependencies
- Namespace strategy
- Assembly dependency flow diagram

### 2) Architecture Patterns In Use
- 4-layer architecture (Client → Api → Core → Shared)
- Dependency direction rules (infrastructure-free Core)
- DI patterns (scoped DbContext, singleton services)
- Error handling (Result<T> pattern)
- **Critical**: EF Core concurrency rules (two-phase pattern, no Task.WhenAll on DbContext)
- Async/await patterns (async-first, no blocking in Client)
- Serialization, validation, caching strategies

### 3) Functional Domain Model (Repo Terminology)
- Glossary of canonical terms (Goal, Objective, Epic, Feature, PBI, Bug, Task)
- Core entities (WorkItemDto, PullRequestDto, TfsVerificationReport)
- Key user workflows mapped to code paths:
  - "User pulls work items from TFS" → UI → Client → Api → Core → Persistence
  - "User views backlog health" → calculations → metrics display
  - "User configures TFS connection" → PAT storage → API calls
- Work item state machines by type

### 4) Data Model & Persistence
- SQLite usage (local cache, disposable)
- EF Core configuration (Code-First, EnsureCreated)
- Key tables/entities (WorkItems, PullRequests, Profiles, Products, Teams)
- Relationships (parent-child, self-referencing, 1:N)
- Concurrency strategy (RowVersion, optimistic locking)

### 5) API Surface
- Base URLs (http://localhost:5291, /swagger, /health)
- Authentication model (NTLM/PAT, client-side PAT storage)
- All controllers and endpoints documented:
  - WorkItemsController, PullRequestsController, PipelinesController
  - SettingsController, ProfilesController, ProductsController, TeamsController
- Request/response DTO conventions
- Error model (HTTP status codes, TfsVerificationReport)

### 6) Integrations & External Systems
- Azure DevOps Server (TFS, on-prem)
- Client code locations (ITfsClient, RealTfsClient, MockTfsClient)
- Auth mechanisms (NTLM, PAT with client-side storage)
- Rate limiting (TfsRequestThrottler)
- Known constraints (WIQL limits, api-version=7.0, N+1 prevention via bulk methods)

### 7) Frontend/UI Composition
- Complete routing map (Home, TFS Config, Work Items, Backlog Health, PR Insights, etc.)
- State management (component-level, scoped services, SignalR for real-time)
- Component library patterns (MudBlazor, Compact components, reusable components)
- Selection context (Product, Profile, Mode Isolation)

### 8) Rules & Conventions (MUST/SHOULD)
- Naming conventions (PascalCase, camelCase, _prefix, Dto/Entity/Service suffixes)
- Folder conventions (Queries/, Commands/, Controllers/, Services/)
- Generated code boundaries (NSwag clients, Mediator handlers - do not edit)
- Coding guidelines (nullable enabled, warnings as errors)
- Security practices (HTML sanitization, PAT client-side only, no logging secrets)

### 9) Testing Strategy
- Test project map (Unit, Integration, Blazor)
- Test types (MSTest, Reqnroll, bUnit, Playwright)
- No live TFS in tests (mocks mandatory)
- CI: All workflows disabled (.yml.disabled)

### 10) Quality Gates & CI/CD
- Build/release/exploratory test workflows (all disabled)
- Linting (TreatWarningsAsErrors=true)
- No coverage gates or automated CI on push

### 11) Known Pain Points & Tech Debt
- Hotspots (RealTfsClient, WorkItemSyncService, PoToolDbContext)
- Performance bottlenecks (N+1 prevented, large syncs risk timeout)
- Fragile boundaries (NSwag regeneration, Mediator codegen, Shared DTO changes)
- Areas Copilot often breaks:
  - EF concurrency violations (Task.WhenAll on DbContext)
  - Client-Core references (forbidden)
  - PAT storage in API (forbidden)
  - Area path mixing below Epic (forbidden by mock data rules)

### 12) Unknowns / Ambiguities
- Migration strategy (EnsureCreated is dev-only)
- API versioning (none currently)
- Deployment model (desktop? web? Docker?)
- Observability (no structured logging)
- Background sync triggers
- User management/auth
- TFS server version compatibility
- Mock data performance (19,640 items)
- Exploratory test execution frequency
- Dependency graph UI (mentioned but not found)

### 13) "Prompting Interface" for ChatGPT
- **Required sections** in every Copilot prompt:
  1. Functional Goal (user-visible outcome, business value)
  2. Scope (in scope, out of scope)
  3. Non-Goals (explicitly excluded)
  4. Acceptance Criteria (testable conditions)
  5. Copilot Hints (architecture, patterns, constraints)
  6. Files to Touch (explicit list)
  7. Files NOT to Touch (explicit list)
  8. Tests (which test projects to update)

- **Style constraints**: Functional-first, concise, technical hints secondary
- **Safety rails**: No broad refactors, minimal diff, keep architecture, avoid generated files

- **Worked example included**: "Add Dependency Graph Visualization"
  - Demonstrates complete prompt structure
  - Shows how to reference architecture, existing services, and constraints
  - Includes specific file paths and test requirements

## Key Insights Captured

### Architecture Hard Rules
1. **Client CANNOT reference Core** - Only Shared (via HTTP API)
2. **Core MUST be infrastructure-free** - No EF, ASP.NET, SignalR, HTTP
3. **PAT storage is client-side only** - Never in API or database
4. **Area paths inherit from Epic** - No mixing below Epic level

### Concurrency Rules (Critical)
- **DbContext is NOT thread-safe** - No concurrent EF operations
- **Two-phase pattern mandatory**:
  - Phase 1: Parallel HTTP/CPU (no EF)
  - Phase 2: Sequential EF (fully awaited, single SaveChangesAsync)
- **Forbidden**: Task.WhenAll with EF, Parallel.ForEach with EF

### TFS Integration Rules
- **API version 7.0 explicit** - Always specify api-version=7.0
- **Bulk methods mandatory** - Prevent N+1 patterns
- **WIQL limits** - On-prem TFS has 10,000 item query limits
- **Verification required** - TfsVerificationReport before write operations

### Battleship Mock Data Theme
- **15,815 to 23,455 work items** - Goal → Objective → Epic → Feature → PBI/Bug → Task
- **10-15 teams** - Portfolio → Program → Feature Teams → Shared Services
- **Area path inheritance** - Epic determines team, all children inherit
- **30-40% cross-team dependencies** - Realistic coordination modeling
- **10-15% invalid states** - Intentional for testing detection

## How to Use This Context Pack

### For ChatGPT Prompt Generation
1. **Paste the entire CONTEXT PACK** into ChatGPT as context
2. **Describe the feature/fix** you want Copilot to implement
3. **ChatGPT will generate a prompt** following the "Prompting Interface" format (§13)
4. **Copy the generated prompt** and give it to Copilot

### For Direct Copilot Usage
- Reference sections by number when giving instructions
- Example: "Per CONTEXT PACK §2 (Architecture Patterns), implement a bulk TFS operation..."
- Example: "Following the worked example in §13, add a new visualization page..."

### For Code Review
- Use §8 (Rules & Conventions) as a checklist
- Verify §11 (Known Pain Points) aren't being introduced
- Check §12 (Unknowns) before making assumptions

## Verification Checklist

✅ All 13 required sections present  
✅ Concrete file paths and type names included  
✅ Rules linked to authoritative docs (ARCHITECTURE_RULES.md, etc.)  
✅ Worked example demonstrates complete prompt structure  
✅ Unknowns/ambiguities explicitly documented  
✅ No invented details (grounded in actual code)  
✅ Concise but comprehensive (672 lines, 34 KB)  

## Files Changed

```
docs/CONTEXT_PACK.md (new file)
```

## Next Steps

1. **Review the CONTEXT PACK** for accuracy and completeness
2. **Test with ChatGPT**: Generate a sample Copilot prompt using the context pack
3. **Iterate if needed**: Add missing details or clarify ambiguities
4. **Use in workflow**: Paste into ChatGPT for all future Copilot prompt generation

---

**Generated**: 2026-01-17  
**Commit**: `Add comprehensive CONTEXT PACK documentation`  
**Status**: ✅ Complete and ready for use
