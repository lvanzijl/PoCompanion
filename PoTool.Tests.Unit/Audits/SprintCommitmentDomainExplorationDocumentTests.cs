namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SprintCommitmentDomainExplorationDocumentTests
{
    [TestMethod]
    public void SprintCommitmentDomainExploration_ReportExistsWithRequiredSectionsConceptsConflictsAndFeasibility()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "exploration", "sprint_commitment_domain_exploration.md");

        Assert.IsTrue(File.Exists(reportPath), "The sprint commitment domain exploration document should exist under docs/exploration.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Sprint Commitment Domain Exploration");
        StringAssert.Contains(report, "## Locations of Sprint Commitment Logic");
        StringAssert.Contains(report, "## Sprint Commitment Detection");
        StringAssert.Contains(report, "## Scope Change Detection");
        StringAssert.Contains(report, "## Spillover Detection");
        StringAssert.Contains(report, "## Planning vs Execution Concepts");
        StringAssert.Contains(report, "## Inconsistencies Found");
        StringAssert.Contains(report, "## Candidate Domain Concepts");
        StringAssert.Contains(report, "## Semantic Conflicts");
        StringAssert.Contains(report, "## Canonical Sprint Model");
        StringAssert.Contains(report, "# Sprint Commitment CDC Feasibility");

        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Sprints/FirstDoneDeliveryLookup.cs");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs");
        StringAssert.Contains(report, "PoTool.Api/Services/SprintTrendProjectionService.cs");
        StringAssert.Contains(report, "PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs");
        StringAssert.Contains(report, "CommitmentTimestamp = SprintStart + 1 day");
        StringAssert.Contains(report, "committed, not Done at sprint end, then direct move to the next sprint");
        StringAssert.Contains(report, "falls back to `ResolvedSprintId` when committed IDs are not supplied");
        StringAssert.Contains(report, "**Core CDC concept**");
        StringAssert.Contains(report, "**Derived CDC metric**");
        StringAssert.Contains(report, "**Application-level metric**");
        StringAssert.Contains(report, "the repository already has the core event signals needed for a Sprint Commitment CDC slice");
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
