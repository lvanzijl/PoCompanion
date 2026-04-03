using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class ClientDataStateContractBatch5AuditTests
{
    private static readonly string[] TargetFiles =
    [
        "PoTool.Client/Components/Forecast/ForecastPanel.razor",
        "PoTool.Client/Components/BacklogHealth/BacklogHealthPanel.razor",
        "PoTool.Client/Components/EffortDistribution/EffortDistributionPanel.razor",
        "PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor",
        "PoTool.Client/Pages/Home/PlanBoard.razor",
        "PoTool.Client/Pages/Home/TrendsWorkspace.razor",
        "PoTool.Client/Pages/Home/BugOverview.razor",
        "PoTool.Client/Components/ReleasePlanning/AddLaneDialog.razor"
    ];

    private static readonly Regex[] ForbiddenPatterns =
    [
        new("@inject\\s+IMetricsClient\\b", RegexOptions.CultureInvariant),
        new("@inject\\s+IPullRequestsClient\\b", RegexOptions.CultureInvariant),
        new("MetricsClient\\.", RegexOptions.CultureInvariant),
        new("PullRequestsClient\\.", RegexOptions.CultureInvariant),
        new("WorkItemService\\.GetAllAsync\\(", RegexOptions.CultureInvariant),
        new("WorkItemService\\.GetByRootIdsAsync\\(", RegexOptions.CultureInvariant),
        new("WorkItemService\\.GetAllWithValidationAsync\\(", RegexOptions.CultureInvariant)
    ];

    private static readonly Regex RequiredDataStatePattern = new(
        "DataStateViewModel<|<DataStateView|Get[A-Za-z]+StateAsync",
        RegexOptions.CultureInvariant);

    [TestMethod]
    public void Batch5Targets_DoNotUseRawCacheBackedClientCalls()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = new List<string>();

        foreach (var relativePath in TargetFiles)
        {
            var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(absolutePath);

            foreach (var pattern in ForbiddenPatterns)
            {
                if (pattern.IsMatch(content))
                {
                    violations.Add($"{relativePath} matches forbidden pattern `{pattern}`");
                }
            }
        }

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    [TestMethod]
    public void Batch5Targets_ContainExplicitDataStateUsage()
    {
        var repositoryRoot = GetRepositoryRoot();
        var missing = TargetFiles
            .Where(relativePath =>
            {
                var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var content = File.ReadAllText(absolutePath);
                return !RequiredDataStatePattern.IsMatch(content);
            })
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), missing);
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
