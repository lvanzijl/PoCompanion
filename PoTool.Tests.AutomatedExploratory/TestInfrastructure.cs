using System.Text;
using Microsoft.Playwright;

namespace PoTool.Tests.AutomatedExploratory;

/// <summary>
/// Test infrastructure for automated exploratory testing.
/// Provides screenshot capture, logging, and reporting capabilities.
/// </summary>
public class TestInfrastructure
{
    private readonly string _screenshotsPath;
    private readonly string _logsPath;
    private readonly List<TestResult> _testResults = new();

    public TestInfrastructure(string outputDirectory)
    {
        _screenshotsPath = Path.Combine(outputDirectory, "screenshots");
        _logsPath = Path.Combine(outputDirectory, "logs");

        Directory.CreateDirectory(_screenshotsPath);
        Directory.CreateDirectory(_logsPath);
    }

    public string ScreenshotsPath => _screenshotsPath;
    public string LogsPath => _logsPath;
    public IReadOnlyList<TestResult> TestResults => _testResults.AsReadOnly();

    public async Task<string> CaptureScreenshotAsync(IPage page, string name, string description)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var filename = $"{name}.png";
        var filepath = Path.Combine(_screenshotsPath, filename);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = filepath,
            FullPage = true
        });

        Console.WriteLine($"Screenshot captured: {filename} - {description}");
        return filepath;
    }

    public void LogTestResult(string testName, bool passed, TimeSpan duration, string? error = null, List<string>? screenshots = null)
    {
        var result = new TestResult
        {
            TestName = testName,
            Passed = passed,
            Duration = duration,
            Error = error,
            Screenshots = screenshots ?? new List<string>()
        };

        _testResults.Add(result);
    }

    public async Task<string> GenerateMarkdownReportAsync(string outputPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Automated Exploratory Test Report");
        sb.AppendLine();
        sb.AppendLine($"**Test Execution Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();

        // Summary
        var totalTests = _testResults.Count;
        var passedTests = _testResults.Count(r => r.Passed);
        var failedTests = totalTests - passedTests;

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Tests:** {totalTests}");
        sb.AppendLine($"- **Passed:** ✅ {passedTests}");
        sb.AppendLine($"- **Failed:** ❌ {failedTests}");
        sb.AppendLine($"- **Success Rate:** {(totalTests > 0 ? (passedTests * 100.0 / totalTests).ToString("F1") : "0")}%");
        sb.AppendLine();

        // Performance metrics
        var totalDuration = TimeSpan.FromMilliseconds(_testResults.Sum(r => r.Duration.TotalMilliseconds));
        var avgDuration = totalTests > 0 ? TimeSpan.FromMilliseconds(_testResults.Average(r => r.Duration.TotalMilliseconds)) : TimeSpan.Zero;

        sb.AppendLine("## Performance Metrics");
        sb.AppendLine();
        sb.AppendLine($"- **Total Execution Time:** {totalDuration.TotalSeconds:F2} seconds");
        sb.AppendLine($"- **Average Test Duration:** {avgDuration.TotalSeconds:F2} seconds");
        sb.AppendLine();

        // Test Results Detail
        sb.AppendLine("## Test Results");
        sb.AppendLine();

        foreach (var result in _testResults)
        {
            var status = result.Passed ? "✅ PASS" : "❌ FAIL";
            sb.AppendLine($"### {result.TestName} - {status}");
            sb.AppendLine();
            sb.AppendLine($"**Duration:** {result.Duration.TotalSeconds:F2} seconds");
            sb.AppendLine();

            if (!result.Passed && !string.IsNullOrEmpty(result.Error))
            {
                sb.AppendLine("**Error:**");
                sb.AppendLine("```");
                sb.AppendLine(result.Error);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (result.Screenshots.Count > 0)
            {
                sb.AppendLine("**Screenshots:**");
                sb.AppendLine();
                foreach (var screenshot in result.Screenshots)
                {
                    var filename = Path.GetFileName(screenshot);
                    sb.AppendLine($"![{filename}](screenshots/{filename})");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Conclusion
        sb.AppendLine("## Conclusion");
        sb.AppendLine();
        if (failedTests == 0)
        {
            sb.AppendLine("✅ All tests passed successfully! The application is functioning as expected.");
        }
        else
        {
            sb.AppendLine($"⚠️ {failedTests} test(s) failed. Please review the errors above and investigate the issues.");
        }

        var report = sb.ToString();
        await File.WriteAllTextAsync(outputPath, report);

        return outputPath;
    }

    public async Task WriteLogAsync(string logName, string content)
    {
        var logPath = Path.Combine(_logsPath, logName);
        await File.WriteAllTextAsync(logPath, content);
    }
}

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Error { get; set; }
    public List<string> Screenshots { get; set; } = new();
}
