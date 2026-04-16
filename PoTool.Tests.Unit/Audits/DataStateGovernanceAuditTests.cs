using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
[TestCategory("Governance")]
public sealed class DataStateGovernanceAuditTests
{
    private const string ExclusionMarkerPrefix = "DataStateGovernance:Exclude(";
    private static readonly string[] SupportedExclusionReasons = ["ActionOnly", "MutationFlow", "DialogFlow"];
    private static readonly Regex ReadSurfacePageNamePattern = new(
        "(overview|trend|insights|execution|roadmap|planning|delivery|queue|triage|health|bug|board|fix)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReadSurfaceComponentNamePattern = new(
        "(Panel\\.razor$|Overview|Trend|Insights)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NullDrivenRenderPattern = new(
        @"@if\s*\([^)]*_(data|trendData|insightsData|projectPlanningSummary|graphData|timelineData|state)[^)]*(==\s*null|!=\s*null|is\s+null|is\s+not\s+null)[^)]*\)",
        RegexOptions.Compiled);
    private static readonly Regex ManualLoadingRenderPattern = new(
        @"@if\s*\(\s*_isLoading\b",
        RegexOptions.Compiled);

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
        foreach (var root in new[] { "PoTool.Client/Pages", "PoTool.Client/Components" })
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
    public void ReadSurfaceExclusionMarkers_AreValidAndLimitedToActionOrMutationFlows()
    {
        var repositoryRoot = GetRepositoryRoot();
        var razorFiles = Directory.GetFiles(Path.Combine(repositoryRoot, "PoTool.Client"), "*.razor", SearchOption.AllDirectories);

        var violations = razorFiles
            .Select(path =>
            {
                var content = File.ReadAllText(path);
                var marker = GetExclusionReason(content);
                if (marker is null)
                {
                    return null;
                }

                var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                if (!SupportedExclusionReasons.Contains(marker, StringComparer.Ordinal))
                {
                    return $"{relativePath} uses unsupported exclusion reason '{marker}'";
                }

                return IsValidActionOrMutationExclusion(relativePath, content)
                    ? null
                    : $"{relativePath} uses {marker} exclusion without action/mutation evidence";
            })
            .OfType<string>()
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    [TestMethod]
    public void ReadSurfaceGovernance_UsesSystemicPatternDetectionWithoutFileAllowlists()
    {
        var candidates = GetGovernedReadSurfaceFiles(GetRepositoryRoot());
        Assert.IsNotEmpty(candidates);
        CollectionAssert.DoesNotContain(
            candidates.ToArray(),
            "PoTool.Client/Pages/Home/ProductRoadmapEditor.razor",
            "Excluded mutation flows must not remain in the governed read-surface candidate set.");
    }

    [TestMethod]
    public void GovernedReadSurfaces_DoNotUseLegacyLoadingOrErrorFallbackRendering()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = GetGovernedReadSurfaceFiles(repositoryRoot)
            .SelectMany(relativePath =>
            {
                var content = ReadMarkupOnly(repositoryRoot, relativePath);
                var fileViolations = new List<string>();
                if (ManualLoadingRenderPattern.IsMatch(content))
                {
                    fileViolations.Add($"{relativePath} contains a manual _isLoading render gate");
                }

                if (content.Contains("<LoadingIndicator", StringComparison.Ordinal))
                {
                    fileViolations.Add($"{relativePath} renders LoadingIndicator directly");
                }

                if (content.Contains("<ErrorDisplay", StringComparison.Ordinal))
                {
                    fileViolations.Add($"{relativePath} renders ErrorDisplay directly");
                }

                if ((content.Contains("_errorMessage", StringComparison.Ordinal) || content.Contains("_loadError", StringComparison.Ordinal))
                    && content.Contains("Severity=\"Severity.Error\"", StringComparison.Ordinal))
                {
                    fileViolations.Add($"{relativePath} renders an inline error MudAlert fallback");
                }

                return fileViolations;
            })
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    [TestMethod]
    public void GovernedReadSurfaces_DoNotUseNullDrivenRenderingBranches()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = GetGovernedReadSurfaceFiles(repositoryRoot)
            .Where(relativePath => NullDrivenRenderPattern.IsMatch(ReadMarkupOnly(repositoryRoot, relativePath)))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static IReadOnlyList<string> GetGovernedReadSurfaceFiles(string repositoryRoot)
    {
        var pagesRoot = Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home");
        var componentsRoot = Path.Combine(repositoryRoot, "PoTool.Client", "Components");

        var pages = Directory.GetFiles(pagesRoot, "*.razor", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                if (relativePath.Contains("/Components/", StringComparison.Ordinal) || relativePath.Contains("/SubComponents/", StringComparison.Ordinal))
                {
                    return false;
                }

                var content = File.ReadAllText(path);
                return content.Contains("@page", StringComparison.Ordinal)
                       && !relativePath.EndsWith("Workspace.razor", StringComparison.Ordinal)
                       && ReadSurfacePageNamePattern.IsMatch(Path.GetFileName(path))
                       && GetExclusionReason(content) is null;
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'));

        var components = Directory.GetFiles(componentsRoot, "*.razor", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                var content = File.ReadAllText(path);
                if (relativePath.Contains("/SubComponents/", StringComparison.Ordinal))
                {
                    return false;
                }

                return ReadSurfaceComponentNamePattern.IsMatch(Path.GetFileName(path))
                       && GetExclusionReason(content) is null;
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'));

        return pages.Concat(components)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadMarkupOnly(string repositoryRoot, string relativePath)
    {
        var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var content = File.ReadAllText(absolutePath);
        var codeIndex = content.IndexOf("@code", StringComparison.Ordinal);
        return codeIndex >= 0 ? content[..codeIndex] : content;
    }

    private static string? GetExclusionReason(string content)
    {
        var markerIndex = content.IndexOf(ExclusionMarkerPrefix, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var start = markerIndex + ExclusionMarkerPrefix.Length;
        var end = content.IndexOf(')', start);
        return end > start ? content[start..end] : null;
    }

    private static bool IsValidActionOrMutationExclusion(string relativePath, string content)
        => relativePath.Contains("Fix", StringComparison.Ordinal)
           || relativePath.Contains("Editor", StringComparison.Ordinal)
           || relativePath.Contains("Onboarding", StringComparison.Ordinal)
           || content.Contains("<MudDialog", StringComparison.Ordinal)
           || content.Contains("EditForm", StringComparison.Ordinal)
           || content.Contains("OnValidSubmit", StringComparison.Ordinal)
           || content.Contains("Mutation", StringComparison.Ordinal);
}
