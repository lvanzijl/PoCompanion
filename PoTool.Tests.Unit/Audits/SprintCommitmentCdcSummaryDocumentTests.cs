namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SprintCommitmentCdcSummaryDocumentTests
{
    [TestMethod]
    public void SprintCommitmentCdcSummary_ReportExistsWithCanonicalConceptsSignalsMetricsAndInterfaces()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "domain", "sprint_commitment_cdc_summary.md");

        Assert.IsTrue(File.Exists(reportPath), "The sprint commitment CDC summary should exist under docs/domain.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Sprint Commitment CDC Summary");
        StringAssert.Contains(report, "## Canonical concepts");
        StringAssert.Contains(report, "## Event signals");
        StringAssert.Contains(report, "## Derived metrics");
        StringAssert.Contains(report, "## CDC service interfaces");
        StringAssert.Contains(report, "## Relationship to delivery trends and forecasting");
        StringAssert.Contains(report, "SprintCommitment");
        StringAssert.Contains(report, "SprintScopeAdded");
        StringAssert.Contains(report, "SprintScopeRemoved");
        StringAssert.Contains(report, "SprintCompletion");
        StringAssert.Contains(report, "SprintSpillover");
        StringAssert.Contains(report, "ISprintCommitmentService");
        StringAssert.Contains(report, "ISprintScopeChangeService");
        StringAssert.Contains(report, "ISprintCompletionService");
        StringAssert.Contains(report, "ISprintSpilloverService");
        StringAssert.Contains(report, "CommitmentTimestamp = SprintStart + 1 day");
        StringAssert.Contains(report, "Snapshot membership is not commitment logic");
        StringAssert.Contains(report, "delivery trends consume committed IDs, completion signals, and spillover outputs from the CDC");
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
