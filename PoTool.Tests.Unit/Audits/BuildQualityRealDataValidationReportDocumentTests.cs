namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityRealDataValidationReportDocumentTests
{
    [TestMethod]
    public void BuildQualityRealDataValidationReport_ReportExistsWithObservedProductionRealityFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "buildquality_real_data_validation_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality real data validation report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality Real Data Validation Report");
        StringAssert.Contains(report, "## 1. Scope");
        StringAssert.Contains(report, "## 2. Data integrity findings");
        StringAssert.Contains(report, "## 3. Provider validation");
        StringAssert.Contains(report, "## 4. API validation");
        StringAssert.Contains(report, "## 5. UI validation");
        StringAssert.Contains(report, "## 6. Edge cases");
        StringAssert.Contains(report, "## 7. Issues found");
        StringAssert.Contains(report, "## 8. Final verdict");
        StringAssert.Contains(report, "## Reviewer notes");
        StringAssert.Contains(report, "### What was validated");
        StringAssert.Contains(report, "### What was intentionally not changed");
        StringAssert.Contains(report, "### Known limitations");

        StringAssert.Contains(report, "TfsIntegration.UseMockClient");
        StringAssert.Contains(report, "PoTool.Api/appsettings.json");
        StringAssert.Contains(report, "PoTool.Api/appsettings.Development.json");
        StringAssert.Contains(report, "PoTool.Tools.TfsRetrievalValidator/appsettings.json");
        StringAssert.Contains(report, "Pipelines tested (ids/names): **none**");
        StringAssert.Contains(report, "/api/buildquality/rolling");
        StringAssert.Contains(report, "/api/buildquality/sprint");
        StringAssert.Contains(report, "/api/buildquality/pipeline");
        StringAssert.Contains(report, "mock BuildQuality ingestion returns empty test-run and coverage collections");
        StringAssert.Contains(report, "no architectural changes");
        StringAssert.Contains(report, "no provider changes");
        StringAssert.Contains(report, "no UI changes");
        StringAssert.Contains(report, "**NOT READY**");
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
