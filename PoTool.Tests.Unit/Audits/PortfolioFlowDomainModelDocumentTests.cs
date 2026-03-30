namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowDomainModelDocumentTests
{
    [TestMethod]
    public void PortfolioFlowDomainModel_ReportExistsWithCanonicalUnitFormulasMappingsAndBoundary()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "architecture", "portfolio-flow-model.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow domain model document should exist under docs/architecture.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Canonical Model");
        StringAssert.Contains(report, "## Canonical Unit");
        StringAssert.Contains(report, "## Stock Definition");
        StringAssert.Contains(report, "## Inflow Definition");
        StringAssert.Contains(report, "## Outflow Definition");
        StringAssert.Contains(report, "## Remaining Scope Definition");
        StringAssert.Contains(report, "## Flow Equations");
        StringAssert.Contains(report, "## Mapping From Current Metrics");
        StringAssert.Contains(report, "## CDC Boundary");
        StringAssert.Contains(report, "## Final Boundary Statement");

        StringAssert.Contains(report, "PortfolioFlow uses **story-point scope** as its canonical unit.");
        StringAssert.Contains(report, "This corresponds to candidate **B**.");
        StringAssert.Contains(report, "the story-point scope of PBIs that newly enter the portfolio backlog during the sprint");
        StringAssert.Contains(report, "exclude **estimation changes**");
        StringAssert.Contains(report, "exclude **reopened items**");
        StringAssert.Contains(report, "the story points delivered in the sprint");
        StringAssert.Contains(report, "the current open backlog scope at sprint end");
        StringAssert.Contains(report, "`NetFlow(s) = Throughput(s) − Inflow(s)`");
        StringAssert.Contains(report, "`CompletionPercent(s) = ((Stock(s) − RemainingScope(s)) / Stock(s)) × 100`");
        StringAssert.Contains(report, "`AddedEffort`");
        StringAssert.Contains(report, "`PlannedEffort`");
        StringAssert.Contains(report, "portfolio ranking");
        StringAssert.Contains(report, "portfolio UI composition");
        StringAssert.Contains(report, "product summaries");
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
