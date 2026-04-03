namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
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
        StringAssert.Contains(report, "docs/analysis/backlog-quality-cdc-summary.md");
        StringAssert.Contains(report, "docs/analysis/effort-diagnostics-cdc-extraction-report.md");
        StringAssert.Contains(report, "docs/analysis/effort-planning-cdc-extraction.md");
        StringAssert.Contains(report, "docs/analysis/delivery-trend-analytics-cdc-summary.md");
        StringAssert.Contains(report, "docs/analysis/forecasting-cdc-summary.md");
        StringAssert.Contains(report, "docs/analysis/portfolio-flow-projection.md");
        StringAssert.Contains(report, "docs/analysis/portfolio-flow-projection-validation.md");
        StringAssert.Contains(report, "docs/analysis/portfolio-flow-consumers-audit.md");
        StringAssert.Contains(report, "docs/analysis/application-semantic-audit.md");
        StringAssert.Contains(report, "docs/analysis/statistical-core-cleanup-report.md");
        StringAssert.Contains(report, "docs/architecture/cdc-domain-map.md");
        StringAssert.Contains(report, "docs/analysis/cdc-completion-summary.md");
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
