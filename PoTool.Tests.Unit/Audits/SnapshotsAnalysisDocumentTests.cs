namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SnapshotsAnalysisDocumentTests
{
    [TestMethod]
    public void SnapshotsAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "snapshots.md");

        Assert.IsTrue(File.Exists(reportPath), "The snapshots analysis report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Snapshot & Budget Model Analysis");
        StringAssert.Contains(report, "## 1. Existing patterns");
        StringAssert.Contains(report, "## 2. How data is persisted over time");
        StringAssert.Contains(report, "## 3. Fit for a snapshot model");
        StringAssert.Contains(report, "## 4. Risks");
        StringAssert.Contains(report, "## 5. Recommended integration approach");
        StringAssert.Contains(report, "RoadmapSnapshotEntity");
        StringAssert.Contains(report, "ActivityEventLedgerEntryEntity");
        StringAssert.Contains(report, "WorkItemRelationshipEdgeEntity");
        StringAssert.Contains(report, "ProductOwnerCacheStateEntity");
        StringAssert.Contains(report, "WorkItemStateClassificationEntity");
        StringAssert.Contains(report, "BugTriageStateEntity");
        StringAssert.Contains(report, "Rhodium.Funding.ProjectElement");
        StringAssert.Contains(report, "BudgetSnapshotEntity");
        StringAssert.Contains(report, "ProductEntity");
        StringAssert.Contains(report, "WorkItemEntity");
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
