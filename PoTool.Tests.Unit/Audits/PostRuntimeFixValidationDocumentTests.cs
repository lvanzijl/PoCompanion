namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PostRuntimeFixValidationDocumentTests
{
    [TestMethod]
    public void PostRuntimeFixValidation_ReportExistsWithRequiredSectionsAndEvidence()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "post_runtime_fix_validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The post runtime fix validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Post Runtime Fix Validation");
        StringAssert.Contains(report, "## Startup Status");
        StringAssert.Contains(report, "## Onboarding Status");
        StringAssert.Contains(report, "## Cache Status");
        StringAssert.Contains(report, "## Page Validation");
        StringAssert.Contains(report, "## Regressions");
        StringAssert.Contains(report, "## Final Decision");
        StringAssert.Contains(report, "dotnet restore PoTool.sln -m:1");
        StringAssert.Contains(report, "dotnet build PoTool.sln --no-restore -m:1");
        StringAssert.Contains(report, "/onboarding");
        StringAssert.Contains(report, "/settings/productowner/edit");
        StringAssert.Contains(report, "/api/workitems/goals/from-tfs");
        StringAssert.Contains(report, "/api/CacheSync/1");
        StringAssert.Contains(report, "workItemCount = 2478");
        StringAssert.Contains(report, "/home/health");
        StringAssert.Contains(report, "/home/backlog-overview");
        StringAssert.Contains(report, "/home/delivery/sprint");
        StringAssert.Contains(report, "/home/delivery/execution");
        StringAssert.Contains(report, "/home/portfolio-progress");
        StringAssert.Contains(report, "/home/delivery/portfolio");
        StringAssert.Contains(report, "/workspace/analysis/forecast");
        StringAssert.Contains(report, "/workspace/analysis/effort");
        StringAssert.Contains(report, "runtime integrity resolution confirmed");
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
