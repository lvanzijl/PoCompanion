using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class ClientDataStateEnforcementHardeningAuditTests
{
    private static readonly string[] GuardedFiles =
    [
        "PoTool.Client/Helpers/GeneratedClientEnvelopeExtensions.cs",
        "PoTool.Client/Services/PipelineService.cs",
        "PoTool.Client/Services/PullRequestService.cs",
        "PoTool.Client/Services/SprintDeliveryMetricsService.cs",
        "PoTool.Client/Services/WorkspaceSignalService.cs",
        "PoTool.Client/Services/WorkItemService.cs",
        "PoTool.Client/Services/WorkItemFilteringService.cs",
        "PoTool.Client/Services/HomeProductBarMetricsService.cs",
        "PoTool.Client/Services/RoadmapAnalyticsService.cs",
        "PoTool.Client/Components/WorkItems/WorkItemExplorer.razor",
        "PoTool.Client/Pages/Home/ProductRoadmaps.razor",
        "PoTool.Client/Pages/Home/ProductRoadmapEditor.razor",
        "PoTool.Client/Pages/Home/BacklogOverviewPage.razor",
        "PoTool.Client/Pages/Home/SubComponents/HealthProductSummaryCard.razor",
        "PoTool.Client/Components/Timeline/TimelinePanel.razor",
        "PoTool.Client/Components/Dependencies/DependenciesPanel.razor",
        "PoTool.Client/Components/WorkItems/SubComponents/ValidationHistoryPanel.razor",
        "PoTool.Client/Components/WorkItems/SubComponents/ValidationSummaryPanel.razor",
        "PoTool.Client/Components/WorkItems/SubComponents/WorkItemDetailPanel.razor"
    ];

    private static readonly Regex[] ForbiddenPayloadPatterns =
    [
        new(@"GetDataOrDefault<", RegexOptions.CultureInvariant),
        new(@"\.GetDataOrDefault\(", RegexOptions.CultureInvariant),
        new(@"\.GetReadOnlyListOrDefault\(", RegexOptions.CultureInvariant),
        new(@"RequireData\(", RegexOptions.CultureInvariant)
    ];

    [TestMethod]
    public void GuardedClientFiles_DoNotUsePayloadOnlyDataStateHelpers()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = new List<string>();

        foreach (var relativePath in GuardedFiles)
        {
            var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(absolutePath);

            foreach (var pattern in ForbiddenPayloadPatterns)
            {
                if (pattern.IsMatch(content))
                {
                    violations.Add($"{relativePath} matches forbidden pattern `{pattern}`");
                }
            }
        }

        Assert.IsEmpty(violations);
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
