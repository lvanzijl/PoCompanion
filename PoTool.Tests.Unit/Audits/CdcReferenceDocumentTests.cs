namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcReferenceDocumentTests
{
    [TestMethod]
    public void CdcReference_ReportExistsWithRequiredSectionsSlicesAndCrossReferences()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "architecture", "cdc-reference.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC reference document should exist under docs/architecture.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Canonical Domain Core Reference");
        StringAssert.Contains(report, "## Purpose of the CDC");
        StringAssert.Contains(report, "## Slice Overview");
        StringAssert.Contains(report, "## Cross-Slice Dependencies");
        StringAssert.Contains(report, "## What Stays Outside the CDC");
        StringAssert.Contains(report, "## Application Boundary");
        StringAssert.Contains(report, "## Persistence Boundary");
        StringAssert.Contains(report, "## Future Architecture Directions");
        StringAssert.Contains(report, "## Compatibility Debt Still Present");

        StringAssert.Contains(report, "### Core Concepts");
        StringAssert.Contains(report, "### BacklogQuality");
        StringAssert.Contains(report, "### SprintCommitment");
        StringAssert.Contains(report, "### DeliveryTrends");
        StringAssert.Contains(report, "### Forecasting");
        StringAssert.Contains(report, "### EffortDiagnostics");
        StringAssert.Contains(report, "### EffortPlanning");
        StringAssert.Contains(report, "### PortfolioFlow");
        StringAssert.Contains(report, "### Shared Statistics");

        StringAssert.Contains(report, "Authoritative CDC consolidation");
        StringAssert.Contains(report, "docs/architecture/domain-model.md");
        StringAssert.Contains(report, "docs/rules/estimation-rules.md");
        StringAssert.Contains(report, "docs/architecture/forecasting-domain-model.md");
        StringAssert.Contains(report, "docs/architecture/portfolio-flow-model.md");
        StringAssert.Contains(report, "docs/architecture/sprint-commitment-domain-model.md");
        StringAssert.Contains(report, "docs/analysis/backlog_quality_cdc_summary.md");
        StringAssert.Contains(report, "docs/analysis/effort_diagnostics_cdc_extraction_report.md");
        StringAssert.Contains(report, "docs/analysis/effort_planning_cdc_extraction.md");
        StringAssert.Contains(report, "docs/analysis/delivery_trend_analytics_cdc_summary.md");
        StringAssert.Contains(report, "docs/analysis/forecasting_cdc_summary.md");
        StringAssert.Contains(report, "docs/analysis/portfolio_flow_projection.md");
        StringAssert.Contains(report, "docs/analysis/portfolio_flow_projection_validation.md");
        StringAssert.Contains(report, "docs/analysis/portfolio_flow_consumers_audit.md");
        StringAssert.Contains(report, "docs/analysis/application_semantic_audit.md");
        StringAssert.Contains(report, "docs/analysis/statistical_core_cleanup_report.md");
        StringAssert.Contains(report, "docs/architecture/cdc-domain-map.md");
        StringAssert.Contains(report, "docs/analysis/cdc_completion_summary.md");
        StringAssert.Contains(report, "story points");
        StringAssert.Contains(report, "effort hours");
        StringAssert.Contains(report, "stock");
        StringAssert.Contains(report, "inflow");
        StringAssert.Contains(report, "throughput");
        StringAssert.Contains(report, "remaining scope");
        StringAssert.Contains(report, "commitment");
        StringAssert.Contains(report, "spillover");
        StringAssert.Contains(report, "delivery trend");
        StringAssert.Contains(report, "forecast");
        StringAssert.Contains(report, "legacy `*Effort` DTO names");
        StringAssert.Contains(report, "compatibility aliases");
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
