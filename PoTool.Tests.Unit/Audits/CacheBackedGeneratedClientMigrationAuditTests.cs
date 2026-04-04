namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class CacheBackedGeneratedClientMigrationAuditTests
{
    [TestMethod]
    public void FullyMigratedCacheBackedServices_DoNotUseRawHttpClientOrApiRoutes()
    {
        var repositoryRoot = GetRepositoryRoot();
        var servicePaths = new[]
        {
            "PoTool.Client/Services/BuildQualityService.cs",
            "PoTool.Client/Services/MetricsStateService.cs",
            "PoTool.Client/Services/PipelineStateService.cs",
            "PoTool.Client/Services/PullRequestStateService.cs",
            "PoTool.Client/Services/ProjectService.cs",
            "PoTool.Client/Services/ReleasePlanningService.cs"
        };

        foreach (var relativePath in servicePaths)
        {
            var source = File.ReadAllText(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            AssertForbiddenPatternsAbsent(source, relativePath);
        }
    }

    [TestMethod]
    public void WorkItemService_CacheBackedReadMethods_DoNotUseRawHttpClientOrApiRoutes()
    {
        var repositoryRoot = GetRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "WorkItemService.cs"));

        var methodNames = new[]
        {
            "GetAllAsync",
            "GetAllStateAsync",
            "GetAllWithValidationAsync",
            "GetAllWithValidationStateAsync",
            "GetValidationTriageSummaryAsync",
            "GetValidationTriageSummaryStateAsync",
            "GetValidationQueueAsync",
            "GetValidationQueueStateAsync",
            "GetValidationFixSessionAsync",
            "GetValidationFixSessionStateAsync",
            "GetByRootIdsAsync",
            "GetByRootIdsStateAsync"
        };

        foreach (var methodName in methodNames)
        {
            AssertMethodDoesNotContainForbiddenPatterns(source, methodName);
        }
    }

    private static void AssertMethodDoesNotContainForbiddenPatterns(string source, string methodName)
    {
        var signature = $" {methodName}(";
        var startIndex = source.LastIndexOf(signature, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            Assert.Fail($"Could not locate method '{methodName}'.");
        }

        var bodyStartIndex = source.LastIndexOf('\n', startIndex);
        var nextMethodIndex = source.IndexOf("\n    public", startIndex + signature.Length, StringComparison.Ordinal);
        var segment = nextMethodIndex >= 0
            ? source[bodyStartIndex..nextMethodIndex]
            : source[bodyStartIndex..];

        AssertForbiddenPatternsAbsent(segment, methodName);
    }

    private static void AssertForbiddenPatternsAbsent(string source, string scope)
    {
        var forbiddenPatterns = new[]
        {
            "HttpClient",
            "_httpClient",
            "GetFromJsonAsync",
            "PostAsJsonAsync",
            "PutAsJsonAsync",
            "ReadFromJsonAsync",
            "\"/api/",
            "\"api/"
        };

        foreach (var forbiddenPattern in forbiddenPatterns)
        {
            Assert.IsFalse(
                source.Contains(forbiddenPattern, StringComparison.Ordinal),
                $"{scope} should not contain '{forbiddenPattern}'.");
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
