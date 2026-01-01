using Microsoft.Playwright;

[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]

namespace PoTool.Tests.AutomatedExploratory;

[TestClass]
public class ExploratoryTests
{
    private static TestInfrastructure? s_infrastructure;
    private static IPlaywright? s_playwright;
    private static IBrowser? s_browser;
    private IBrowserContext? _context;
    private IPage? _page;

    private const string BaseUrl = "http://localhost:5001";
    private const int DefaultTimeout = 30000; // 30 seconds

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "test-results");
        s_infrastructure = new TestInfrastructure(outputDir);
        Console.WriteLine($"Test infrastructure initialized. Output directory: {outputDir}");

        s_playwright = await Playwright.CreateAsync();
        s_browser = await s_playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        _context = await s_browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });
        _page = await _context.NewPageAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.CloseAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_browser != null) await s_browser.CloseAsync();
        if (s_playwright != null) s_playwright.Dispose();

        if (s_infrastructure != null)
        {
            var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "test-results", "AUTOMATED_TEST_REPORT.md");
            await s_infrastructure.GenerateMarkdownReportAsync(reportPath);
            Console.WriteLine($"Test report generated: {reportPath}");
        }
    }

    private async Task<(bool success, List<string> screenshots, Exception? error)> RunFeatureTest(
        string testName, string route, string screenshotName, string description)
    {
        var startTime = DateTime.UtcNow;
        var screenshots = new List<string>();
        Exception? error = null;

        try
        {
            await _page!.GotoAsync($"{BaseUrl}{route}", 
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = DefaultTimeout });
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var screenshot = await s_infrastructure!.CaptureScreenshotAsync(_page, screenshotName, description);
            screenshots.Add(screenshot);

            var duration = DateTime.UtcNow - startTime;
            s_infrastructure.LogTestResult(testName, true, duration, null, screenshots);
            return (true, screenshots, null);
        }
        catch (Exception ex)
        {
            error = ex;
            var duration = DateTime.UtcNow - startTime;
            s_infrastructure!.LogTestResult(testName, false, duration, ex.ToString(), screenshots);
            return (false, screenshots, ex);
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(1)]
    public async Task Test01_HomePage()
    {
        var result = await RunFeatureTest("01-HomePage", "", "01-home-page", "Home page with navigation cards");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(2)]
    public async Task Test02_TfsConfiguration()
    {
        var result = await RunFeatureTest("02-TfsConfiguration", "/tfsconfig", "02-tfs-configuration", "TFS Configuration page");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(3)]
    public async Task Test03_BacklogHealth()
    {
        var result = await RunFeatureTest("03-BacklogHealth", "/backlog-health", "03-backlog-health", "Backlog Health dashboard");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(4)]
    public async Task Test04_EffortDistribution()
    {
        var result = await RunFeatureTest("04-EffortDistribution", "/effort-distribution", "04-effort-distribution", "Effort Distribution heat map");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(5)]
    public async Task Test05_PrInsights()
    {
        var result = await RunFeatureTest("05-PrInsights", "/pr-insights", "05-pr-insights", "PR Insights dashboard");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(6)]
    public async Task Test06_StateTimeline()
    {
        var result = await RunFeatureTest("06-StateTimeline", "/state-timeline", "06-state-timeline", "State Timeline visualization");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(7)]
    public async Task Test07_EpicForecast()
    {
        var result = await RunFeatureTest("07-EpicForecast", "/epic-forecast", "07-epic-forecast", "Epic Forecast with velocity chart");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(8)]
    public async Task Test08_DependencyGraph()
    {
        var result = await RunFeatureTest("08-DependencyGraph", "/dependency-graph", "08-dependency-graph", "Dependency Graph visualization");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(9)]
    public async Task Test09_VelocityDashboard()
    {
        var result = await RunFeatureTest("09-VelocityDashboard", "/velocity-dashboard", "09-velocity-dashboard", "Velocity Dashboard with trends");
        if (!result.success) throw result.error!;
    }

    [TestMethod]
    [TestCategory("UI")]
    [Priority(10)]
    public async Task Test10_SettingsModal()
    {
        var result = await RunFeatureTest("10-SettingsModal", "", "10-settings-modal", "Home page (settings modal testing)");
        if (!result.success) throw result.error!;
    }
}
