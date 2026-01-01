## ARCHITECTURE_RULES.md

This document outlines the core architectural rules and patterns that govern the PoCompanion system.

## 1. Domain-Driven Design (DDD) Principles

### 1.1 Bounded Contexts
- Each major feature area represents a bounded context
- Clear boundaries between contexts with explicit interfaces
- Shared kernel for common domain concepts

### 1.2 Entities and Value Objects
- Entities have unique identifiers and lifecycle
- Value objects are immutable and defined by their attributes
- Rich domain models with behavior, not anemic models

### 1.3 Aggregates
- Each aggregate has a root entity
- External references only through the aggregate root
- Maintain consistency boundaries within aggregates

## 2. Clean Architecture

### 2.1 Dependency Rule
- Dependencies point inward: UI → Application → Domain
- Domain layer has no external dependencies
- Application layer depends only on Domain
- Infrastructure implements interfaces defined in Application/Domain

### 2.2 Layer Responsibilities
- **Domain**: Business logic, entities, value objects, domain services
- **Application**: Use cases, application services, DTOs, interfaces
- **Infrastructure**: Data access, external services, framework concerns
- **Presentation**: API controllers, SignalR hubs, UI concerns

## 3. CQRS Pattern

### 3.1 Command-Query Separation
- Commands change state, return void or minimal confirmation
- Queries return data, never modify state
- Clear separation in code structure

### 3.2 MediatR Usage
- All commands and queries go through MediatR
- Handlers are independent and testable
- Cross-cutting concerns via pipeline behaviors

## 4. Repository Pattern

### 4.1 Generic Repository
- IRepository<T> for common CRUD operations
- Specific repositories extend for specialized queries
- Repository interfaces in Domain/Application layer
- Implementation in Infrastructure layer

### 4.2 Unit of Work
- Transaction management at application service level
- Coordinate multiple repository operations
- Ensure data consistency across aggregates

## 5. API Design

### 5.1 RESTful Principles
- Resource-based URLs
- Proper HTTP verbs (GET, POST, PUT, DELETE)
- Status codes convey meaning
- Versioning strategy in place

### 5.2 Real-time Communication
- SignalR for server-to-client updates
- Hub per feature area
- Strongly-typed hub interfaces
- Connection lifecycle management

## 6. Data Access

### 6.1 Entity Framework Core
- Code-first approach with migrations
- DbContext per bounded context or aggregate
- No lazy loading (explicit eager or projection)
- Query optimization with AsNoTracking for read-only

### 6.2 Database Design
- Normalized schema
- Foreign key constraints
- Appropriate indexes
- Audit fields (CreatedAt, UpdatedAt, etc.)

## 7. Error Handling

### 7.1 Exception Strategy
- Domain exceptions for business rule violations
- Application exceptions for use case failures
- Infrastructure exceptions for technical issues
- Global exception handling middleware

### 7.2 Validation
- Input validation at API boundary
- Business rule validation in domain layer
- FluentValidation for complex validation rules
- Return meaningful error messages

## 8. Configuration and Environment

### 8.1 Configuration Management
- appsettings.json for base configuration
- appsettings.{Environment}.json for environment-specific
- User secrets for local development
- Environment variables for production secrets

### 8.2 HTTP-only Development Rule

**Overview**: The PoCompanion system is configured to use HTTP-only in development environments to avoid certificate complexity and related development friction.

**Configuration Locations**:
1. **appsettings.json** / **appsettings.Development.json**
   - API base URL: `http://localhost:5291`
   - All service URLs must use HTTP scheme

2. **launchSettings.json** (API project)
   - Configure Kestrel to listen on HTTP only
   - Application URL: `http://localhost:5291`
   - Remove or comment out HTTPS URLs

3. **SignalR Hub URLs**
   - Hub connections use HTTP URLs: `http://localhost:5291/hubs/[hubname]`
   - Client-side hub connection configuration

4. **CORS Configuration** (ApiServiceCollectionExtensions.cs)
   - Allow HTTP origins in development
   - `WithOrigins("http://localhost:5291")`
   - Ensure AllowCredentials() for SignalR

**Re-enabling HTTPS for Production**:

When moving to production or if HTTPS is required:

1. **Kestrel Configuration**
   - Update launchSettings.json or Kestrel configuration to enable HTTPS
   - Configure SSL certificate (self-signed for dev, trusted for production)

2. **Base Addresses**
   - Update all `http://` URLs to `https://`
   - Check appsettings files, client configuration, hub URLs

3. **CORS Configuration**
   - Update allowed origins to use HTTPS scheme
   - Review credential policies for cross-origin requests

4. **SignalR URLs**
   - Update hub connection URLs to HTTPS
   - Verify WebSocket upgrade works over HTTPS

5. **Middleware**
   - Enable `app.UseHttpsRedirection()` in pipeline
   - Consider HSTS middleware for production

**Rationale**:
- **Development Benefits**: Eliminates certificate trust issues, mixed content warnings, and CORS complications during local development
- **Rapid Iteration**: Developers can run the system immediately without certificate setup
- **Browser Compatibility**: Avoids localhost certificate browser warnings
- **SignalR Reliability**: HTTP WebSocket connections are simpler to debug

**Important**: Production deployments MUST use HTTPS for security. This HTTP-only configuration is strictly for development environments. Never deploy to production without proper HTTPS/TLS configuration.

## 9. Testing Strategy

### 9.1 Unit Tests
- Test domain logic in isolation
- Mock dependencies using interfaces
- High coverage for business-critical code
- Fast and independent tests

### 9.2 Integration Tests
- Test API endpoints end-to-end
- In-memory database for data access tests
- Test SignalR hub interactions
- Validate cross-cutting concerns

### 9.3 Test Organization
- Mirror production code structure
- Arrange-Act-Assert pattern
- Descriptive test names
- One assertion per test (where practical)

## 10. Code Quality

### 10.1 Naming Conventions
- PascalCase for classes, methods, properties
- camelCase for parameters, local variables
- Meaningful, descriptive names
- Avoid abbreviations unless widely known

### 10.2 Code Structure
- Single Responsibility Principle
- Small, focused methods and classes
- Avoid deep nesting (max 3 levels)
- Keep files under 300 lines when possible

### 10.3 Comments and Documentation
- XML comments for public APIs
- Explain "why" not "what" in comments
- Keep comments up-to-date with code
- README files for complex subsystems

## 11. Security

### 11.1 Authentication & Authorization
- JWT tokens for API authentication
- Role-based or claims-based authorization
- Secure token storage
- Token expiration and refresh strategy

### 11.2 Data Protection
- Sensitive data encryption at rest
- Secure communication channels (TLS)
- Input sanitization to prevent injection
- Principle of least privilege

## 12. Performance

### 12.1 Optimization Guidelines
- Profile before optimizing
- Cache frequently accessed data
- Async/await for I/O operations
- Pagination for large data sets

### 12.2 SignalR Performance
- Connection pooling
- Backplane for scale-out scenarios
- Message size optimization
- Connection lifecycle management

## Compliance and Updates

This document should be:
- Reviewed quarterly or when significant architectural decisions are made
- Updated to reflect new patterns or rule changes
- Referenced in code reviews to ensure adherence
- Used for onboarding new team members

**Last Updated**: 2026-01-01