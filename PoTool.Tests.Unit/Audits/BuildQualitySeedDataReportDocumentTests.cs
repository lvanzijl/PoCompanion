namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualitySeedDataReportDocumentTests
{
    [TestMethod]
    public void BuildQualitySeedDataReport_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "buildquality_seed_data_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality seed data report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality Seed Data Report");
        StringAssert.Contains(report, "## 1. Dataset extended");
        StringAssert.Contains(report, "## 2. Scenario coverage");
        StringAssert.Contains(report, "## 3. Test validation");
        StringAssert.Contains(report, "## 4. UI validation (manual)");
        StringAssert.Contains(report, "## 5. Issues found");
        StringAssert.Contains(report, "## 6. Final verdict");

        StringAssert.Contains(report, "Incident Response Control");
        StringAssert.Contains(report, "5 builds");
        StringAssert.Contains(report, "3 test runs");
        StringAssert.Contains(report, "3 coverage entries");
        StringAssert.Contains(report, "READY");
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
