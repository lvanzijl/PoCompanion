using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class EffortPlanningCdcExtractionAuditTests
{
    [TestMethod]
    public void EffortPlanningExtraction_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "effort-planning-cdc-extraction.md");

        Assert.IsTrue(File.Exists(reportPath), "The effort planning extraction report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);
        StringAssert.Contains(report, "# EffortPlanning CDC Extraction");
        StringAssert.Contains(report, "## Removed Handler Calculations");
        StringAssert.Contains(report, "## New CDC EffortPlanning Slice");
        StringAssert.Contains(report, "## Updated Handlers");
        StringAssert.Contains(report, "## Statistical Helper Usage");
        StringAssert.Contains(report, "## Test Adjustments");
        StringAssert.Contains(report, "## Lines of Code Removed");
    }

    [TestMethod]
    public void EffortPlanningHandlers_StayAtOrchestrationAndDtoMappingLevel()
    {
        var repositoryRoot = GetRepositoryRoot();
        var distributionHandler = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs");
        var qualityHandler = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs");
        var suggestionsHandler = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs");

        StringAssert.Contains(distributionHandler, "_effortDistributionService.Analyze");
        StringAssert.Contains(qualityHandler, "_effortEstimationQualityService.Analyze");
        StringAssert.Contains(suggestionsHandler, "_effortEstimationSuggestionService.GenerateSuggestion");

        Assert.IsFalse(distributionHandler.Contains("CalculateHeatMapCells", StringComparison.Ordinal));
        Assert.IsFalse(qualityHandler.Contains("StatisticsMath.Variance", StringComparison.Ordinal));
        Assert.IsFalse(suggestionsHandler.Contains("CalculateSimilarity", StringComparison.Ordinal));
        Assert.IsFalse(suggestionsHandler.Contains("CalculateConfidence", StringComparison.Ordinal));
        Assert.IsFalse(Regex.IsMatch(distributionHandler, @"\bMath\."), "Distribution handler should not contain direct math helper calls.");
        Assert.IsFalse(Regex.IsMatch(qualityHandler, @"\bMath\."), "Quality handler should not contain direct math helper calls.");
    }

    private static string ReadRepositoryFile(string repositoryRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
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
