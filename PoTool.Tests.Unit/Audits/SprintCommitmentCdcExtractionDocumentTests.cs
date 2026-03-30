namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SprintCommitmentCdcExtractionDocumentTests
{
    [TestMethod]
    public void SprintCommitmentCdcExtraction_ReportExistsWithWrappedFilesFallbacksInterfacesAndMigrationTasks()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "sprint_commitment_cdc_extraction.md");

        Assert.IsTrue(File.Exists(reportPath), "The sprint commitment CDC extraction audit should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Sprint Commitment CDC Extraction");
        StringAssert.Contains(report, "## Files moved or wrapped");
        StringAssert.Contains(report, "## Fallbacks removed");
        StringAssert.Contains(report, "## CDC interfaces introduced");
        StringAssert.Contains(report, "## Remaining migration tasks for application layer");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCommitmentModels.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "CommittedWorkItemIds is now required");
        StringAssert.Contains(report, "ResolvedSprintId fallback removed from SprintDeliveryProjectionService");
        StringAssert.Contains(report, "ISprintCommitmentService");
        StringAssert.Contains(report, "ISprintScopeChangeService");
        StringAssert.Contains(report, "ISprintCompletionService");
        StringAssert.Contains(report, "ISprintSpilloverService");
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
