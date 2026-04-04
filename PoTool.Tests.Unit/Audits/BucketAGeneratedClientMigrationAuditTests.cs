namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class BucketAGeneratedClientMigrationAuditTests
{
    [TestMethod]
    public void FullyMigratedBucketAServices_DoNotUseRawHttpClientOrApiRoutes()
    {
        var repositoryRoot = GetRepositoryRoot();

        AssertFileDoesNotContainForbiddenPatterns(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "TriageTagService.cs"));
        AssertFileDoesNotContainForbiddenPatterns(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "ReleaseNotesService.cs"));
        AssertFileDoesNotContainForbiddenPatterns(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "ConfigurationTransferService.cs"));
    }

    [TestMethod]
    public void ScopedBucketAMethods_DoNotUseRawHttpClientOrApiRoutes()
    {
        var repositoryRoot = GetRepositoryRoot();
        var projectServiceSource = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "ProjectService.cs"));
        var teamServiceSource = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "TeamService.cs"));
        var startupServiceSource = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "StartupOrchestratorService.cs"));
        var cacheSyncServiceSource = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "CacheSyncService.cs"));

        AssertMethodDoesNotContainForbiddenPatterns(projectServiceSource, "GetAllProjectsAsync");
        AssertMethodDoesNotContainForbiddenPatterns(projectServiceSource, "GetProjectAsync");
        AssertMethodDoesNotContainForbiddenPatterns(projectServiceSource, "GetProjectProductsAsync");
        AssertMethodDoesNotContainForbiddenPatterns(teamServiceSource, "CreateTeamAsync");
        AssertMethodDoesNotContainForbiddenPatterns(startupServiceSource, "GetStartupReadinessAsync");
        AssertMethodDoesNotContainForbiddenPatterns(cacheSyncServiceSource, "GetCacheStatusAsync");
        AssertMethodDoesNotContainForbiddenPatterns(cacheSyncServiceSource, "IsSyncRunningAsync");
        AssertMethodDoesNotContainForbiddenPatterns(cacheSyncServiceSource, "GetCacheInsightsAsync");
        AssertMethodDoesNotContainForbiddenPatterns(cacheSyncServiceSource, "GetActivityLedgerValidationAsync");
        AssertMethodDoesNotContainForbiddenPatterns(cacheSyncServiceSource, "GetChangesSinceSyncAsync");
    }

    private static void AssertFileDoesNotContainForbiddenPatterns(string path)
    {
        var source = File.ReadAllText(path);
        AssertForbiddenPatternsAbsent(source, path);
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
