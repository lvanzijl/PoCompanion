namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class TransportNamingAlignmentDocumentTests
{
    [TestMethod]
    public void TransportNamingAlignment_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "transport_naming_alignment.md");

        Assert.IsTrue(File.Exists(reportPath), "The transport naming alignment audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Transport Naming Alignment Audit");
        StringAssert.Contains(report, "## Scope");
        StringAssert.Contains(report, "## Detection inventory");
        StringAssert.Contains(report, "## Classification");
        StringAssert.Contains(report, "## Updated DTOs");
        StringAssert.Contains(report, "## Remaining legacy-only fields");
        StringAssert.Contains(report, "## Migration readiness assessment");
        StringAssert.Contains(report, "EpicCompletionForecastDto");
        StringAssert.Contains(report, "FeatureProgressDto");
        StringAssert.Contains(report, "EpicProgressDto");
        StringAssert.Contains(report, "FeatureDeliveryDto");
        StringAssert.Contains(report, "SprintForecast");
        StringAssert.Contains(report, "TotalStoryPoints");
        StringAssert.Contains(report, "DoneStoryPoints");
        StringAssert.Contains(report, "RemainingStoryPoints");
        StringAssert.Contains(report, "ExpectedCompletedStoryPoints");
        StringAssert.Contains(report, "RemainingStoryPointsAfterSprint");
        StringAssert.Contains(report, "GetEpicCompletionForecastQueryHandler.cs");
        StringAssert.Contains(report, "DeliveryTrendProgressRollupMapper.cs");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler.cs");
        StringAssert.Contains(report, "docs/architecture/cdc-reference.md");
        StringAssert.Contains(report, "legacy transport aliases for story points");
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
