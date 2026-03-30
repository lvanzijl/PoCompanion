namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowFeasibilityDocumentTests
{
    [TestMethod]
    public void PortfolioFlowFeasibility_ReportExistsWithRequiredSectionsAndKeyFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "portfolio-flow-feasibility.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow feasibility report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Feasibility Audit");
        StringAssert.Contains(report, "## Stock Reconstruction Feasibility");
        StringAssert.Contains(report, "## Inflow Detection Feasibility");
        StringAssert.Contains(report, "## Throughput Detection Feasibility");
        StringAssert.Contains(report, "## Remaining Scope Feasibility");
        StringAssert.Contains(report, "## Missing Signals");
        StringAssert.Contains(report, "## Computational Cost");
        StringAssert.Contains(report, "## Implementation Risk");

        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "ActivityEventIngestionService.cs");
        StringAssert.Contains(report, "SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "FirstDoneDeliveryLookup.cs");
        StringAssert.Contains(report, "StateReconstructionLookup.cs");
        StringAssert.Contains(report, "ResolvedWorkItemEntity");
        StringAssert.Contains(report, "RevisionFieldWhitelist");
        StringAssert.Contains(report, "Microsoft.VSTS.Scheduling.StoryPoints");
        StringAssert.Contains(report, "EnteredPortfolio(w)");
        StringAssert.Contains(report, "first Done transition detection exists");
        StringAssert.Contains(report, "reopen handling exists");
        StringAssert.Contains(report, "not fully implementable with current repository data as-is");
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
