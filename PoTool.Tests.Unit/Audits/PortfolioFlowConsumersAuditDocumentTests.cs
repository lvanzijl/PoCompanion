namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowConsumersAuditDocumentTests
{
    [TestMethod]
    public void PortfolioFlowConsumersAudit_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "portfolio_flow_consumers_audit.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow consumers audit report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Projection Consumer Audit");
        StringAssert.Contains(report, "## Consumer Inventory");
        StringAssert.Contains(report, "## Legacy Reconstruction Paths");
        StringAssert.Contains(report, "## Canonical Projection Usage Verification");
        StringAssert.Contains(report, "## Consumers Already Migrated");
        StringAssert.Contains(report, "## Legacy Reconstruction Removed");
        StringAssert.Contains(report, "## Remaining Migration Tasks");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "PortfolioFlowProjectionService.cs");
        StringAssert.Contains(report, "SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "PortfolioFlowProjectionEntity");
        StringAssert.Contains(report, "StockStoryPoints");
        StringAssert.Contains(report, "RemainingScopeStoryPoints");
        StringAssert.Contains(report, "InflowStoryPoints");
        StringAssert.Contains(report, "ThroughputStoryPoints");
        StringAssert.Contains(report, "CompletionPercent");
        StringAssert.Contains(report, "The portfolio progress trend handler no longer reads `SprintMetricsProjectionEntity` for stock/flow calculations.");
        StringAssert.Contains(report, "Remove `PercentDone`, `RemainingEffort`, `AddedEffort`, `ThroughputEffort`, `TotalScopeEffort`, and `NetFlow` compatibility aliases");
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
