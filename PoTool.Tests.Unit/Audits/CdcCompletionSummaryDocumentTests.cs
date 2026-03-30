namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcCompletionSummaryDocumentTests
{
    [TestMethod]
    public void CdcCompletionSummary_ReportExistsWithRequiredSectionsAndCompletionStatus()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "cdc_completion_summary.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC completion summary should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Completion Summary");
        StringAssert.Contains(report, "## Completed Slices");
        StringAssert.Contains(report, "## Application Alignment Status");
        StringAssert.Contains(report, "## UI Semantic Alignment Status");
        StringAssert.Contains(report, "## Projection Status");
        StringAssert.Contains(report, "## Remaining Structural Work");
        StringAssert.Contains(report, "## Recommended Next Phase");

        StringAssert.Contains(report, "Core Concepts");
        StringAssert.Contains(report, "BacklogQuality");
        StringAssert.Contains(report, "SprintCommitment");
        StringAssert.Contains(report, "DeliveryTrends");
        StringAssert.Contains(report, "Forecasting");
        StringAssert.Contains(report, "EffortDiagnostics");
        StringAssert.Contains(report, "PortfolioFlow");
        StringAssert.Contains(report, "Shared Statistics");

        StringAssert.Contains(report, "legacy `*Effort` DTO names");
        StringAssert.Contains(report, "The CDC is now stable enough to support:");
        StringAssert.Contains(report, "persistence abstraction");
        StringAssert.Contains(report, "optional hexagonal architecture");
        StringAssert.Contains(report, "later API contract cleanup");
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
