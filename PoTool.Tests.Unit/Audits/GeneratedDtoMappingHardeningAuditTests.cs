namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class GeneratedDtoMappingHardeningAuditTests
{
    [TestMethod]
    public void GeneratedDtoMappingPath_DoesNotUseJsonOrReflection()
    {
        var repositoryRoot = GetRepositoryRoot();
        var files = new[]
        {
            "PoTool.Client/Helpers/GeneratedCacheEnvelopeHelper.cs",
            "PoTool.Client/Helpers/GeneratedClientDtoMappings.cs",
            "PoTool.Client/Helpers/GeneratedClientEnvelopeContracts.cs",
            "PoTool.Client/Helpers/GeneratedClientEnvelopeExtensions.cs",
            "PoTool.Client/ApiClient/ApiClient.DeliveryFilters.cs",
            "PoTool.Client/ApiClient/ApiClient.PipelineFilters.cs",
            "PoTool.Client/ApiClient/ApiClient.PullRequestFilters.cs",
            "PoTool.Client/ApiClient/ApiClient.SprintFilters.cs",
            "PoTool.Client/ApiClient/ApiClient.PortfolioConsumption.cs",
            "PoTool.Client/Services/BuildQualityService.cs",
            "PoTool.Client/Services/MetricsStateService.cs",
            "PoTool.Client/Services/PipelineStateService.cs",
            "PoTool.Client/Services/PullRequestStateService.cs",
            "PoTool.Client/Services/ProjectService.cs",
            "PoTool.Client/Services/ReleasePlanningService.cs"
        };

        var forbiddenPatterns = new[]
        {
            "JsonSerializer",
            "System.Reflection",
            "BindingFlags",
            "GetProperty(",
            "Deserialize(",
            "Serialize("
        };

        foreach (var relativePath in files)
        {
            var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var source = File.ReadAllText(absolutePath);

            foreach (var forbiddenPattern in forbiddenPatterns)
            {
                Assert.IsFalse(
                    source.Contains(forbiddenPattern, StringComparison.Ordinal),
                    $"{relativePath} should not contain '{forbiddenPattern}'.");
            }
        }
    }

    [TestMethod]
    public void GeneratedClientCallSites_AvoidInlineSharedMappingLambdas()
    {
        var repositoryRoot = GetRepositoryRoot();
        var files = new[]
        {
            "PoTool.Client/ApiClient/ApiClient.DeliveryFilters.cs",
            "PoTool.Client/ApiClient/ApiClient.PipelineFilters.cs",
            "PoTool.Client/ApiClient/ApiClient.PullRequestFilters.cs",
            "PoTool.Client/ApiClient/ApiClient.SprintFilters.cs",
            "PoTool.Client/Services/BuildQualityService.cs",
            "PoTool.Client/Services/HomeProductBarMetricsService.cs",
            "PoTool.Client/Services/MetricsStateService.cs",
            "PoTool.Client/Services/PipelineService.cs",
            "PoTool.Client/Services/PipelineStateService.cs",
            "PoTool.Client/Services/PullRequestService.cs",
            "PoTool.Client/Services/PullRequestStateService.cs",
            "PoTool.Client/Services/ReleasePlanningService.cs",
            "PoTool.Client/Services/SprintDeliveryMetricsService.cs",
            "PoTool.Client/Services/WorkItemService.cs",
            "PoTool.Client/Services/WorkspaceSignalService.cs"
        };

        var forbiddenPatterns = new[]
        {
            "static data => data.ToShared()",
            "static data => data.ToReadOnlyList()"
        };

        foreach (var relativePath in files)
        {
            var absolutePath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var source = File.ReadAllText(absolutePath);

            foreach (var forbiddenPattern in forbiddenPatterns)
            {
                Assert.IsFalse(
                    source.Contains(forbiddenPattern, StringComparison.Ordinal),
                    $"{relativePath} should use the generated-client envelope ergonomics instead of inline mapper '{forbiddenPattern}'.");
            }
        }
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
