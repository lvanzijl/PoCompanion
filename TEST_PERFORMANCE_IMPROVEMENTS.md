# Test Performance Improvements

## Summary

This document details the performance optimizations made to the test suite to address slow test execution times.

## Changes Made

### 1. Integration Tests - Shared WebApplicationFactory (MAJOR)

**Problem**: Each of the 15 step definition classes in the integration tests was creating its own `IntegrationTestWebApplicationFactory` instance. This meant that for each test scenario using multiple step classes, multiple web servers were being spun up, causing significant performance overhead.

**Solution**: 
- Created `SharedTestContext` class that provides a single, lazily-initialized `IntegrationTestWebApplicationFactory` instance
- Updated all 15 step definition classes to accept `SharedTestContext` through constructor dependency injection
- The factory is now reused across all test scenarios

**Files Modified**:
- Created: `PoTool.Tests.Integration/Support/SharedTestContext.cs`
- Modified: All 15 files in `PoTool.Tests.Integration/StepDefinitions/`:
  - DependencyGraphControllerSteps.cs
  - ErrorScenarioSteps.cs
  - FilteringControllerSteps.cs
  - HealthCalculationControllerSteps.cs
  - HealthSteps.cs
  - MetricsControllerSteps.cs
  - ProfilesControllerSteps.cs
  - PullRequestsControllerSteps.cs
  - SettingsControllerSteps.cs
  - SettingsSteps.cs
  - SignalRHubSteps.cs
  - TfsConfigurationSteps.cs
  - TfsVerificationSteps.cs
  - ValidationEnhancementsSteps.cs
  - WorkItemsControllerSteps.cs

**Expected Impact**: Significant reduction in test execution time by eliminating redundant web server startup overhead. Each scenario now reuses the same server instance.

### 2. Unit Tests - Reduced Timestamp Ordering Delays

**Problem**: Tests for TfsConfigurationService were using `Task.Delay(100)` and `Task.Delay(50)` to ensure different timestamps for ordering tests.

**Solution**: 
- Reduced delays from 100ms to 10ms
- Reduced delays from 50ms to 10ms
- Modern `DateTimeOffset.UtcNow` has sufficient precision (millisecond or better) that 10ms is adequate for timestamp differentiation

**Files Modified**:
- `PoTool.Tests.Unit/TfsConfigurationServiceSqliteTests.cs` (3 delays reduced)
- `PoTool.Tests.Unit/TfsConfigurationServiceTests.cs` (1 delay reduced)

**Expected Impact**: Saves approximately 270ms per test run (3×90ms + 1×40ms) across 4 affected tests.

### 3. SQLite Tests - In-Memory Database

**Problem**: SQLite tests were creating actual database files on disk, including:
- Creating temp file path
- Disk I/O for database creation
- Disk I/O for EnsureCreated
- Disk I/O for EnsureDeleted
- File deletion cleanup

**Solution**: 
- Changed from disk-based SQLite (`Data Source={path}`) to in-memory SQLite (`Data Source=:memory:`)
- Database.OpenConnection() keeps the in-memory database alive for test duration
- Database.CloseConnection() properly disposes of it
- Still tests actual SQLite provider behavior (not EF InMemory provider)

**Files Modified**:
- `PoTool.Tests.Unit/TfsConfigurationServiceSqliteTests.cs`

**Expected Impact**: Eliminates all disk I/O overhead while maintaining test validity. In-memory SQLite is significantly faster than disk-based for test scenarios.

### 4. Blazor Test Delays - No Changes

**Decision**: The `Task.Delay(400)` in `WorkItemToolbarTests.cs` was left unchanged. This delay is necessary to wait for a debounce timer (300ms) + buffer (100ms) in the actual UI component. Reducing this could make tests flaky.

## Known Issues

### Build Error - NSwag Generated Code

There's a pre-existing issue where NSwag generates incorrect default value assignments for integer properties in `PoTool.Client/ApiClient/ApiClient.g.cs`:

```csharp
public int DefaultPictureId { get; set; } = "0";  // Error: string to int
public int TimeoutSeconds { get; set; } = "30";   // Error: string to int
```

This is caused by the OpenAPI specification having ambiguous type definitions:
```json
"type": ["integer", "string"],
"default": 0
```

**This issue is unrelated to the test performance improvements** and was present before these changes. It prevents building and running tests but should be addressed separately.

## Expected Overall Impact

- **Integration Tests**: Major improvement - eliminated N×(server startup time) where N is the number of step definition classes used per scenario
- **Unit Tests**: Minor improvement - ~270ms savings from reduced delays
- **SQLite Tests**: Moderate improvement - eliminated disk I/O overhead for database operations

The most significant improvement is the shared WebApplicationFactory for integration tests, which eliminates redundant server startup overhead that was likely the main cause of slow test execution.

## Testing Validation

Due to the pre-existing build errors in the generated API client code, full test execution could not be validated. However, the changes made are:
- Low-risk refactorings (dependency injection)
- Straightforward optimizations (reduced delays, in-memory database)
- Do not change test logic or assertions

Once the NSwag/OpenAPI issues are resolved, tests should run successfully and demonstrate improved performance.
