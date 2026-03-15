namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class StatisticalHelperAuditDocumentTests
{
    [TestMethod]
    public void StatisticalHelperAudit_ReportExistsWithRequiredSectionsAndKeyFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var auditPath = Path.Combine(repositoryRoot, "docs", "audits", "statistical_helper_audit.md");

        Assert.IsTrue(File.Exists(auditPath), "The statistical helper audit report should exist under docs/audits.");

        var audit = File.ReadAllText(auditPath);

        StringAssert.Contains(audit, "# Statistical Helper Audit");
        StringAssert.Contains(audit, "## Summary");
        StringAssert.Contains(audit, "## Inventory of Statistical Logic");
        StringAssert.Contains(audit, "## Semantic Drift");
        StringAssert.Contains(audit, "## Consolidation Opportunities");
        StringAssert.Contains(audit, "## Keep Local");
        StringAssert.Contains(audit, "## Recommended Next Step");

        StringAssert.Contains(audit, "PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs");
        StringAssert.Contains(audit, "PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs");
        StringAssert.Contains(audit, "PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs");
        StringAssert.Contains(audit, "confidence");
        StringAssert.Contains(audit, "percentile");
    }

    [TestMethod]
    public void StatisticalCoreCleanupReport_ExistsWithOwnershipConsolidationSection()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "statistical_core_cleanup_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The statistical core cleanup report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Statistical Core Cleanup Report");
        StringAssert.Contains(report, "## EffortDiagnostics Statistics Ownership Consolidation");
        StringAssert.Contains(report, "## Shared Pure-Math Statistics Core Introduced");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/EffortDiagnostics");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs");
        StringAssert.Contains(report, "StandardDeviation");
        StringAssert.Contains(report, "PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs");
        StringAssert.Contains(report, "## Variance Duplication Removed from Estimation Handlers");
        StringAssert.Contains(report, "## Percentile Semantics Standardized");
        StringAssert.Contains(report, "Linear interpolation");
        StringAssert.Contains(report, "PoTool.Shared/Statistics/PercentileMath.cs");
        StringAssert.Contains(report, "handlers updated");
        StringAssert.Contains(report, "local helpers removed");
        StringAssert.Contains(report, "behavior preserved");
    }

    [TestMethod]
    public void EstimationHandlers_DoNotRetainLocalVarianceHelpers()
    {
        var repositoryRoot = GetRepositoryRoot();
        var qualityHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "Metrics", "GetEffortEstimationQualityQueryHandler.cs");
        var suggestionsHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "Metrics", "GetEffortEstimationSuggestionsQueryHandler.cs");

        var qualityHandler = File.ReadAllText(qualityHandlerPath);
        var suggestionsHandler = File.ReadAllText(suggestionsHandlerPath);

        Assert.IsFalse(qualityHandler.Contains("private double CalculateVariance", StringComparison.Ordinal));
        Assert.IsFalse(suggestionsHandler.Contains("private double CalculateVariance", StringComparison.Ordinal));
        StringAssert.Contains(qualityHandler, "StatisticsMath.Variance");
        StringAssert.Contains(suggestionsHandler, "StatisticsMath.Variance");
    }

    [TestMethod]
    public void PercentileConsumers_UseSharedLinearInterpolationHelper()
    {
        var repositoryRoot = GetRepositoryRoot();
        var prInsightsHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "PullRequests", "GetPullRequestInsightsQueryHandler.cs");
        var prDeliveryHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "PullRequests", "GetPrDeliveryInsightsQueryHandler.cs");
        var pipelineHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "Pipelines", "GetPipelineInsightsQueryHandler.cs");
        var capacityHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "Metrics", "GetCapacityCalibrationQueryHandler.cs");
        var prSprintHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "PullRequests", "GetPrSprintTrendsQueryHandler.cs");
        var prCalculatorPath = Path.Combine(repositoryRoot, "PoTool.Client", "Services", "PullRequestInsightsCalculator.cs");
        var pipelineCalculatorPath = Path.Combine(repositoryRoot, "PoTool.Client", "Services", "PipelineInsightsCalculator.cs");
        var bugCalculatorPath = Path.Combine(repositoryRoot, "PoTool.Client", "Services", "BugInsightsCalculator.cs");

        var prInsightsHandler = File.ReadAllText(prInsightsHandlerPath);
        var prDeliveryHandler = File.ReadAllText(prDeliveryHandlerPath);
        var pipelineHandler = File.ReadAllText(pipelineHandlerPath);
        var capacityHandler = File.ReadAllText(capacityHandlerPath);
        var prSprintHandler = File.ReadAllText(prSprintHandlerPath);
        var prCalculator = File.ReadAllText(prCalculatorPath);
        var pipelineCalculator = File.ReadAllText(pipelineCalculatorPath);
        var bugCalculator = File.ReadAllText(bugCalculatorPath);

        Assert.IsFalse(prInsightsHandler.Contains("private static double Percentile(", StringComparison.Ordinal));
        Assert.IsFalse(prDeliveryHandler.Contains("private static double Percentile(", StringComparison.Ordinal));
        Assert.IsFalse(pipelineHandler.Contains("private static double Percentile(", StringComparison.Ordinal));
        Assert.IsFalse(capacityHandler.Contains("private static double Percentile(", StringComparison.Ordinal));
        Assert.IsFalse(prSprintHandler.Contains("private static double Percentile(", StringComparison.Ordinal));

        StringAssert.Contains(prInsightsHandler, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(prDeliveryHandler, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(pipelineHandler, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(capacityHandler, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(prSprintHandler, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(prCalculator, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(pipelineCalculator, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(bugCalculator, "PercentileMath.LinearInterpolation");
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
