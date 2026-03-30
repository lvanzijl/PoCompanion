namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class FinalPreUsageValidationDocumentTests
{
    [TestMethod]
    public void FinalPreUsageValidation_ReportExistsWithRequiredSectionsAndEvidence()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "final_pre_usage_validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The final pre-usage validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Final Pre-Usage Validation");
        StringAssert.Contains(report, "## Startup");
        StringAssert.Contains(report, "## Onboarding");
        StringAssert.Contains(report, "## Cache");
        StringAssert.Contains(report, "## Pages");
        StringAssert.Contains(report, "## Regressions");
        StringAssert.Contains(report, "## Decision");
        StringAssert.Contains(report, "dotnet restore PoTool.sln -m:1");
        StringAssert.Contains(report, "dotnet build PoTool.sln --no-restore -m:1");
        StringAssert.Contains(report, "/onboarding");
        StringAssert.Contains(report, "/api/tfsconfig");
        StringAssert.Contains(report, "/api/workitems/area-paths/from-tfs");
        StringAssert.Contains(report, "/api/workitems/goals/from-tfs");
        StringAssert.Contains(report, "/api/CacheSync/1/sync");
        StringAssert.Contains(report, "workItemCount = 6743");
        StringAssert.Contains(report, "SprintMetricsProjections = 24");
        StringAssert.Contains(report, "PortfolioFlowProjections = 24");
        StringAssert.Contains(report, "/home/health");
        StringAssert.Contains(report, "/home/delivery/sprint");
        StringAssert.Contains(report, "/home/delivery/execution");
        StringAssert.Contains(report, "/home/portfolio-progress");
        StringAssert.Contains(report, "/home/delivery/portfolio");
        StringAssert.Contains(report, "/workspace/analysis/forecast");
        StringAssert.Contains(report, "/workspace/analysis/effort");
        StringAssert.Contains(report, "SYSTEM IS READY FOR REAL USAGE");
        StringAssert.Contains(report, "TFS configuration not found");
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
