namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PortfolioCdcUiAuditTests
{
    [TestMethod]
    public void PortfolioCdcReadOnlyPanel_ConsumesReadOnlyDtosWithoutUiAggregations()
    {
        var repositoryRoot = GetRepositoryRoot();
        var panelPath = $"{repositoryRoot}/PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor";
        var pagePath = $"{repositoryRoot}/PoTool.Client/Pages/Home/PortfolioProgressPage.razor";

        var panel = File.ReadAllText(panelPath);
        var page = File.ReadAllText(pagePath);

        StringAssert.Contains(panel, "PortfolioProgressDto");
        StringAssert.Contains(panel, "PortfolioSnapshotDto");
        StringAssert.Contains(panel, "PortfolioComparisonDto");
        StringAssert.Contains(panel, "PortfolioTrendDto");
        StringAssert.Contains(panel, "PortfolioDecisionSignalDto");
        StringAssert.Contains(panel, "GetPortfolioProgressAsync");
        StringAssert.Contains(panel, "GetPortfolioSnapshotsAsync");
        StringAssert.Contains(panel, "GetPortfolioComparisonAsync");
        StringAssert.Contains(panel, "GetPortfolioTrendsAsync");
        StringAssert.Contains(panel, "GetPortfolioSignalsAsync");
        Assert.IsFalse(panel.Contains(".Sum(", StringComparison.Ordinal), "The UI component should not aggregate domain values.");
        Assert.IsFalse(panel.Contains(".Average(", StringComparison.Ordinal), "The UI component should not calculate averages.");
        Assert.IsFalse(panel.Contains(".GroupBy(", StringComparison.Ordinal), "The UI component should not regroup trend data.");
        StringAssert.Contains(page, "<PortfolioCdcReadOnlyPanel />");
    }

    [TestMethod]
    public void PortfolioCdcReadOnlyPanel_RendersDecisionFirstStructuralLayout()
    {
        var repositoryRoot = GetRepositoryRoot();
        var panelPath = $"{repositoryRoot}/PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor";

        var panel = File.ReadAllText(panelPath);

        StringAssert.Contains(panel, "Portfolio summary");
        StringAssert.Contains(panel, "Decision signal");
        StringAssert.Contains(panel, "Trend Overview");
        StringAssert.Contains(panel, "Changed Work Packages");
        StringAssert.Contains(panel, "Stable Work Packages");
        StringAssert.Contains(panel, "Snapshot comparison");
        StringAssert.Contains(panel, "MudExpansionPanels");

        var summaryIndex = panel.IndexOf("Portfolio summary", StringComparison.Ordinal);
        var signalIndex = panel.IndexOf("Decision signal", StringComparison.Ordinal);
        var trendIndex = panel.IndexOf("Trend Overview", StringComparison.Ordinal);
        var changedWorkPackagesIndex = panel.IndexOf("Changed Work Packages", StringComparison.Ordinal);

        Assert.IsTrue(summaryIndex >= 0 && signalIndex > summaryIndex && trendIndex > signalIndex && changedWorkPackagesIndex > trendIndex,
            "The CDC layout should render summary, decision signal, trend overview, then changed work packages in order.");
    }

    [TestMethod]
    public void PortfolioCdcReadOnlyPanel_RendersRefinedTrendAndSignalDetails()
    {
        var repositoryRoot = GetRepositoryRoot();
        var panelPath = $"{repositoryRoot}/PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor";

        var panel = File.ReadAllText(panelPath);

        StringAssert.Contains(panel, "vs {GetCompactSnapshotLabel(_comparison.PreviousSnapshotLabel)} ({FormatDate(_comparison.PreviousTimestamp)})");
        StringAssert.Contains(panel, "Stable Work Packages (@GetStableWorkPackageItems().Count unchanged)");
        StringAssert.Contains(panel, "GetChangedWorkPackageDeltaText");
        StringAssert.Contains(panel, "GetChangeReasonSummary");
        StringAssert.Contains(panel, "FormatTrendOverviewDelta(context.Delta)");
        StringAssert.Contains(panel, "\"— →\"");
        StringAssert.Contains(panel, "GetSnapshotDisplayLabel");
        StringAssert.Contains(panel, "GetCompactSnapshotLabel");
        StringAssert.Contains(panel, "CalculateDisplayedRatioDelta");
        StringAssert.Contains(panel, "Math.Round(value, 0, MidpointRounding.AwayFromZero)");
        Assert.IsFalse(panel.Contains("Secondary signals:", StringComparison.Ordinal), "Secondary signal text should be deemphasized without a prefix label.");
        Assert.IsFalse(panel.Contains("<MudTh>Direction</MudTh>", StringComparison.Ordinal), "Direction columns should be merged into delta columns.");
        Assert.IsFalse(panel.Contains("Active-only", StringComparison.Ordinal), "Trend rows should not repeat active-only noise.");
        Assert.IsFalse(panel.Contains("One dominant signal first", StringComparison.Ordinal), "Decision helper text should be removed once the UI is self-explanatory.");
        Assert.IsFalse(panel.Contains("Snapshot trend, delta, and direction are aligned for quick scanning.", StringComparison.Ordinal), "Trend helper text should be removed.");
        Assert.IsFalse(panel.Contains("Rows with progress, lifecycle, or weight changes are shown first.", StringComparison.Ordinal), "Changed work package helper text should be removed.");
        Assert.IsFalse(panel.Contains("[P]", StringComparison.Ordinal), "Unexplained shorthand should not remain in changed work package indicators.");
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
