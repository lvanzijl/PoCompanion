namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class BuildQualityUiFinalIntegrationReportDocumentTests
{
    [TestMethod]
    public void BuildQualityUiFinalIntegrationReport_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality-ui-final-integration-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality UI final integration report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality UI Final Integration Report");
        StringAssert.Contains(report, "## 1. Scope validated");
        StringAssert.Contains(report, "## 2. Health validation");
        StringAssert.Contains(report, "## 3. Sprint/Delivery validation");
        StringAssert.Contains(report, "## 4. Pipeline Insights validation");
        StringAssert.Contains(report, "## 5. Cross-page consistency");
        StringAssert.Contains(report, "## 6. Issues found");
        StringAssert.Contains(report, "## 7. Final verdict");
        StringAssert.Contains(report, "HealthWorkspace.razor");
        StringAssert.Contains(report, "SprintTrend.razor");
        StringAssert.Contains(report, "PipelineInsights.razor");
        StringAssert.Contains(report, "BuildQualitySummaryComponent.razor");
        StringAssert.Contains(report, "BuildQualityCompactComponent.razor");
        StringAssert.Contains(report, "BuildQualityTooltipComponent.razor");
        StringAssert.Contains(report, "BuildQualityPresentation.cs");
        StringAssert.Contains(report, "GetRollingWindowAsync(...)");
        StringAssert.Contains(report, "GetSprintAsync(...)");
        StringAssert.Contains(report, "GetPipelineAsync(...)");
        StringAssert.Contains(report, "SuccessRate");
        StringAssert.Contains(report, "TestPassRate");
        StringAssert.Contains(report, "Coverage");
        StringAssert.Contains(report, "Confidence");
        StringAssert.Contains(report, "MAJOR");
        StringAssert.Contains(report, "MINOR");
        StringAssert.Contains(report, "READY WITH FIXES");
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
