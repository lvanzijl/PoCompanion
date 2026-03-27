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
