namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class ProjectionDeterminismAuditDocumentTests
{
    [TestMethod]
    public void ProjectionDeterminismAudit_ReportExistsWithRequiredServicesChecksAndCoverage()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "projection-determinism-audit.md");

        Assert.IsTrue(File.Exists(reportPath), "The projection determinism audit report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Projection Determinism Audit");
        StringAssert.Contains(report, "## Projection inventory");
        StringAssert.Contains(report, "## Determinism verification");
        StringAssert.Contains(report, "## Coverage updates");
        StringAssert.Contains(report, "## Audit conclusion");
        StringAssert.Contains(report, "SprintDeliveryProjectionService");
        StringAssert.Contains(report, "PortfolioFlowProjectionService");
        StringAssert.Contains(report, "CompletionForecastService");
        StringAssert.Contains(report, "VelocityCalibrationService");
        StringAssert.Contains(report, "EffortTrendForecastService");
        StringAssert.Contains(report, "rebuild deterministically");
        StringAssert.Contains(report, "do not duplicate inflow");
        StringAssert.Contains(report, "do not duplicate throughput");
        StringAssert.Contains(report, "produce identical results across rebuilds");
        StringAssert.Contains(report, "Compute_RepeatedWithSameInputs_ProducesIdenticalProjection");
        StringAssert.Contains(report, "ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionDeterministicallyWithoutDuplicates");
        StringAssert.Contains(report, "ForecastingServices_RepeatedWithSameInputs_ProduceIdenticalOutputs");
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
