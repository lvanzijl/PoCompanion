namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class WorkspaceUxRulesDocumentTests
{
    [TestMethod]
    public void UiRules_DocumentDefinesWorkspaceTypesTileRulesAndHubConstraints()
    {
        var repositoryRoot = GetRepositoryRoot();
        var mirrorPath = Path.Combine(repositoryRoot, "docs", "rules", "ui-rules.md");
        var authoritativePath = Path.Combine(repositoryRoot, ".github", "copilot-instructions.md");

        Assert.IsTrue(File.Exists(mirrorPath), "The UI rules mirror should exist under docs/rules.");
        Assert.IsTrue(File.Exists(authoritativePath), "The authoritative copilot instructions should exist under .github.");

        var mirror = File.ReadAllText(mirrorPath);
        var authoritative = File.ReadAllText(authoritativePath);

        StringAssert.Contains(mirror, "No semantic interpretation is allowed.");
        StringAssert.Contains(mirror, "Historical leakage");
        StringAssert.Contains(mirror, "../../.github/copilot-instructions.md");
        StringAssert.Contains(authoritative, "### 12.4 Workspace rules");
        StringAssert.Contains(authoritative, "Navigation workspaces use static tiles and should not require heavy hub-entry data loading.");
        StringAssert.Contains(authoritative, "Signal workspaces may expose dynamic signal tiles only when the runtime signal is meaningful and independently loaded.");
        StringAssert.Contains(authoritative, "Static tiles must remain understandable without live data.");
        StringAssert.Contains(authoritative, "Dynamic tiles must degrade gracefully and must not require heavy hub-entry queries.");
    }

    [TestMethod]
    public void WorkspaceHubTileAnalysis_ReflectsCurrentSignalWorkspaceExamples()
    {
        var repositoryRoot = GetRepositoryRoot();
        var documentPath = Path.Combine(repositoryRoot, "docs", "analysis", "workspace-hub-tile-analysis.md");

        Assert.IsTrue(File.Exists(documentPath), "The workspace hub tile analysis document should exist under docs/analysis.");

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
