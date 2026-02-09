## Architecture Rules

This document outlines the key architectural rules and patterns used in PoCompanion to ensure consistency, maintainability, and code quality across the project.

---

## 1. Clean Architecture Principles

### 1.1 Separation of Concerns
- The project is organized into distinct layers:
  - **API**: Entry point for HTTP requests
  - **Application**: Business logic and use cases
  - **Domain**: Core business entities and rules
  - **Infrastructure**: External concerns (database, file system, etc.)

### 1.2 Dependency Rule
- Dependencies must point inward toward the Domain
- The Domain layer should have no external dependencies
- The Application layer may reference Domain but not Infrastructure or API
- The Infrastructure and API layers may reference Application and Domain

---

## 2. CQRS Pattern

### 2.1 Command and Query Separation
- **Commands**: Represent actions that change state
  - Placed in `Application/Features/{Entity}/Commands/`
  - Must implement `IRequest<Result<T>>` or `IRequest<Result>`
  - Should have a single responsibility

- **Queries**: Represent data retrieval operations
  - Placed in `Application/Features/{Entity}/Queries/`
  - Must implement `IRequest<Result<T>>`
  - Should be read-only

### 2.2 Mediator Integration (Source-Generated Only)
- MediatR is forbidden in this solution
- All commands and queries are handled through the source-generated Mediator library
- Controllers should only orchestrate requests, not contain business logic

---

## 3. Result Pattern

### 3.1 Result Type Usage
- All service methods must return `Result<T>` or `Result`
- Success: `Result.Success(value)` or `Result.Success()`
- Failure: `Result.Failure<T>(error)` or `Result.Failure(error)`

### 3.2 Error Handling
- Errors should be descriptive and actionable
- Use the `Error` class to create structured error messages
- Avoid throwing exceptions for expected business rule violations

---

## 4. Entity Framework Core

### 4.1 DbContext Configuration
- Entity configurations must be in separate classes implementing `IEntityTypeConfiguration<T>`
- Configurations should be placed in `Infrastructure/Data/Configurations/`
- Apply configurations in `OnModelCreating` using `ApplyConfigurationsFromAssembly`

### 4.2 Repository Pattern
- Repositories are optional and should only be used when additional abstraction provides value
- Prefer direct DbContext usage in handlers for simple CRUD operations
- Complex queries should use EF Core's LINQ capabilities

---

## 5. Validation

### 5.1 FluentValidation
- All commands and queries with input parameters must have validators
- Validators should be placed alongside their corresponding command/query
- Validators must inherit from `AbstractValidator<T>`

### 5.2 Validation Pipeline
- Validation is executed automatically via MediatR pipeline behavior
- Validation failures return `Result.Failure` with detailed error messages
- Controllers should not perform validation logic

---

## 6. API Controllers

### 6.1 Controller Structure
- Controllers should be thin and delegate to MediatR
- Use `[ApiController]` attribute for automatic model validation
- Follow RESTful conventions for endpoint naming

### 6.2 Response Formatting
- Return appropriate HTTP status codes:
  - 200 OK for successful queries
  - 201 Created for successful resource creation
  - 204 No Content for successful commands with no return value
  - 400 Bad Request for validation failures
  - 404 Not Found for missing resources
  - 500 Internal Server Error for unexpected failures

---

## 7. Dependency Injection

### 7.1 Service Registration
- Services should be registered in `DependencyInjection.cs` files in each layer
- Use appropriate lifetimes:
  - **Transient**: For lightweight, stateless services
  - **Scoped**: For services tied to a request (e.g., DbContext)
  - **Singleton**: For stateless services with no dependencies on scoped services

### 7.2 Interface-Based Dependencies
- Depend on abstractions (interfaces) rather than concrete implementations
- Place interfaces near their consumers when possible

---

## 8. Development Practices

### 8.1 Testing
- Unit tests should be isolated and fast
- Integration tests should verify end-to-end scenarios
- Use test fixtures to reduce setup duplication
- Mock external dependencies in unit tests

### 8.2 HTTP-only Development Rule

**Overview**: The PoCompanion system is configured to use HTTP-only during development to simplify the development workflow and avoid common HTTPS-related issues.

#### Where HTTPS is Disabled

The following configuration locations explicitly use HTTP instead of HTTPS:

1. **API Base URL** (`appsettings.json`):
   - `ApiBaseUrl` is set to `http://localhost:5291`
   - Used by the client to connect to the API

2. **Kestrel Launch Settings** (`launchSettings.json`):
   - Application URL configured for HTTP only
   - HTTPS profile removed or disabled

3. **SignalR Hub URLs**:
   - Hub connection URLs use HTTP protocol
   - Auto-derived from the API base URL

4. **CORS Configuration** (`ApiServiceCollectionExtensions.cs`, lines 154-164):
   - CORS policy configured to allow HTTP origins
   - Development origins use `http://localhost` addresses

#### How to Re-enable HTTPS

If HTTPS is required for development or testing, follow these steps:

1. **Update Kestrel Configuration** (`launchSettings.json`):
   - Add or uncomment the HTTPS profile
   - Configure the HTTPS port (typically 5292 or 7291)

2. **Change Base Addresses**:
   - Update `ApiBaseUrl` in `appsettings.json` to use `https://localhost:{port}`
   - Ensure SSL certificate is trusted locally

3. **Update CORS Configuration** (`ApiServiceCollectionExtensions.cs`):
   - Add HTTPS origins to the CORS policy
   - Update lines 154-164 to include `https://` URLs

4. **SignalR URLs Auto-Update**:
   - SignalR hub URLs will automatically use HTTPS when the base URL is changed
   - No additional configuration needed

5. **HTTPS Redirection** (`ApiApplicationBuilderExtensions.cs`, lines 136-142):
   - `UseHttpsRedirection` is already conditionally configured
   - It will activate automatically in non-development environments

#### Rationale

The HTTP-only development configuration provides several benefits:

- **Avoids Certificate Issues**: No need to generate, trust, or manage self-signed SSL certificates during development
- **Eliminates Mixed Content Errors**: Prevents browser warnings about mixing HTTP and HTTPS resources
- **Reduces CORS Complexity**: Simplifies CORS configuration by avoiding protocol mismatches
- **Simplifies TLS Debugging**: Removes the encryption layer, making it easier to inspect network traffic during development

#### Production Requirements

**IMPORTANT**: This HTTP-only configuration is strictly for development environments. Production deployments **MUST** use HTTPS to ensure:

- Data encryption in transit
- Protection against man-in-the-middle attacks
- Compliance with security best practices
- Browser security feature compatibility (e.g., secure cookies, service workers)

The application is designed to seamlessly transition to HTTPS in production through environment-specific configuration. The `UseHttpsRedirection` middleware (lines 136-142 in `ApiApplicationBuilderExtensions.cs`) is already set up to enforce HTTPS in non-development environments.

---

## 9. Code Quality

### 9.1 Naming Conventions
- Use PascalCase for public members and types
- Use camelCase for private fields and local variables
- Prefix private fields with underscore (`_fieldName`)
- Use descriptive names that convey intent

### 9.2 Code Reviews
- All changes should be reviewed before merging
- Reviews should check for adherence to architecture rules
- Focus on maintainability, not just functionality

---

## 10. Documentation

### 10.1 Code Comments
- Use XML documentation comments for public APIs
- Avoid obvious comments that restate the code
- Explain "why" rather than "what" when commenting

### 10.2 Architecture Documentation
- Keep this document up-to-date with architectural changes
- Document significant design decisions
- Include rationale for non-obvious choices

---

## Conclusion

Adhering to these architecture rules ensures that PoCompanion remains maintainable, testable, and scalable. As the project evolves, these rules may be updated to reflect new patterns and practices. Always discuss significant deviations from these rules with the team before implementation.
Following these architectural rules ensures that PoCompanion remains maintainable, testable, and scalable as the project evolves.

---

## Solution Hierarchy and Assembly References

### Assembly Dependency Rules

The solution follows a strict layered architecture with clear dependency boundaries:

```
┌─────────────────────────────────────────────────┐
│  Dependency Flow (A → B means "A references B") │
├─────────────────────────────────────────────────┤
│  PoTool.Client  →  PoTool.Shared                │
│  PoTool.Api     →  PoTool.Core + PoTool.Shared  │
│  PoTool.Core    →  PoTool.Shared                │
│  PoTool.Shared  →  (nothing - leaf assembly)    │
└─────────────────────────────────────────────────┘
```

### What Belongs in Each Assembly

#### PoTool.Shared (Data Contracts)
**Purpose:** Contains data contracts (DTOs) that cross assembly boundaries, particularly between API and Client.

**Contains:**
- DTOs (Data Transfer Objects)
- Enums used in DTOs
- Constants used in DTOs
- Request/Response models
- Exception types that cross boundaries

**Must NOT contain:**
- Business logic
- Validation logic
- Entity Framework entities
- Services or interfaces
- Any dependencies except System.*

**References:** Nothing (leaf assembly)

**Examples:**
- `WorkItemDto`, `PullRequestDto`, `PipelineDto`
- `TfsVerificationReport`
- All `*Request` and `*Response` models
- `TfsException` and derived exception types

#### PoTool.Core (Business Logic)
**Purpose:** Contains domain logic, business rules, and application orchestration.

**Contains:**
- Domain services
- Query/Command definitions (MediatR)
- Business logic interfaces (IRepository, ITfsClient)
- Domain exceptions
- Validation logic
- Use case orchestration
- Domain-specific helper classes

**Must NOT contain:**
- Infrastructure concerns (DB, HTTP, File I/O)
- UI components
- EF Core entities (those belong in Api/Persistence)
- DTOs (those belong in Shared)

**References:** PoTool.Shared only

**Examples:**
- `IWorkItemRepository`, `ITfsClient`
- `GetWorkItemsQuery`, `SyncWorkItemsCommand`
- `WorkItemValidationService`
- `WorkItemType`, `WorkItemHierarchyHelper`

#### PoTool.Api (Backend Infrastructure)
**Purpose:** ASP.NET Core backend with controllers, handlers, persistence, and TFS integration.

**Contains:**
- Controllers (HTTP endpoints)
- MediatR Handlers (query/command execution)
- EF Core DbContext and entities
- Repository implementations
- TFS client implementations
- SignalR hubs

**Must NOT contain:**
- UI components
- Client-specific logic

**References:** PoTool.Core + PoTool.Shared

**Examples:**
- `WorkItemsController`, `PullRequestsController`
- `GetWorkItemsQueryHandler`
- `WorkItemRepository`, `RealTfsClient`
- `WorkItemEntity` (EF Core entity)

#### PoTool.Client (Blazor WebAssembly Frontend)
**Purpose:** Browser-based UI using Blazor WebAssembly.

**Contains:**
- Razor components (.razor files)
- Pages and layouts
- Client services (HTTP wrappers around NSwag-generated clients)
- View models
- Client-side state management

**Must NOT contain:**
- Business logic (belongs in Core)
- Direct HTTP API calls (use NSwag-generated clients)
- Database access
- Server-side concerns

**References:** PoTool.Shared only (NO reference to Core)

**Communication:** Via HTTP using NSwag-generated clients in `PoTool.Client.ApiClient` namespace

**Examples:**
- `WorkItemExplorer.razor`, `TfsConfig.razor`
- `WorkItemService`, `ProfileService`
- View-specific models like `TreeNode`

### Why Client Cannot Reference Core

**Reason:** 
1. Client runs in browser (WebAssembly), Core may contain server-side dependencies
2. Creates tight coupling between presentation and business logic
3. Violates separation of concerns - Client should only know about data shapes (DTOs), not business rules
4. Makes it impossible to evolve Core independently of UI

**Instead:** 
- Client uses NSwag-generated API clients which consume Shared DTOs
- All business logic stays in Core
- API handlers map between Core domain models and Shared DTOs

### Enforcement

**Manual:** Code review against this document (see `docs/PROCESS_RULES.md`)

**Automated:** (Future) CI checks will prevent violations of these boundaries

### Validation Checklist

Use this to verify compliance:

- [ ] Shared has no ProjectReferences (except System.*)
- [ ] Core references only Shared
- [ ] Api references Core + Shared
- [ ] Client references only Shared (NOT Core)
- [ ] All DTOs are in Shared
- [ ] All business logic is in Core
- [ ] All EF entities are in Api/Persistence
