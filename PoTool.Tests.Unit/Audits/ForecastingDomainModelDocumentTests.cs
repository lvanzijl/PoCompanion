namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ForecastingDomainModelDocumentTests
{
    [TestMethod]
    public void ForecastingDomainModel_ReportExistsWithRequiredConceptsInputsOutputsAndBoundaries()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "domain", "forecasting_domain_model.md");

        Assert.IsTrue(File.Exists(reportPath), "The forecasting domain model document should exist under docs/domain.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Forecasting Domain Model");
        StringAssert.Contains(report, "## Domain Purpose");
        StringAssert.Contains(report, "## Canonical Domain Concepts");
        StringAssert.Contains(report, "### DeliveryForecast");
        StringAssert.Contains(report, "### VelocityCalibration");
        StringAssert.Contains(report, "### CompletionProjection");
        StringAssert.Contains(report, "### ForecastDistribution");
        StringAssert.Contains(report, "## Canonical Inputs");
        StringAssert.Contains(report, "### DeliveryTrendSeries");
        StringAssert.Contains(report, "### SprintDeliverySummary");
        StringAssert.Contains(report, "### Remaining backlog scope");
        StringAssert.Contains(report, "## Canonical Outputs");
        StringAssert.Contains(report, "### ProjectedCompletionDate");
        StringAssert.Contains(report, "### ExpectedDeliveryRate");
        StringAssert.Contains(report, "### ForecastConfidence");
        StringAssert.Contains(report, "## Relationships");
        StringAssert.Contains(report, "## Domain Boundaries");
        StringAssert.Contains(report, "### What Forecasting consumes but does not own");
        StringAssert.Contains(report, "## Dependencies");
        StringAssert.Contains(report, "## Final Boundary Statement");

        StringAssert.Contains(report, "Forecasting must consume canonical facts from adjacent slices instead of re-implementing those calculations.");
        StringAssert.Contains(report, "DeliveryTrends historical reconstruction");
        StringAssert.Contains(report, "SprintAnalytics calculations and sprint-window semantics");
        StringAssert.Contains(report, "shared statistics helpers");
        StringAssert.Contains(report, "story points delivered per sprint");
        StringAssert.Contains(report, "Effort-hours remain diagnostic calibration data");
        StringAssert.Contains(report, "future-prediction slice layered on top of canonical historical analytics");
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
