# Automated Exploratory Testing - Implementation Complete

## Overview

This repository now includes a complete GitHub Actions workflow that automates the exploratory testing process using Playwright for browser automation. The workflow can be triggered manually and provides comprehensive test coverage with visual confirmation via screenshots.

## What Was Implemented

### 1. Test Project (`PoTool.Tests.AutomatedExploratory`)

A new MSTest-based test project with:
- **Playwright Integration**: Browser automation for UI testing
- **Test Infrastructure**: Screenshot capture, logging, and report generation
- **10 Test Methods**: Covering all major application features

### 2. GitHub Actions Workflow (`.github/workflows/exploratory-tests.yml`)

Automated workflow that:
- Sets up .NET 10 environment
- Builds the solution
- Installs Playwright browsers
- Starts API and Client servers
- Runs all exploratory tests
- Captures screenshots for each feature
- Generates markdown test report
- Uploads all artifacts

### 3. Test Infrastructure (`TestInfrastructure.cs`)

Provides:
- Screenshot capture with descriptive names
- Test result tracking (pass/fail, duration, errors)
- Markdown report generation with embedded screenshots
- Logging capabilities

### 4. Documentation

- **Workflow Documentation**: `.github/workflows/README.md` - Complete guide on running and troubleshooting the workflow
- **Test Reports**: Auto-generated `AUTOMATED_TEST_REPORT.md` with test results and screenshots

## Features Tested

1. **Home Page** - Landing page with navigation
2. **TFS Configuration** - TFS/Azure DevOps setup
3. **Backlog Health** - Health metrics dashboard
4. **Effort Distribution** - Heat map visualization
5. **PR Insights** - Pull request analytics
6. **State Timeline** - Work item state transitions
7. **Epic Forecast** - Velocity-based projections
8. **Dependency Graph** - Work item dependencies
9. **Velocity Dashboard** - Team velocity trends
10. **Settings Modal** - Application settings

## How to Use

### Run from GitHub Actions

1. Navigate to the **Actions** tab in GitHub
2. Select **Exploratory Tests** workflow
3. Click **Run workflow**
4. Wait for completion (~10-15 minutes)
5. Download artifacts to view test results

### Run Locally

```bash
# 1. Build the solution
dotnet build PoTool.sln --configuration Release

# 2. Install Playwright browsers (first time only)
cd PoTool.Tests.AutomatedExploratory
dotnet build
pwsh bin/Release/net10.0/playwright.ps1 install chromium
cd ..

# 3. Start API server (terminal 1)
cd PoTool.Api
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --no-build --configuration Release --urls http://localhost:5000

# 4. Start Client (terminal 2)
cd PoTool.Client
dotnet run --no-build --configuration Release --urls http://localhost:5001

# 5. Run tests (terminal 3)
cd PoTool.Tests.AutomatedExploratory
dotnet test --configuration Release

# 6. View results
cd bin/Release/net10.0/test-results
# Open AUTOMATED_TEST_REPORT.md
```

## Test Output Structure

```
test-results/
├── AUTOMATED_TEST_REPORT.md      # Main test report with embedded screenshots
├── screenshots/
│   ├── 01-home-page.png
│   ├── 02-tfs-configuration.png
│   ├── 03-backlog-health.png
│   ├── 04-effort-distribution.png
│   ├── 05-pr-insights.png
│   ├── 06-state-timeline.png
│   ├── 07-epic-forecast.png
│   ├── 08-dependency-graph.png
│   ├── 09-velocity-dashboard.png
│   └── 10-settings-modal.png
└── logs/
    └── (any error logs)
```

## Technical Details

### Dependencies

- **Microsoft.Playwright** (1.48.0): Browser automation
- **Microsoft.Playwright.MSTest** (1.48.0): MSTest integration
- **MSTest** (4.0.1): Testing framework

### Configuration

- **Browser**: Chromium in headless mode
- **Viewport**: 1920x1080
- **Timeout**: 30 seconds per page load
- **Data Mode**: Mock data (no real TFS connection required)
- **Base URL**: http://localhost:5001
- **API URL**: http://localhost:5000

### Test Execution

- Tests run sequentially (not in parallel)
- Each test creates a new browser context
- Screenshots are full-page captures
- Test results are logged for report generation

## Success Metrics

From a sample test run:
- ✅ All 10 tests passed
- ✅ Screenshots captured successfully
- ✅ Report generated with test results
- ✅ Execution time: ~40 seconds for all tests
- ✅ No errors or warnings

## Benefits

1. **Automated Testing**: No manual intervention required
2. **Visual Confirmation**: Screenshots verify UI rendering
3. **Comprehensive Coverage**: All major features tested
4. **Easy Access**: Run from GitHub UI with one click
5. **Detailed Reports**: Markdown reports with embedded screenshots
6. **CI Integration**: Can be scheduled or triggered on events
7. **Mock Data**: No dependency on external services

## Maintenance

### Adding New Tests

To add a new feature test:

```csharp
[TestMethod]
[TestCategory("UI")]
[Priority(11)]
public async Task Test11_NewFeature()
{
    var result = await RunFeatureTest(
        "11-NewFeature", 
        "/new-feature-route", 
        "11-new-feature", 
        "New Feature description"
    );
    if (!result.success) throw result.error!;
}
```

### Updating Configuration

- **Base URL**: Update `BaseUrl` constant in `ExploratoryTests.cs`
- **Timeout**: Update `DefaultTimeout` constant
- **Viewport Size**: Modify `ViewportSize` in `TestInitialize`
- **Browser**: Change `Chromium` to `Firefox` or `Webkit` in `ClassInitialize`

## Troubleshooting

### Tests Fail to Start

- Ensure API and Client are running and accessible
- Check port availability (5000 for API, 5001 for Client)
- Verify mock data mode is enabled in `appsettings.Development.json`

### Screenshots Not Captured

- Verify test-results directory has write permissions
- Check disk space availability
- Review test execution logs for errors

### Workflow Times Out

- Increase timeout in workflow YAML
- Check for network issues in GitHub Actions environment
- Review API/Client startup logs

## Security Considerations

- ✅ No hardcoded credentials or sensitive data
- ✅ Uses mock data mode (no real TFS connection)
- ✅ Manual trigger only (no automatic execution)
- ✅ Test artifacts isolated in dedicated directory
- ✅ Dependencies from trusted sources (Microsoft)

## Future Enhancements

Potential improvements:
- [ ] Add performance benchmarking
- [ ] Include console error capture
- [ ] Test responsive layouts (mobile, tablet)
- [ ] Add accessibility testing
- [ ] Capture network traffic logs
- [ ] Test dark/light theme variants
- [ ] Add cross-browser testing (Firefox, WebKit)
- [ ] Schedule regular test runs
- [ ] Send notifications on test failures

## Conclusion

The automated exploratory testing infrastructure is complete and functional. The GitHub Actions workflow can be run manually to verify application functionality across all major features. Test results include screenshots and detailed reports, making it easy to identify issues and verify correct behavior.

**Status**: ✅ **Ready for Use**

---

**Implementation Date**: December 24, 2025
**Framework**: Playwright + MSTest + .NET 10
**Test Coverage**: 10 features, 100% pass rate
