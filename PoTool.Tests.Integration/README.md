# Integration Tests - PO Companion

This project contains integration tests using **Reqnroll** (BDD framework) and **MSTest**.

## Purpose

Integration tests verify end-to-end API behavior and ensure:
- All Web API endpoints work correctly
- All SignalR hub methods function as expected
- Request/response flows are validated
- API contracts are maintained

## Architecture

- **Framework**: Reqnroll (Gherkin-based BDD)
- **Test Runner**: MSTest
- **Test Approach**: Full ASP.NET Core Web API testing
- **Database**: In-memory database for each test
- **TFS Mock**: File-based mock data (no real TFS calls)

## Structure

```
PoTool.Tests.Integration/
├── Features/              # .feature files (Gherkin scenarios)
├── StepDefinitions/       # C# step implementations
├── Support/               # Test infrastructure
│   ├── IntegrationTestWebApplicationFactory.cs
│   └── MockTfsClient.cs
└── reqnroll.json         # Reqnroll configuration
```

## Coverage Requirements

Per Architecture Rules section 10.2, integration tests MUST:

- ✅ Cover **100% of Web API endpoints**
- ✅ Cover **100% of SignalR hub methods**
- ✅ Test happy paths
- ✅ Test error conditions
- ✅ Test validation failures
- ✅ Use file-based TFS mocks (no real TFS)

## Running Tests

```bash
# Run all integration tests
dotnet test PoTool.Tests.Integration

# Run specific feature
dotnet test --filter "FullyQualifiedName~TfsConfiguration"

# Run with detailed output
dotnet test PoTool.Tests.Integration --logger "console;verbosity=detailed"
```

## Test Independence

- Each scenario is independent
- Database is reset between scenarios
- No test depends on execution order
- Each test cleans up its own data

## Current Coverage

### API Endpoints
- [x] GET /api/tfsconfig
- [x] POST /api/tfsconfig
- [x] GET /api/tfsvalidate
- [ ] GET /api/workitems
- [ ] GET /api/workitems/{id}
- [ ] POST /api/workitems/sync
- [ ] GET /api/settings
- [ ] PUT /api/settings
- [ ] GET /health

### SignalR Hubs
- [ ] WorkItemHub.SyncStatus
- [ ] WorkItemHub.RequestSync

## Adding New Tests

1. Create a `.feature` file in `Features/`
2. Write scenarios in Gherkin syntax
3. Generate step definitions (Reqnroll will help)
4. Implement step definitions in `StepDefinitions/`
5. Run and verify

## Example Feature

```gherkin
Feature: TFS Configuration Management
    As a user
    I want to configure my TFS connection
    So that I can connect to Azure DevOps

Scenario: Save TFS configuration
    Given I have valid TFS credentials
    When I save the TFS configuration
    Then the configuration should be saved successfully
```

## Notes

- TFS client is always mocked in integration tests
- Database is always in-memory
- No mocking of Api, Core, or database layers
- Tests exercise HTTP endpoints as external clients would
