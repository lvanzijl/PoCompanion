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
   - Blazor Hybrid (not WebAssembly)
   - Razor class library
   - UI logic and presentation
   - Communicates only with Api

4. **Shell**
   - MAUI Hybrid desktop application
   - Hosts Blazor frontend in BlazorWebView
   - Manages backend lifecycle via ApiHostService
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
- MUST be Blazor Hybrid (Razor class library)
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

### 2.4 Shell
Shell:
- Hosts the frontend
- Starts and monitors the backend
- Performs health checks only

Shell MUST NOT:
- Contain business logic
- Access TFS
- Perform data processing
- Communicate with backend other than startup and health monitoring

---

## 3. Hosting model

### 3.1 Current model
- Application runs as a single executable
- Backend is started in-process by the Shell
- Frontend runs in a MAUI WebView
- Communication via `localhost` HTTP + SignalR

### 3.2 Future model
Backend MUST be able to run without code changes as:
- Separate process
- Windows service
- Containerized service

Frontend communication remains unchanged.

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

**See comprehensive rules in:** `PAT_STORAGE_BEST_PRACTICES.md`

Summary:
- PAT MUST be stored client-side using MAUI SecureStorage (platform secure storage)
- PAT MUST NEVER be persisted on the server/API (not in database, cache, or logs)
- API receives PAT per request or per session for validation/usage only
- Server-side storage is for non-sensitive configuration only (URL, Project, settings)
- Client-side storage is for credentials and session-specific data

---

## 8. Logging & health

- Central logging is mandatory for:
  - TFS mutations
  - Backend errors
  - Significant actions
- Logs MUST NOT contain sensitive data
- Backend MUST expose a health endpoint
- Shell monitors backend health via this endpoint

---

## 9. Project structure

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

- **Shell**
  - MAUI desktop application

- **Tests.Unit**
  - MSTest unit tests
  - File-based TFS fakes

---

## 10. Testing rules

### 10.1 Unit tests

- MSTest is mandatory
- Business logic MUST be tested in Core
- Controllers and UI contain no logic to unit-test
- Tests MUST NOT connect to real TFS
- TFS behavior is simulated via file-based mocks

### 10.2 Integration tests

Integration tests MUST verify end-to-end API behavior with Reqnroll (BDD framework).

#### 10.2.1 Framework and tooling
- Integration tests MUST use **Reqnroll** for BDD-style test definitions
- Tests MUST be defined in `.feature` files using Gherkin syntax
- Step definitions MUST be implemented in C# using Reqnroll bindings
- MSTest MUST be used as the test runner

#### 10.2.2 Test entry points
Integration tests MUST cover:
- All Web API endpoints exposed by the Api layer
- All SignalR events and hub methods
- All request/response flows between frontend and backend

#### 10.2.3 Test execution model
- Tests MUST start the full ASP.NET Core Web API application
- Tests MUST use a real database instance (in-memory or temporary)
- Tests MUST exercise HTTP endpoints as external clients would
- Tests MUST connect to SignalR hubs as real clients would
- No mocking of Api, Core, or database layers is allowed

#### 10.2.4 TFS mocking (only exception)
- TFS integration is the ONLY component that MAY be mocked
- TFS mocks MUST use file-based test data
- TFS mocks MUST simulate realistic responses and error conditions
- TFS mocks MUST NOT alter business logic behavior

#### 10.2.5 Coverage requirements
- **100% coverage** of all Web API endpoints is MANDATORY
- **100% coverage** of all SignalR hub methods is MANDATORY
- Each endpoint MUST be tested for:
  - Successful execution (happy path)
  - Error conditions and validation failures
  - Authorization and authentication requirements (when applicable)

#### 10.2.6 Test organization
- Integration tests MUST be in a separate test project
- Feature files MUST be organized by feature area or domain
- Step definitions MUST be reusable across multiple scenarios
- Test data MUST be isolated per test scenario

#### 10.2.7 Test independence
- Each integration test scenario MUST be independent
- Tests MUST clean up their own test data
- Tests MUST NOT depend on execution order
- Database state MUST be reset between test scenarios

---

## 11. Mediator usage

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

## 12. Dependency policy

- New dependencies require explicit approval
- Prefer fewer dependencies over convenience
- Each dependency is evaluated on:
  - Maintenance status
  - License
  - Security impact
  - Overlap with existing packages

---

## 13. Dependency Injection

- Only Microsoft.Extensions.DependencyInjection is allowed
- No alternative DI containers
- No service locator pattern
- Constructor injection is mandatory

---

## 14. Architectural invariants (never break)

1. Frontend never talks to TFS
2. Core remains infrastructure-free
3. Backend is the single integration point
4. All TFS mutations are explicit and traceable
5. Views define navigation; features define content only
6. Backend runs in-process and out-of-process without changes
7. Unit tests never use real TFS
8. Only approved dependencies are allowed
9. Microsoft DI is mandatory
10. Only the source-generated Mediator is allowed
11. Never manually edit generated code files (*.g.cs)

---

## 15. MAUI Hybrid Architecture

### 15.1 Hosting Model
- The application is a **MAUI Hybrid** app
- ASP.NET Core API runs **in-process** within the MAUI app via `ApiHostService`
- Blazor UI runs in a `BlazorWebView` (Blazor Hybrid, not WebAssembly)
- Communication is via HTTP and SignalR over localhost (default: http://localhost:5291)
- Single executable deployment containing all components

### 15.2 API Lifecycle
- API is started by `ApiHostService` during MAUI app initialization
- API listens on `http://localhost:5291` (configurable)
- API is stopped gracefully on app shutdown
- Health checks ensure API availability before UI loads
- `ApiInitializer` performs readiness polling with retry logic

### 15.3 Future Scalability
- API configuration MUST remain environment-agnostic
- API MUST be able to run standalone without code changes (via PoTool.Api project)
- Client MUST NOT assume in-process hosting
- Communication MUST always be via HTTP/SignalR (no direct method calls)
- To scale to client-server model: change HttpClient BaseAddress only

### 15.4 Deployment
- Single executable contains:
  - MAUI shell
  - API layer (embedded)
  - Blazor components (embedded)
  - All dependencies
- No separate backend deployment needed for single-user scenarios
- Database is local SQLite by default (located in app data directory)

### 15.5 Platform Support
- **Windows** (primary target) - net10.0-windows10.0.19041.0
- **macOS** (via Mac Catalyst) - net10.0-maccatalyst
- **Android** (optional) - net10.0-android
- **iOS** (optional) - net10.0-ios

### 15.6 Development Workflow
- Run `PoTool.Maui` project to start complete application
- API automatically starts on app launch
- Blazor debugging available in development mode
- API can still be run standalone via `PoTool.Api` for testing

### 15.7 Architecture Compliance
- All existing architecture rules (sections 1-14) remain in force
- Layer boundaries are maintained (Core, Api, Frontend, Shell)
- Communication between layers is strictly via HTTP/SignalR
- No direct method calls between Frontend and Api
- This model supports both in-process and out-of-process hosting
