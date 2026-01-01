# Mock Data Implementation Guide

## Overview

The Battleship Mock Data Generation System provides a comprehensive, themati data generation solution for PO Companion that strictly follows the Battleship Incident Handling theme and enforces all data quality rules defined in `mock-data-rules.md`.

## Architecture

### Components

The system consists of four main generators and a coordinating facade:

1. **BattleshipWorkItemGenerator** - Generates work item hierarchy (Goals → Objectives → Epics → Features → PBIs/Bugs → Tasks)
2. **BattleshipDependencyGenerator** - Creates dependency links between work items with 30-40% cross-team dependencies
3. **BattleshipPullRequestGenerator** - Generates pull requests with full metadata including reviews, comments, and file changes
4. **MockDataValidator** - Validates all generated data against rules from mock-data-rules.md
5. **BattleshipMockDataFacade** - Coordinates all generators with hybrid caching for performance

### Data Volumes

The system generates:
- **Work Items**: ~19,000-23,000 total
  - 10 Goals (exact)
  - 25-35 Objectives
  - 80-120 Epics
  - 400-600 Features
  - 2,500-3,500 PBIs
  - 800-1,200 Bugs
  - 12,000-18,000 Tasks
- **Dependencies**: 15,000-20,000 links (30-40% cross-team)
- **Pull Requests**: 150 PRs with full metadata

### Performance

- Initial generation: < 5 seconds for complete dataset
- Cached access: Near-instant (singleton pattern)
- Validation: ~1 second for full dataset

## Usage

### Dependency Injection

The system is registered in `ApiServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<BattleshipWorkItemGenerator>();
services.AddSingleton<BattleshipDependencyGenerator>();
services.AddSingleton<BattleshipPullRequestGenerator>();
services.AddSingleton<MockDataValidator>();
services.AddSingleton<BattleshipMockDataFacade>();
```

### Accessing Mock Data

#### From Handlers

```csharp
public class MyHandler
{
    private readonly BattleshipMockDataFacade _mockDataFacade;

    public MyHandler(BattleshipMockDataFacade mockDataFacade)
    {
        _mockDataFacade = mockDataFacade;
    }

    public async Task Handle()
    {
        // Get all work items
        var workItems = _mockDataFacade.GetMockHierarchy();

        // Get work items for specific goals
        var goalIds = new List<int> { 1000, 1001 };
        var filtered = _mockDataFacade.GetMockHierarchyForGoals(goalIds);

        // Get pull requests
        var prs = _mockDataFacade.GetMockPullRequests();

        // Get dependencies
        var deps = _mockDataFacade.GetMockDependencies();
    }
}
```

#### Validation

```csharp
// Validate all generated data
var report = _mockDataFacade.ValidateData();
Console.WriteLine(report.GetSummary());

// Check if data is valid
if (report.IsValid())
{
    Console.WriteLine("All validations passed!");
}
```

### Cache Management

```csharp
// Pre-generate all data (warmup)
_mockDataFacade.WarmupCache();

// Invalidate cache and force regeneration
_mockDataFacade.InvalidateCache();
```

## Battleship Theme

All generated data follows the Battleship Incident Handling theme:

### Example Work Items

- **Goal**: "Mission-Ready Incident Response Platform"
- **Objective**: "Rapid Fire Detection and Automated Suppression"
- **Epic**: "Engine Room Fire Detection System - Sensor Network"
- **Feature**: "Multi-Sensor Real-Time Fire Detection and Suppression System Component"
- **PBI**: "As a damage control officer, I need to view real-time status for Engine Room Component"
- **Bug**: "Multi-Sensor Real-Time Fire Detection Component not triggering alerts"
- **Task**: "Implement API endpoints for Multi-Sensor Component"

### Team Structure

Teams are organized in a realistic hierarchy:

```
Battleship Systems (Portfolio)
  ├── Incident Detection (Program)
  │   ├── Fire Detection (Feature Team)
  │   ├── Leakage Monitoring (Feature Team)
  │   └── Collision Detection (Feature Team)
  ├── Incident Response (Program)
  │   ├── Emergency Protocols (Feature Team)
  │   ├── Crew Safety (Feature Team)
  │   └── Medical Response (Feature Team)
  ├── Damage Control (Program)
  │   ├── Hull Integrity (Feature Team)
  │   ├── Repair Coordination (Feature Team)
  │   └── Resource Management (Feature Team)
  └── Shared Services
      ├── Communication Systems (Shared Services Team)
      └── DevOps & Infrastructure (Shared Services Team)
```

## Area Path Inheritance

**Critical Rule**: Epic determines team ownership, and ALL descendants must inherit the same area path.

Example:
```
Epic (Area: \Battleship Systems\Incident Detection\Fire Detection)
  └── Feature (Area: \Battleship Systems\Incident Detection\Fire Detection) ← Inherited
      └── PBI (Area: \Battleship Systems\Incident Detection\Fire Detection) ← Inherited
          └── Task (Area: \Battleship Systems\Incident Detection\Fire Detection) ← Inherited
```

This ensures clean team backlogs with no area path mixing.

## Dependencies

### Cross-Team Dependencies

30-40% of dependencies cross team boundaries, modeling realistic inter-team coordination:

```
PBI-1234 (Crew Safety team)
  └── Predecessor: PBI-987 (Hull Integrity team) [Cross-team]
  └── Successor: PBI-2456 (Communication Systems team) [Cross-team]
```

### Link Types

- **Predecessor** (40%): Must complete before dependent starts
- **Successor** (40%): Dependent that starts after current
- **Related** (15%): Related but no blocking relationship
- **Duplicate** (5%): Duplicate work items

### Invalid Dependencies for Testing

The system intentionally generates 5-10% invalid dependencies for testing validation features:
- Circular dependencies (A → B → C → A)
- Orphaned links (links to non-existent work items)
- Self-dependencies (item depends on itself)
- Temporal violations (current sprint depends on future sprint)

## Pull Requests

### Full Metadata

Each PR includes:
- Title, description, status (active/completed/abandoned)
- Creator, created date, completed date
- Source/target branches, repository name
- 1-5 reviewers with votes (Approved, Rejected, Waiting)
- 0-20 comment threads with resolution tracking
- 1-50 file changes per iteration
- Work item links (70-80% of PRs linked to work items)
- Labels (bug-fix, feature, high-priority, etc.)

### Example PR

```
PR-1001: "Add fire detection sensor integration"
  Status: Completed
  Repository: Battleship-Incident-Backend
  Creator: alice.johnson@battleship.mil
  Reviewers:
    - bob.smith@battleship.mil (Approved)
    - charlie.davis@battleship.mil (Approved with suggestions)
  Work Items:
    - PBI-1234 (Implement real-time sensor data aggregation)
    - PBI-1235 (Add automated alert trigger logic)
  Comments: 5 threads (all resolved)
  Files Changed: 12
  Lines Added: 245
  Lines Deleted: 78
```

## Validation

The `MockDataValidator` validates all rules from `mock-data-rules.md`:

### Validation Checks

1. **Hierarchy Integrity** - Every child has valid parent
2. **Area Path Consistency** - Epic inheritance enforced
3. **Iteration Path Validity** - All paths exist and are valid
4. **State Validity** - States match work item types
5. **Estimation Validity** - Fibonacci values used
6. **Dependency Integrity** - Valid references, cross-team %
7. **PR Integrity** - Required fields, work item links

### Validation Report

```
Mock Data Validation Report
========================================
Work Items:
  Total: 19640
  Goals: 10 (Valid: True)
  Objectives: 30 (Valid: True)
  Epics: 100 (Valid: True)
  Features: 500 (Valid: True)
  PBIs: 3000 (Valid: True)
  Bugs: 1000 (Valid: True)
  Tasks: 15000 (Valid: True)

Data Quality:
  Hierarchy Integrity: True (Orphaned: 0)
  Area Path Consistency: True (Violations: 0)
  Iteration Path Valid: True (Invalid: 0)
  State Validity: True (Invalid: 0)
  Fibonacci Estimation: True (Non-Fibonacci: 0)
  Unestimated: 25.0%

Dependencies:
  Total: 18500
  Cross-Team: 35.2%
  Invalid: 7.5%

Pull Requests:
  Total: 150 (Valid: True)
  Active: 18.0%
  Completed: 72.0%
  Abandoned: 10.0%
  With Work Item Links: 75.0%
  Metadata Valid: True

Theme Validation:
  Battleship Theme: True (10/10 goals compliant)

Overall Valid: True
```

## Testing

### Unit Tests

Located in `PoTool.Tests.Unit/Services/MockData/`:
- `BattleshipWorkItemGeneratorTests.cs` - 13 tests for work item generation
- `MockDataValidatorTests.cs` - 12 tests for validation logic

### Running Tests

```bash
dotnet test --filter "FullyQualifiedName~BattleshipWorkItemGeneratorTests"
dotnet test --filter "FullyQualifiedName~MockDataValidatorTests"
```

## Migration from Old System

### Old Providers (Deprecated)

- `MockDataProvider.cs` - OLD system with wrong theme
- `MockPullRequestDataProvider.cs` - OLD system with insufficient data

### New System (Current)

- `BattleshipMockDataFacade.cs` - NEW unified system

### Handler Updates

All handlers have been updated to use the new facade:
- `GetGoalHierarchyQueryHandler.cs` ✓
- `SyncPullRequestsCommandHandler.cs` ✓
- `MockTfsClient.cs` ✓

## Extensibility

### Adding New Data Scenarios

To add new mock data scenarios:

1. Create a new generator in `PoTool.Api/Services/MockData/`
2. Register it in DI
3. Add accessor methods to `BattleshipMockDataFacade`
4. Update `MockDataValidator` if new validations are needed
5. Add tests in `PoTool.Tests.Unit/Services/MockData/`

### Customizing Generation

Generators use controlled randomization with a consistent seed (42) for reproducible data. To customize:

```csharp
public class CustomWorkItemGenerator : BattleshipWorkItemGenerator
{
    public CustomWorkItemGenerator() : base()
    {
        // Use different seed for variety
        _random = new Random(123);
    }

    // Override methods to customize generation
}
```

## Troubleshooting

### Cache Issues

If data seems stale:
```csharp
_mockDataFacade.InvalidateCache();
```

### Performance Issues

If generation is slow:
```csharp
// Pre-warm cache on startup
_mockDataFacade.WarmupCache();
```

### Validation Failures

Check the validation report for details:
```csharp
var report = _mockDataFacade.ValidateData();
Console.WriteLine(report.GetSummary());
```

## Future Enhancements

Potential improvements:
1. Configurable data volumes
2. Multiple theme options
3. Time-based data evolution (simulating sprint progression)
4. Custom validation rules
5. Export/import for sharing mock data sets
6. Performance dataset generation (50,000+ work items)

## References

- `docs/mock-data-rules.md` - Authoritative rules for mock data
- `docs/ARCHITECTURE_RULES.md` - Architecture conventions
- `PoTool.Api/Services/MockData/` - Implementation code
- `PoTool.Tests.Unit/Services/MockData/` - Unit tests
