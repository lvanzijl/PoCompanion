namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ForecastingCdcSummaryDocumentTests
{
    [TestMethod]
    public void ForecastingCdcSummary_ReportExistsWithRequiredSectionsAndVerdict()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "forecasting_cdc_summary.md");

        Assert.IsTrue(File.Exists(reportPath), "The forecasting CDC summary should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Forecasting CDC Summary");
        StringAssert.Contains(report, "## Scope re-audited");
        StringAssert.Contains(report, "## Expected CDC ownership");
        StringAssert.Contains(report, "## What moved into the CDC");
        StringAssert.Contains(report, "## What remains outside the CDC");
        StringAssert.Contains(report, "## Implementation audit");
        StringAssert.Contains(report, "## Remaining issues");
        StringAssert.Contains(report, "## Test validation");
        StringAssert.Contains(report, "## Final verdict");

        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Forecasting/Services/EffortTrendForecastService.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs");
        StringAssert.Contains(report, "no forecast calculations remain in handlers, UI calculators, or API services");
        StringAssert.Contains(report, "Classification: **none blocking**");
        StringAssert.Contains(report, "handlers prepare inputs, call forecasting services, and map results to DTOs");
        StringAssert.Contains(report, "Forecasting CDC ready");
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
