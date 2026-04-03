namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class DtoContractCleanupDocumentTests
{
    [TestMethod]
    public void DtoContractCleanupAudit_ReportExistsWithRequiredSectionsAndMappings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "dto-contract-cleanup.md");

        Assert.IsTrue(File.Exists(reportPath), "The DTO contract cleanup audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# DTO Contract Cleanup — Canonical Naming");
        StringAssert.Contains(report, "## Scope");
        StringAssert.Contains(report, "## Legacy DTO fields");
        StringAssert.Contains(report, "## Canonical aliases introduced");
        StringAssert.Contains(report, "## Compatibility strategy");
        StringAssert.Contains(report, "EpicCompletionForecastDto");
        StringAssert.Contains(report, "SprintExecutionSummaryDto");
        StringAssert.Contains(report, "FeatureProgressDto");
        StringAssert.Contains(report, "EpicProgressDto");
        StringAssert.Contains(report, "FeatureDeliveryDto");
        StringAssert.Contains(report, "ProductSprintMetricsDto");
        StringAssert.Contains(report, "TotalEffort");
        StringAssert.Contains(report, "CompletedEffort");
        StringAssert.Contains(report, "RemainingEffort");
        StringAssert.Contains(report, "PlannedEffort");
        StringAssert.Contains(report, "CommittedStoryPoints");
        StringAssert.Contains(report, "DeliveredStoryPoints");
        StringAssert.Contains(report, "RemainingStoryPoints");
        StringAssert.Contains(report, "AddedStoryPoints");
        StringAssert.Contains(report, "SpilloverStoryPoints");
        StringAssert.Contains(report, "Compatibility alias");
        StringAssert.Contains(report, "Deprecated in future contract revision");
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
