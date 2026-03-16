namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class EffortPlanningBoundaryAuditDocumentTests
{
    [TestMethod]
    public void EffortPlanningBoundaryAudit_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var auditPath = Path.Combine(repositoryRoot, "docs", "audits", "effort_planning_boundary_audit.md");

        Assert.IsTrue(File.Exists(auditPath), "The effort planning boundary audit should exist under docs/audits.");

        var audit = File.ReadAllText(auditPath);

        StringAssert.Contains(audit, "# Effort Planning Boundary Audit");
        StringAssert.Contains(audit, "## CDC Service Responsibilities");
        StringAssert.Contains(audit, "## Handler Responsibilities");
        StringAssert.Contains(audit, "## Statistical Helper Consistency");
        StringAssert.Contains(audit, "## Boundary Compliance");
        StringAssert.Contains(audit, "## Remaining Adapter Logic");

        StringAssert.Contains(audit, "EffortDistributionService.cs");
        StringAssert.Contains(audit, "EffortEstimationQualityService.cs");
        StringAssert.Contains(audit, "EffortEstimationSuggestionService.cs");
        StringAssert.Contains(audit, "StatisticsMath.cs");
        StringAssert.Contains(audit, "BuildRationale(...)");
        StringAssert.Contains(audit, "not fully boundary-clean yet");
        StringAssert.Contains(audit, "GetEffortDistributionQueryHandlerTests.cs");
        StringAssert.Contains(audit, "GetEffortEstimationQualityQueryHandlerTests.cs");
        StringAssert.Contains(audit, "GetEffortEstimationSuggestionsQueryHandlerTests.cs");
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
