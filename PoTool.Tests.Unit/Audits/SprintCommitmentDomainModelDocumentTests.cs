namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SprintCommitmentDomainModelDocumentTests
{
    [TestMethod]
    public void SprintCommitmentDomainModel_ReportExistsWithCanonicalConceptsSignalsMetricsRelationshipsAndMigration()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "architecture", "sprint-commitment-domain-model.md");

        Assert.IsTrue(File.Exists(reportPath), "The sprint commitment domain model document should exist under docs/architecture.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Sprint Commitment Domain Model");
        StringAssert.Contains(report, "## Canonical Concepts");
        StringAssert.Contains(report, "### SprintCommitment");
        StringAssert.Contains(report, "### SprintScope");
        StringAssert.Contains(report, "### SprintScopeAdded");
        StringAssert.Contains(report, "### SprintScopeRemoved");
        StringAssert.Contains(report, "### SprintCompletion");
        StringAssert.Contains(report, "### SprintThroughput");
        StringAssert.Contains(report, "### SprintSpillover");
        StringAssert.Contains(report, "### SprintWorkItemSnapshot");
        StringAssert.Contains(report, "### SprintPlanningEvent");
        StringAssert.Contains(report, "## Event Signals");
        StringAssert.Contains(report, "## Derived Metrics");
        StringAssert.Contains(report, "## Relationship to Existing CDC Slices");
        StringAssert.Contains(report, "## Migration Strategy");

        StringAssert.Contains(report, "CommitmentTimestamp = SprintStart + 1 day");
        StringAssert.Contains(report, "the work item’s first canonical transition into Done inside the sprint window");
        StringAssert.Contains(report, "work not Done at sprint end whose first post-sprint move is directly into the next sprint");
        StringAssert.Contains(report, "`SprintCompletionRate = DeliveredSP / (CommittedSP - RemovedSP)`");
        StringAssert.Contains(report, "`ChurnRate = (AddedSP + RemovedSP) / (CommittedSP + AddedSP)`");
        StringAssert.Contains(report, "`SpilloverRate = SpilloverSP / (CommittedSP - RemovedSP)`");
        StringAssert.Contains(report, "DeliveryTrends a downstream consumer of Sprint Commitment semantics");
        StringAssert.Contains(report, "`StarvedPbis` heuristic from sprint execution UI");
        StringAssert.Contains(report, "`ResolvedSprintId` may remain a current-state cache");
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
