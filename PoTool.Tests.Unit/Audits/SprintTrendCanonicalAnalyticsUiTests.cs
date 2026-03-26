namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SprintTrendCanonicalAnalyticsUiTests
{
    [TestMethod]
    public void SprintTrend_UsesCanonicalAnalyticsDtosAndDisplayComponents()
    {
        var repositoryRoot = GetRepositoryRoot();
        var sprintTrend = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "SprintTrend.razor"));

        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.Progress.ProductProgress");
        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.Progress.ProductForecastConsumed");
        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.Progress.ProductForecastRemaining");
        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.Comparison.ProgressDelta");
        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.Comparison.ForecastConsumedDelta");
        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.Comparison.ForecastRemainingDelta");
        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.PlanningQuality");
        StringAssert.Contains(sprintTrend, "selectedProductAnalytics.Insights");
        StringAssert.Contains(sprintTrend, "epic.AggregatedProgress");
        StringAssert.Contains(sprintTrend, "epic.ForecastConsumedEffort");
        StringAssert.Contains(sprintTrend, "epic.ForecastRemainingEffort");
        StringAssert.Contains(sprintTrend, "epic.ExcludedFeaturesCount");
        StringAssert.Contains(sprintTrend, "epic.IncludedFeaturesCount");
        StringAssert.Contains(sprintTrend, "epic.TotalWeight");
        StringAssert.Contains(sprintTrend, "feature.CalculatedProgress");
        StringAssert.Contains(sprintTrend, "feature.Override");
        StringAssert.Contains(sprintTrend, "feature.EffectiveProgress");
        StringAssert.Contains(sprintTrend, "feature.Effort");
        StringAssert.Contains(sprintTrend, "feature.ForecastConsumedEffort");
        StringAssert.Contains(sprintTrend, "feature.ForecastRemainingEffort");
        StringAssert.Contains(sprintTrend, "feature.IsExcluded");
        StringAssert.Contains(sprintTrend, "<CanonicalNullablePercentage");
        StringAssert.Contains(sprintTrend, "<CanonicalForecastValue");
        StringAssert.Contains(sprintTrend, "<CanonicalDeltaValue");
        StringAssert.Contains(sprintTrend, "<CanonicalPlanningQuality");
        StringAssert.Contains(sprintTrend, "<CanonicalInsightList");

        Assert.DoesNotContain(sprintTrend, "selectedProductAnalytics.Progress.ProductProgress ?? 0");
        Assert.DoesNotContain(sprintTrend, "selectedProductAnalytics.Comparison.ProgressDelta ?? 0");
        Assert.DoesNotContain(sprintTrend, "feature.CalculatedProgress ?? 0");
        Assert.DoesNotContain(sprintTrend, "feature.Override ?? 0");
        Assert.DoesNotContain(sprintTrend, "feature.EffectiveProgress ?? 0");
        Assert.DoesNotContain(sprintTrend, "GetValueOrDefault()");
    }

    [TestMethod]
    public void CanonicalDisplayComponents_PreserveUnknownStatesWithoutNumericFallbacks()
    {
        var repositoryRoot = GetRepositoryRoot();
        var nullablePercentage = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "Components", "CanonicalNullablePercentage.razor"));
        var forecastValue = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "Components", "CanonicalForecastValue.razor"));
        var deltaValue = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "Components", "CanonicalDeltaValue.razor"));

        StringAssert.Contains(nullablePercentage, "Unknown");
        StringAssert.Contains(forecastValue, "Unknown");
        StringAssert.Contains(deltaValue, "Unknown");
        Assert.DoesNotContain(nullablePercentage, "?? 0");
        Assert.DoesNotContain(forecastValue, "?? 0");
        Assert.DoesNotContain(deltaValue, "?? 0");
        Assert.DoesNotContain(nullablePercentage, "GetValueOrDefault");
        Assert.DoesNotContain(forecastValue, "GetValueOrDefault");
        Assert.DoesNotContain(deltaValue, "GetValueOrDefault");
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
