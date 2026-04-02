namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class BuildQualityApplicationPageIntegrationReportDocumentTests
{
    [TestMethod]
    public void BuildQualityApplicationPageIntegrationReport_ReportExistsWithRequiredSectionsAndLockedContent()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality-application-page-integration-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality application and page integration report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Prompt 3 — BuildQuality Application & Page Integration Report");
        StringAssert.Contains(report, "## 1. Purpose");
        StringAssert.Contains(report, "## 2. In scope");
        StringAssert.Contains(report, "## 3. Out of scope");
        StringAssert.Contains(report, "## 4. Locked decisions applied");
        StringAssert.Contains(report, "## 5. Application-layer responsibilities");
        StringAssert.Contains(report, "## 6. Page integration model");
        StringAssert.Contains(report, "## 7. Data contracts per page");
        StringAssert.Contains(report, "## 8. Data flow (text diagram)");
        StringAssert.Contains(report, "## 9. Separation-of-concerns rules");
        StringAssert.Contains(report, "## 10. Integration risks");
        StringAssert.Contains(report, "## 11. Consistency with input report");
        StringAssert.Contains(report, "## 12. Drift check");
        StringAssert.Contains(report, "## 13. Open questions introduced");

        StringAssert.Contains(report, "default branch only");
        StringAssert.Contains(report, "no feature branches");
        StringAssert.Contains(report, "no nightly builds");
        StringAssert.Contains(report, "no WorkItem linkage");
        StringAssert.Contains(report, "no additional metrics");
        StringAssert.Contains(report, "Backlog Health remains unchanged in this phase");
        StringAssert.Contains(report, "Build Quality");
        StringAssert.Contains(report, "Delivery");
        StringAssert.Contains(report, "Pipeline Insights");
        StringAssert.Contains(report, "SuccessRate = succeeded / (succeeded + failed + partiallySucceeded)");
        StringAssert.Contains(report, "TestPassRate = passed / (total - notApplicable)");
        StringAssert.Contains(report, "TestVolume = total - notApplicable");
        StringAssert.Contains(report, "Coverage = covered_lines / total_lines");
        StringAssert.Contains(report, "Confidence = BuildThresholdMet + TestThresholdMet");
        StringAssert.Contains(report, "no builds -> `SuccessRate = Unknown`");
        StringAssert.Contains(report, "no test runs -> `TestPassRate = Unknown`");
        StringAssert.Contains(report, "no coverage OR `total_lines == 0` -> `Coverage = Unknown`");
        StringAssert.Contains(report, "PoTool.Core");
        StringAssert.Contains(report, "PoTool.Api");
        StringAssert.Contains(report, "PoTool.Shared");
        StringAssert.Contains(report, "PoTool.Client");
        StringAssert.Contains(report, "No drift detected.");
        StringAssert.Contains(report, "None.");
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
