namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class StateClassificationsAnalysisDocumentTests
{
    [TestMethod]
    public void StateClassificationsAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "state-classifications.md");

        Assert.IsTrue(File.Exists(reportPath), "The state classifications analysis report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# State Classifications & Refinement Gating Analysis");
        StringAssert.Contains(report, "## 1. Canonical classification model");
        StringAssert.Contains(report, "## 2. Where classifications are defined");
        StringAssert.Contains(report, "## 3. Mapping and lookup mechanics");
        StringAssert.Contains(report, "## 4. How classifications are used");
        StringAssert.Contains(report, "## 5. Existing concepts related to “Approved” or “Refinement Ready”");
        StringAssert.Contains(report, "## 6. Gaps vs desired “Approved = refinement ready” behavior");
        StringAssert.Contains(report, "## 7. Recommended extension points");
        StringAssert.Contains(report, "Approved -> New");
        StringAssert.Contains(report, "FeatureOwnerState");
        StringAssert.Contains(report, "WorkItemStateClassificationService");
        StringAssert.Contains(report, "StateClassificationDefaults");
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
