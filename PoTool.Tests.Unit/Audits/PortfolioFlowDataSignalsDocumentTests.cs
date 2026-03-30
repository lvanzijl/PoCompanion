namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowDataSignalsDocumentTests
{
    [TestMethod]
    public void PortfolioFlowDataSignals_ReportExistsWithRequiredSectionsAndDecisions()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "architecture", "portfolio-flow-data-signals.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow data signals design document should exist under docs/architecture.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Data Signals");
        StringAssert.Contains(report, "## Required Historical Signals");
        StringAssert.Contains(report, "## StoryPoints History");
        StringAssert.Contains(report, "## Portfolio Membership Timeline");
        StringAssert.Contains(report, "## Portfolio Entry Event");
        StringAssert.Contains(report, "## Historical Scope Resolution");
        StringAssert.Contains(report, "## Projection Strategy");
        StringAssert.Contains(report, "## Migration Impact");

        StringAssert.Contains(report, "Microsoft.VSTS.Scheduling.StoryPoints");
        StringAssert.Contains(report, "StoryPointChanged");
        StringAssert.Contains(report, "PortfolioMembershipChanged");
        StringAssert.Contains(report, "EnteredPortfolio(w)");
        StringAssert.Contains(report, "Option A");
        StringAssert.Contains(report, "Choose **Option A**");
        StringAssert.Contains(report, "PoTool.ResolvedProductId");
        StringAssert.Contains(report, "CanonicalStoryPointResolutionService");
        StringAssert.Contains(report, "ledger = source of historical facts");
        StringAssert.Contains(report, "projection = source of portfolio-flow series");
        StringAssert.Contains(report, "legacy metrics");
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
