namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PreCleanupAppValidationDocumentTests
{
    [TestMethod]
    public void PreCleanupAppValidation_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "pre-cleanup-app-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The pre-cleanup app validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Pre-Cleanup App Validation");
        StringAssert.Contains(report, "## Build / Startup Status");
        StringAssert.Contains(report, "## Page Rendering Status");
        StringAssert.Contains(report, "## Binding / DTO Status");
        StringAssert.Contains(report, "## Semantic Label Validation");
        StringAssert.Contains(report, "## Regressions Found");
        StringAssert.Contains(report, "## Safe To Continue Decision");
        StringAssert.Contains(report, "dotnet restore PoTool.sln -m:1");
        StringAssert.Contains(report, "dotnet build PoTool.sln --no-restore -m:1");
        StringAssert.Contains(report, "/home/health");
        StringAssert.Contains(report, "/home/delivery/sprint");
        StringAssert.Contains(report, "/home/delivery/execution");
        StringAssert.Contains(report, "/home/portfolio-progress");
        StringAssert.Contains(report, "/home/delivery/portfolio");
        StringAssert.Contains(report, "/workspace/analysis/effort");
        StringAssert.Contains(report, "/workspace/analysis/forecast");
        StringAssert.Contains(report, "/api/metrics/effort-estimation-quality");
        StringAssert.Contains(report, "/api/metrics/effort-estimation-suggestions");
        StringAssert.Contains(report, "UseMockClient");
        StringAssert.Contains(report, "/api/workitems/goals/from-tfs");
        StringAssert.Contains(report, "not yet safe to continue");
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
