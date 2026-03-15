namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class DeliveryTrendAnalyticsCdcSummaryDocumentTests
{
    [TestMethod]
    public void DeliveryTrendAnalyticsCdcSummary_ReportExistsWithRequiredSectionsAndVerdict()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "delivery_trend_analytics_cdc_summary.md");

        Assert.IsTrue(File.Exists(reportPath), "The delivery trend analytics CDC summary should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Delivery Trend Analytics CDC Summary");
        StringAssert.Contains(report, "## Scope re-audited");
        StringAssert.Contains(report, "## Expected CDC ownership");
        StringAssert.Contains(report, "## What moved into the CDC");
        StringAssert.Contains(report, "## What remains outside the CDC");
        StringAssert.Contains(report, "## Implementation audit");
        StringAssert.Contains(report, "## Remaining issues");
        StringAssert.Contains(report, "## Test validation");
        StringAssert.Contains(report, "## CDC Minor Cleanup Completed");
        StringAssert.Contains(report, "## Final verdict");

        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressSummaryCalculator.cs");
        StringAssert.Contains(report, "PoTool.Api/Services/SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs");
        StringAssert.Contains(report, "no local formula duplication remains");
        StringAssert.Contains(report, "Classification: **minor cleanup**");
        StringAssert.Contains(report, "CDC services are now injected via DI");
        StringAssert.Contains(report, "API orchestration responsibilities remain unchanged");
        StringAssert.Contains(report, "remain exclusively in `PoTool.Core.Domain`");
        StringAssert.Contains(report, "Delivery Trend Analytics CDC ready after minor cleanup");
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
