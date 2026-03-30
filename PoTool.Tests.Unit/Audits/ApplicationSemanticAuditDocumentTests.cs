namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ApplicationSemanticAuditDocumentTests
{
    [TestMethod]
    public void ApplicationSemanticAudit_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = $"{repositoryRoot}/docs/analysis/application_semantic_audit.md";

        Assert.IsTrue(File.Exists(reportPath), "The application semantic audit report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Application Semantic Audit — Effort vs StoryPoints Usage");
        StringAssert.Contains(report, "## Scope");
        StringAssert.Contains(report, "## Legacy naming detected");
        StringAssert.Contains(report, "## Semantic mismatches");
        StringAssert.Contains(report, "## Canonical replacements");
        StringAssert.Contains(report, "## Backward compatibility requirements");
        StringAssert.Contains(report, "## UI usage evaluation");
        StringAssert.Contains(report, "## Migration strategy");

        StringAssert.Contains(report, "EpicCompletionForecastDto.cs");
        StringAssert.Contains(report, "PortfolioDeliveryDtos.cs");
        StringAssert.Contains(report, "SprintExecutionDtos.cs");
        StringAssert.Contains(report, "SprintTrendDtos.cs");
        StringAssert.Contains(report, "DeliveryTrends.razor");
        StringAssert.Contains(report, "SprintExecution.razor");
        StringAssert.Contains(report, "PortfolioProgressPage.razor");
        StringAssert.Contains(report, "ProductRoadmaps.razor");

        StringAssert.Contains(report, "TotalEffort");
        StringAssert.Contains(report, "DoneEffort");
        StringAssert.Contains(report, "RemainingEffort");
        StringAssert.Contains(report, "PlannedEffort");
        StringAssert.Contains(report, "CommittedStoryPoints");
        StringAssert.Contains(report, "DeliveredStoryPoints");
        StringAssert.Contains(report, "RemainingStoryPoints");
        StringAssert.Contains(report, "AddedStoryPoints");
        StringAssert.Contains(report, "SpilloverStoryPoints");
        StringAssert.Contains(report, "Must stay for backward compatibility");
        StringAssert.Contains(report, "effort-hour fields as if they were story points");
        StringAssert.Contains(report, "Portfolio flow remains an **effort-based proxy model**");
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists($"{current.FullName}/PoTool.sln"))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
