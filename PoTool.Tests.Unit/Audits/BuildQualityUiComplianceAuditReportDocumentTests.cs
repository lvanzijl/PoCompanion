namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class BuildQualityUiComplianceAuditReportDocumentTests
{
    [TestMethod]
    public void BuildQualityUiComplianceAuditReport_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "buildquality-ui-compliance-audit-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality UI compliance audit report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality UI Compliance Audit Report");
        StringAssert.Contains(report, "## 1. Summary");
        StringAssert.Contains(report, "## 2. Scope handling analysis");
        StringAssert.Contains(report, "## 3. Recomputation analysis");
        StringAssert.Contains(report, "## 4. Build-level (Pipeline Insights) analysis");
        StringAssert.Contains(report, "## 5. Rolling window handling");
        StringAssert.Contains(report, "## 6. Unknown handling");
        StringAssert.Contains(report, "## 7. Drift detection results");
        StringAssert.Contains(report, "## 8. Violations");
        StringAssert.Contains(report, "## 9. Final verdict");
        StringAssert.Contains(report, "## Reviewer-ready notes");

        StringAssert.Contains(report, "Scope handling: **PASS**");
        StringAssert.Contains(report, "No recomputation: **PASS**");
        StringAssert.Contains(report, "HealthWorkspace.razor");
        StringAssert.Contains(report, "SprintTrend.razor");
        StringAssert.Contains(report, "PipelineInsights.razor");
        StringAssert.Contains(report, "BuildQualityPresentation.cs");
        StringAssert.Contains(report, "BuildQualitySummaryComponent.razor");
        StringAssert.Contains(report, "BuildQualityCompactComponent.razor");
        StringAssert.Contains(report, "BuildQualityTooltipComponent.razor");
        StringAssert.Contains(report, "GetRollingWindowAsync(...)");
        StringAssert.Contains(report, "GetSprintAsync(...)");
        StringAssert.Contains(report, "GetPipelineAsync(...)");
        StringAssert.Contains(report, "FailedBuilds + PartiallySucceededBuilds");
        StringAssert.Contains(report, "QualityStateLabel");
        StringAssert.Contains(report, "QualityStrokeColor");
        StringAssert.Contains(report, "None.");
        StringAssert.Contains(report, "**PASS — SAFE TO MERGE**");
        StringAssert.Contains(report, "refreshed `buildquality-ui-compliance-audit-report.md` to match current UI implementation after the final cleanup");
        StringAssert.Contains(report, "updated the matching MSTest document audit to enforce the current result");
        StringAssert.Contains(report, "no backend/provider/query/DTO changes");
        StringAssert.Contains(report, "no UI redesign beyond the already completed compliance fix and chart cleanup");
        StringAssert.Contains(report, "no formula/threshold/Unknown semantic changes");

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
