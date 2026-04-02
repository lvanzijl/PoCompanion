namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PortfolioFlowDomainExplorationDocumentTests
{
    [TestMethod]
    public void PortfolioFlowDomainExploration_ReportExistsWithRequiredSectionsAndKeyFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "portfolio-flow-domain-exploration.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow exploration report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Domain Exploration");
        StringAssert.Contains(report, "## Summary");
        StringAssert.Contains(report, "## Inventory");
        StringAssert.Contains(report, "## Domain Families");
        StringAssert.Contains(report, "## Dependencies");
        StringAssert.Contains(report, "## Semantic Overlaps");
        StringAssert.Contains(report, "## Contradictions");
        StringAssert.Contains(report, "## CDC Candidates");
        StringAssert.Contains(report, "## Recommendation");

        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "DeliveryProgressSummaryCalculator.cs");
        StringAssert.Contains(report, "PortfolioProgressTrendDtos.cs");
        StringAssert.Contains(report, "PortfolioDeliveryDtos.cs");
        StringAssert.Contains(report, "PortfolioProgressPage.razor");
        StringAssert.Contains(report, "PortfolioDelivery.razor");
        StringAssert.Contains(report, "there is no monolithic PortfolioFlow slice ready for extraction");
        StringAssert.Contains(report, "`AddedEffort` is only a proxy from `SprintMetricsProjection.PlannedEffort`");
        StringAssert.Contains(report, "`CompletedEffort` in portfolio delivery DTOs is semantically story-point delivery");
        StringAssert.Contains(report, "**B — promising but needs semantic clarification**");
        StringAssert.Contains(report, "**C — still application aggregation / presentation logic**");
        StringAssert.Contains(report, "splitting into multiple portfolio-related slices");
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
