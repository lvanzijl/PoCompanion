# Architecture Rules — PO Companion

This document defines all binding architectural rules for the PO Companion.
All code, refactorings, generated output, and AI-assisted changes MUST comply with these rules.

Violations are architectural errors, not implementation shortcuts.

---

## 1. Architecture overview

The application consists of four strictly separated layers:

1. **Core**
   - Business logic
   - Domain models
   - Interfaces

2. **Api**
   - ASP.NET Core Web API
   - SignalR hubs
   - Infrastructure implementations
   - Persistence and integrations

3. **Frontend**
   - Blazor WebAssembly
   - Razor class library
   - UI logic and presentation
   - Communicates only with Api

4. **Hosting**
   - ASP.NET Core Web API hosts both backend and frontend
   - Serves Blazor WebAssembly as static files
   - Single executable deployment

Layer boundaries are absolute.

---

## 2. Layer boundaries (hard rules)

### 2.1 Core
Core MUST NOT reference:
- ASP.NET Core
- EF Core
- SignalR
- HTTP
- TFS APIs
- UI frameworks
- Configuration systems

Core MUST:
- Contain all business logic
- Be fully unit-testable
- Be infrastructure-agnostic

---

### 2.2 Api
Api:
- MAY reference Core
- MAY reference infrastructure and third-party systems
- MUST expose all functionality via Web API and SignalR
- MUST be the only layer allowed to access TFS

Api MUST NOT:
- Contain UI logic
- Bypass Core business rules

---

### 2.3 Frontend
Frontend:
- MUST be Blazor WebAssembly (Razor class library)
- MUST communicate exclusively via:
  - HTTP Web API
  - SignalR
- MUST NOT access TFS directly
- MUST NOT contain business logic

Frontend MUST NOT:
- Call backend services directly (even in-process)
- Store sensitive data locally
- Depend on backend runtime hosting model

---

### 2.4 Hosting
Hosting:
- ASP.NET Core Web API serves both API and frontend
- Frontend is served as static files via Blazor WebAssembly Server hosting
- Single executable contains all components

Hosting MUST NOT:
- Allow direct method calls between frontend and backend
- Bypass API layer for frontend communication

---

## 3. Hosting model

### 3.1 Current model
- Application runs as a single ASP.NET Core executable
- Backend Web API and frontend are hosted in-process
- Frontend is Blazor WebAssembly served as static files
- Communication via HTTP + SignalR

### 3.2 Deployment flexibility
Backend MUST be able to run without code changes as:
- Standalone process
- Windows service
- Containerized service
- Cloud-hosted service

Frontend communication remains unchanged regardless of deployment model.

---

## 4. Communication rules

- Frontend ↔ Backend communication ONLY via:
  - HTTP Web API
  - SignalR
- Direct method calls across layers are FORBIDDEN
- No shared runtime objects between layers

SignalR:
- Used only for backend-to-frontend notifications
- MUST NOT be used for:
  - business state synchronization
  - orchestration
  - implicit commands

---

## 5. Persistence rules

- Local database is non-canonical
- Stored data is derived and disposable
- Loss of local data MUST NOT break functionality

Database is used only for:
- User settings
- Local caches
- Internal metadata

---

## 6. TFS integration

- TFS access is restricted to Api layer
- All TFS access MUST go through an interface (e.g. `ITfsClient`)
- Concrete implementations handle:
  - Authentication (PAT)
  - API calls
  - Result mapping

### 6.1 Mutations
All TFS mutations MUST:
- Represent one explicit backend action
- Have no hidden side effects
- Be logged with action type and outcome

Frontend MUST NOT trigger implicit TFS mutations.

---

## 7. Authentication & secrets

- Rule (architecture-level):
- PAT is client-side only.
- PAT is never persisted server-side (db/files/cache/logs).
- API may receive PAT only for immediate use/validation and must not retain it.

Authority:

- All details (storage mechanism, encryption, lifecycle, XSS mitigations, migration) are defined in PAT_STORAGE_BEST_PRACTICES.md and override any summaries. 

---

## 8. Client runtime rules (Blazor WebAssembly)

### 8.1 No sync-over-async in PoTool.Client

In `PoTool.Client` (Blazor WebAssembly), **synchronous waiting on asynchronous operations is forbidden**.

Blazor WebAssembly runs on a **single-threaded runtime**. Blocking calls can deadlock the UI, freeze rendering, or break event handling.

#### Forbidden patterns (non-exhaustive)
The following MUST NOT appear anywhere in `PoTool.Client`:

- `.Result`
- `.Wait()`
- `GetAwaiter().GetResult()`
- `AsTask().Result`
- `AsTask().Wait()`

This applies to:
- UI components
- Services
- Storage helpers
- JS interop wrappers
- Any client-side abstraction

#### Required pattern
- All client-side APIs MUST be **Task-based and fully asynchronous**.
- UI lifecycle methods MUST use async variants (`OnInitializedAsync`, etc.).
- UI event handlers MUST return `Task` and `await` downstream calls.
- Async call chains MUST remain async end-to-end.

#### Migration rule
If an existing API is synchronous but internally relies on async behavior:
- Introduce an **explicit async interface**
- Migrate call sites to async
- Mark the synchronous API as obsolete and non-functional if necessary

Wrapping async code in blocking adapters is an architectural violation.

--- 

## 9. Logging & health

- Central logging is mandatory for:
  - TFS mutations
  - Backend errors
  - Significant actions
- Logs MUST NOT contain sensitive data
- Backend MUST expose a health endpoint
- Health endpoint can be used for monitoring and readiness checks

---

## 10. Project structure

The solution MUST contain at least:

- **Core**
  - Domain models
  - Pure services
  - Interfaces

- **Api**
  - Controllers
  - SignalR hubs
  - EF Core contexts
  - Infrastructure implementations

- **Frontend**
  - Blazor WebAssembly application

- **Tests.Unit**
  - MSTest unit tests
  - File-based TFS fakes

---

## 11. Testing rules

### 11.1 Unit tests

- MSTest is mandatory
- Business logic MUST be tested in Core
- Controllers and UI contain no logic to unit-test
- Tests MUST NOT connect to real TFS
- TFS behavior is simulated via file-based mocks

### 11.2 Integration tests

Integration tests MUST verify end-to-end API behavior with Reqnroll (BDD framework).

#### 11.2.1 Framework and tooling
- Integration tests MUST use **Reqnroll** for BDD-style test definitions
- Tests MUST be defined in `.feature` files using Gherkin syntax
- Step definitions MUST be implemented in C# using Reqnroll bindings
- MSTest MUST be used as the test runner

#### 11.2.2 Test entry points
Integration tests MUST cover:
- All Web API endpoints exposed by the Api layer
- All SignalR events and hub methods
- All request/response flows between frontend and backend

#### 11.2.3 Test execution model
- Tests MUST start the full ASP.NET Core Web API application
- Tests MUST use a real database instance (in-memory or temporary)
- Tests MUST exercise HTTP endpoints as external clients would
- Tests MUST connect to SignalR hubs as real clients would
- No mocking of Api, Core, or database layers is allowed

#### 11.2.4 TFS mocking (only exception)
- TFS integration is the ONLY component that MAY be mocked
- TFS mocks MUST use file-based test data
- TFS mocks MUST simulate realistic responses and error conditions
- TFS mocks MUST NOT alter business logic behavior

#### 11.2.5 Coverage requirements
- **85% coverage** of all Web API endpoints is MANDATORY
- **85% coverage** of all SignalR hub methods is MANDATORY
- Each endpoint MUST be tested for:
  - Successful execution (happy path)
  - Error conditions and validation failures
  - Authorization and authentication requirements (when applicable)

#### 11.2.6 Test organization
- Integration tests MUST be in a separate test project
- Feature files MUST be organized by feature area or domain
- Step definitions MUST be reusable across multiple scenarios
- Test data MUST be isolated per test scenario

#### 11.2.7 Test independence
- Each integration test scenario MUST be independent
- Tests MUST clean up their own test data
- Tests MUST NOT depend on execution order
- Database state MUST be reset between test scenarios

---

## 12. Mediator usage

- MediatR is FORBIDDEN
- Only the source-generated **Mediator** library is allowed

Mediator MAY be used only for:
- Commands (mutations)
- Queries (read operations)
- Pipelines (logging, validation)

Mediator MUST NOT be used for:
- UI orchestration
- Navigation
- Cross-command chaining

Commands and queries live in Core.  
Handlers and pipelines live in Api.

---

## 13. Dependency policy

- New dependencies require explicit approval
- Prefer fewer dependencies over convenience
- Each dependency is evaluated on:
  - Maintenance status
  - License
  - Security impact
  - Overlap with existing packages

---

## 14. Dependency Injection

- Only Microsoft.Extensions.DependencyInjection is allowed
- No alternative DI containers
- No service locator pattern
- Constructor injection is mandatory

---

## 15. Architectural invariants (never break)

1. Frontend never talks to TFS
2. Core remains infrastructure-free
3. Backend is the single integration point
4. All TFS mutations are explicit and traceable
5. Views define navigation; features define content only
6. Backend can be deployed standalone or as a hosted service without code changes
7. Unit tests never use real TFS
8. Only approved dependencies are allowed
9. Microsoft DI is mandatory
10. Only the source-generated Mediator is allowed
11. Never manually edit generated code files (*.g.cs)
12. Warnings are not allowed - all projects MUST treat warnings as errors

---

## 16. Code Quality

### 16.1 Warnings Policy
- **All warnings MUST be treated as errors** in all projects
- Projects MUST set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in their .csproj files (MSBuild XML)
- No exceptions to this rule
- Code MUST build without any warnings before being merged

### 16.2 Rationale
- Warnings often indicate potential bugs or design issues
- Treating warnings as errors enforces clean code
- Prevents accumulation of technical debt
- Ensures consistent code quality across the codebase
