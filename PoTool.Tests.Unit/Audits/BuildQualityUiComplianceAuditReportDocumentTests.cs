namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BuildQualityUiComplianceAuditReportDocumentTests
{
    [TestMethod]
    public void BuildQualityUiComplianceAuditReport_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "buildquality_ui_compliance_audit_report.md");

        Assert.IsTrue(File.Exists(reportPath), "The BuildQuality UI compliance audit report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# BuildQuality UI Compliance Audit Report");
        StringAssert.Contains(report, "## 1. Summary");
        StringAssert.Contains(report, "## 2. Scope handling analysis");
        StringAssert.Contains(report, "## 3. Recomputation analysis");
        StringAssert.Contains(report, "## 4. Build-level (Pipeline Insights) analysis");
        StringAssert.Contains(report, "## 5. Rolling window handling");
        StringAssert.Contains(report, "## 6. Unknown handling");
        StringAssert.Contains(report, "## 7. Violations");
        StringAssert.Contains(report, "## 8. Final verdict");

        StringAssert.Contains(report, "Scope handling: **PASS**");
        StringAssert.Contains(report, "No recomputation: **FAIL**");
        StringAssert.Contains(report, "HealthWorkspace.razor");
        StringAssert.Contains(report, "SprintTrend.razor");
        StringAssert.Contains(report, "PipelineInsights.razor");
        StringAssert.Contains(report, "BuildQualityPresentation.cs");
        StringAssert.Contains(report, "BuildQualitySummaryComponent.razor");
        StringAssert.Contains(report, "BuildQualityCompactComponent.razor");
        StringAssert.Contains(report, "BuildQualityTooltipComponent.razor");
        StringAssert.Contains(report, "GoodThreshold = 0.90d");
        StringAssert.Contains(report, "WarningThreshold = 0.70d");
        StringAssert.Contains(report, "FailedBuilds + PartiallySucceededBuilds");
        StringAssert.Contains(report, "GetOverallState(detail.Result)");
        StringAssert.Contains(report, "GetRollingWindowAsync");
        StringAssert.Contains(report, "GetSprintAsync");
        StringAssert.Contains(report, "GetPipelineAsync");
        StringAssert.Contains(report, "**FAIL**");
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
