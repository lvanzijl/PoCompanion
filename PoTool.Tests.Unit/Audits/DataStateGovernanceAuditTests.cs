using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
[TestCategory("Governance")]
public sealed class DataStateGovernanceAuditTests
{
    private static readonly string[] UiRoots =
    [
        "PoTool.Client/Pages",
        "PoTool.Client/Components"
    ];

    private static readonly string[] GovernedRendererFiles =
    [
        "PoTool.Client/Components/Common/CanonicalDataStateView.razor",
        "PoTool.Client/Components/Common/TrendDataStateView.razor",
        "PoTool.Client/Pages/BugsTriage.razor",
        "PoTool.Client/Pages/Home/DeliveryTrends.razor",
        "PoTool.Client/Pages/Home/ProjectPlanningOverview.razor",
        "PoTool.Client/Pages/Home/SprintTrendActivity.razor",
        "PoTool.Client/Pages/Home/ValidationQueuePage.razor",
        "PoTool.Client/Pages/Home/ValidationTriagePage.razor"
    ];

    private static readonly string[] GovernedReadSurfaceFiles =
    [
        "PoTool.Client/Pages/Home/BacklogOverviewPage.razor",
        "PoTool.Client/Pages/Home/MultiProductPlanning.razor",
        "PoTool.Client/Pages/Home/PlanBoard.razor",
        "PoTool.Client/Pages/Home/SprintTrendActivity.razor",
        "PoTool.Client/Components/Dependencies/DependenciesPanel.razor",
        "PoTool.Client/Components/Timeline/TimelinePanel.razor"
    ];

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

    [TestMethod]
    public void UiLayers_DoNotReferenceNotReadyStates()
    {
        var repositoryRoot = GetRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "DataStateDto.NotReady",
            "DataStateResultStatus.NotReady",
            "CacheBackedClientState.NotReady"
        };

        var violations = new List<string>();
        foreach (var root in UiRoots)
        {
            var absoluteRoot = Path.Combine(repositoryRoot, root.Replace('/', Path.DirectorySeparatorChar));
            foreach (var file in Directory.GetFiles(absoluteRoot, "*.*", SearchOption.AllDirectories)
                         .Where(path => path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal)))
            {
                var content = File.ReadAllText(file);
                foreach (var token in forbiddenTokens)
                {
                    if (content.Contains(token, StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/')} contains {token}");
                    }
                }
            }
        }

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    [TestMethod]
    public void GovernedRendererFiles_DoNotUseStandaloneLegacyStateDisplays()
    {
        var repositoryRoot = GetRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "<LoadingIndicator",
            "<ErrorDisplay",
            "<DataStatePanel"
        };

        var violations = new List<string>();
        foreach (var relativePath in GovernedRendererFiles)
        {
            var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(absolutePath);
            foreach (var token in forbiddenTokens)
            {
                if (content.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add($"{relativePath} contains {token}");
                }
            }
        }

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    [TestMethod]
    public void GovernedReadSurfaces_DoNotUseManualLoadingFlagsOrLegacyErrorDisplays()
    {
        var repositoryRoot = GetRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "<LoadingIndicator",
            "<ErrorDisplay",
            "private bool _isLoading",
            "private string? _loadError",
            "private string? _errorMessage",
            "private ErrorResponse?",
            "IsLoading=\"@_isLoading\"",
            "Severity=\"Severity.Error\""
        };

        var violations = new List<string>();
        foreach (var relativePath in GovernedReadSurfaceFiles)
        {
            var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(absolutePath);

            foreach (var token in forbiddenTokens)
            {
                if (content.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add($"{relativePath} contains {token}");
                }
            }
        }

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    [TestMethod]
    public void GovernedReadSurfaces_DoNotUseNullDrivenRenderingBranches()
    {
        var repositoryRoot = GetRepositoryRoot();
        var nullDrivenRenderPattern = new Regex(
            @"@if\s*\([^)]*(==\s*null|!=\s*null|is\s+null|is\s+not\s+null)[^)]*\)",
            RegexOptions.Compiled);

        var violations = GovernedReadSurfaceFiles
            .Select(relativePath =>
            {
                var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var content = File.ReadAllText(absolutePath);
                return nullDrivenRenderPattern.IsMatch(content) ? relativePath : null;
            })
            .Where(static path => path is not null)
            .Cast<string>()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
