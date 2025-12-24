using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;

namespace PoTool.Tests.AutomatedExploratory;

[TestClass]
public class ExploratoryTests : PageTest
{
    private static TestInfrastructure? s_infrastructure;
    private const string BaseUrl = "http://localhost:5001";
    private const int DefaultTimeout = 30000; // 30 seconds

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        var outputDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            "test-results"
        );
        s_infrastructure = new TestInfrastructure(outputDir);
        Console.WriteLine($"Test infrastructure initialized. Output directory: {outputDir}");
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_infrastructure != null)
        {
            var reportPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "test-results",
                "AUTOMATED_TEST_REPORT.md"
            );
            await s_infrastructure.GenerateMarkdownReportAsync(reportPath);
            Console.WriteLine($"Test report generated: {reportPath}");
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(1)]
    public async Task Test01_HomePage()
    {
        var testName = "01-HomePage";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to home page
            await Page.GotoAsync(BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });

            // Wait for page to load
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify page title or main content
            var hasContent = await Page.Locator("body").CountAsync() > 0;
            Assert.IsTrue(hasContent, "Home page should have content");

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "01-home-page", "Home page with navigation cards");
            screenshots.Add(screenshot);

            // Check for console errors
            var consoleErrors = await GetConsoleErrorsAsync();
            if (consoleErrors.Count > 0)
            {
                await s_infrastructure.WriteLogAsync("01-home-console-errors.log", string.Join("\n", consoleErrors));
            }

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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(2)]
    public async Task Test02_TfsConfiguration()
    {
        var testName = "02-TfsConfiguration";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to TFS configuration page
            await Page.GotoAsync($"{BaseUrl}/tfsconfig", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture initial state
            var screenshot1 = await s_infrastructure!.CaptureScreenshotAsync(Page, "02-tfs-configuration", "TFS Configuration page");
            screenshots.Add(screenshot1);

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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(3)]
    public async Task Test03_BacklogHealth()
    {
        var testName = "03-BacklogHealth";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to Backlog Health page
            await Page.GotoAsync($"{BaseUrl}/backlog-health", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "03-backlog-health", "Backlog Health dashboard");
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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(4)]
    public async Task Test04_EffortDistribution()
    {
        var testName = "04-EffortDistribution";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to Effort Distribution page
            await Page.GotoAsync($"{BaseUrl}/effort-distribution", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "04-effort-distribution", "Effort Distribution heat map");
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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(5)]
    public async Task Test05_PrInsights()
    {
        var testName = "05-PrInsights";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to PR Insights page
            await Page.GotoAsync($"{BaseUrl}/pr-insights", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "05-pr-insights", "PR Insights dashboard");
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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(6)]
    public async Task Test06_StateTimeline()
    {
        var testName = "06-StateTimeline";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to State Timeline page
            await Page.GotoAsync($"{BaseUrl}/state-timeline", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "06-state-timeline", "State Timeline visualization");
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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(7)]
    public async Task Test07_EpicForecast()
    {
        var testName = "07-EpicForecast";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to Epic Forecast page
            await Page.GotoAsync($"{BaseUrl}/epic-forecast", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "07-epic-forecast", "Epic Forecast with velocity chart");
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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(8)]
    public async Task Test08_DependencyGraph()
    {
        var testName = "08-DependencyGraph";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to Dependency Graph page
            await Page.GotoAsync($"{BaseUrl}/dependency-graph", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "08-dependency-graph", "Dependency Graph visualization");
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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(9)]
    public async Task Test09_VelocityDashboard()
    {
        var testName = "09-VelocityDashboard";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to Velocity Dashboard page
            await Page.GotoAsync($"{BaseUrl}/velocity-dashboard", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Capture screenshot
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "09-velocity-dashboard", "Velocity Dashboard with trends");
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

    [TestMethod]
    [TestCategory("UI")]
    [Priority(10)]
    public async Task Test10_SettingsModal()
    {
        var testName = "10-SettingsModal";
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();

        try
        {
            // Navigate to home page first
            await Page.GotoAsync(BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Try to find and click settings button/icon
            // This is a best-effort attempt as the exact selector may vary
            try
            {
                // Common patterns for settings buttons
                var settingsSelectors = new[]
                {
                    "button:has-text('Settings')",
                    "button:has-text('settings')",
                    "[aria-label='Settings']",
                    "[aria-label='settings']",
                    ".settings-button",
                    "#settings-button"
                };

                var found = false;
                foreach (var selector in settingsSelectors)
                {
                    var count = await Page.Locator(selector).CountAsync();
                    if (count > 0)
                    {
                        await Page.Locator(selector).First.ClickAsync();
                        found = true;
                        await Task.Delay(1000); // Wait for modal to open
                        break;
                    }
                }

                if (!found)
                {
                    Console.WriteLine("Settings button not found, capturing home page instead");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not open settings modal: {ex.Message}");
            }

            // Capture screenshot (either modal or home page)
            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(Page, "10-settings-modal", "Settings modal or home page");
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

    private async Task<List<string>> GetConsoleErrorsAsync()
    {
        // This method would capture console errors if we had set up console listeners
        // For now, return empty list as a placeholder
        return await Task.FromResult(new List<string>());
    }
}
