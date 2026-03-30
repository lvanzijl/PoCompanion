namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CompatibilityCleanupPhase3DocumentTests
{
    [TestMethod]
    public void CompatibilityCleanupPhase3_ReportAndCdcReferenceContainRequiredUpdates()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "compatibility-cleanup-phase3.md");
        var cdcReferencePath = Path.Combine(repositoryRoot, "docs", "architecture", "cdc-reference.md");

        Assert.IsTrue(File.Exists(reportPath), "The phase 3 compatibility cleanup audit should exist under docs/analysis.");
        Assert.IsTrue(File.Exists(cdcReferencePath), "The CDC reference should exist under docs/architecture.");

        var report = File.ReadAllText(reportPath);
        var cdcReference = File.ReadAllText(cdcReferencePath);

        StringAssert.Contains(report, "# Compatibility Cleanup Phase 3");
        StringAssert.Contains(report, "## Removed Legacy Alias Fields");
        StringAssert.Contains(report, "## DTOs Updated");
        StringAssert.Contains(report, "## Handlers and Mappers Updated");
        StringAssert.Contains(report, "## UI Consumers Updated");
        StringAssert.Contains(report, "## Fields Intentionally Kept as Effort");
        StringAssert.Contains(report, "## Validation Results");
        StringAssert.Contains(report, "## Remaining Compatibility Debt");
        StringAssert.Contains(report, "EpicCompletionForecastDto.TotalEffort");
        StringAssert.Contains(report, "SprintForecast.ExpectedCompletedEffort");
        StringAssert.Contains(report, "FeatureProgressDto.DoneEffort");
        StringAssert.Contains(report, "EpicProgressDto.DoneEffort");
        StringAssert.Contains(report, "FeatureDeliveryDto.TotalEffort");
        StringAssert.Contains(report, "PoTool.Client/swagger.json");
        StringAssert.Contains(report, "PoTool.Client/ApiClient/ApiClient.g.cs");
        StringAssert.Contains(report, "ProductDeliveryDto.CompletedEffort");
        StringAssert.Contains(report, "PortfolioDeliverySummaryDto.TotalCompletedEffort");

        StringAssert.Contains(cdcReference, "removed in compatibility cleanup phase 3");
        StringAssert.Contains(cdcReference, "no longer part of the active transport contract");
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
