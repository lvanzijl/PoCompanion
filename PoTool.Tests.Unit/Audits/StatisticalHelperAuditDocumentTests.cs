namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class StatisticalHelperAuditDocumentTests
{
    [TestMethod]
    public void StatisticalHelperAudit_ReportExistsWithRequiredSectionsAndKeyFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var auditPath = Path.Combine(repositoryRoot, "docs", "audits", "statistical_helper_audit.md");

        Assert.IsTrue(File.Exists(auditPath), "The statistical helper audit report should exist under docs/audits.");

        var audit = File.ReadAllText(auditPath);

        StringAssert.Contains(audit, "# Statistical Helper Audit");
        StringAssert.Contains(audit, "## Summary");
        StringAssert.Contains(audit, "## Inventory of Statistical Logic");
        StringAssert.Contains(audit, "## Semantic Drift");
        StringAssert.Contains(audit, "## Consolidation Opportunities");
        StringAssert.Contains(audit, "## Keep Local");
        StringAssert.Contains(audit, "## Recommended Next Step");

        StringAssert.Contains(audit, "PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsStatistics.cs");
        StringAssert.Contains(audit, "PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs");
        StringAssert.Contains(audit, "PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs");
        StringAssert.Contains(audit, "confidence");
        StringAssert.Contains(audit, "percentile");
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
