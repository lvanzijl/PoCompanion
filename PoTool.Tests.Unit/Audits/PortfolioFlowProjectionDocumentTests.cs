namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PortfolioFlowProjectionDocumentTests
{
    [TestMethod]
    public void PortfolioFlowProjection_ReportExistsWithRequiredSectionsAndImplementationNotes()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "portfolio-flow-projection.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow projection audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Projection");
        StringAssert.Contains(report, "## Projection Entity");
        StringAssert.Contains(report, "## Projection Algorithm");
        StringAssert.Contains(report, "## Signal Sources");
        StringAssert.Contains(report, "## Validation Tests");
        StringAssert.Contains(report, "## Migration Path To PortfolioFlow CDC");
        StringAssert.Contains(report, "PortfolioFlowProjectionEntity");
        StringAssert.Contains(report, "PortfolioFlowProjectionService");
        StringAssert.Contains(report, "StockStoryPoints");
        StringAssert.Contains(report, "RemainingScopeStoryPoints");
        StringAssert.Contains(report, "InflowStoryPoints");
        StringAssert.Contains(report, "ThroughputStoryPoints");
        StringAssert.Contains(report, "CompletionPercent");
        StringAssert.Contains(report, "PortfolioEntryLookup");
        StringAssert.Contains(report, "StateReconstructionLookup");
        StringAssert.Contains(report, "CanonicalStoryPointResolutionService");
        StringAssert.Contains(report, "SprintTrendProjectionService");
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
