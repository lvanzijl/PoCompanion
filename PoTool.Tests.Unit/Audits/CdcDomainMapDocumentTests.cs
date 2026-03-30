namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcDomainMapDocumentTests
{
    [TestMethod]
    public void CdcDomainMap_ReportExistsWithRequiredSectionsNodesAndDependencyRules()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "architecture", "cdc-domain-map.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC domain map document should exist under docs/architecture.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Domain Map");
        StringAssert.Contains(report, "## Nodes");
        StringAssert.Contains(report, "## Edges");
        StringAssert.Contains(report, "## Layering");
        StringAssert.Contains(report, "## Dependency Rules");
        StringAssert.Contains(report, "## Interpretation Flow");
        StringAssert.Contains(report, "## Diagram Notes");

        StringAssert.Contains(report, "Raw Work-Item Snapshots");
        StringAssert.Contains(report, "Raw Work-Item History");
        StringAssert.Contains(report, "Sprint Metadata");
        StringAssert.Contains(report, "Core Concepts");
        StringAssert.Contains(report, "BacklogQuality");
        StringAssert.Contains(report, "SprintCommitment");
        StringAssert.Contains(report, "DeliveryTrends");
        StringAssert.Contains(report, "Forecasting");
        StringAssert.Contains(report, "EffortDiagnostics");
        StringAssert.Contains(report, "EffortPlanning");
        StringAssert.Contains(report, "PortfolioFlow");
        StringAssert.Contains(report, "Shared Statistics");
        StringAssert.Contains(report, "Application Adapters");
        StringAssert.Contains(report, "Projection Persistence");
        StringAssert.Contains(report, "UI and Client Consumers");

        StringAssert.Contains(report, "SprintCommitment -> DeliveryTrends");
        StringAssert.Contains(report, "DeliveryTrends -> Forecasting");
        StringAssert.Contains(report, "EffortPlanning -> Application Adapters");
        StringAssert.Contains(report, "SprintCommitment -> PortfolioFlow");
        StringAssert.Contains(report, "application, persistence, and UI layers consume CDC outputs and must not feed semantics back into the CDC");
        StringAssert.Contains(report, "stock, inflow, throughput, and remaining scope");
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
