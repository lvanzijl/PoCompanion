namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcReplayFixtureValidationDocumentTests
{
    [TestMethod]
    public void CdcReplayFixtureValidation_ReportExistsWithRequiredSectionsAndCoverage()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "cdc-replay-fixture-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC replay fixture validation audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Replay Fixture Validation");
        StringAssert.Contains(report, "## Replayable Data Sources");
        StringAssert.Contains(report, "## CDC Replay Tests Added");
        StringAssert.Contains(report, "## Invariant Results");
        StringAssert.Contains(report, "## Determinism Results");
        StringAssert.Contains(report, "## Known Fixture Limitations");
        StringAssert.Contains(report, "SprintFacts");
        StringAssert.Contains(report, "PortfolioFlow");
        StringAssert.Contains(report, "DeliveryTrends");
        StringAssert.Contains(report, "Forecasting");
        StringAssert.Contains(report, "EffortPlanning");
        StringAssert.Contains(report, "RecordedPayloads/per_item_revisions_page_1.json");
        StringAssert.Contains(report, "SprintTrendProjectionServiceSqliteTests");
        StringAssert.Contains(report, "CdcReplayFixtureValidationTests.cs");
        StringAssert.Contains(report, "no negative remaining scope");
        StringAssert.Contains(report, "no effort/story-point cross-mixing");
        StringAssert.Contains(report, "deterministic repeated outputs");
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
