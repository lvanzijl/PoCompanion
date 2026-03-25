namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class WorkspaceUxRulesDocumentTests
{
    [TestMethod]
    public void UiRules_DocumentDefinesWorkspaceTypesTileRulesAndHubConstraints()
    {
        var repositoryRoot = GetRepositoryRoot();
        var documentPath = Path.Combine(repositoryRoot, "docs", "UI_RULES.md");

        Assert.IsTrue(File.Exists(documentPath), "The UI rules document should exist under docs.");

        var document = File.ReadAllText(documentPath);

        StringAssert.Contains(document, "Workspace entry pages are divided into two explicit categories");
        StringAssert.Contains(document, "Navigation workspaces");
        StringAssert.Contains(document, "Signal workspace");
        StringAssert.Contains(document, "Health, Delivery, and Planning");
        StringAssert.Contains(document, "Trends");
        StringAssert.Contains(document, "DYNAMIC tiles are allowed **only** in signal workspaces.");
        StringAssert.Contains(document, "dynamic tile signals must answer **\"why click now?\"**");
        StringAssert.Contains(document, "Navigation hubs must:");
        StringAssert.Contains(document, "avoid heavy data loading on entry");
        StringAssert.Contains(document, "Health, Delivery, and Planning are navigation hubs with STATIC tiles.");
        StringAssert.Contains(document, "Trends is the signal workspace and may mix STATIC tiles with DYNAMIC tiles when a real signal exists.");
    }

    [TestMethod]
    public void WorkspaceHubTileAnalysis_ReflectsCurrentSignalWorkspaceExamples()
    {
        var repositoryRoot = GetRepositoryRoot();
        var documentPath = Path.Combine(repositoryRoot, "docs", "audits", "workspace_hub_tile_analysis.md");

        Assert.IsTrue(File.Exists(documentPath), "The workspace hub tile analysis document should exist under docs/audits.");

        var document = File.ReadAllText(documentPath);

        StringAssert.Contains(document, "# Workspace Hub Tile Analysis");
        StringAssert.Contains(document, "| Bug Trend | DYNAMIC |");
        StringAssert.Contains(document, "| PR Trend | DYNAMIC |");
        StringAssert.Contains(document, "| Pipeline Insights | DYNAMIC |");
        StringAssert.Contains(document, "Three workspace-entry tiles currently behave as dynamic, signal-driven tiles");
        StringAssert.Contains(document, "Health, Delivery, and Planning tiles should remain static.");
        StringAssert.Contains(document, "Bug Trend, PR Trend, and Pipeline Insights should remain dynamic only when a real signal exists.");
        StringAssert.Contains(document, "TrendsWorkspace.razor");
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
