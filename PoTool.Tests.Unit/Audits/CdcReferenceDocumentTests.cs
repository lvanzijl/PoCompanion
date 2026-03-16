namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcReferenceDocumentTests
{
    [TestMethod]
    public void CdcReference_ReportExistsWithRequiredSectionsSlicesAndCrossReferences()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "domain", "cdc_reference.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC reference document should exist under docs/domain.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Reference");
        StringAssert.Contains(report, "## CDC Overview");
        StringAssert.Contains(report, "## Core primitives");
        StringAssert.Contains(report, "## Event signals");
        StringAssert.Contains(report, "## Domain slices");
        StringAssert.Contains(report, "## Projection consumers");
        StringAssert.Contains(report, "## Application boundaries");

        StringAssert.Contains(report, "### Estimation semantics");
        StringAssert.Contains(report, "### Backlog quality");
        StringAssert.Contains(report, "### Effort diagnostics");
        StringAssert.Contains(report, "### Statistical helpers");
        StringAssert.Contains(report, "### Delivery trends");
        StringAssert.Contains(report, "### Forecasting");
        StringAssert.Contains(report, "### Portfolio flow");
        StringAssert.Contains(report, "### Sprint commitment");

        StringAssert.Contains(report, "single authoritative CDC reference");
        StringAssert.Contains(report, "To avoid duplicated semantic descriptions");
        StringAssert.Contains(report, "docs/domain/domain_model.md");
        StringAssert.Contains(report, "docs/domain/rules/estimation_rules.md");
        StringAssert.Contains(report, "docs/domain/backlog_quality_domain_model.md");
        StringAssert.Contains(report, "docs/domain/effort_diagnostics_domain_model.md");
        StringAssert.Contains(report, "docs/domain/forecasting_domain_model.md");
        StringAssert.Contains(report, "docs/domain/portfolio_flow_model.md");
        StringAssert.Contains(report, "docs/domain/sprint_commitment_domain_model.md");
        StringAssert.Contains(report, "docs/audits/backlog_quality_cdc_summary.md");
        StringAssert.Contains(report, "docs/audits/effort_diagnostics_cdc_extraction_report.md");
        StringAssert.Contains(report, "docs/audits/delivery_trend_analytics_cdc_summary.md");
        StringAssert.Contains(report, "docs/audits/forecasting_cdc_summary.md");
        StringAssert.Contains(report, "docs/audits/portfolio_flow_projection.md");
        StringAssert.Contains(report, "docs/audits/portfolio_flow_projection_validation.md");
        StringAssert.Contains(report, "docs/audits/portfolio_flow_consumers_audit.md");
        StringAssert.Contains(report, "docs/audits/application_semantic_audit.md");
        StringAssert.Contains(report, "docs/exploration/sprint_commitment_domain_exploration.md");
        StringAssert.Contains(report, "Projection reports document materialization, validation, and consumer migration.");
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
