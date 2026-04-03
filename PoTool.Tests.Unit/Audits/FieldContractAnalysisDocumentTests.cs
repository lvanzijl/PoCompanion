namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class FieldContractAnalysisDocumentTests
{
    [TestMethod]
    public void FieldContractAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "field-contract.md");

        Assert.IsTrue(File.Exists(reportPath), "The field contract analysis report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Field Contract & Usage Analysis");
        StringAssert.Contains(report, "## 2. Rhodium.Funding.ProjectNumber");
        StringAssert.Contains(report, "## 3. Rhodium.Funding.ProjectElement");
        StringAssert.Contains(report, "## 4. Microsoft.VSTS.Scheduling.Effort");
        StringAssert.Contains(report, "## 5. Microsoft.VSTS.Common.TimeCriticality");
        StringAssert.Contains(report, "## 6. Risks / gaps");
        StringAssert.Contains(report, "## 7. Required changes for full support");
        StringAssert.Contains(report, "RequiredWorkItemFields");
        StringAssert.Contains(report, "RevisionFieldWhitelist");
        StringAssert.Contains(report, "WorkItemEntity");
        StringAssert.Contains(report, "WorkItemDto");
        StringAssert.Contains(report, "WorkItemRepository");
        StringAssert.Contains(report, "VerifyWorkItemFieldsAsync");
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
