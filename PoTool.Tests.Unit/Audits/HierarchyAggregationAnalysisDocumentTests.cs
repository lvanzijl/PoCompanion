namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class HierarchyAggregationAnalysisDocumentTests
{
    [TestMethod]
    public void HierarchyAggregationAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analyze", "hierarchy-aggregation.md");

        Assert.IsTrue(File.Exists(reportPath), "The hierarchy aggregation analysis report should exist under docs/analyze.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Hierarchy & Aggregation Analysis");
        StringAssert.Contains(report, "## 1. Hierarchy model");
        StringAssert.Contains(report, "## 2. Parent/child relationships and traversal");
        StringAssert.Contains(report, "## 3. Story point aggregation");
        StringAssert.Contains(report, "## 4. Effort usage and aggregation");
        StringAssert.Contains(report, "## 5. Centralization vs duplication");
        StringAssert.Contains(report, "## 6. Gaps vs desired CDC model");
        StringAssert.Contains(report, "## 7. Risks");
        StringAssert.Contains(report, "ParentTfsId");
        StringAssert.Contains(report, "HierarchyRollupService");
        StringAssert.Contains(report, "CanonicalStoryPointResolutionService");
        StringAssert.Contains(report, "SprintCdcServices");
        StringAssert.Contains(report, "WorkItemRelationshipSnapshotService");
        StringAssert.Contains(report, "removed PBIs");
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
