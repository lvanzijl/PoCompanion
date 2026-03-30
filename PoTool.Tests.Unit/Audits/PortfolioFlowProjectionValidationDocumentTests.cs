namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioFlowProjectionValidationDocumentTests
{
    [TestMethod]
    public void PortfolioFlowProjectionValidation_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "portfolio_flow_projection_validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The portfolio flow projection validation audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# PortfolioFlow Projection Validation");
        StringAssert.Contains(report, "## Legacy Comparison");
        StringAssert.Contains(report, "## Edge Case Validation");
        StringAssert.Contains(report, "## Historical Reconstruction Validation");
        StringAssert.Contains(report, "## Determinism Check");
        StringAssert.Contains(report, "## Projection Readiness For Application Migration");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler");
        StringAssert.Contains(report, "PortfolioFlowProjectionEntity");
        StringAssert.Contains(report, "effort → story points");
        StringAssert.Contains(report, "commitment proxy → real inflow");
        StringAssert.Contains(report, "deterministic");
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
