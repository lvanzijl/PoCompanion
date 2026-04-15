namespace PoTool.Tests.Unit.Audits;

[TestClass]
[TestCategory("Governance")]
public sealed class DataStateGovernanceAuditTests
{
    [TestMethod]
    public void DataStatePanel_IsOnlyUsedByTheGovernedDataStateView()
    {
        var repositoryRoot = GetRepositoryRoot();
        var razorFiles = Directory.GetFiles(Path.Combine(repositoryRoot, "PoTool.Client"), "*.razor", SearchOption.AllDirectories);

        var panelHosts = razorFiles
            .Where(path => File.ReadAllText(path).Contains("<DataStatePanel", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "PoTool.Client/Components/Common/DataStateView.razor"
            },
            panelHosts,
            "Deprecated direct DataStatePanel usage must stay isolated behind the governed DataStateView wrapper.");
    }

    [TestMethod]
    public void ClientPages_DoNotRenderDataStatePanelDirectly()
    {
        var repositoryRoot = GetRepositoryRoot();
        var pageFiles = Directory.GetFiles(Path.Combine(repositoryRoot, "PoTool.Client", "Pages"), "*.razor", SearchOption.AllDirectories);

        var directUsages = pageFiles
            .Where(path => File.ReadAllText(path).Contains("<DataStatePanel", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            directUsages,
            "Pages must render canonical data states through DataStateView or CanonicalDataStateView instead of using DataStatePanel directly.");
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
