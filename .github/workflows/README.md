# GitHub Actions Workflows

This repository contains several GitHub Actions workflows for continuous integration, deployment, and testing.

## Workflows

### 1. Build and Test Gates (`build.yml`)
- **Trigger:** Pull requests to all branches; pushes to `main` and `release/**`
- **Purpose:** Enforce repository test governance through explicit CI gates
- **Jobs:**
  - `Core Gate`
  - `API Contract Gate`
  - `Governance Gate`
- **Outputs:** Per-job TRX results, full console logs, and failing-test summaries in the Actions artifacts section

### 2. Release (`release.yml`)
- **Trigger:** Version tags (e.g., `v1.0.0`)
- **Purpose:** Automated releases when version tags are pushed
- **Outputs:** GitHub releases with published artifacts

### 3. Exploratory Tests (`exploratory-tests.yml`)
- **Trigger:** Manual (`workflow_dispatch`)
- **Purpose:** Automated exploratory testing with Playwright
- **Duration:** ~10-15 minutes
- **Outputs:** 
  - Test report (markdown)
  - Screenshots of all features
  - Test execution logs

## Exploratory Tests Workflow

### How to Run Manually

1. Go to the **Actions** tab in the GitHub repository
2. Click on **Exploratory Tests** in the left sidebar
3. Click the **Run workflow** button (top right)
4. Select the branch you want to test (usually `main`)
5. Click **Run workflow**

### What It Does

The exploratory tests workflow:

1. ✅ Sets up .NET 10 environment
2. ✅ Restores and builds the solution
3. ✅ Installs Playwright browsers for automation
4. ✅ Starts the API server (with mock data)
5. ✅ Starts the Client application
6. ✅ Runs automated tests for all 10 features:
   - Home Page
   - TFS Configuration
   - Backlog Health
   - Effort Distribution
   - PR Insights
   - State Timeline
   - Epic Forecast
   - Dependency Graph
   - Velocity Dashboard
   - Settings Modal
7. ✅ Captures screenshots at each step
8. ✅ Generates a comprehensive test report
9. ✅ Uploads all results as artifacts

### Viewing Test Results

After the workflow completes:

1. Go to the workflow run in the **Actions** tab
2. Scroll down to the **Artifacts** section at the bottom
3. Download the artifacts:
   - `exploratory-test-results` - Contains the test report and screenshots
   - `api-logs` - API server logs for troubleshooting
   - `client-logs` - Client application logs

### Test Report

The test report (`AUTOMATED_TEST_REPORT.md`) includes:

- ✅ Test execution timestamp
- ✅ Summary (passed/failed tests, success rate)
- ✅ Performance metrics (execution time, page load times)
- ✅ Detailed results for each feature
- ✅ Screenshots embedded in markdown
- ✅ Error messages for failed tests

### Running Tests Locally

To run the exploratory tests on your local machine:

```bash
# 1. Build the solution
dotnet build PoTool.sln --configuration Release

# 2. Install Playwright browsers (first time only)
cd PoTool.Tests.AutomatedExploratory
dotnet build
pwsh bin/Release/net10.0/playwright.ps1 install chromium
cd ..

# 3. Start the API server (in one terminal)
cd PoTool.Api
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --no-build --configuration Release --urls http://localhost:5000

# 4. Start the Client (in another terminal)
cd PoTool.Client
dotnet run --no-build --configuration Release

# 5. Run the tests (in a third terminal)
cd PoTool.Tests.AutomatedExploratory
dotnet test --configuration Release

# 6. View the results
cd bin/Release/net10.0/test-results
# Open AUTOMATED_TEST_REPORT.md in a markdown viewer
```

### Troubleshooting

#### Workflow Fails to Start Servers

- Check the `api-logs` and `client-logs` artifacts
- Ensure the mock data configuration is correct
- Verify that ports 5000 and 5001 are not in use

#### Tests Fail Due to Timeouts

- Increase the timeout values in `ExploratoryTests.cs` (DefaultTimeout constant)
- Check if the application is taking longer to load in the CI environment

#### Screenshots Are Missing

- Ensure the test-results directory is being created
- Check that Playwright has write permissions
- Review test execution logs for errors

#### No Artifacts Uploaded

- Verify the workflow completed (even with failures)
- Check the "Upload test results" step in the workflow run
- Ensure at least one test executed before the workflow failed

### Configuration

The tests use the following configuration:

- **Base URL:** `http://localhost:5001`
- **API URL:** `http://localhost:5000`
- **Browser:** Chromium (headless mode in CI)
- **Test Framework:** MSTest
- **Automation:** Playwright for .NET
- **Data Mode:** Mock data (no real TFS connection)

### Adding More Tests

To add more test scenarios:

1. Open `PoTool.Tests.AutomatedExploratory/ExploratoryTests.cs`
2. Add a new test method following the existing pattern:

```csharp
[TestMethod]
[TestCategory("UI")]
[Priority(11)]
public async Task Test11_NewFeature()
{
    var testName = "11-NewFeature";
    var startTime = DateTime.UtcNow;
    var screenshots = new List<string>();

    try
    {
        await Page.GotoAsync($"{BaseUrl}/new-feature", 
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var screenshot = await s_infrastructure!.CaptureScreenshotAsync(
            Page, "11-new-feature", "New Feature description");
        screenshots.Add(screenshot);

        var duration = DateTime.UtcNow - startTime;
        s_infrastructure.LogTestResult(testName, true, duration, null, screenshots);
    }
    catch (Exception ex)
    {
        var duration = DateTime.UtcNow - startTime;
        s_infrastructure!.LogTestResult(testName, false, duration, ex.ToString(), screenshots);
        throw;
    }
}
```

3. Rebuild and run the tests

### CI/CD Integration

The exploratory tests workflow is designed to be run manually, but you can also:

- **Schedule regular runs:** Uncomment the `schedule` trigger in the workflow
- **Run on PR:** Add the workflow to run on pull requests
- **Add status badge:** Add a badge to the README showing the last test status

Example badge:
```markdown
![Exploratory Tests](https://github.com/your-org/your-repo/workflows/Exploratory%20Tests/badge.svg)
```

### Best Practices

1. ✅ Run exploratory tests before major releases
2. ✅ Review screenshots to verify UI rendering
3. ✅ Check performance metrics for regression
4. ✅ Update tests when new features are added
5. ✅ Keep mock data synchronized with test scenarios
6. ✅ Document any known issues in test results

### Support

## Local gate commands

Run the same gate entry points used by CI:

```bash
./.github/scripts/run-core-gate.sh
./.github/scripts/run-api-contract-gate.sh
./.github/scripts/run-governance-gate.sh
```

Each script writes TRX, console logs, and failing-test summaries under `/tmp/po-test-gates/...` by default.

## Branch protection handoff

Repository admins should configure branch protection using the exact CI job names, not just the workflow name.

- Handoff instructions: `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-branch-protection-handoff.md`
- Operational hardening report: `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-operational-enforcement-hardening.md`

For issues with the exploratory tests workflow:

1. Check the workflow logs in the Actions tab
2. Download and review artifacts (logs and test results)
3. Run tests locally to reproduce the issue
4. Refer to the troubleshooting section above
5. Open an issue with workflow run details
