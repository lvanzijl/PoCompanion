namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioHandlerSimplificationDocumentTests
{
    [TestMethod]
    public void PortfolioHandlerSimplification_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "portfolio_handler_simplification.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio handler simplification report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Portfolio Handler Simplification");
        StringAssert.Contains(report, "## Removed Handler Calculations");
        StringAssert.Contains(report, "## New CDC Portfolio Results");
        StringAssert.Contains(report, "## Updated Handlers");
        StringAssert.Contains(report, "## DTO Compatibility Decisions");
        StringAssert.Contains(report, "## Test Adjustments");
        StringAssert.Contains(report, "## Lines of Code Removed");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler");
        StringAssert.Contains(report, "IPortfolioFlowSummaryService");
        StringAssert.Contains(report, "IPortfolioDeliverySummaryService");
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
