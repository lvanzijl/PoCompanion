namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ForecastingSemanticAuditDocumentTests
{
    [TestMethod]
    public void ForecastingSemanticAudit_ReportExistsWithRequiredSectionsAndReadinessClassification()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "forecasting_semantic_audit.md");

        Assert.IsTrue(reportPath is not null && File.Exists(reportPath), "The forecasting semantic audit report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Forecasting Semantic Audit");
        StringAssert.Contains(report, "## Scope");
        StringAssert.Contains(report, "## Canonical forecasting semantics");
        StringAssert.Contains(report, "### Velocity");
        StringAssert.Contains(report, "### Throughput");
        StringAssert.Contains(report, "### Completion projection");
        StringAssert.Contains(report, "### Delivery probability");
        StringAssert.Contains(report, "### Capacity assumptions");
        StringAssert.Contains(report, "## Formula comparison");
        StringAssert.Contains(report, "## Semantic conflicts and contradictions");
        StringAssert.Contains(report, "## Recommended CDC boundaries");
        StringAssert.Contains(report, "## Readiness classification");
        StringAssert.Contains(report, "## Final recommendation");

        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Client/Components/Forecast/ForecastPanel.razor");
        StringAssert.Contains(report, "PoTool.Shared/Metrics/EpicCompletionForecastDto.cs");
        StringAssert.Contains(report, "PoTool.Shared/Statistics/PercentileMath.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "Rolling velocity");
        StringAssert.Contains(report, "does not exist as a named production formula");
        StringAssert.Contains(report, "Classification: **Partially ready**");
        StringAssert.Contains(report, "Forecasting is layered on top of DeliveryTrends");
        StringAssert.Contains(report, "confidence/probability has multiple meanings");
        StringAssert.Contains(report, "story-point scope still appears behind legacy effort names");
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
