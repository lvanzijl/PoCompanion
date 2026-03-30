namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowSemanticAuditDocumentTests
{
    [TestMethod]
    public void PortfolioFlowSemanticAudit_ReportExistsWithRequiredSectionsAndClassification()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "portfolio-flow-semantic-audit.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow semantic audit report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Semantic Audit");
        StringAssert.Contains(report, "## Unit Consistency");
        StringAssert.Contains(report, "## Flow Model Analysis");
        StringAssert.Contains(report, "## Added Scope Analysis");
        StringAssert.Contains(report, "## Progress Semantics");
        StringAssert.Contains(report, "## Aggregation Rules");
        StringAssert.Contains(report, "## CDC Readiness");

        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "PortfolioProgressTrendDtos.cs");
        StringAssert.Contains(report, "PortfolioDeliveryDtos.cs");
        StringAssert.Contains(report, "PortfolioProgressPage.razor");
        StringAssert.Contains(report, "PortfolioDelivery.razor");
        StringAssert.Contains(report, "CompletedEffort");
        StringAssert.Contains(report, "`AddedEffort` should be treated as a **commitment proxy**, not as portfolio backlog inflow");
        StringAssert.Contains(report, "`PortfolioDelivery` mixes product effort totals with feature story-point totals");
        StringAssert.Contains(report, "Classification: **Needs semantic correction**");
        StringAssert.Contains(report, "portfolio delivery aggregation should remain application logic");
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
