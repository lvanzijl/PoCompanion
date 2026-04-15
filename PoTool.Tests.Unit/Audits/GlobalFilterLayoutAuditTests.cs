namespace PoTool.Tests.Unit.Audits;

[TestClass]
[TestCategory("Governance")]
public sealed class GlobalFilterLayoutAuditTests
{
    [TestMethod]
    public void MainLayout_PlacesSharedFilterControlsBeforePageBody()
    {
        var layoutPath = Path.Combine(GetRepositoryRoot(), "PoTool.Client", "Layout", "MainLayout.razor");
        var content = File.ReadAllText(layoutPath);

        var summaryIndex = content.IndexOf("<FilterSummaryBar />", StringComparison.Ordinal);
        var controlsIndex = content.IndexOf("<GlobalFilterControls />", StringComparison.Ordinal);
        var bodyIndex = content.IndexOf("@Body", StringComparison.Ordinal);

        Assert.IsTrue(summaryIndex >= 0, "MainLayout must render the shared filter summary.");
        Assert.IsTrue(controlsIndex >= 0, "MainLayout must render the shared filter controls.");
        Assert.IsTrue(bodyIndex >= 0, "MainLayout must render the page body.");
        Assert.IsTrue(
            summaryIndex < controlsIndex && controlsIndex < bodyIndex,
            "MainLayout must place shared filter controls directly after the shared filter summary and before page content.");
    }

    [TestMethod]
    public void SharedFilterChrome_IsHostedOnlyByMainLayout()
    {
        var repositoryRoot = GetRepositoryRoot();
        var razorFiles = Directory.GetFiles(Path.Combine(repositoryRoot, "PoTool.Client"), "*.razor", SearchOption.AllDirectories);

        var summaryHosts = razorFiles
            .Where(path => File.ReadAllText(path).Contains("<FilterSummaryBar", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .ToArray();

        var controlHosts = razorFiles
            .Where(path => File.ReadAllText(path).Contains("<GlobalFilterControls", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "PoTool.Client/Layout/MainLayout.razor" },
            summaryHosts,
            "Shared filter summary should be hosted only by MainLayout.");

        CollectionAssert.AreEqual(
            new[] { "PoTool.Client/Layout/MainLayout.razor" },
            controlHosts,
            "Shared filter controls should be hosted only by MainLayout.");
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
