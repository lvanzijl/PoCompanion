namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowSignalEnablementDocumentTests
{
    [TestMethod]
    public void PortfolioFlowSignalEnablement_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "portfolio_flow_signal_enablement.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow signal enablement audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Signal Enablement");
        StringAssert.Contains(report, "## StoryPoints History Added");
        StringAssert.Contains(report, "## Resolved Product Membership Timeline Added");
        StringAssert.Contains(report, "## Portfolio Entry Derivation Added");
        StringAssert.Contains(report, "## Tests Added");
        StringAssert.Contains(report, "## Remaining Work Before PortfolioFlow Projection");
        StringAssert.Contains(report, "Microsoft.VSTS.Scheduling.StoryPoints");
        StringAssert.Contains(report, "PoTool.ResolvedProductId");
        StringAssert.Contains(report, "PortfolioEntryLookup");
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
